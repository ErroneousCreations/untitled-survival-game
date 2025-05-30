using Unity.Netcode;
using UnityEngine;

public class NetworkObjectLOD : NetworkBehaviour
{
    public float VisibilityDistance = 5.0f;
    private float cacheVisDist;

    /// <summary>
    /// This is automatically invoked when spawning the network prefab
    /// relative to each client.
    /// </summary>
    /// <param name="clientId">client identifier to check</param>
    /// <returns>true/false whether it is visible to the client or not</returns>
    private bool CheckVisibility(ulong clientId)
    {
        // If not spawned, then always return false
        if (!IsSpawned)
        {
            return false;
        }

        if (!(NetworkManager.ConnectedClients[clientId].PlayerObject)) { return false; }

        // We can do a simple distance check between the NetworkObject instance position and the client
        return (NetworkManager.ConnectedClients[clientId].PlayerObject.transform.position - transform.position).sqrMagnitude <= cacheVisDist;
    }

    public override void OnNetworkSpawn()
    {
        cacheVisDist = VisibilityDistance*VisibilityDistance;
        if (IsServer)
        {
            // The server handles visibility checks and should subscribe when spawned locally on the server-side.
            NetworkObject.CheckObjectVisibility += CheckVisibility;
            // If we want to continually update, we don't need to check every frame but should check at least once per tick
            NetworkManager.NetworkTickSystem.Tick += OnNetworkTick;
        }
        base.OnNetworkSpawn();
    }

    private void OnNetworkTick()
    {
        // If CheckObjectVisibility is enabled, check the distance to clients
        // once per network tick.
        foreach (var clientId in NetworkManager.ConnectedClientsIds)
        {
            var shouldBeVisibile = CheckVisibility(clientId);
            var isVisibile = NetworkObject.IsNetworkVisibleTo(clientId);
            if (shouldBeVisibile && !isVisibile)
            {
                // Note: This will invoke the CheckVisibility check again
                NetworkObject.NetworkShow(clientId);
            }
            else if (!shouldBeVisibile && isVisibile)
            {
                NetworkObject.NetworkHide(clientId);
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkObject.CheckObjectVisibility -= CheckVisibility;
            NetworkManager.NetworkTickSystem.Tick -= OnNetworkTick;
        }
        base.OnNetworkDespawn();
    }
}