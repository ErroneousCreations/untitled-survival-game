using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine.UI;

public class Spawner_Femtanyl : BaseCreatureSpawner<AI_Femtanyl>
{
    private bool hasScout;

    [Header("Items")]
    public Vector2Int ItemAmountRange;
    public PickupableItem[] itemPool;
    public float ItemSpawnRange;

    protected override void Loaded(List<string> data)
    {
        base.Loaded(data);
        hasScout = data[0].Split('|')[2] == "1"; // Check if the scout exists in the saved data
    }

    protected override void OnBeforeSaveInitialise()
    {
        base.OnBeforeSaveInitialise();
        hasScout = true; //well there will always be a scout first time it spawns them so yeah
        for (int i = 0; i < Random.Range(ItemAmountRange.x, ItemAmountRange.y + 1); i++)
        {
            var item = Instantiate(itemPool[Random.Range(0, itemPool.Length)], transform.position + Vector3.up * 1.5f + Extensions.RandomCircle * ItemSpawnRange, Quaternion.identity);
            item.NetworkObject.Spawn();
            item.InitSavedData();
        }
    }

    protected override void SpawnNewCreature(int i)
    {
        AI_Femtanyl newFemtanyl = Instantiate(creature, transform.position + Extensions.RandomCircle * SpawnRange, Quaternion.identity);
        newFemtanyl.NetworkObject.Spawn();
        newFemtanyl.InitialiseCreature(Random.Range(0, 9999), transform.position, i == 0);
        if (i == 0) { hasScout = true; } // Set hasScout to true if this is the first creature spawned
        spawnedCreatures.Add(newFemtanyl);
    }

    protected override void LoadCreature(int i, string[] data)
    {
        AI_Femtanyl newFemtanyl = Instantiate(creature, new Vector3(float.Parse(data[0]), float.Parse(data[1]), float.Parse(data[2])), Quaternion.identity);
        newFemtanyl.NetworkObject.Spawn();
        newFemtanyl.InitialiseCreature(int.Parse(data[3]), transform.position, data[4] == "1");
        newFemtanyl.SetRighthandItem(new ItemData(data[5]));
        newFemtanyl.SetLefthandItem(new ItemData(data[6]));
        newFemtanyl.health.ApplySavedData(data[7]);
        spawnedCreatures.Add(newFemtanyl);
    }

    protected override void DespawnCreatures()
    {
        for (int i = 0; i < spawnedCreatures.Count; i++)
        {
            if (spawnedCreatures[i] != null)
            {
                spawnedCreatures[i].NetworkObject.Despawn(true);
            }
        }
        base.DespawnCreatures();
    }

    protected override void OnRespawn()
    {
        base.OnRespawn();
        for (int i = 0; i < enemyCount - mySaver.SavedData.Count - 1; i++)
        {
            var data = $"{Extensions.VecToString(transform.position + Extensions.RandomCircle*SpawnRange, "|")}|{Random.Range(0, 9999)}|{!hasScout}|null|null|null";
            mySaver.SavedData.Add(data);
            if (spawned) { LoadCreature(0, data.Split('|')); } //spawn the actual creature if we want to do that
            if (!hasScout) { hasScout = true; } // Set hasScout to true after we spawn a new scout
        }
        var obs = Physics.OverlapSphere(transform.position + Vector3.up * 1.5f, ItemSpawnRange, Extensions.ItemLayermask);
        if (obs.Length <= 4)
        {
            for (int i = 0; i < Random.Range(ItemAmountRange.x, ItemAmountRange.y + 1); i++)
            {
                var item = Instantiate(itemPool[Random.Range(0, itemPool.Length)], transform.position + Vector3.up*1.5f + Extensions.RandomCircle * ItemSpawnRange, Quaternion.identity);
                item.NetworkObject.Spawn();
                item.InitSavedData();
            }
        }
    }

    protected override string GetFirstSaveData => base.GetFirstSaveData + $"|{hasScout}";

    protected override void UpdateSaveData()
    {
        for (int i = 0; i < spawnedCreatures.Count; i++)
        {
            if(i+1 >= mySaver.SavedData.Count) { mySaver.SavedData.Add(""); } //ensure it exists
            if (spawnedCreatures[i] != null)
            {
                mySaver.SavedData[i + 1] = $"{Extensions.VecToString(spawnedCreatures[i].transform.position, "|")}|{spawnedCreatures[i].GetID}|{(spawnedCreatures[i].GetIsScout ? "1" : "0")}|{spawnedCreatures[i].GetRighthandItem}|{spawnedCreatures[i].GetLefthandItem}|{spawnedCreatures[i].health.GetSavedData}";
            }
            else
            {
                if (mySaver.SavedData[i+1].Length>0 && mySaver.SavedData[i + 1].Split('|')[4] == "1") { hasScout = false; } // Set hasScout to false if the scout is dead
                mySaver.SavedData.RemoveAt(i + 1); //remove the data if the creature is null
                spawnedCreatures.RemoveAt(i); //remove the creature from the list
                i--; //decrement i to account for the removed creature
                currRespawnCooldown = RespawnTime; //reset the respawn cooldown
            }
        }
    }

    protected override void DrawGizmos()
    {
        base.DrawGizmos();
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position+Vector3.up*1.5f, ItemSpawnRange);
    } 
}

