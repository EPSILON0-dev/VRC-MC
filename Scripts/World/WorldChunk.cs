using System;
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

    public void Init()
    {
        // Prepare the blocks storage
        Blocks = new ushort[SubChunksCount][];
        BlockCounts = new int[SubChunksCount];

        // For testing, fill with some random blocks
        GenerateTerrain();

        // Mark all subchunks as unmodified
        // Also done in GenerateTerrain, but just in case
        SubchunkModified = new bool[SubChunksCount];
        for (int i = 0; i < SubchunkModified.Length; i++)
            SubchunkModified[i] = false;
        ChunkModified = false;

        // Prepare the block texture
        ChunkBlockTexture = new Texture2D(TextureSize, TextureSize, TextureFormat.R8, false, true);
        ChunkBlockTexture.filterMode = FilterMode.Point;
        GenerateChunkTexture();
    }

    public void SetBlock(Vector3Int position, ushort block, bool modifyTexture = true)
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

        if (!Manager.DisableAllLogs)
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
                var color = new Color32((byte)block, 0, 0, 0);
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

    public void GenerateChunkTexture()
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

        // TODO ... time slice ...
        double startTime = Time.realtimeSinceStartupAsDouble;

        for (int index = 0; index < SubChunksCount; index++)
        {
            Color32[] colors = new Color32[SubchunkBlockCount];
            if (Blocks[index] == null) continue;
            for (int i = 0; i < SubchunkBlockCount; i++)
            {
                // Only 16 for now
                colors[i] = new Color32(
                    (byte)(Blocks[index][i] & 0xFF),
                    0, 0, 0
                );
            }
            ChunkBlockTexture.SetPixels32(0, index * blockHeight, blockWidth, blockHeight, colors);
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

    private void GenerateTerrain()
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
        const int stone = 3;
        const int dirt = 2;

        double startTime = Time.realtimeSinceStartupAsDouble;

        // Simple generator -- Stone up to y=32, dirt up to y=40
        for (int i = 0; i < SubChunksCount; i++)
        {
            // As a test fill with ones
            Blocks[i] = new ushort[SubchunkBlockCount];
            for (int j = 0; j < SubchunkBlockCount; j++)
            {
                // Ah yes... magic numbers, they'll be gone soon
                if (i < 16)
                {
                    Blocks[i][j] = stone;
                }
                else if (i < 20)
                {
                    Blocks[i][j] = dirt;
                }
                else
                {
                    Blocks[i][j] = 0;
                }
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
