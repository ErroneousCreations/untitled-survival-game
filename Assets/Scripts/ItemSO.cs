using System.Collections.Generic;
using UnityEngine;
using AYellowpaper.SerializedCollections;

[CreateAssetMenu(fileName = "New Item", menuName = "ScriptableObjects/Item")]
public class ItemSO : ScriptableObject
{
    [Header("Item Properties")]
    public string Name;
    public string ID;
    /// <summary>
    /// In litres for backpack capacity
    /// </summary>
    public float Volume;
    public ItemTypeEnum ItemType;
    public List<string> BaseSavedData;
    public PickupableItem ItemPrefab;
    public Object _itemBehaviour;
    public SerializedDictionary<string, string> CustomItemProperties;

    [Header("Held Properties")]
    public Mesh HeldMesh;
    public Material[] HeldMats;

    public Item ToItem => new() { ID = ID,
        Name = Name,
        Volume = Volume,
        ItemType = ItemType,
        BaseSavedData = BaseSavedData,
        ItemPrefab = ItemPrefab,
        _itemBehaviour = _itemBehaviour,
        CustomItemProperties = CustomItemProperties,
        HeldMesh = HeldMesh,
        HeldMats = HeldMats
    };
}
