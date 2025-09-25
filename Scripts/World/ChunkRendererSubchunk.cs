using System;
using UdonSharp;
using Unity.Mathematics;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ChunkRendererSubchunk : UdonSharpBehaviour
{
    // Constants
    public const int Size = 8;
    public const int BlockCount = Size * Size * Size;

    // References assigned in the editor
    public WorldManager Manager;
    public ChunkRenderer ParentRenderer;
    public int IndexInParent = -1;

    // References
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;

    public void Init()
    {
        if (GetComponent<MeshFilter>() == null || GetComponent<MeshRenderer>() == null)
        {
            Debug.LogError($"CHUNK RENDERER SUBCHUNK : {ParentRenderer.gameObject.name} :" +
                $" {gameObject.name} : Missing MeshFilter or MeshRenderer component!");
            return;
        }

        _meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();
        _meshRenderer.enabled = false;
    }

    public void GenerateMesh()
    {
        GenerateMeshInternal();
        SetMaterialPropertyBlock();
        EnableMeshRenderer();
    }

    public Mesh GetMesh()
    {
        return _meshFilter.sharedMesh;
    }

    public void DisableMeshRenderer()
    {
        if (_meshRenderer == null) return;
        _meshRenderer.enabled = false;
    }

    public void EnableMeshRenderer()
    {
        if (_meshRenderer == null) return;
        _meshRenderer.enabled = true;
    }

    private void SetMaterialPropertyBlock()
    {
        if (_meshRenderer == null) return;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetTexture("_BlockPlacementTex", ParentRenderer.Chunk.ChunkBlockTexture);
        block.SetFloat("_IndexInParent", IndexInParent);
        _meshRenderer.SetPropertyBlock(block);
    }

    private void GenerateMeshInternal()
    {
        const ushort isNotFullMask = 0x2000;

        if (_meshFilter == null || _meshRenderer == null)
        {
            Debug.LogError($"CHUNK RENDERER SUBCHUNK : {ParentRenderer.gameObject.name} :" +
                $" {gameObject.name} : MeshFilter or MeshRenderer not initialized!");
            return;
        }

        Vector3Int subchunkOrigin = Vector3Int.FloorToInt(transform.position);
        ushort[] blocks = ParentRenderer.Chunk.Blocks[IndexInParent];
        ushort[] frontBlocks = Manager.GetArrayAtPos(subchunkOrigin + new Vector3Int(0, 0, -Size));
        ushort[] backBlocks = Manager.GetArrayAtPos(subchunkOrigin + new Vector3Int(0, 0, Size));
        ushort[] leftBlocks = Manager.GetArrayAtPos(subchunkOrigin + new Vector3Int(-Size, 0, 0));
        ushort[] rightBlocks = Manager.GetArrayAtPos(subchunkOrigin + new Vector3Int(Size, 0, 0));
        ushort[] topBlocks = Manager.GetArrayAtPos(subchunkOrigin + new Vector3Int(0, Size, 0));
        ushort[] bottomBlocks = Manager.GetArrayAtPos(subchunkOrigin + new Vector3Int(0, -Size, 0));

        // Assign an empty mesh if there are no blocks
        if (blocks == null)
        {
            _meshFilter.mesh = new Mesh();
            DisableMeshRenderer();
            if (!Manager.DisableAllLogs)
            {
                Debug.Log(
                    $"CHUNK RENDERER SUBCHUNK : {gameObject.name} : " +
                    $"No blocks in this subchunk, assigned empty mesh."
                );
            }
            return;
        }

        double timeStart = Time.realtimeSinceStartupAsDouble;

        // Generate the mesh
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[BlockCount * 6 * 4];

        // Do the naive appreach mesh generation
        int vertexCount = 0;
        for (int blockIndex = BlockCount - 1; blockIndex >= 0; blockIndex--)
        {
            var block = blocks[blockIndex];
            if (block == 0) continue;

            Vector3Int blockPos = new Vector3Int(
                blockIndex & 7,
                blockIndex >> 6,
                (blockIndex >> 3) & 7
            );
            Vector3 blockOffset = blockPos;

            int frontBlock = (blockIndex & 56) == 0 ?
                (frontBlocks != null ? frontBlocks[blockIndex | 56] : 0) :
                blocks[blockIndex - 8];
            int backBlock = (blockIndex & 56) == 56 ?
                (backBlocks != null ? backBlocks[blockIndex & ~56] : 0) :
                blocks[blockIndex + 8];
            int rightBlock = (blockIndex & 7) == 7 ?
                (rightBlocks != null ? rightBlocks[blockIndex & ~7] : 0) :
                blocks[blockIndex + 1];
            int leftBlock = (blockIndex & 7) == 0 ?
                (leftBlocks != null ? leftBlocks[blockIndex | 7] : 0) :
                blocks[blockIndex - 1];
            int topBlock = (blockIndex & 448) == 448 ?
                (topBlocks != null ? topBlocks[blockIndex & ~448] : 0) :
                blocks[blockIndex + 64];
            int bottomBlock = (blockIndex & 448) == 0 ?
                (bottomBlocks != null ? bottomBlocks[blockIndex | 448] : 0) :
                blocks[blockIndex - 64];

            bool frontVisible  = frontBlock  == 0 || (frontBlock  & isNotFullMask) != 0;
            bool backVisible   = backBlock   == 0 || (backBlock   & isNotFullMask) != 0;
            bool rightVisible  = rightBlock  == 0 || (rightBlock  & isNotFullMask) != 0;
            bool leftVisible   = leftBlock   == 0 || (leftBlock   & isNotFullMask) != 0;
            bool topVisible    = topBlock    == 0 || (topBlock    & isNotFullMask) != 0;
            bool bottomVisible = bottomBlock == 0 || (bottomBlock & isNotFullMask) != 0;

            if (frontVisible)
            {
                vertices[vertexCount++] = new Vector3(0, 0, 0) + blockOffset;
                vertices[vertexCount++] = new Vector3(1, 0, 0) + blockOffset;
                vertices[vertexCount++] = new Vector3(1, 1, 0) + blockOffset;
                vertices[vertexCount++] = new Vector3(0, 1, 0) + blockOffset;
            }

            if (backVisible)
            {
                vertices[vertexCount++] = new Vector3(1, 0, 1) + blockOffset;
                vertices[vertexCount++] = new Vector3(0, 0, 1) + blockOffset;
                vertices[vertexCount++] = new Vector3(0, 1, 1) + blockOffset;
                vertices[vertexCount++] = new Vector3(1, 1, 1) + blockOffset;
            }

            if (rightVisible)
            {
                vertices[vertexCount++] = new Vector3(1, 0, 0) + blockOffset;
                vertices[vertexCount++] = new Vector3(1, 0, 1) + blockOffset;
                vertices[vertexCount++] = new Vector3(1, 1, 1) + blockOffset;
                vertices[vertexCount++] = new Vector3(1, 1, 0) + blockOffset;
            }

            if (leftVisible)
            {
                vertices[vertexCount++] = new Vector3(0, 0, 1) + blockOffset;
                vertices[vertexCount++] = new Vector3(0, 0, 0) + blockOffset;
                vertices[vertexCount++] = new Vector3(0, 1, 0) + blockOffset;
                vertices[vertexCount++] = new Vector3(0, 1, 1) + blockOffset;
            }

            if (topVisible)
            {
                vertices[vertexCount++] = new Vector3(0, 1, 0) + blockOffset;
                vertices[vertexCount++] = new Vector3(1, 1, 0) + blockOffset;
                vertices[vertexCount++] = new Vector3(1, 1, 1) + blockOffset;
                vertices[vertexCount++] = new Vector3(0, 1, 1) + blockOffset;
            }

            if (bottomVisible)
            {
                vertices[vertexCount++] = new Vector3(0, 0, 1) + blockOffset;
                vertices[vertexCount++] = new Vector3(1, 0, 1) + blockOffset;
                vertices[vertexCount++] = new Vector3(1, 0, 0) + blockOffset;
                vertices[vertexCount++] = new Vector3(0, 0, 0) + blockOffset;
            }
        }

        // Shrink the arrays to fit the actual data
        var shrunkVertices = new Vector3[vertexCount];
        var uvs = new Vector2[vertexCount];
        var indices = new int[vertexCount / 4 * 6];
        Array.Copy(vertices, shrunkVertices, vertexCount);
        Array.Copy(Manager.PrecomputedUVs, uvs, vertexCount);
        Array.Copy(Manager.PrecomputedIndices, indices, vertexCount / 4 * 6);
        vertices = shrunkVertices;

        // Delete the old mesh if there was one
        if (_meshFilter.mesh != null)
        {
            Destroy(_meshFilter.mesh);
        }

        // Upload the mesh
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = indices;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        _meshFilter.mesh = mesh;
        EnableMeshRenderer();

        double timeEnd = Time.realtimeSinceStartupAsDouble;

        if (!Manager.DisableSpamLogs && !Manager.DisableAllLogs)
        {
            Debug.Log(
                $"CHUNK RENDERER SUBCHUNK : {gameObject.name} : Generated mesh " +
                $"in {(timeEnd - timeStart) * 1000.0:0.00} ms"
            );
        }
    }
}
