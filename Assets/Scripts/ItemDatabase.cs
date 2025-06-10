using System.Collections.Generic;
using UnityEngine;
using AYellowpaper.SerializedCollections;
using Unity.Collections;
using Unity.Netcode;

[System.Serializable]
public struct Item
{
    [Header("Item Properties")]
    public string Name, ID;
    /// <summary>
    /// In litres for backpack capacity
    /// </summary>
    public float Volume;
    public ItemTypeEnum ItemType;
    public List<string> BaseSavedData;
    public PickupableItem ItemPrefab;
    [SerializeField] private Object _itemBehaviour;
    public SerializedDictionary<string, string> CustomItemProperties;

    [Header("Held Properties")]
    public Mesh HeldMesh;
    public Material[] HeldMats;

    public readonly bool IsValid => !string.IsNullOrEmpty(ID);

    public static readonly Item Empty = new Item { ID = string.Empty };

    public IItemBehaviour ItemBehaviour => _itemBehaviour as IItemBehaviour;
}

/// <summary>
/// The item saved in your inventory
/// </summary>
[System.Serializable]
public struct ItemData : INetworkSerializable
{
    public FixedString64Bytes ID; // Unity-friendly network string
    public List<FixedString128Bytes> SavedData;
    public List<float> TempData;

    public readonly bool IsValid => !ID.IsEmpty;

    public static readonly ItemData Empty = new() { ID = string.Empty, SavedData = null, TempData = null };

    public ItemData(string id, List<string> savedData)
    {
        ID = new FixedString64Bytes(id);
        SavedData = new List<FixedString128Bytes>(savedData.Count);
        foreach (var data in savedData)
        {
            SavedData.Add(new FixedString64Bytes(data));
        }

        TempData = new List<float>(); // Initialize empty temp data
    }

    public ItemData(string saveditem)
    {
        if(saveditem == "null")
        {
            ID = string.Empty;
            SavedData = null;
            TempData = null;
            return;
        }
        var split = saveditem.Split('`');
        ID = split[0];
        SavedData = new List<FixedString128Bytes>();
        TempData = new List<float>();
        if (split.Length < 2) { return; }
        for (int i = 1; i < split.Length; i++)
        {
            SavedData.Add(split[i]);
        }
    }

    public override string ToString()
    {
        if (!IsValid) { return "null"; }
        var returned = "";
        returned += ID;
        if(SavedData == null || SavedData.Count <= 0) { return returned; }
        returned += "`";
        foreach (var data in SavedData)
        {
            returned += data + "`";
        }
        return returned[..^1];
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ID);

        // Serialize SavedData
        int savedCount = SavedData != null ? SavedData.Count : 0;
        serializer.SerializeValue(ref savedCount);

        if (serializer.IsReader)
        {
            if (SavedData == null)
                SavedData = new List<FixedString128Bytes>(savedCount);
            else
                SavedData.Clear();

            for (int i = 0; i < savedCount; i++)
            {
                FixedString64Bytes str = default;
                serializer.SerializeValue(ref str);
                SavedData.Add(str);
            }
        }
        else
        {
            for (int i = 0; i < savedCount; i++)
            {
                var str = SavedData[i];
                serializer.SerializeValue(ref str);
            }
        }

        // Serialize TempData
        int tempCount = TempData != null ? TempData.Count : 0;
        serializer.SerializeValue(ref tempCount);

        if (serializer.IsReader)
        {
            if (TempData == null)
                TempData = new List<float>(tempCount);
            else
                TempData.Clear();

            for (int i = 0; i < tempCount; i++)
            {
                float value = 0;
                serializer.SerializeValue(ref value);
                TempData.Add(value);
            }
        }
        else
        {
            for (int i = 0; i < tempCount; i++)
            {
                float value = TempData[i];
                serializer.SerializeValue(ref value);
            }
        }
    }
}

[CreateAssetMenu( fileName = "ItemDatabase", menuName = "ScriptableObjects/ItemDatabase")]
public class ItemDatabase : ScriptableObject
{
    [System.Serializable]
    public struct CraftingRecipe
    {
        public string Result;
        public List<string> SAVEDATAOVERRIDE;
        public string Ingredient1, Ingredient2;
    }

    private struct ItemPair : System.IEquatable<ItemPair>
    {
        public readonly string idA;
        public readonly string idB;

        public ItemPair(string a, string b)
        {
            // Always order alphabetically
            if (string.CompareOrdinal(a, b) <= 0)
            {
                idA = a;
                idB = b;
            }
            else
            {
                idA = b;
                idB = a;
            }
        }

        public bool Equals(ItemPair other) => idA == other.idA && idB == other.idB;
        public override bool Equals(object obj) => obj is ItemPair other && Equals(other);
        public override int GetHashCode() => System.HashCode.Combine(idA, idB);
    }

    [SerializeField] private SerializedDictionary<string, Item> ITEMS;

    [SerializeField] private List<CraftingRecipe> recipes;

    private Dictionary<ItemPair, CraftingRecipe> recipeLookup;

    private static ItemDatabase instance;

    public static bool ItemExists(string itemcode)
    {
        if (instance == null)
        {
            instance = Resources.Load<ItemDatabase>("ItemDatabase");
        }
        return instance.ITEMS.ContainsKey(itemcode);
    }

    public static Item GetItem(string itemCode)
    {
        if (instance == null)
        {
            instance = Resources.Load<ItemDatabase>("ItemDatabase");
        }
        if (instance.ITEMS.TryGetValue(itemCode, out var item))
        {
            return item;
        }
        else
        {
            Debug.LogError($"Item with code {itemCode} not found in database.");
            return default;
        }
    }

    public static bool GetCraft(string itema, string itemb, out string result, out List<string> resultitemdata)
    {
        if (instance == null)
        {
            instance = Resources.Load<ItemDatabase>("ItemDatabase");
        }

        if(instance.recipeLookup == null)
        {
            instance.recipeLookup = new Dictionary<ItemPair, CraftingRecipe>();
            foreach (var recipe in instance.recipes)
            {
                var pair = new ItemPair(recipe.Ingredient1, recipe.Ingredient2);
                instance.recipeLookup[pair] = recipe;
            }
        }

        var thepair = new ItemPair(itema, itemb);
        if (instance.recipeLookup.TryGetValue(thepair, out CraftingRecipe temp))
        {
            resultitemdata = temp.SAVEDATAOVERRIDE != null && temp.SAVEDATAOVERRIDE.Count > 0 ? temp.SAVEDATAOVERRIDE : GetItem(temp.Result).BaseSavedData;
            result = temp.Result;
            return true;
        }
        else
        {
            resultitemdata = null;
            result = string.Empty;
            return false;
        }
    }
}
