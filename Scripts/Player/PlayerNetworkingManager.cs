using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PlayerNetworkingManager : UdonSharpBehaviour
{
    [Tooltip("Objects to assign ownership to players")]
    public GameObject[] objects;
    public PlayerController playerController;

    void Start()
    {
        if (Networking.IsMaster)
        {
            AssignObjectOwnership();
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "AssignMyObject");
        }
    }

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        ReassignOwnership();
    }

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        ReassignOwnership();
    }

    private void ReassignOwnership()
    {
        if (!Networking.IsMaster) return;
        AssignObjectOwnership();
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "AssignMyObject");
    }

    private void AssignObjectOwnership()
    {
        VRCPlayerApi[] players = new VRCPlayerApi[80]; // VRChat max players
        players = VRCPlayerApi.GetPlayers(players);
        int count = VRCPlayerApi.GetPlayerCount();

        for (int i = 0; i < count && i < objects.Length; i++)
        {
            if (Utilities.IsValid(objects[i]))
            {
                Networking.SetOwner(players[i], objects[i]);
            }
        }
    }

    public void AssignMyObject()
    {
        for (int i = 0; i < objects.Length; i++)
        {
            if (Utilities.IsValid(objects[i]) && Networking.IsOwner(objects[i]))
            {
                playerController.BlockPlaceRequester =
                    objects[i].GetComponent<BlockPlaceRequester>();
                return;
            }
        }

        playerController.BlockPlaceRequester = null;
        Debug.LogWarning("No owned object found for player " + Networking.LocalPlayer.displayName);
    }
}
