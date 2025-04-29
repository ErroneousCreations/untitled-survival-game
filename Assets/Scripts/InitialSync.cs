using UnityEngine;
using Unity.Netcode;

public class InitialSync : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            return;

        SyncPosRPC(transform.position, transform.rotation);
    }

    [Rpc(SendTo.NotOwner)]
    private void SyncPosRPC(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
        Destroy(this);
    }
}