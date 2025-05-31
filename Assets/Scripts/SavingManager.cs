using UnityEngine;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using Unity.Netcode;
using Unity.Collections;
using System.IO;
using System.IO.Compression;
using System.Text;
using System;
using System.Collections;

public class SavingManager : NetworkBehaviour
{
    public enum SaveFileLocationEnum { Survival, Deathmatch, TeamDeathmatch }

    public static readonly string[] SAVE_LOCATIONS = { "/Saves/SV", "/Saves/DM", "/Saves/TDM" };

    [SerializeField] private SerializedDictionary<string, string> DefaultWorldData;
    [SerializeField] private SerializedDictionary<string, SavedObject> SavedObjects;
    [SerializeField] private SerializedDictionary<string, SavedNetObject> SavedNetObjects;
    private Dictionary<string, PlayerSavedData> SavedPlayerData = new();

    public static bool LOADING = false;

    public struct PlayerSavedData
    {
        public string UUID;
        public string InventoryData;
        public float headHP, bodyHP, legsHP, blood, consciousness, shock, hunger;
        public bool respawnOnLoad; //if your downed you fucking die
        public string WoundsData;
        public Vector3 position;
        public float rotation;

        public override string ToString()
        {
            return $"{UUID},{InventoryData},{System.Math.Round(headHP, 2)},{System.Math.Round(bodyHP, 2)},{System.Math.Round(legsHP, 2)},{System.Math.Round(blood, 2)},{System.Math.Round(consciousness, 2)},{System.Math.Round(shock, 2)},{System.Math.Round(hunger, 2)},{(respawnOnLoad ? 1 : 0)},{System.Math.Round(position.x, 3).ToString() + "," + System.Math.Round(position.y, 3).ToString() + "," + System.Math.Round(position.z, 3).ToString()},{System.Math.Round(rotation, 1)},{WoundsData}";
        }

        public PlayerSavedData(string uUID, string inventoryData, float headHP, float bodyHP, float legsHP, float blood, float consciousness, float shock, float hunger, bool respawnOnLoad, Vector3 position, float rotation, string woundsdata)
        {
            UUID = uUID;
            InventoryData = inventoryData;
            this.headHP = headHP;
            this.bodyHP = bodyHP;
            this.legsHP = legsHP;
            this.blood = blood;
            this.consciousness = consciousness;
            this.shock = shock;
            this.hunger = hunger;
            this.respawnOnLoad = respawnOnLoad;
            this.position = position;
            this.rotation = rotation;
            WoundsData = woundsdata;
        }

        public PlayerSavedData(string saveddata)
        {
            var split = saveddata.Split(',');
            UUID = split[0];
            InventoryData = split[1];
            headHP = float.Parse(split[2]);
            bodyHP = float.Parse(split[3]);
            legsHP = float.Parse(split[4]);
            blood = float.Parse(split[5]);
            consciousness = float.Parse(split[6]);
            shock = float.Parse(split[7]);
            hunger = float.Parse(split[8]);
            respawnOnLoad = split[9] == "0" ? false : true;
            position = new Vector3(float.Parse(split[10]), float.Parse(split[11]), float.Parse(split[12]));
            rotation = float.Parse(split[13]);
            WoundsData = split[14];
        }
    }
    private Dictionary<string, string> CurrentWorldData;
    public static string GetWorldSaveData(string key)
    {
        if (!instance.CurrentWorldData.ContainsKey(key)) { Debug.LogError("No world data with key: "+key); return ""; }
        return instance.CurrentWorldData[key];
    }
    public static void SetWorldSaveData(string key, string value)
    {
        if (!instance.CurrentWorldData.ContainsKey(key)) { Debug.LogError("No world data with key: " + key); return; }
        instance.CurrentWorldData[key] = value;
    }
    private static SavingManager instance;
    private void Awake()
    {
        instance = this;
        CurrentWorldData = new(DefaultWorldData);
        CheckDirectories();
    }

    private void Update()
    {
        if(!IsSpawned || LOADING || !IsServer) { return; }
        foreach (var player in GameManager.GetUUIDS)
        {
            var pexists = Player.PLAYERBYID.TryGetValue(player.Key, out var playerobj);
            var playerdata = new PlayerSavedData();
            if (pexists)
            {
                playerdata = new PlayerSavedData(
                    player.Value,
                    playerobj.pi.GetSavedData,
                    playerobj.ph.headHealth.Value,
                    playerobj.ph.bodyHealth.Value,
                    playerobj.ph.legHealth.Value,
                    playerobj.ph.currentBlood.Value,
                    playerobj.ph.consciousness.Value,
                    playerobj.ph.shock.Value,
                    playerobj.ph.hunger.Value,
                    !playerobj.ph.isConscious.Value,
                    playerobj.transform.position,
                    playerobj.transform.eulerAngles.y,
                    playerobj.ph.GetSavedWounds
                    );
            }
            else
            {
                playerdata = new PlayerSavedData(
                    player.Value,
                    "null",
                    1, 1, 1, 1, 1, 0, 1,
                    true,
                    Vector3.zero,
                    0,
                    "null"
                    );
            }
            if (instance.SavedPlayerData.ContainsKey(player.Value)) { instance.SavedPlayerData[player.Value] = playerdata; }
            else { instance.SavedPlayerData.Add(player.Value, playerdata); }
        }
    }

    private static string SavedItemDataToString(NetworkList<FixedString128Bytes> list)
    {
        if(list == null || list.Count <= 0) { return ""; }
        string final = "";
        foreach (var item in list)
        {
            final += item.ToString() + ",";
        }
        return final[..^1];
    }

    private static string StrListToString(List<string> list)
    {
        string final = "";
        foreach (var item in list)
        {
            final += item + ",";
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

    private static byte[] CompressString(string input)
    {
        var bytes = Encoding.ASCII.GetBytes(input);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal))
            gzip.Write(bytes, 0, bytes.Length);
        return output.ToArray();
    }

    private static string DecompressString(byte[] input)
    {
        using var inputstream = new MemoryStream(input);
        using var gzip = new GZipStream(inputstream, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return Encoding.ASCII.GetString(output.ToArray());
    }

    public static void Save(SaveFileLocationEnum loc, int slot)
    {
        CheckDirectories();
        var targetpath = Application.dataPath + SAVE_LOCATIONS[(int)loc] + $"/{slot+1}.sav";
        string savedata = "";
        savedata += $"{World.CurrentSeed};";
        foreach (var data in instance.CurrentWorldData)
        {
            savedata += $"{data.Key},{data.Value}\\";
        }
        savedata = savedata[..^1];
        savedata += ";";
        if(SavedObject.SAVEDOBJECTS.Count <= 0) { savedata += "null"; }
        else
        {
            foreach (var item in SavedObject.SAVEDOBJECTS)
            {
                var saved = StrListToString(item.SavedData);
                savedata += $"{item.SavedObjectID},{Extensions.VecToString(item.transform.position, ",")} , {Extensions.VecToString(item.transform.eulerAngles, ",")}{(saved.Length > 0 ? ","+saved : "")}\\";
            }
            savedata = savedata[..^1];
        }
        savedata += ";";
        foreach (var wfl in World.GetWorldFeatures)
        {
            foreach (var wf in wfl)
            {
                if (wf == null) { savedata += "-1|"; continue; }
                var wfsaved = wf.GetSavedData;
                savedata += $"{wf.GetFeatureType}{(string.IsNullOrEmpty(wfsaved) ? "" : ","+wfsaved)}|";
            }
            savedata = savedata[..^1];
            savedata += "\\";
        }
        savedata = savedata[..^1];
        savedata += ";";
        foreach (var player in GameManager.GetUUIDS)
        {
            var pexists = Player.PLAYERBYID.TryGetValue(player.Key, out var playerobj);
            var playerdata = new PlayerSavedData();
            if (pexists)
            {
                playerdata = new PlayerSavedData(
                    player.Value,
                    playerobj.pi.GetSavedData,
                    playerobj.ph.headHealth.Value,
                    playerobj.ph.bodyHealth.Value,
                    playerobj.ph.legHealth.Value,
                    playerobj.ph.currentBlood.Value,
                    playerobj.ph.consciousness.Value,
                    playerobj.ph.shock.Value,
                    playerobj.ph.hunger.Value,
                    !playerobj.ph.isConscious.Value,
                    playerobj.transform.position,
                    playerobj.transform.eulerAngles.y,
                    playerobj.ph.GetSavedWounds
                    );
            }
            else
            {
                playerdata = new PlayerSavedData(
                    player.Value,
                    "null",
                    1, 1, 1, 1, 1, 0, 1,
                    true,
                    Vector3.zero,
                    0,
                    "null"
                    );
            }
            if (instance.SavedPlayerData.ContainsKey(player.Value)) { instance.SavedPlayerData[player.Value] = playerdata; }
            else { instance.SavedPlayerData.Add(player.Value, playerdata); }
        }
        foreach (var player in instance.SavedPlayerData)
        {
            savedata += $"{player.Value}\\";
        }
        savedata = savedata[..^1];
        savedata += "*"; //* delineates the end of the non-networked savedata
        bool addedanything = false;
        foreach (var item in PickupableItem.ITEMS)
        {
            var saved = SavedItemDataToString(item.CurrentSavedData);
            savedata += $"{item.itemCode},{Extensions.VecToString(item.transform.position, ",")} , {Extensions.VecToString(item.transform.eulerAngles, ",")}{(saved.Length > 0 ? ","+saved : "")}\\";
            addedanything = true;
        }
        foreach (var corpse in PlayerCorpseProxy.CORPSES) //save embedded items from corpses :3
        {
            var stuff = corpse.GetSavedItemsFromCorpse;
            savedata += stuff;
            if(!addedanything && stuff.Length > 0) { addedanything = true; } //if we added something from a corpse, set addedanything to true
        }
        foreach (var corpse in CreatureCorpseProxy.CORPSES) //save embedded items from corpses :3
        {
            var stuff = corpse.GetSavedItemsFromCorpse;
            savedata += stuff;
            if (!addedanything && stuff.Length > 0) { addedanything = true; } //if we added something from a corpse, set addedanything to true
        }
        if (!addedanything) { savedata += "null"; }
        else { savedata = savedata[..^1]; }
        savedata += ";";
        if(World.GetNetWorldFeatures.Count <= 0) { savedata += "null"; }
        else
        {
            foreach (var wfl in World.GetNetWorldFeatures)
            {
                foreach (var wf in wfl)
                {
                    if (wf == null) { savedata += "-1|"; continue; }
                    var wfsaved = wf.GetSavedData;
                    savedata += $"{wf.GetFeatureType}{(string.IsNullOrEmpty(wfsaved) ? "" : "," + wfsaved)}|";
                }
                savedata = savedata[..^1];
                savedata += "\\";
            }
            savedata = savedata[..^1];
        }
        savedata += ";";
        if(SavedNetObject.SAVEDNETOBJECTS.Count <= 0) { savedata += "null"; }
        else
        {
            foreach (var item in SavedNetObject.SAVEDNETOBJECTS)
            {
                var saved = SavedItemDataToString(item.SavedData);
                savedata += $"{item.SavedObjectID},{Extensions.VecToString(item.transform.position, ",")},{Extensions.VecToString(item.transform.eulerAngles, ",")}{(saved.Length > 0 ? "," + saved : "")}\\";
            }
            savedata = savedata[..^1];
        }
        File.WriteAllText(targetpath, savedata);
    }

    private static List<byte[]> SplitBytes(byte[] source, int chunkSize)
    {
        var result = new List<byte[]>();
        for (int i = 0; i < source.Length; i += chunkSize)
        {
            int length = Math.Min(chunkSize, source.Length - i);
            byte[] chunk = new byte[length];
            Array.Copy(source, i, chunk, 0, length);
            result.Add(chunk);
        }
        return result;
    }

    //loads the data from the save file, called on server btw
    public static void Load(SaveFileLocationEnum loc, int slot)
    {
        instance.StartCoroutine(instance.LoadCoroutine(loc, slot));
    }

    private IEnumerator LoadCoroutine(SaveFileLocationEnum loc, int slot)
    {
        CheckDirectories();
        var targetpath = Application.dataPath + SAVE_LOCATIONS[(int)loc] + $"/{slot + 1}.sav";
        if (!File.Exists(targetpath)) { Debug.LogError("No save file found at: " + targetpath); yield break; }
        LOADING = true;
        string savedata = File.ReadAllText(targetpath);
        MenuController.ToggleLoadingScreen(true);

        string localdata = savedata.Split('*')[0];
        string[] splitlocaldata = localdata.Split(';');

        //split and send relevant save data to clients (please work ffs)
        var sendtoclients = CompressString($"{splitlocaldata[0]};{splitlocaldata[3]};{splitlocaldata[1]};{splitlocaldata[2]};{splitlocaldata[4]}");
        var chunks = SplitBytes(sendtoclients, 1024);
        instance.PrepareForSaveFileRPC(sendtoclients.Length);
        yield return new WaitForSeconds(0.5f);//give time for the fucking world to spawn
        for (int i = 0; i < chunks.Count; i++)
        {
            yield return null;
            instance.ReceiveSavefilePartRPC(chunks[i]);
        }

        instance.SavedPlayerData = new();
        foreach (var playerdata in splitlocaldata[4].Split('\\'))
        {
            var split = playerdata.Split(',');
            instance.SavedPlayerData.Add(split[0], new PlayerSavedData(playerdata));
        }

        string serverdata = savedata.Split('*')[1];
        var splitserverdata = serverdata.Split(';');
        if (splitserverdata[0] != "null")
        {
            foreach (var item in splitserverdata[0].Split('\\'))
            {
                var split = item.Split(',');
                if (!ItemDatabase.ItemExists(split[0])) { continue; }
                var savedobj = Instantiate(ItemDatabase.GetItem(split[0]).ItemPrefab, new Vector3(float.Parse(split[1]), float.Parse(split[2]), float.Parse(split[3])), Quaternion.Euler(float.Parse(split[4]), float.Parse(split[5]), float.Parse(split[6])));
                savedobj.NetworkObject.Spawn();
                if (split.Length < 8) { continue; } //no saved data
                var savedobjdata = new List<FixedString128Bytes>();
                for (int i = 7; i < split.Length; i++)
                {
                    savedobjdata.Add(split[i]);
                }
                savedobj.InitSavedData(savedobjdata);
            }
            yield return null;
        }
        if (splitserverdata[1] != "null") { GameManager.GetWorld.LoadNetWorldFeatures(splitserverdata[1].Split('\\')); yield return null; }
        if (splitserverdata[2] != "null")
        {
            foreach (var item in splitserverdata[2].Split('\\'))
            {
                var split = item.Split(',');
                if (!instance.SavedNetObjects.ContainsKey(split[0])) { continue; }
                var savedobj = Instantiate(instance.SavedNetObjects[split[0]], Vector3.zero, Quaternion.identity);
                savedobj.NetworkObject.Spawn();
                var savedobjdata = new List<string>();
                if (split.Length > 7)
                {
                    for (int i = 7; i < split.Length; i++)
                    {
                        savedobjdata.Add(split[i]);
                    }
                }
                savedobj.Init(savedobjdata, new Vector3(float.Parse(split[1]), float.Parse(split[2]), float.Parse(split[3])), new Vector3(float.Parse(split[4]), float.Parse(split[5]), float.Parse(split[6])));
            }
            yield return null;
        }
        MenuController.ToggleLoadingScreen(false);
        UIManager.FadeToGame();
        LOADING = false;
    }

    [Rpc(SendTo.Everyone)]
    private void PrepareForSaveFileRPC(int length)
    {
        receivedBytes = new byte[length];
        bytesReceived = 0;
        if (!IsOwner) { MenuController.ToggleLoadingScreen(true); LOADING = true; }
    }

    private static byte[] receivedBytes;
    private static int bytesReceived = 0;

    [Rpc(SendTo.Everyone)]
    private void ReceiveSavefilePartRPC(byte[] chunk)
    {
        Array.Copy(chunk, 0, receivedBytes, bytesReceived, chunk.Length);
        bytesReceived += chunk.Length;

        if (bytesReceived >= receivedBytes.Length)
        {
            string data = DecompressString(receivedBytes);
            string[] split = data.Split(';');
            int seed = int.Parse(split[0]);
            string Wfeatures = split[1];
            string worlddata = split[2];
            string savedobjects = split[3];
            string playerdata = split[4];
            GameManager.GetWorld.Init(seed, Wfeatures.Split('\\'));
            LoadOthers(worlddata, savedobjects, playerdata);
            receivedBytes = new byte[0];
            bytesReceived = 0;
            if (!IsOwner) { MenuController.ToggleLoadingScreen(false); UIManager.FadeToGame(); LOADING = false; }
        }
    }

    private void LoadOthers(string worlddata, string savedobjects, string playerdata)
    {
        string[] splitworlddata = worlddata.Split('\\');
        CurrentWorldData = new(DefaultWorldData);
        foreach (var wd in splitworlddata)
        {
            var split = wd.Split(',');
            if(CurrentWorldData.ContainsKey(split[0])) { CurrentWorldData[split[0]] = split[1]; }
        }
        if(savedobjects != "null") {
            string[] splitobjects = savedobjects.Split('\\');
            var k = 0;
            foreach (var obj in splitobjects)
            {
                var split = obj.Split(',');
                if (SavedObjects.ContainsKey(split[0]))
                {
                    var savedobj = Instantiate(instance.SavedObjects[split[0]], Vector3.zero, Quaternion.identity);
                    var savedobjdata = new List<string>();
                    if (split.Length > 7)
                    {
                        for (int i = 7; i < split.Length; i++)
                        {
                            savedobjdata.Add(split[i]);
                        }
                    }
                    savedobj.Init(savedobjdata, new Vector3(float.Parse(split[1]), float.Parse(split[2]), float.Parse(split[3])), new Vector3(float.Parse(split[4]), float.Parse(split[5]), float.Parse(split[6])), k);
                }
                k++;
            }
        }
        bool playerisnew = true;
        var data = new PlayerSavedData(Extensions.UniqueIdentifier, "null", 1, 1, 1, 1, 1, 1, 1, true, Vector3.zero, 0, "null");

        var splitplayerdata = playerdata.Split('\\');
        foreach (var player in splitplayerdata)
        {
            if (player.Split(',')[0] == Extensions.UniqueIdentifier)
            {
                data = new PlayerSavedData(player);
                playerisnew = false;
                break;
            }
        }

        if(!playerisnew && data.respawnOnLoad && GameManager.GetGameMode != GameModeEnum.Survival)
        {
            GameManager.EnableSpectator();
            return;
        }

        GameManager.RespawnPlayer();
        if (data.respawnOnLoad) { return; } //they just need to respawn other data is irrelevant
        var playerobj = Player.LocalPlayer;
        playerobj.ph.headHealth.Value = data.headHP;
        playerobj.ph.bodyHealth.Value = data.bodyHP;
        playerobj.ph.legHealth.Value = data.legsHP;
        playerobj.ph.currentBlood.Value = data.blood;
        playerobj.ph.consciousness.Value = data.consciousness;
        playerobj.ph.shock.Value = data.shock;
        playerobj.ph.hunger.Value = data.hunger;
        playerobj.Teleport(data.position);
        playerobj.transform.eulerAngles = new Vector3(0, data.rotation, 0);
        playerobj.pi.InitFromSavedData(data.InventoryData);
        playerobj.ph.ApplySavedWounds(data.WoundsData);
    }

    public static bool GetWorldInfo(SaveFileLocationEnum type, int save, out string info)
    {
        if (File.Exists(Application.dataPath + SAVE_LOCATIONS[(int)type] + $"/{save + 1}.sav"))
        {
            var data = File.ReadAllText(Application.dataPath + SAVE_LOCATIONS[(int)type] + $"/{save + 1}.sav");
            var split = data.Split('*')[0].Split(';');
            var day = "0";
            var worlddata = split[1].Split('\\');
            foreach (var wd in worlddata)
            {
                var splitwd = wd.Split(',');
                if (splitwd[0] == "day")
                {
                    day = splitwd[1];
                    break;
                }
            }
            info = $"Seed: {split[0]}\nDay: {day}";
            return true;
        }
        else
        {
            info = "Create new world";
            return false;
        }
    }

    public static bool WorldExists(SaveFileLocationEnum type, int save)
    {
        return File.Exists(Application.dataPath + SAVE_LOCATIONS[(int)type] + $"/{save + 1}.sav");
    }

    public static void DeleteWorld(SaveFileLocationEnum type, int save)
    {
        if (WorldExists(type, save))
        {
            File.Delete(Application.dataPath + SAVE_LOCATIONS[(int)type] + $"/{save + 1}.sav");
        }
    }
}
