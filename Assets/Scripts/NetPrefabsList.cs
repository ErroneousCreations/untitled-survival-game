using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class NetPrefabsList : NetworkBehaviour
{
    [System.Serializable]
    public struct NetPrefab
    {
        public string ID;
        public NetworkObject prefab;
    }

    public NetPrefab[] netPrefabs;

    public static Dictionary<string, NetworkObject> NetworkPrefabList = new();
    public static NetPrefabsList instance;

    private void Awake()
    {
        instance = this;
        NetworkPrefabList = new();
        for (int i = 0; i < netPrefabs.Length; i++)
        {
            NetworkPrefabList.Add(netPrefabs[i].ID, netPrefabs[i].prefab);
        }
    }

    public static NetworkObject GetNetPrefab(string id)
    {
        if (!NetworkPrefabList.ContainsKey(id)) { return null; }
        return NetworkPrefabList[id];
    }

    public static void SpawnObject(string prefab, Vector3 pos, Quaternion rot, float destroytime = -1)
    {
        instance.SpawnObject_ServerRPC(prefab, pos, rot, destroytime);
    }

    public static void SpawnObjectWithOwner(string prefab, Vector3 pos, Quaternion rot, ulong owner, float destroytime = -1)
    {
        instance.SpawnObjectOwner_ServerRPC(prefab, pos, rot, owner, destroytime);
    }

    /// <summary>
    /// Spawn an object on ALL BUT ONE CLIENT (its just not visible to the specific client)
    /// </summary>
    public static void SpawnObjectExcept(string prefab, Vector3 pos, Quaternion rot, ulong except, float destroytime = -1)
    {
        instance.SpawnObjectExcept_ServerRPC(prefab, pos, rot, except, destroytime);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SpawnObjectOwner_ServerRPC(string ob, Vector3 pos, Quaternion rot, ulong owner, float destroytime)
    {
        var spawned = Instantiate(GetNetPrefab(ob), pos, rot);
        spawned.SpawnWithOwnership(owner);
        if (destroytime > 0) { Destroy(spawned.gameObject, destroytime); }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SpawnObject_ServerRPC(string ob, Vector3 pos, Quaternion rot, float destroytime)
    {
        var spawned = Instantiate(GetNetPrefab(ob), pos, rot);
        spawned.transform.rotation = rot;
        spawned.Spawn();
        if (destroytime > 0) { Destroy(spawned.gameObject, destroytime); }
    }
    

    [ServerRpc(RequireOwnership = false)]
    private void SpawnObjectExcept_ServerRPC(string ob, Vector3 pos, Quaternion rot, ulong except, float destroytime)
    {
        SpawnObjectExcept_ClientRPC(ob, pos, rot, except, destroytime);
    }

    [ClientRpc]
    private void SpawnObjectExcept_ClientRPC(string ob, Vector3 pos, Quaternion rot, ulong except, float destroytime)
    {
        if (NetworkManager.Singleton.LocalClientId == except) { return; }
        var spawned = Instantiate(GetNetPrefab(ob), pos, rot);
        if (destroytime > 0) { Destroy(spawned.gameObject, destroytime); }
    }
}
