using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PlayerController : UdonSharpBehaviour
{
    public WorldManager WorldManager;
    public GameObject[] PlayerColliders;
    public Transform Selection;
    public float PlayerOffsetY = 0.1f;
    public float ReachDistance = 5f;
    public int RaycastIterations = 20;

    [Tooltip("Do not assign, assigned automatically by PlayerNetworkingManager")]
    public BlockPlaceRequester BlockPlaceRequester;

    private readonly Vector3Int[] _colliderOffsets = {
        new Vector3Int(0, 0, 0),
        new Vector3Int(1, 0, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 0, 1),
        new Vector3Int(0, 0, -1),
        new Vector3Int(0, 1, 0),
        new Vector3Int(1, 1, 0),
        new Vector3Int(-1, 1, 0),
        new Vector3Int(0, 1, 1),
        new Vector3Int(0, 1, -1),
        // new Vector3Int(0, 2, 0),
        new Vector3Int(1, 2, 0),
        new Vector3Int(-1, 2, 0),
        new Vector3Int(0, 2, 1),
        new Vector3Int(0, 2, -1),
        new Vector3Int(0, 3, 0)
    };

    public Vector3Int[] ColliderOffsets => _colliderOffsets;

    void Start()
    {
        if (PlayerColliders == null || PlayerColliders.Length != _colliderOffsets.Length)
        {
            Debug.LogError("Player colliders not assigned correctly!");
            return;
        }
    }

    void Update()
    {
        FollowPlayerTransform();
        UpdateColliders();
        RaycastBlockManipulation();
    }

    public Vector3 GetPlayerPosition()
    {
        return Networking.LocalPlayer.GetPosition();
    }

    public void TeleportToStart()
    {
        Networking.LocalPlayer.TeleportTo(new Vector3(0.5f, 100.5f, 0.5f), Quaternion.identity);
    }

    private void FollowPlayerTransform()
    {
        gameObject.transform.position =
            Vector3Int.FloorToInt(Networking.LocalPlayer.GetPosition());
    }

    private void UpdateColliders()
    {
        Vector3 playerPos = transform.position;
        playerPos.y += PlayerOffsetY;
        Vector3Int playerPosInt = Vector3Int.FloorToInt(playerPos);

        for (int i = 0; i < PlayerColliders.Length; i++)
        {
            bool colliderSolid = 0 != WorldManager.GetBlock(
                playerPosInt + ColliderOffsets[i]
            );
            PlayerColliders[i].SetActive(colliderSolid);
        }
    }

    private void RaycastBlockManipulation()
    {
        bool performedAction = false;
        double timeStart = Time.realtimeSinceStartupAsDouble;

        var head = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        Vector3 origin = head.position;
        Vector3 rotation = head.rotation * Vector3.forward;

        if (WorldManager.RayCast(origin, rotation, out Vector3Int hitPos,
            out Vector3Int normal, ReachDistance, RaycastIterations))
        {
            Selection.position = hitPos + new Vector3(0.5f, 0.5f, 0.5f);

            if (Input.GetMouseButtonDown(0))
            {
                PlaceBlockNetworked(hitPos, 0);
                performedAction = true;
            }

            if (Input.GetMouseButtonDown(1))
            {
                PlaceBlockNetworked(hitPos + normal, 3);
                performedAction = true;
            }
        }
        else
        {
            Selection.position = new Vector3(0, -1000, 0);
        }

        if (performedAction && !WorldManager.DisableAllLogs)
        {
            double timeEnd = Time.realtimeSinceStartupAsDouble;
            Debug.Log($"PLAYER CONTROLLER : Block action performed in {(timeEnd - timeStart) * 1000:0.00}ms");
        }
    }

    private void PlaceBlockNetworked(Vector3Int pos, int type)
    {
        // Place block locally for immediate feedback
        WorldManager.SetBlock(pos, (ushort)type);
        WorldManager.EnqueueMeshGeneration(pos);

        // Send the request over the network
        BlockPlaceRequester.RequestBlockPlace(pos, type);
    }
}
