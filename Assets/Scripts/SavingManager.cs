using UnityEngine;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using Unity.Netcode;
using Unity.Collections;
using System.IO;

public class SavingManager : MonoBehaviour
{
    public enum SaveFileLocationEnum { Survival, Deathmatch, TeamDeathmatch }

    public static readonly string[] SAVE_LOCATIONS = { "/Saves/SV", "/Saves/DM", "/Saves/TDM" };

    [SerializeField] private SerializedDictionary<string, string> DefaultWorldData;
    private Dictionary<string, string> CurrentWorldData;

    public static string GetWorldSaveData(string key)
    {
        if (!instance.CurrentWorldData.ContainsKey(key)) { Debug.LogError("No world data with key: "+key); return ""; }
        return instance.CurrentWorldData[key];
    }

    private static SavingManager instance;

    private void Awake()
    {
        instance = this;
        CurrentWorldData = new(DefaultWorldData);
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

    private static void CheckDirectories()
    {
        if (!Directory.Exists(Application.dataPath + "/Saves"))
        {
            Directory.CreateDirectory(Application.dataPath + "/Saves");
        }

        foreach (var save in SAVE_LOCATIONS)
        {
            if (!Directory.Exists(Application.dataPath + save))
            {
                Directory.CreateDirectory(Application.dataPath + save);
            }
        }
    }

    public static void Save(SaveFileLocationEnum loc, int slot)
    {
        var targetpath = Application.dataPath + SAVE_LOCATIONS[(int)loc] + $"/{slot+1}.sav";
        string savedata = "";
        foreach (var data in instance.CurrentWorldData)
        {
            savedata += $"{data.Key},{data.Value}\\";
        }
        savedata = savedata[..^1];
        savedata += ";";
        foreach (var item in PickupableItem.ITEMS)
        {
            savedata += $"{item.itemCode},{VecToString(item.transform.position)},{VecToString(item.transform.eulerAngles)},{SavedItemDataToString(item.CurrentSavedData)}\\";
        }
        savedata = savedata[..^1];
        savedata += ";";
        foreach (var wfl in World.GetWorldFeatures)
        {
            foreach (var wf in wfl)
            {
                savedata += wf ? $"{wf.GetGeneratedFeatureIndex},{wf.GetSavedData}|" : "-1|";
            }
            savedata = savedata[..^1];
            savedata += "\\";
        }
        savedata = savedata[..^1];
        savedata += ";";
        foreach (var wfl in World.GetNetWorldFeatures)
        {
            foreach (var wf in wfl)
            {
                savedata += wf ? $"{wf.GetGeneratedFeatureIndex},{wf.GetSavedData}|" : "-1|";
            }
            savedata = savedata[..^1];
            savedata += "\\";
        }
        savedata = savedata[..^1];
        savedata += ";";

    }
}
