#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UdonSharp;
using UnityEngine;
using UnityEditor;
using UdonSharpEditor;

[CustomEditor(typeof(WorldManager), true)]
public class WorldManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Default for now
        UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target, false, false);
        DrawDefaultInspector();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Destroy Structure"))
        {
            DestroyStructure();
        }
        if (GUILayout.Button("Generate Structure"))
        {
            GenerateStructure();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Force Initialize"))
        {
            ForceInitialize();
        }
        if (GUILayout.Button("Test Chunk"))
        {
            TestChunk();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Test Combining"))
        {
            TestCombining();
        }
        GUI.enabled = false;
        if (GUILayout.Button("---")) { } // Placeholder
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    private void DestroyStructure()
    {
        WorldManager worldManager = (WorldManager)target;
        Transform transform = worldManager.transform;

        // Delete all child objects
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            DestroyImmediate(child);
        }

        // Clear the "connection" fields
        worldManager.WorldChunks = new WorldChunk[0];
        worldManager.ChunkRenderers = new ChunkRenderer[0];
    }

    private void GenerateStructure()
    {
        DestroyStructure();
        GenerateRenderers();
        GenerateChunks();
        GenerateBorderColliders();
    }

    private void GenerateRenderers()
    {
        WorldManager manager = (WorldManager)target;

        int totalRenderers = manager.RenderRegionSizeInChunks * manager.RenderRegionSizeInChunks;
        manager.ChunkRenderers = new ChunkRenderer[totalRenderers];
        var renderersParent = new GameObject("Renderers");
        renderersParent.transform.parent = manager.transform;

        for (int i = 0; i < totalRenderers; i++)
        {
            GameObject renderer = GenerateRenderer(i);
            renderer.transform.parent = renderersParent.transform;
            manager.ChunkRenderers[i] = renderer.GetComponent<ChunkRenderer>();
        }
    }

    private GameObject GenerateRenderer(int index)
    {
        WorldManager manager = (WorldManager)target;

        GameObject rendererObj = new GameObject($"{index}");
        ChunkRenderer renderer = rendererObj.AddComponent<ChunkRenderer>();

        // For now add disabled filter and renderer
        rendererObj.AddComponent<MeshFilter>();
        MeshRenderer chunkMeshRenderer = rendererObj.AddComponent<MeshRenderer>();
        chunkMeshRenderer.enabled = false;
        chunkMeshRenderer.material = manager.ChunkMaterial;

        renderer.Chunk = null; // Will be assigned later
        renderer.SubChunks = new ChunkRendererSubchunk[WorldChunk.SubChunksCount];
        renderer.Manager = manager;

        for (int i = 0; i < WorldChunk.SubChunksCount; i++)
        {
            GameObject subchunkObj = new GameObject($"{i}");
            subchunkObj.transform.parent = rendererObj.transform;

            subchunkObj.AddComponent<MeshFilter>();
            MeshRenderer subchunkMeshRenderer = subchunkObj.AddComponent<MeshRenderer>();
            subchunkMeshRenderer.material = manager.ChunkMaterial;

            Vector3 subchunkPosition = new Vector3(
                (i % WorldChunk.SubChunksCountX) * WorldChunk.SubChunksSize,
                (i / (WorldChunk.SubChunksCountX * WorldChunk.SubChunksCountZ)) * WorldChunk.SubChunksSize,
                ((i / WorldChunk.SubChunksCountX) % WorldChunk.SubChunksCountZ) * WorldChunk.SubChunksSize
            );
            subchunkObj.transform.localPosition = subchunkPosition;

            ChunkRendererSubchunk subchunk = subchunkObj.AddComponent<ChunkRendererSubchunk>();
            subchunk.Manager = manager;
            subchunk.ParentRenderer = renderer;
            subchunk.IndexInParent = i;

            renderer.SubChunks[i] = subchunk;
        }

        return rendererObj;
    }

    private void GenerateChunks()
    {
        WorldManager manager = (WorldManager)target;

        int totalChunks = manager.WorldSizeInChunks * manager.WorldSizeInChunks;
        manager.WorldChunks = new WorldChunk[totalChunks];
        var chunksParent = new GameObject("Chunks");
        chunksParent.transform.parent = manager.transform;

        for (int i = 0; i < totalChunks; i++)
        {
            GameObject chunkObj = new GameObject($"{i}");
            chunkObj.transform.parent = chunksParent.transform;

            Vector3 chunkPosition = new Vector3(
                (i % manager.WorldSizeInChunks) * WorldChunk.BlockCountX,
                0,
                (i / manager.WorldSizeInChunks) * WorldChunk.BlockCountZ
            );
            chunkPosition.x += manager.WorldOffsetInBlocks;
            chunkPosition.z += manager.WorldOffsetInBlocks;
            chunkObj.transform.localPosition = chunkPosition;

            WorldChunk chunk = chunkObj.AddComponent<WorldChunk>();
            chunk.Manager = manager;
            manager.WorldChunks[i] = chunk;
        }
    }

    private void GenerateBorderColliders()
    {
        WorldManager manager = (WorldManager)target;

        int min = manager.WorldOffsetInBlocks;
        int max = min + manager.WorldSizeInChunks * WorldChunk.BlockCountX;

        var front = GenerateAABBCollider(new Vector3(min, 0, min - 1), new Vector3(max, WorldChunk.BlockCountY * 2, min));
        var back = GenerateAABBCollider(new Vector3(min, 0, max), new Vector3(max, WorldChunk.BlockCountY * 2, max + 1));
        var left = GenerateAABBCollider(new Vector3(min - 1, 0, min - 1), new Vector3(min, WorldChunk.BlockCountY * 2, max + 1));
        var right = GenerateAABBCollider(new Vector3(max, 0, min - 1), new Vector3(max + 1, WorldChunk.BlockCountY * 2, max + 1));

        var colliders = new GameObject("BorderColliders");
        colliders.transform.parent = manager.transform;

        front.transform.parent = colliders.transform;
        back.transform.parent = colliders.transform;
        left.transform.parent = colliders.transform;
        right.transform.parent = colliders.transform;
    }

    private GameObject GenerateAABBCollider(Vector3 min, Vector3 max)
    {
        GameObject colliderObj = new GameObject("AABBCollider");
        colliderObj.transform.localPosition = (min + max) / 2;

        BoxCollider boxCollider = colliderObj.AddComponent<BoxCollider>();
        boxCollider.size = max - min;

        return colliderObj;
    }

    private void ForceInitialize()
    {
        WorldManager manager = (WorldManager)target;
        manager.Init();

        // Initialize all chunks
        for (int i = 0; i < manager.WorldChunks.Length; i++)
        {
            manager.WorldChunks[i].Init();
        }

        // Initialize all renderers
        for (int i = 0; i < manager.ChunkRenderers.Length; i++)
        {
            manager.ChunkRenderers[i].Init();
        }
    }

    private void TestChunk()
    {
        // Assign the renderer 0 to the middle chunk and generate its mesh
        WorldManager manager = (WorldManager)target;
        if (manager.WorldChunks.Length == 0 || manager.ChunkRenderers.Length == 0) return;

        int middleIndex = manager.WorldChunks.Length / 2;
        manager.ChunkRenderers[0].Chunk = manager.WorldChunks[middleIndex];
        manager.ChunkRenderers[0].gameObject.transform.position =
            manager.WorldChunks[middleIndex].gameObject.transform.position;

        manager.ChunkRenderers[0].GenerateAllSubchunks();
    }

    private void TestCombining()
    {
        WorldManager manager = (WorldManager)target;
        if (manager.WorldChunks.Length == 0 || manager.ChunkRenderers.Length == 0) return;

        manager.ChunkRenderers[0].GenerateCombinedMesh();
    }
}
#endif