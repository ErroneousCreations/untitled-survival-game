using UnityEngine;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using Unity.Netcode;
using Unity.Collections;
using static UnityEditor.Progress;

public struct SavedItem
{
    public string itemId;
    public Vector3 pos, rot;
    public List<string> savedData;
}

public struct SavedWorldFeature
{
    public List<string> savedWFData;
}

public struct SavedObject
{
    public string savedObjectId;
    public List<string> savedObjectData;
    public Vector3 pos, rot;
}

public class SavingManager : MonoBehaviour
{
    public enum SaveFileLocationEnum { Survival, Deathmatch, TeamDeathmatch }

    public static readonly string[] SAVE_LOCATIONS = { "/Saves/SV/", "/Saves/DM/", "/Saves/TDM/" };

    public SerializedDictionary<string, string> DefaultWorldData;

    private static SavingManager instance;

    private void Awake()
    {
        instance = this;
    }

    private static string VecToString(Vector3 pos)
    {
        return System.Math.Round(pos.x, 3).ToString() + "," + System.Math.Round(pos.y, 3).ToString() + "," + System.Math.Round(pos.z, 3).ToString();
    }

    private static string SavedItemDataToString(NetworkList<FixedString64Bytes> list)
    {
        string final = "";
        foreach (var item in list)
        {
            final += item.ToString() + ",";
        }
        return final[..^1];

    }

    public static void Save(SaveFileLocationEnum loc, int slot)
    {
        var targetpath = Application.dataPath + SAVE_LOCATIONS[(int)loc] + $"{slot+1}.sav";
        string savedata = "";
        foreach (var item in PickupableItem.ITEMS)
        {
            savedata += $"{item.itemCode},{VecToString(item.transform.position)},{VecToString(item.transform.eulerAngles)},{SavedItemDataToString(item.CurrentSavedData)}\\";
        }
        savedata = savedata[..^1];
        savedata += ";";
        foreach (var wf in World.GetWorldFeatures)
        {
            savedata += $"\\";
        }
    }
}
