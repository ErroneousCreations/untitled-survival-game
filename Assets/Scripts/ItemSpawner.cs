using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class ItemSpawner : MonoBehaviour
{
    public List<Transform> SpawnPoints;
    public List<PickupableItem> SpawnPool;
    [SerializeField] private float SpawnProbability;
    [SerializeField] private bool OnlySpawnBeforeSave;
    [SerializeField] private bool DrawGizmos = true;

    private void Start()
    {
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer) { return; }
        if(OnlySpawnBeforeSave && World.LoadingFromSave) { return; }
        Random.InitState(World.CurrentSeed + (int)(transform.position.x * 100) + (int)(transform.position.z * 100) + (int)transform.position.y);
        foreach (var spawnPoint in SpawnPoints)
        {
            if (Random.value < SpawnProbability)
            {
                var item = Instantiate(SpawnPool[Random.Range(0, SpawnPool.Count)], spawnPoint.position, spawnPoint.rotation);
                item.NetworkObject.Spawn();
                item.InitSavedData();
            }
        }
        Destroy(this);
    }

    private void OnDrawGizmos()
    {
        if (!DrawGizmos) { return; }
        foreach (var spawnPoint in SpawnPoints)
        {
            if (!spawnPoint) { continue; }
            Gizmos.color = Color.red;
            //Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.5f);
            foreach (var ob in SpawnPool)
            {
                if (!ob) { continue; }
                Gizmos.matrix = Matrix4x4.identity;
                Matrix4x4 rotationMatrix = spawnPoint.localToWorldMatrix;
                Gizmos.matrix = rotationMatrix;
                var rend = ob.GetComponentInChildren<MeshRenderer>();
                var offset = rend.transform.localPosition;
                var extents = rend.bounds.extents;
                var centre = rend.bounds.center;
                Gizmos.DrawWireCube(offset + centre, extents * 2);
            }
        }
    }
}
