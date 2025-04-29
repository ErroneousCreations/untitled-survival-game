using UnityEngine;
using EditorAttributes;
using System.Collections.Generic;
using Unity.Netcode;


public class WorldgenSpawner : MonoBehaviour
{
    [SerializeField] private bool Networked;

    [SerializeField, EnableField(nameof(Networked))] private List<NetworkObject> netSpawnPool;
    [DisableField(nameof(Networked)), SerializeField] private List<GameObject> spawnPool;
    [SerializeField] private List<Transform> spawnPoints;
    [SerializeField] private float spawnProbability = 0.95f;
    [SerializeField] private bool onlySpawnBeforeSave = true, drawGizmos = true;
    [SerializeField, HideField(nameof(Networked))] private bool parentOnSpawnpoint = true;

    private void Start()
    {
        if (World.LoadingFromSave && onlySpawnBeforeSave) { Destroy(this); return; }

        if (Networked)
        {
            if(!NetworkManager.Singleton.IsServer) { Destroy(this); return; }
            foreach (var spawnPoint in spawnPoints)
            {
                if (World.RandomValue < spawnProbability)
                {
                    Instantiate(netSpawnPool[World.RandomIntRange(0, netSpawnPool.Count)], spawnPoint.position, spawnPoint.rotation, null).Spawn();
                }
            }
        }
        else
        {
            foreach (var spawnPoint in spawnPoints)
            {
                if (World.RandomValue < spawnProbability)
                {
                    Instantiate(spawnPool[World.RandomIntRange(0, spawnPool.Count)], spawnPoint.position, spawnPoint.rotation, parentOnSpawnpoint ? spawnPoint : null);
                }
            }
        }

        Destroy(this);
    }

    private void OnDrawGizmos()
    {
        if(!drawGizmos) { return; }
        foreach (var spawnPoint in spawnPoints)
        {
            if(!spawnPoint) { continue; }
            if (Networked)
            {
                Gizmos.color = Color.red;
                //Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.5f);
                foreach (var ob in netSpawnPool)
                {
                    if (!ob) { continue; }
                    Gizmos.matrix = Matrix4x4.identity;
                    Matrix4x4 rotationMatrix = spawnPoint.localToWorldMatrix;
                    Gizmos.matrix = rotationMatrix;
                    var rend = ob.GetComponentInChildren<MeshRenderer>();
                    var offset = rend.transform.localPosition;
                    var extents = rend.bounds.extents;
                    var centre = rend.bounds.center;
                    Gizmos.DrawWireCube(offset + centre, extents*2);
                }
            }
            else
            {
                Gizmos.color = Color.green;
                //Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.5f);
                foreach (var ob in spawnPool)
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
}
