
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class BlockPlaceRequester : UdonSharpBehaviour
{
    [UdonSynced] private int _blockPlacePosX;
    [UdonSynced] private int _blockPlacePosY;
    [UdonSynced] private int _blockPlacePosZ;
    [UdonSynced] private int _blockPlaceType;

    public WorldManager WorldManager;

    void Start()
    {
        if (WorldManager == null)
        {
            Debug.LogError("WorldManager not assigned in BlockPlaceRequester!");
        }
    }

    public void RequestBlockPlace(Vector3Int position, int type)
    {
        if (!Networking.IsOwner(gameObject))
        {
            Debug.LogWarning("Requester is not owned by the requesting player!");
            return;
        }

        // Send the request over the network
        _blockPlacePosX = position.x;
        _blockPlacePosY = position.y;
        _blockPlacePosZ = position.z;
        _blockPlaceType = type;
        RequestSerialization();
    }

    public override void OnDeserialization()
    {
        Debug.Log($"[REQUESTER : {gameObject.name}] Block place request received");
        var pos = new Vector3Int(_blockPlacePosX, _blockPlacePosY, _blockPlacePosZ);
        WorldManager.SetBlock(pos, (ushort)_blockPlaceType);
        WorldManager.EnqueueMeshGeneration(pos, enqueueNeighbouring: true, highPriority: true);
    }
}
