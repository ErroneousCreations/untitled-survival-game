using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class Spawner_Femtanyl : MonoBehaviour
{
    public AI_Femtanyl femtanyl;
    public Vector2Int SpawnAmount;
    public float SpawnRange;

    public void Loaded(List<string> data)
    {
        if(data.Count == 0) { return; }
        foreach (var enemy in data)
        {
            var split = enemy.Split('~');
            AI_Femtanyl newFemtanyl = Instantiate(femtanyl, new Vector3(float.Parse(split[0]), float.Parse(split[1]), float.Parse(split[2])), Quaternion.identity);
            newFemtanyl.NetworkObject.Spawn();
            newFemtanyl.InitialiseCreature(int.Parse(split[3]), transform.position, split[4]=="1");
            newFemtanyl.SetRighthandItem(new ItemData(split[5]));
            newFemtanyl.SetLefthandItem(new ItemData(split[6]));
        }
    }

    private void Start()
    {
        if (World.LoadingFromSave) { return; }
        if (!NetworkManager.Singleton.IsServer) { Destroy(gameObject); return; }
        var amount = Random.Range(SpawnAmount.x, SpawnAmount.y + 1);
        for (int i = 0; i < amount; i++)
        {
            AI_Femtanyl newFemtanyl = Instantiate(femtanyl, transform.position + Extensions.RandomCircle * SpawnRange, Quaternion.identity);
            newFemtanyl.NetworkObject.Spawn();
            newFemtanyl.InitialiseCreature(Random.Range(0, 9999), transform.position, i == 0);
        }
    }

    //todo set the savedata so it actually gets saved
}

