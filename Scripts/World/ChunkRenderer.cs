using System;
using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ChunkRenderer : UdonSharpBehaviour
{
    // References assigned in the editor
    public ChunkRendererSubchunk[] SubChunks;
    public WorldChunk Chunk;
    public WorldManager Manager;

    // References
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;

    public void Init()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();

        if (_meshFilter == null || _meshRenderer == null)
        {
            Debug.LogError(
                $"CHUNK RENDERER : {gameObject.name} : " +
                $"Missing MeshFilter or MeshRenderer component!"
            );
        }

        for (int i = 0; i < SubChunks.Length; i++)
        {
            if (SubChunks[i] == null)
            {
                Debug.LogError(
                    $"CHUNK RENDERER : {gameObject.name} : " +
                    $"Subchunk at index {i} is not assigned!"
                );
                continue;
            }

            SubChunks[i].Init();
            SubChunks[i].DisableMeshRenderer();
        }
    }

    public void SetChunk(WorldChunk chunk)
    {
        if (chunk == null)
        {
            Chunk = null;
            return;
        }
        Chunk = chunk;
        transform.position = chunk.transform.position;

        if (!Manager.DisableAllLogs)
        {
            Debug.Log($"CHUNK RENDERER : {gameObject.name} : Set chunk to {chunk.gameObject.name}");
        }
    }

    public void GenerateAllSubchunks()
    {
        if (SubChunks == null)
        {
            Debug.LogWarning(
                $"CHUNK RENDERER : {gameObject.name} : " +
                $"SubChunks array is not assigned!"
            );
            return;
        }

        for (int i = 0; i < SubChunks.Length; i++)
        {
            if (SubChunks[i] == null)
            {
                Debug.LogWarning(
                    $"CHUNK RENDERER : {gameObject.name} : " +
                    $"Subchunk at index {i} is not assigned, skipping!"
                );
                continue;
            }
            SubChunks[i].GenerateMesh();
        }
    }

    public void GenerateSubchunkAtIndex(int index)
    {
        if (index < 0 || index >= SubChunks.Length)
        {
            Debug.LogWarning(
                $"CHUNK RENDERER : {gameObject.name} : " +
                $"Invalid subchunk index {index}, out of bounds!"
            );
            return;
        }

        if (SubChunks == null || SubChunks[index] == null)
        {
            Debug.LogWarning(
                $"CHUNK RENDERER : {gameObject.name} : " +
                $"Subchunk at index {index} is not assigned, skipping!"
            );
            return;
        }

        SubChunks[index].GenerateMesh();
    }

    public void GenerateSubchunkAtPos(Vector3Int localPosition)
    {
        if (localPosition.x < 0 || localPosition.x >= WorldChunk.BlockCountX ||
            localPosition.y < 0 || localPosition.y >= WorldChunk.BlockCountY ||
            localPosition.z < 0 || localPosition.z >= WorldChunk.BlockCountZ)
        {
            Debug.LogWarning(
                $"CHUNK RENDERER : {gameObject.name} : " +
                $"Invalid local position {localPosition}, out of bounds!"
            );
            return;
        }

        Vector3Int chunkPos = localPosition / ChunkRendererSubchunk.Size;
        int index = chunkPos.x + chunkPos.z * 2 + chunkPos.y * 4;

        if (SubChunks == null || SubChunks[index] == null)
        {
            Debug.LogWarning(
                $"CHUNK RENDERER : {gameObject.name} : " +
                $"Subchunk at index {index} is not assigned, skipping!"
            );
            return;
        }

        SubChunks[index].GenerateMesh();
    }

    // TODO rework
    public void GenerateCombinedMesh()
    {
        double timeStart = Time.realtimeSinceStartupAsDouble;

        CombineInstance[] combine = new CombineInstance[SubChunks.Length];
        for (int i = 0; i < SubChunks.Length; i++)
        {
            combine[i].mesh = SubChunks[i].GetMesh();
            combine[i].transform = SubChunks[i].transform.localToWorldMatrix;
        }

        Mesh combinedMesh = new Mesh();
        combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        combinedMesh.CombineMeshes(combine, mergeSubMeshes: true, useMatrices: true);
        _meshFilter.mesh = combinedMesh;

        if (!Manager.DisableAllLogs)
        {
            double timeEnd = Time.realtimeSinceStartupAsDouble;
            Debug.Log($"CHUNK RENDERER : {gameObject.name} : Combined mesh generated in {(timeEnd - timeStart) * 1000:0.00}ms");
        }
    }

    public void DisableSubchunkRenderers()
    {
        foreach (var subchunk in SubChunks)
        {
            if (subchunk == null)
            {
                Debug.LogWarning(
                    $"CHUNK RENDERER : {gameObject.name} : " +
                    $"One of the subchunks is not assigned, skipping!"
                );
                continue;
            }
            subchunk.DisableMeshRenderer();
        }
    }
}
