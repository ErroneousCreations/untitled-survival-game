using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using EditorAttributes;

public class NetWorldFeature : NetworkBehaviour
{
    // first one is the index of what world feature it is (e.g. house or tree) second is the index of the generated feature in the world (if its house 1 or house 2 etc) and last one is the index of the type of feature (e.g. house type 1 or house type 2)
    private NetworkVariable<int> worldFeatureIndex = new(), generatedFeatureIndex = new(), featureTypeIndex = new();
    public bool Destroyable;
    [SerializeField, ReadOnly, ShowField(nameof(Destroyable))] private NetworkVariable<float> CurrHealth;
    [SerializeField, ShowField(nameof(Destroyable))] private float MaxHealth;
    [SerializeField, ShowField(nameof(Destroyable))] private List<LootDropStruct> drops;
    [ShowField(nameof(Destroyable))] public string Breakparticle;

    public void Init(int worldFeatureIndex, int generatedFeatureIndex, int featureTypeIndex)
    {
        this.worldFeatureIndex.Value = worldFeatureIndex;
        this.generatedFeatureIndex.Value = generatedFeatureIndex;
        this.featureTypeIndex.Value = featureTypeIndex;
        if (Destroyable)
        {
            CurrHealth = new NetworkVariable<float>();
            CurrHealth.Value = MaxHealth;
        }
    }


    /// <summary>
    /// Gets the saved data that will be saved with this object. subclasses can change this to have custom saved data (e.g. cooldown or some bs idk)
    /// </summary>
    public virtual string GetSavedData
    {
        get
        {
            return System.Math.Round(CurrHealth.Value, 1).ToString();
        }
    }

    /// <summary>
    /// Loads whatever you want from given saved data. subclasses can change this to do stuff with their custom saved data
    /// </summary>
    public virtual void LoadFromSavedData(string data)
    {
        CurrHealth.Value = float.Parse(data);
    }

    public void SpawnItemAtMe(string item)
    {
        SpawnItemRPC(item);
    }

    [Rpc(SendTo.Owner, RequireOwnership = false)]
    private void SpawnItemRPC(string id)
    {
        var curr = Instantiate(ItemDatabase.GetItem(id).ItemPrefab, transform.position + Vector3.up*0.5f, Quaternion.identity);
        curr.NetworkObject.Spawn();
        curr.InitSavedData();
    }

    public void Attack(float damage)
    {
        if (!Destroyable) return;
        AttackRPC(damage);
    }

    [Rpc(SendTo.Owner, RequireOwnership = false)]
    public void AttackRPC(float damage)
    {
        if (!Destroyable) return;
        CurrHealth.Value -= damage;
        if(CurrHealth.Value <= 0)
        {
            NetworkObject.Despawn();
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
    }

    public void RemoveFeature()
    {
        RemoveRPC();
    }

    [Rpc(SendTo.Owner, RequireOwnership = false)]
    private void RemoveRPC()
    {
        NetworkObject.Despawn();
    }

    public void ChangeFeature(int newfeaturetype)
    {
        SwapRPC(newfeaturetype);
    }

    [Rpc(SendTo.Owner, RequireOwnership = false)]
    private void SwapRPC(int newtype)
    {
        var ob = Instantiate(World.GetNetWorldFeaturePrefab(worldFeatureIndex.Value, newtype));
        ob.NetworkObject.Spawn();
        ob.Init(worldFeatureIndex.Value, generatedFeatureIndex.Value, newtype);
        NetworkObject.Despawn();
    }

    /// <summary>
    /// The index of the world feature in the worldgenerator script (e.g. house or tree)
    /// </summary>
    public int GetFeatureIndex => worldFeatureIndex.Value;
    /// <summary>
    /// The index of the generated feature in the world (e.g. house 1 or house 2 etc)
    /// </summary>
    public int GetGeneratedFeatureIndex => generatedFeatureIndex.Value;
    /// <summary>
    /// The index of the type of feature (e.g. house type 1 or house type 2)
    /// </summary>
    public int GetFeatureType => featureTypeIndex.Value;
}
