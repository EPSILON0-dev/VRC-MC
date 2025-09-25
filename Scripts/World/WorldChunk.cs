using System;
using System.Numerics;
using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class WorldChunk : UdonSharpBehaviour
{
    // Constants
    public const int SubChunksSize = 8;
    public const int SubChunksCountX = 2;
    public const int SubChunksCountY = 8;
    public const int SubChunksCountZ = 2;
    public const int SubChunksCount = 8 * 2 * 2;
    public const int BlockCountX = 16;
    public const int BlockCountY = 64;
    public const int BlockCountZ = 16;
    public const int BlockCount = BlockCountX * BlockCountY * BlockCountZ;
    public const int SubchunkBlockCount = ChunkRendererSubchunk.BlockCount;
    public const int TextureSize = 128;

    // Block storage
    public WorldManager Manager;
    public Texture2D ChunkBlockTexture;
    public ushort[][] Blocks;
    public int[] BlockCounts;
    public bool[] SubchunkModified;
    public bool ChunkModified;

    // References
    public ChunkRenderer Renderer = null;

    // Init state
    private int _initStep = 0;
    private bool _initDone = false;
    public bool InitDone => _initDone;

    public bool Init()
    {
        int terrainSteps = Manager.GeneratorTimesliceDivision;
        int textureSteps = Manager.GeneratorTextureTimesliceDivision;

        // Already initialized
        if (_initDone) return true;

        // Setup (Step 0)
        if (_initStep == 0)
        {
            Blocks = new ushort[SubChunksCount][];
            BlockCounts = new int[SubChunksCount];
        }

        // Terrain steps
        if (_initStep < terrainSteps)
        {
            int startXZ = _initStep * (BlockCountX * BlockCountZ) / terrainSteps;
            int endXZ = (_initStep + 1) * (BlockCountX * BlockCountZ) / terrainSteps;
            GenerateTerrain(startXZ, endXZ);
        }

        // Texture steps
        else if (_initStep < terrainSteps + textureSteps)
        {
            if (_initStep == terrainSteps)
            {
                ChunkBlockTexture = new Texture2D(TextureSize, TextureSize, TextureFormat.RG16, false, true);
                ChunkBlockTexture.filterMode = FilterMode.Point;
            }   

            int step = _initStep - terrainSteps;
            int startSubchunk = step * SubChunksCount / textureSteps;
            int endSubchunk = (step + 1) * SubChunksCount / textureSteps;
            GenerateChunkTexture(startSubchunk, endSubchunk);
        }

        // Finalization step
        if (_initStep == terrainSteps + textureSteps - 1)
        {
            // Mark all subchunks as unmodified
            // Also done in GenerateTerrain, but just in case
            SubchunkModified = new bool[SubChunksCount];
            for (int i = 0; i < SubchunkModified.Length; i++)
                SubchunkModified[i] = false;
            ChunkModified = false;

            // Finalize initialization
            _initDone = true;
            return true;
        }

        _initStep++;
        return false;
    }

    public void SetBlock(Vector3Int position, ushort block, bool modifyTexture = true, bool log = true)
    {
        int subchunkIndex = GetSubChunkIndex(position);
        if (subchunkIndex == -1)
        {
            Debug.LogWarning(
                $"WORLD CHUNK : {gameObject.name} : " +
                $"Attempted to set block out of bounds at {position}"
            );
            return;
        }
        int blockIndex = GetBlockIndex(position);
        if (blockIndex == -1) return;

        SetAtIndex(subchunkIndex, blockIndex, block, modifyTexture);

        if (!Manager.DisableAllLogs && log)
        {
            Debug.Log(
                $"WORLD CHUNK : {gameObject.name} : " +
                $"Set block to {block} at {position} (c: {subchunkIndex}, b: {blockIndex})"
            );
        }
    }

    public void SetAtIndex(int chunkIndex, int blockIndex, ushort block, bool modifyTexture = false)
    {
        if (Blocks == null)
        {
            Debug.LogError(
                $"WORLD CHUNK : {gameObject.name} : " +
                $"Blocks array is not initialized, cannot set block!"
            );
            return;
        }

        if (chunkIndex < 0 || chunkIndex >= SubChunksCount ||
            blockIndex < 0 || blockIndex >= SubchunkBlockCount)
        {
            return;
        }

        // Subchunk doesn't exist, block break -- Do nothing
        if (block == 0 && Blocks[chunkIndex] == null) { }

        // Subchunk doesn't exist, block place -- Create chunk
        else if (Blocks[chunkIndex] == null)
        {
            Blocks[chunkIndex] = new ushort[SubchunkBlockCount];
            Array.Clear(Blocks[chunkIndex], 0, SubchunkBlockCount);
            BlockCounts[chunkIndex] = 1;
            Blocks[chunkIndex][blockIndex] = block;
        }

        // Subchunk exists, block break -- Set to 0, possibly delete chunk
        else if (block == 0)
        {
            ushort oldBlock = Blocks[chunkIndex][blockIndex];
            if (oldBlock != 0)
            {
                Blocks[chunkIndex][blockIndex] = 0;
                BlockCounts[chunkIndex]--;
                if (BlockCounts[chunkIndex] == 0)
                {
                    Blocks[chunkIndex] = null;
                }
            }
        }

        // Subchunk exists, block place -- Set to new block
        else
        {
            ushort oldBlock = Blocks[chunkIndex][blockIndex];
            if (oldBlock == 0) BlockCounts[chunkIndex]++;
            Blocks[chunkIndex][blockIndex] = block;
        }

        // Update the texture
        if (modifyTexture)
        {
            int index = chunkIndex * SubchunkBlockCount + blockIndex;
            int x = index % TextureSize;
            int y = index / TextureSize;
            // Don't set air blocks in the texture to avoid flickering
            if (block != 0)
            {
                var color = new Color32(
                    (byte)(block & 0xff),         // Block ID
                    (byte)((block >> 8) & 0xff),  // Block Attributes
                    0, 0
                );
                ChunkBlockTexture.SetPixel(x, y, color);
            }
            ChunkBlockTexture.Apply();
        }

        // Mark subchunk as modified
        SubchunkModified[chunkIndex] = true;
        ChunkModified = true;
    }

    public ushort GetBlock(Vector3Int position)
    {
        // Calls can start coming in before Init
        if (Blocks == null) return 0;

        if (position.x < 0 || position.x >= BlockCountX ||
            position.y < 0 || position.y >= BlockCountY ||
            position.z < 0 || position.z >= BlockCountZ)
        {
            // Don't log error, these are acceptable
            return 0;
        }

        int subchunkIndex = GetSubChunkIndex(position);
        int blockIndex = GetBlockIndex(position);

        if (Blocks[subchunkIndex] == null)
        {
            return 0;
        }

        return Blocks[subchunkIndex][blockIndex];
    }

    public void GenerateChunkTexture(int startSubchunk = 0, int endSubchunk = SubChunksCount)
    {
        if (Blocks == null)
        {
            Debug.LogError(
                $"WORLD CHUNK : {gameObject.name} : " +
                $"Blocks array is not initialized, cannot generate texture!"
            );
            return;
        }

        const int blockWidth = 128;
        const int blockHeight = 4;

        double startTime = Time.realtimeSinceStartupAsDouble;

        for (int subchunk = startSubchunk; subchunk < endSubchunk; subchunk++)
        {
            Color32[] colors = new Color32[SubchunkBlockCount];
            if (Blocks[subchunk] == null)
            {
                Array.Clear(colors, 0, SubchunkBlockCount);
                ChunkBlockTexture.SetPixels32(0, subchunk * blockHeight, blockWidth, blockHeight, colors);
                continue;
            }

            for (int i = 0; i < SubchunkBlockCount; i++)
            {
                colors[i] = new Color32(
                    (byte)(Blocks[subchunk][i] & 0xFF),
                    (byte)((Blocks[subchunk][i] >> 8) & 0xFF),
                    0, 0
                );
            }
            ChunkBlockTexture.SetPixels32(0, subchunk * blockHeight, blockWidth, blockHeight, colors);
        }
        ChunkBlockTexture.Apply();

        double endTime = Time.realtimeSinceStartupAsDouble;

        if (!Manager.DisableAllLogs)
        {
            Debug.Log(
                $"WORLD CHUNK : {gameObject.name} : " +
                $"Generated chunk texture in {(endTime - startTime) * 1000.0:0.00}ms"
            );
        }
    }

    private void GenerateTerrain(int startXZ = 0, int endXZ = BlockCountX * BlockCountZ)
    {
        if (Blocks == null)
        {
            Debug.LogError(
                $"WORLD CHUNK : {gameObject.name} : " +
                $"Blocks array is not initialized, cannot generate terrain!"
            );
            return;
        }

        // TODO some manager for the block...
        const int bedrock = 1;
        const int stone = 2;
        const int dirt = 5;
        const int grass = 6;

        // Generate 2 layer perlin noise terrain
        const float perlinFrequency1 = 1f / 128f;
        const float perlinFrequency2 = 1f / 32f;
        const float perlinScale1 = 7f;
        const float perlinScale2 = 2.5f;
        const float baseHeight = 32f;
            
        double startTime = Time.realtimeSinceStartupAsDouble;

        Vector2Int chunkOrigin = new Vector2Int(
            (int)transform.position.x - Manager.WorldOffsetInBlocks,
            (int)transform.position.z - Manager.WorldOffsetInBlocks
        );

        for (int xz = startXZ; xz < endXZ; xz++)
        {
            int chunkX = xz % BlockCountX;
            int chunkZ = xz / BlockCountX;

            float perlinX = chunkOrigin.x + chunkX + Manager.GeneratorOffset.x;
            float perlinY = chunkOrigin.y + chunkZ + Manager.GeneratorOffset.y;

            float perlin1 = (Mathf.PerlinNoise(
                perlinX * perlinFrequency1,
                perlinY * perlinFrequency1
            ) * 2f - 1f) * perlinScale1;

            float perlin2 = (Mathf.PerlinNoise(
                perlinX * perlinFrequency2,
                perlinY * perlinFrequency2
            ) * 2f - 1f) * perlinScale2;

            // Hope we don't overshoot
            float height = baseHeight + perlin1 * perlin2;

            // Fill blocks up to height
            for (int y = 0; y <= (int)height; y++)
            {
                int subchunkIndex = GetSubChunkIndex(new Vector3Int(chunkX, y, chunkZ));
                int blockIndex = GetBlockIndex(new Vector3Int(chunkX, y, chunkZ));

                if (subchunkIndex == -1 || blockIndex == -1)
                {
                    Debug.LogError(
                        $"WORLD CHUNK : {gameObject.name} : " +
                        $"Calculated out of bounds subchunk or block index at " +
                        $"{new Vector3Int(chunkX, y, chunkZ)}"
                    );
                    continue;
                }

                if (Blocks[subchunkIndex] == null)
                {
                    Blocks[subchunkIndex] = new ushort[SubchunkBlockCount];
                    Array.Clear(Blocks[subchunkIndex], 0, SubchunkBlockCount);
                    BlockCounts[subchunkIndex] = 0;
                }

                ushort blockType = stone;
                if (y == 0) blockType = bedrock;
                else if (y == (int)height && height > 3f) blockType = grass;
                else if (y >= (int)height - 3) blockType = dirt;

                Blocks[subchunkIndex][blockIndex] = blockType;
                BlockCounts[subchunkIndex]++;
            }
        }

        // Mark all subchunks as unmodified
        SubchunkModified = new bool[SubChunksCount];
        for (int i = 0; i < SubchunkModified.Length; i++)
        {
            SubchunkModified[i] = false;
        }

        double endTime = Time.realtimeSinceStartupAsDouble;

        if (!Manager.DisableAllLogs)
        {
            Debug.Log(
                $"WORLD CHUNK : {gameObject.name} : " +
                $"Generated terrain in {(endTime - startTime) * 1000.0:0.00}ms"
            );
        }
    }

    private int GetSubChunkIndex(Vector3Int position)
    {
        if (position.x < 0 || position.x >= BlockCountX ||
            position.y < 0 || position.y >= BlockCountY ||
            position.z < 0 || position.z >= BlockCountZ)
        {
            return -1;
        }

        var subchunkPos = new Vector3Int(
            position.x / SubChunksSize,
            position.y / SubChunksSize,
            position.z / SubChunksSize
        );
        return (subchunkPos.y * (SubChunksCountX * SubChunksCountZ)) +
            (subchunkPos.z * SubChunksCountX) + subchunkPos.x;
    }

    private int GetBlockIndex(Vector3Int position)
    {
        // If we got to this point, position is valid, check is redundant
        /*
        if (position.x < 0 || position.x >= BlockCountX ||
            position.y < 0 || position.y >= BlockCountY ||
            position.z < 0 || position.z >= BlockCountZ)
        {
            return -1;
        }
        */

        var blockPos = new Vector3Int(
            position.x % SubChunksSize,
            position.y % SubChunksSize,
            position.z % SubChunksSize
        );
        return (blockPos.y * (SubChunksSize * SubChunksSize)) +
            (blockPos.z * SubChunksSize) + blockPos.x;
    }
}
