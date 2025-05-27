using UnityEngine;
using Unity.Netcode;

public class WorldFeature_Tree : WorldFeature
{
    private float DropItemsTime = 0;
    public CutTreeTrunk CutTree;

    public override void LoadFromSavedData(string data)
    {
        var split = data.Split(',');
        CurrHealth = float.Parse(split[0]);
        DropItemsTime = float.Parse(split[1]);
    }

    public override string GetSavedData
    {
        get
        {
            return $"{System.Math.Round(CurrHealth, 1)},{System.Math.Round(DropItemsTime, 1)}";
        }
    }

    protected override void Die()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            var ob = Instantiate(CutTree, transform.position, transform.rotation);
            ob.NetworkObject.Spawn();
            for (int i = 0; i < drops.Count; i++)
            {
                if (Random.value <= drops[i].Chance)
                {
                    var curr = Instantiate(ItemDatabase.GetItem(drops[i].ItemID).ItemPrefab, transform.position + Vector3.up * 0.75f, Quaternion.identity);
                    curr.NetworkObject.Spawn();
                    curr.InitSavedData();
                }
            }
        }
        Destroy(gameObject);
    }
}
