using UnityEngine;
using EditorAttributes;
using DG.Tweening;
using System.Collections.Generic;
using System.ComponentModel.Design;
using Unity.Netcode;

[System.Serializable]
public struct LootDropStruct
{
    public string ItemID;
    public float Chance;
}

public class WorldFeature : MonoBehaviour
{
    // first one is the index of what world feature it is (e.g. house or tree) second is the index of the generated feature in the world (if its house 1 or house 2 etc) and last one is the index of the type of feature (e.g. house type 1 or house type 2)
    private int worldFeatureIndex, generatedFeatureIndex, featureTypeIndex;

    [SerializeField] private bool Destroyable;
    [SerializeField, ReadOnly, ShowField(nameof(Destroyable))] private float CurrHealth;
    [SerializeField, ShowField(nameof(Destroyable))] private float MaxHealth;
    [SerializeField, ShowField(nameof(Destroyable))] private List<LootDropStruct> drops;

    public void Init(int worldFeatureIndex, int generatedFeatureIndex, int featureTypeIndex)
    {
        this.worldFeatureIndex = worldFeatureIndex;
        this.generatedFeatureIndex = generatedFeatureIndex;
        this.featureTypeIndex = featureTypeIndex;
        if (Destroyable)
        {
            CurrHealth = MaxHealth;
        }
    }

    public void SpawnItemAtMe(string item)
    {
        World.SpawnItemAtWorldfeature(item, worldFeatureIndex, generatedFeatureIndex);
    }

    public void Attack(float damage)
    {
        if(!Destroyable) return;
        World.WorldFeatureDamaged(worldFeatureIndex, generatedFeatureIndex, damage);
    }

    public void __RemoveHealth(float damage)
    {
        CurrHealth -= damage;
        if (CurrHealth <= 0)
        {
            if (NetworkManager.Singleton.IsServer)
            {
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
        else
        {
            transform.DOPunchScale(Vector3.one * 0.95f, 0.2f);
        }
    }

    public void RemoveFeature()
    {
        World.WorldFeatureDestroyed(worldFeatureIndex, generatedFeatureIndex);
    }

    public void ChangeFeature(int newfeaturetype)
    {
        World.WorldFeatureSwapped(worldFeatureIndex, generatedFeatureIndex, newfeaturetype);
    }

    /// <summary>
    /// The index of the world feature in the worldgenerator script (e.g. house or tree)
    /// </summary>
    public int GetFeatureIndex => worldFeatureIndex;
    /// <summary>
    /// The index of the generated feature in the world (e.g. house 1 or house 2 etc)
    /// </summary>
    public int GetGeneratedFeatureIndex => generatedFeatureIndex;
    /// <summary>
    /// The index of the type of feature (e.g. house type 1 or house type 2)
    /// </summary>
    public int GetFeatureType => featureTypeIndex;
}
