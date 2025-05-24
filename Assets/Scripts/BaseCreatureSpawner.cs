using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class BaseCreatureSpawner<T> : MonoBehaviour
{
    public T creature;
    public Vector2Int SpawnAmount;
    public float SpawnRange, RespawnTime;
    public SavedObject mySaver;
    protected List<T> spawnedCreatures = new List<T>();
    protected List<Rigidbody> playersNearby = new();
    protected float currRespawnCooldown;
    protected int enemyCount = 0;
    protected bool spawned = false;

    /// <summary>
    /// make sure to call base here or else you fuck it up
    /// </summary>
    protected virtual void Loaded(List<string> data)
    {
        enemyCount = int.Parse(data[0].Split('|')[0]);
        currRespawnCooldown = float.Parse(data[0].Split('|')[1]);
    }

    private void Start()
    {
        OnStart();   
    }

    protected virtual void OnStart()
    {
        if (!NetworkManager.Singleton.IsServer) { Destroy(gameObject); return; }
        mySaver.OnDataLoaded_Data += Loaded;
        if (World.LoadingFromSave) { return; }
        //init stuff
        OnBeforeSaveInitialise();
    }

    protected virtual void OnBeforeSaveInitialise()
    {
        currRespawnCooldown = RespawnTime;
        enemyCount = Random.Range(SpawnAmount.x, SpawnAmount.y + 1);
    }

    protected virtual void SpawnCreatures()
    {
        spawned = true;
        if (mySaver.SavedData.Count <= 1)
        {
            spawnedCreatures = new();
            for (int i = 0; i < enemyCount; i++)
            {
                SpawnNewCreature(i);
            }
        }
        else
        {
            //skip 0 cuz its the respawn time and stuff
            for (int i = 1; i < mySaver.SavedData.Count; i++)
            {
                LoadCreature(i, mySaver.SavedData[i].Split('|'));
            }
        }
    }

    protected virtual void LoadCreature(int i, string[] split)
    {
        //unimplemented
    }

    protected virtual void SpawnNewCreature(int i)
    {
        //unimplemented
    }

    /// <summary>
    /// When you implement this, call base AFTER!!!!
    /// </summary>
    protected virtual void DespawnCreatures()
    {
        spawnedCreatures.Clear();
        spawned = false;
    }

    /// <summary>
    /// When you implement this, call base FIRST!!!!
    /// </summary>
    protected virtual void OnRespawn()
    {
        if(mySaver.SavedData.Count <= 1) { return; } //never even spawned our enemies before, don't just respawn them
        currRespawnCooldown = RespawnTime;
    }

    private void Update()
    {
        if (!mySaver.GetLoaded && World.LoadingFromSave) { return; }

        for (int i = 0; i < playersNearby.Count; i++)
        {
            if (!playersNearby[i])
            {
                playersNearby.RemoveAt(i);
                i--;
            }
        }

        if (!spawned && playersNearby.Count > 0)
        {
            SpawnCreatures();
        }

        if (spawned && playersNearby.Count == 0)
        {
            DespawnCreatures();
        }

        mySaver.SavedData[0] = GetFirstSaveData;

        if (spawned) { UpdateSaveData(); }
    }

    protected virtual string GetFirstSaveData => $"{System.Math.Round(currRespawnCooldown)}|{enemyCount}";

    protected virtual void UpdateSaveData()
    {

    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !playersNearby.Contains(other.attachedRigidbody))
        {
            playersNearby.Add(other.attachedRigidbody);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && playersNearby.Contains(other.attachedRigidbody))
        {
            playersNearby.Remove(other.attachedRigidbody);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, SpawnRange);
        DrawGizmos();
    }

    protected virtual void DrawGizmos()
    {

    }
}

