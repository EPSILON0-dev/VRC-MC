using System;
using UdonSharp;
using UnityEngine;


[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class WorldManager : UdonSharpBehaviour
{
    // References assigned in the editor
    public PlayerController Player;
    public WorldChunk[] WorldChunks;
    public ChunkRenderer[] ChunkRenderers;
    public Material ChunkMaterial;
    public int WorldSizeInChunks = 4;
    public int WorldOffsetInBlocks = -32;
    public int RenderRegionSizeInChunks = 1;
    public int MeshGenerationsPerFrame = 1;
    public double TimeBetweenChunkShifts = 2f;
    public bool DisableSpamLogs = false;
    public bool DisableAllLogs = false;

    // Initialization process
    private bool _isInitialized = false;
    private int _initStage = 0;
    public bool IsInitialized => _isInitialized;

    // Chunk rendering and updates
    private Vector2Int _currentCenterChunk;
    private double _lastRendererShift;
    // Queue is newest first, organized as [chunkIndex, submeshIndex, ...]
    private Vector2Int[] _generateQueue;
    private int[] _rendererBindings;

    // Precomputed arrays for the render chunks
    private Vector2[] _precomputedUVs;
    private int[] _precomputedIndices;
    public Vector2[] PrecomputedUVs => _precomputedUVs;
    public int[] PrecomputedIndices => _precomputedIndices;

    // ----------------------------------------------------------------------

    void Start()
    {
        Init();
    }

    void Update()
    {
        UpdateInternal();
    }

    public void Init()
    {
        double timeStart = Time.realtimeSinceStartupAsDouble;

        PrecomputeArrays();
        _generateQueue = new Vector2Int[0];
        _lastRendererShift = Time.realtimeSinceStartupAsDouble;
        _rendererBindings = new int[ChunkRenderers.Length];
        for (int i = 0; i < _rendererBindings.Length; i++)
        {
            _rendererBindings[i] = -1;
        }

        double timeEnd = Time.realtimeSinceStartupAsDouble;
        float initTime = (float)(timeEnd - timeStart);

        if (!DisableAllLogs)
        {
            Debug.Log($"WORLD MANAGER : Preinitialization step completed in {initTime * 1000:0.00}ms");
        }
    }

    public void UpdateInternal()
    {
        if (_isInitialized)
        {
            ShiftRenderersIfNeeded();
            UpdateQueues();
        }
        else
        {
            InitializeStep();
        }
    }

    // ----------------------------------------------------------------------

    public ushort[] GetArrayAtPos(Vector3Int globalPosition, bool skipOffsetAdjust = false)
    {
        if (!skipOffsetAdjust)
        {
            globalPosition.x -= WorldOffsetInBlocks;
            globalPosition.z -= WorldOffsetInBlocks;
        }


        int chunkIndex = GetWorldChunkIndex(globalPosition, true);
        if (chunkIndex < 0 || chunkIndex >= WorldChunks.Length)
        {
            // Don't log error, these are acceptable
            return null;
        }

        Vector3Int chunkPos = new Vector3Int(
            globalPosition.x % WorldChunk.BlockCountX / ChunkRendererSubchunk.Size,
            globalPosition.y % WorldChunk.BlockCountY / ChunkRendererSubchunk.Size,
            globalPosition.z % WorldChunk.BlockCountZ / ChunkRendererSubchunk.Size
        );
        int subchunkIndex = chunkPos.x + chunkPos.z * 2 + chunkPos.y * 4;

        if (subchunkIndex < 0 || subchunkIndex >= WorldChunk.SubChunksCount)
        {
            // Don't log error, these are acceptable
            // Log error for now
            Debug.LogWarning(
                $"WORLD MANAGER : Invalid subchunk index {subchunkIndex} at position {globalPosition}"
            );
            return null;
        }

        return WorldChunks[chunkIndex].Blocks[subchunkIndex];
    }

    public ushort GetBlock(Vector3Int globalPosition, bool skipOffsetAdjust = false)
    {
        int chunkIndex = GetWorldChunkIndex(globalPosition, skipOffsetAdjust);
        if (chunkIndex < 0 || chunkIndex >= WorldChunks.Length)
        {
            // Don't log error, these are acceptable
            return 0;
        }
        var blockPos = GetPositionInWorldChunk(globalPosition, skipOffsetAdjust);

        return WorldChunks[chunkIndex].GetBlock(blockPos);
    }

    public void SetBlock(Vector3Int globalPosition, ushort block, bool skipOffsetAdjust = false, bool log = true)
    {

        int chunkIndex = GetWorldChunkIndex(globalPosition, skipOffsetAdjust);
        if (chunkIndex < 0 || chunkIndex >= WorldChunks.Length)
        {
            Debug.LogWarning(
                $"WORLD MANAGER : Attempted to set block out of bounds at {globalPosition}"
            );
            return;
        }
        var blockPos = GetPositionInWorldChunk(globalPosition, skipOffsetAdjust);

        WorldChunks[chunkIndex].SetBlock(blockPos, block);
        EnqueueMeshGeneration(globalPosition, enqueueNeighbouring: true);

        if (log && !DisableAllLogs)
        {
            Debug.Log(
                $"WORLD MANAGER : Set block to {block} at {globalPosition} " +
                $"(c: {chunkIndex}, b: {blockPos})"
            );
        }
    }

    // ----------------------------------------------------------------------

    public void EnqueueMeshGeneration(Vector3Int globalPosition, bool enqueueNeighbouring = false)
    {
        int chunkIndex = GetWorldChunkIndex(globalPosition);
        if (chunkIndex < 0 || chunkIndex >= WorldChunks.Length)
        {
            Debug.LogWarning(
                $"WORLD MANAGER : Attempted to enqueue mesh generation out of bounds at {globalPosition}"
            );
            return;
        }

        Vector3Int localPosition = GetPositionInWorldChunk(globalPosition);
        Vector3Int chunkPos = localPosition / ChunkRendererSubchunk.Size;
        int subchunkIndex = chunkPos.x + chunkPos.z * 2 + chunkPos.y * 4;

        Vector2Int[] newQueue = new Vector2Int[_generateQueue.Length + 1];
        newQueue[0] = new Vector2Int(chunkIndex, subchunkIndex);
        Array.Copy(_generateQueue, 0, newQueue, 1, _generateQueue.Length);
        _generateQueue = newQueue;

        if (!DisableSpamLogs)
        {
            Debug.Log(
                $"WORLD MANAGER : Enqueued mesh generation for block at {globalPosition} " +
                $"(c: {chunkIndex}, s: {subchunkIndex})"
            );
        }

        // Enqueue neighbouring subchunks if needed
        if (enqueueNeighbouring)
        {
            int enqueuedCount = 1; // Include the main one
            Vector3Int subchunkPos = new Vector3Int(
                localPosition.x % ChunkRendererSubchunk.Size,
                localPosition.y % ChunkRendererSubchunk.Size,
                localPosition.z % ChunkRendererSubchunk.Size
            );

            var globalPosCopy = globalPosition; // Scary Udon bug fix

            if (subchunkPos.x == 0)
            {
                EnqueueMeshGeneration(globalPosCopy + new Vector3Int(-1, 0, 0));
                enqueuedCount++;
            }
            else if (subchunkPos.x == ChunkRendererSubchunk.Size - 1)
            {
                EnqueueMeshGeneration(globalPosCopy + new Vector3Int(1, 0, 0));
                enqueuedCount++;
            }

            if (subchunkPos.y == 0)
            {
                EnqueueMeshGeneration(globalPosCopy + new Vector3Int(0, -1, 0));
                enqueuedCount++;
            }
            else if (subchunkPos.y == ChunkRendererSubchunk.Size - 1)
            {
                EnqueueMeshGeneration(globalPosCopy + new Vector3Int(0, 1, 0));
                enqueuedCount++;
            }

            if (subchunkPos.z == 0)
            {
                EnqueueMeshGeneration(globalPosCopy + new Vector3Int(0, 0, -1));
                enqueuedCount++;
            }
            else if (subchunkPos.z == ChunkRendererSubchunk.Size - 1)
            {
                EnqueueMeshGeneration(globalPosCopy + new Vector3Int(0, 0, 1));
                enqueuedCount++;
            }

            if (!DisableAllLogs)
            {
                Debug.Log(
                    $"WORLD MANAGER : Enqueued {enqueuedCount} mesh generation(s) for block at {globalPosition}"
                );
            }
        }
    }

    public void EnqueueWholeChunkMeshGeneration(int chunkIndex)
    {
        const int shiftAmount = WorldChunk.SubChunksCount;

        Vector2Int[] newQueue = new Vector2Int[_generateQueue.Length + shiftAmount];
        for (int i = 0; i < shiftAmount; i++)
        {
            newQueue[i] = new Vector2Int(chunkIndex, i);
        }
        Array.Copy(_generateQueue, 0, newQueue, shiftAmount, _generateQueue.Length);
        _generateQueue = newQueue;
    }

    // ----------------------------------------------------------------------

    // DDA my beloved :3
    public bool RayCast(Vector3 origin, Vector3 direction, out Vector3Int hitPos,
        out Vector3Int normal, float maxDistance, int maxIters = 20)
    {
        Vector3Int blockPos = new Vector3Int(
            Mathf.FloorToInt(origin.x),
            Mathf.FloorToInt(origin.y),
            Mathf.FloorToInt(origin.z)
        );

        Vector3 deltaDist = new Vector3(
            Mathf.Abs(1 / direction.x),
            Mathf.Abs(1 / direction.y),
            Mathf.Abs(1 / direction.z)
        );

        Vector3Int step = new Vector3Int(
            direction.x < 0 ? -1 : 1,
            direction.y < 0 ? -1 : 1,
            direction.z < 0 ? -1 : 1
        );

        Vector3 sideDist = new Vector3(
            (direction.x < 0) ?
                (origin.x - blockPos.x) * deltaDist.x :
                (blockPos.x + 1 - origin.x) * deltaDist.x,
            (direction.y < 0) ?
                (origin.y - blockPos.y) * deltaDist.y :
                (blockPos.y + 1 - origin.y) * deltaDist.y,
            (direction.z < 0) ?
                (origin.z - blockPos.z) * deltaDist.z :
                (blockPos.z + 1 - origin.z) * deltaDist.z
        );

        while (maxIters-- > 0)
        {
            if (sideDist.x < sideDist.y && sideDist.x < sideDist.z)
            {
                sideDist.x += deltaDist.x;
                blockPos.x += step.x;
                normal = new Vector3Int(-step.x, 0, 0);
            }
            else if (sideDist.y < sideDist.x && sideDist.y < sideDist.z)
            {
                sideDist.y += deltaDist.y;
                blockPos.y += step.y;
                normal = new Vector3Int(0, -step.y, 0);
            }
            else
            {
                sideDist.z += deltaDist.z;
                blockPos.z += step.z;
                normal = new Vector3Int(0, 0, -step.z);
            }

            // Check if block is solid
            ushort block = GetBlock(blockPos, skipOffsetAdjust: false);
            if (block != 0)
            {
                hitPos = blockPos;
                return true;
            }

            // Check distance
            float dist = Vector3.Distance(origin, blockPos);
            if (dist > maxDistance)
            {
                hitPos = Vector3Int.zero;
                normal = Vector3Int.zero;
                return false;
            }
        }

        hitPos = Vector3Int.zero;
        normal = Vector3Int.zero;
        return false;
    }

    // ----------------------------------------------------------------------

    private void ShiftRenderersIfNeeded()
    {
        // Do the timing check
        double currentTime = Time.realtimeSinceStartupAsDouble;
        if (currentTime - _lastRendererShift < TimeBetweenChunkShifts) return;
        double timeStart = Time.realtimeSinceStartupAsDouble;

        // Check if the player is still in the same chunk
        Vector2Int playerChunkPos = GetPlayerChunk();
        Vector2Int playerChunkPosDiff = _currentCenterChunk - playerChunkPos;
        if (playerChunkPosDiff.x > -2 && playerChunkPosDiff.x < 2 &&
            playerChunkPosDiff.y > -2 && playerChunkPosDiff.y < 2) return;

        if (!DisableAllLogs)
        {
            Debug.Log(
                $"WORLD MANAGER : Shifting renderers to new center chunk {playerChunkPos} " +
                $"(was {_currentCenterChunk})"
            );
        }

        // Compute the new bounds for the renderers
        Vector2Int minChunk = playerChunkPos - new Vector2Int(
            RenderRegionSizeInChunks / 2,
            RenderRegionSizeInChunks / 2
        );
        Vector2Int maxChunk = playerChunkPos + new Vector2Int(
            RenderRegionSizeInChunks / 2,
            RenderRegionSizeInChunks / 2
        );

        // Clamp the bounds to the world size
        if (minChunk.x < 0) minChunk.x = 0;
        if (minChunk.y < 0) minChunk.y = 0;
        if (maxChunk.x >= WorldSizeInChunks) maxChunk.x = WorldSizeInChunks - 1;
        if (maxChunk.y >= WorldSizeInChunks) maxChunk.y = WorldSizeInChunks - 1;

        // Find the renderers that are bound to chunks outside the region
        int[] freeRenderers = new int[ChunkRenderers.Length];
        int freeRendererCount = 0;
        int reusedRenderers = 0;
        for (int i = 0; i < _rendererBindings.Length; i++)
        {
            // If the renderer was not bound, forward it to the free list
            if (_rendererBindings[i] < 0 || _rendererBindings[i] >= WorldChunks.Length)
            {
                freeRenderers[freeRendererCount++] = i;
                continue;
            }

            // Check if the bound chunk is in the region
            Vector2Int chunkPos = new Vector2Int(
                _rendererBindings[i] % WorldSizeInChunks,
                _rendererBindings[i] / WorldSizeInChunks
            );

            bool inBounds = chunkPos.x >= minChunk.x && chunkPos.x <= maxChunk.x &&
                chunkPos.y >= minChunk.y && chunkPos.y <= maxChunk.y;
            if (inBounds)
            {
                reusedRenderers++;
                continue;
            }

            // Unbind the renderer
                freeRenderers[freeRendererCount++] = i;
            if (_rendererBindings[i] >= 0 && _rendererBindings[i] < WorldChunks.Length)
            {
                WorldChunks[_rendererBindings[i]].Renderer = null;
                ChunkRenderers[i].SetChunk(null);
                ChunkRenderers[i].DisableSubchunkRenderers();
                _rendererBindings[i] = -1;
            }
        }

        // Find the chunks in the region that still need a renderer
        Vector2Int[] unboundChunks = new Vector2Int[ChunkRenderers.Length];
        int unboundIndex = 0;
        for (int z = minChunk.y; z <= maxChunk.y; z++)
        {
            for (int x = minChunk.x; x <= maxChunk.x; x++)
            {
                // If the chunk doesn't have a renderer, add it to the unbound list
                int chunkIndex = x + z * WorldSizeInChunks;
                if (WorldChunks[chunkIndex].Renderer != null) continue;
                unboundChunks[unboundIndex++] = new Vector2Int(x, z);
            }
        }

        // Rebind the free renderers
        for (int i = 0; i < unboundIndex; i++)
        {
            Vector2Int chunkPos = unboundChunks[i];
            int chunkIndex = chunkPos.x + chunkPos.y * WorldSizeInChunks;

            ChunkRenderers[freeRenderers[i]].SetChunk(WorldChunks[chunkIndex]);
            ChunkRenderers[freeRenderers[i]].DisableSubchunkRenderers();
            WorldChunks[chunkIndex].Renderer = ChunkRenderers[freeRenderers[i]];
            EnqueueWholeChunkMeshGeneration(chunkIndex);
            _rendererBindings[freeRenderers[i]] = chunkIndex;
        }

        _currentCenterChunk = playerChunkPos;
        _lastRendererShift = currentTime;

        if (!DisableAllLogs)
        {
            double timeEnd = Time.realtimeSinceStartupAsDouble;
            Debug.Log($"WORLD MANAGER : Renderer shift completed in {(timeEnd - timeStart) * 1000:0.00}ms, " +
                $"used {reusedRenderers} old and {unboundIndex} new binding(s)");
        }
    }

    private Vector2Int GetPlayerChunk()
    {
        Vector3 playerPos = Player.GetPlayerPosition();
        return new Vector2Int(
            Mathf.FloorToInt((playerPos.x - WorldOffsetInBlocks) / (float)WorldChunk.BlockCountX),
            Mathf.FloorToInt((playerPos.z - WorldOffsetInBlocks) / (float)WorldChunk.BlockCountZ)
        );
    }

    private void PrecomputeArrays()
    {
        // All blocks times 6 sides
        int arraySize = ChunkRendererSubchunk.BlockCount * 6;
        _precomputedUVs = new Vector2[arraySize * 4];
        _precomputedIndices = new int[arraySize * 6];

        for (int i = arraySize - 1; i >= 0; i--)
        {
            int i4 = i * 4;
            int i6 = i * 6;

            _precomputedUVs[i4] = new Vector2(0, 0);
            _precomputedUVs[i4 + 1] = new Vector2(1, 0);
            _precomputedUVs[i4 + 2] = new Vector2(1, 1);
            _precomputedUVs[i4 + 3] = new Vector2(0, 1);
            _precomputedIndices[i6 + 0] = i4 + 0;
            _precomputedIndices[i6 + 1] = i4 + 2;
            _precomputedIndices[i6 + 2] = i4 + 1;
            _precomputedIndices[i6 + 3] = i4 + 0;
            _precomputedIndices[i6 + 4] = i4 + 3;
            _precomputedIndices[i6 + 5] = i4 + 2;
        }
    }

    // ----------------------------------------------------------------------

    private int GetWorldChunkIndex(Vector3Int globalPosition, bool skipOffsetAdjust = false)
    {
        if (!skipOffsetAdjust)
        {
            globalPosition.x -= WorldOffsetInBlocks;
            globalPosition.z -= WorldOffsetInBlocks;
        }

        // Check bounds
        if (globalPosition.x < 0 || globalPosition.x >= WorldSizeInChunks * WorldChunk.BlockCountX ||
            globalPosition.y < 0 || globalPosition.y >= WorldChunk.BlockCountY ||
            globalPosition.z < 0 || globalPosition.z >= WorldSizeInChunks * WorldChunk.BlockCountZ)
        {
            return -1;
        }

        // Find the chunk
        int chunkX = globalPosition.x / WorldChunk.BlockCountX;
        int chunkZ = globalPosition.z / WorldChunk.BlockCountZ;
        return chunkX + chunkZ * WorldSizeInChunks;
    }

    private Vector3Int GetPositionInWorldChunk(Vector3Int globalPosition, bool skipOffsetAdjust = false)
    {
        if (!skipOffsetAdjust)
        {
            globalPosition.x -= WorldOffsetInBlocks;
            globalPosition.z -= WorldOffsetInBlocks;
        }

        // If we got to this point, position is valid, check is redundant
        /*
        if (globalPosition.x < 0 || globalPosition.x >= WorldSizeInChunks.x * WorldChunk.BlockCountX ||
            globalPosition.y < 0 || globalPosition.y >= WorldChunk.BlockCountY ||
            globalPosition.z < 0 || globalPosition.z >= WorldSizeInChunks.y * WorldChunk.BlockCountZ)
        {
            return Vector3Int.zero;
        }
        */

        return new Vector3Int(
            globalPosition.x % WorldChunk.BlockCountX,
            globalPosition.y,
            globalPosition.z % WorldChunk.BlockCountZ
        );
    }

    // ----------------------------------------------------------------------

    private void InitializeStep()
    {
        int worldChunksInitEnd = WorldChunks.Length;
        int renderersInitEnd = worldChunksInitEnd + ChunkRenderers.Length;

        double timeStart = Time.realtimeSinceStartupAsDouble;

        if (_initStage < worldChunksInitEnd)
        {
            WorldChunks[_initStage].Init();
        }
        else if (_initStage < renderersInitEnd)
        {
            ChunkRenderers[_initStage - worldChunksInitEnd].Init();
        }
        else
        {
            Debug.Log("WORLD MANAGER : Initialization complete");
            _isInitialized = true;
            Player.TeleportToStart();
            return;
        }

        if (!DisableAllLogs)
        {
            double timeEnd = Time.realtimeSinceStartupAsDouble;
            Debug.Log(
                $"WORLD MANAGER : Initialization step {_initStage} completed in {(timeEnd - timeStart) * 1000:0.00}ms"
            );
        }
        _initStage++;
    }

    private void UpdateQueues()
    {
        if (_generateQueue.Length == 0) return;

        double timeStart = Time.realtimeSinceStartupAsDouble;
        int count = 0;

        // Perform limited number of updates per frame
        for (int i = 0; i < MeshGenerationsPerFrame; i++)
        {
            if (_generateQueue.Length == 0)
            {
                break;
            }

            // Perform the request
            Vector2Int request = _generateQueue[_generateQueue.Length - 1];
            if (request.x < 0 || request.x >= WorldChunks.Length ||
                request.y < 0 || request.y >= WorldChunk.SubChunksCount)
            {
                Debug.LogWarning(
                    $"WORLD MANAGER : Invalid index ({request.x}, {request.y}) in mesh generation queue, skipping!"
                );
            }
            else if (WorldChunks[request.x] == null || WorldChunks[request.x].Renderer == null)
            {
                Debug.LogWarning(
                    $"WORLD MANAGER : World chunk or renderer at index {request.x} is not assigned, skipping!"
                );
            }
            else
            {
                WorldChunks[request.x].Renderer.GenerateSubchunkAtIndex(request.y);
            }

            count++;
        }

        // Remove the requests from the queue
        Vector2Int[] newQueue = new Vector2Int[_generateQueue.Length - MeshGenerationsPerFrame];
        Array.Copy(_generateQueue, 0, newQueue, 0, newQueue.Length);
        _generateQueue = newQueue;

        double timeEnd = Time.realtimeSinceStartupAsDouble;
        if (!DisableSpamLogs && !DisableAllLogs)
        {
            Debug.Log(
                $"WORLD MANAGER : {count} mesh update(s) processed in {(timeEnd - timeStart) * 1000:0.00}ms"
            );
        }
    }
}