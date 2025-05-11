using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using System.Collections;
using Unity.Collections;
using System.Text;
using System.Linq;
using UnityEngine.UI;
using System.IO;

public enum GameStateEnum { Lobby, Ingame }
public enum GameModeEnum { Survival, Deathmatch, TeamDeathmatch, Arena }

public class GameManager : NetworkBehaviour
{
    public Player ThePlayer;
    public World world;

    private NetworkVariable<GameModeEnum> Gamemode = new();
    private NetworkVariable<GameStateEnum> Gamestate = new();
    private Dictionary<ulong, FixedString128Bytes> USERNAMES = new(); //for the server to keep track and stuff
    private Dictionary<ulong, string> UNIQUEUSERIDS = new(); //for the server to keep track and stuff
    private Dictionary<ulong, string> localUserNames = new(); //for clients
    private static GameManager instance;

    //lobby
    private List<ulong> readiedPlayers = new();
    private NetworkVariable<float> startTimer = new(0);
    private bool readied, inlobbychannel, inspectatorchannel;
    private World currWorld;
    private int currentSaveFile, currentSeed = -1;

    public static bool IsSpectating = false;

    public static World GetWorld => instance.currWorld;

    public static Dictionary<ulong, string> GetUUIDS => instance.UNIQUEUSERIDS;

    public static SavingManager.SaveFileLocationEnum GetSaveFileLocation => GetGameMode == GameModeEnum.Arena ? SavingManager.SaveFileLocationEnum.Survival : (SavingManager.SaveFileLocationEnum)GetGameMode;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectCallback;
        NetworkManager.Singleton.ConnectionApprovalCallback += ApproveClient;
    }

    public override void OnNetworkSpawn()
    {
        UIManager.ShowLobby();
        readiedPlayers = new();
        //since we are in lobby we join the lobby channel
        readied = false;
        inlobbychannel = false;
        VivoxManager.JoinLobbyChannel(() => { inlobbychannel = true; });
        UIManager.SetGamemodeIndicator(GameModeEnum.Survival);
        UIManager.SetSaveIndicator(0);
        UIManager.SetWorldInfo(SavingManager.GetWorldInfo(SavingManager.SaveFileLocationEnum.Survival, 0, out string info), info);
    }

    private void ApproveClient(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        var usernameandid = Encoding.UTF8.GetString(request.Payload);

        USERNAMES.Add(request.ClientNetworkId, usernameandid.Split('\v')[0]);
        UNIQUEUSERIDS.Add(request.ClientNetworkId, usernameandid.Split('\v')[1]);
        response.Approved = true;
        response.CreatePlayerObject = false;
    }

    private void OnClientDisconnectCallback(ulong obj)
    {
        if (!IsServer) { return; }
        DisconnectedRPC(obj, readiedPlayers.ToArray());
        USERNAMES.Remove(obj);
        if (readiedPlayers.Contains(obj)) { readiedPlayers.Remove(obj); } 
    }

    [Rpc(SendTo.Everyone)]
    private void DisconnectedRPC(ulong id, ulong[] readied)
    {
        if(Extensions.LocalClientID == id) { return; }
        if (localUserNames.ContainsKey(id)) { localUserNames.Remove(id); }
        var rlist = readied.ToList();
        UIManager.UpdatePlayerList(localUserNames, rlist);
        readiedPlayers = rlist;
    }

    private void OnClientConnectedCallback(ulong obj)
    {
        if (!IsServer) { return; }
        NewPlayerRPC(USERNAMES[obj].ToString(), readiedPlayers.ToArray(), obj);
        if (obj != 0) {
            var usernames = new FixedString128Bytes[USERNAMES.Count];
            var ids = new ulong[USERNAMES.Count];
            var keys = USERNAMES.Keys.ToList();
            for (int i = 0; i < usernames.Length; i++)
            {
                usernames[i] = USERNAMES[keys[i]];
                ids[i] = keys[i];   
            }
            SyncConnectedsRPC(usernames, ids, readiedPlayers.ToArray(), GetGameMode, currentSaveFile, SavingManager.GetWorldInfo(GetSaveFileLocation, currentSaveFile, out string info), info, RpcTarget.Single(obj, RpcTargetUse.Temp));
        }
    }

    [Rpc(SendTo.Everyone)]
    private void NewPlayerRPC(string username, ulong[] readied, ulong id)
    {
        if (Extensions.LocalClientID == id && id != 0) { return; }
        localUserNames.Add(id, username);
        var rlist = readied.ToList();
        UIManager.UpdatePlayerList(localUserNames, rlist);
        readiedPlayers = rlist;
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void SyncConnectedsRPC(FixedString128Bytes[] usernames, ulong[] ids, ulong[] readied, GameModeEnum gamemode, int save, bool worldexists, string info, RpcParams @params)
    {
        localUserNames = new();
        for (int i = 0; i < ids.Length; i++) {
            localUserNames.Add(ids[i], usernames[i].ToString());
        }
        var rlist = readied.ToList();
        UIManager.UpdatePlayerList(localUserNames, rlist);
        readiedPlayers = rlist;

        UIManager.SetGamemodeIndicator(gamemode);
        UIManager.SetSaveIndicator(save);
        UIManager.SetWorldInfo(worldexists, info);
    }

    public static GameStateEnum GetGamestate => instance.Gamestate.Value;

    public static GameModeEnum GetGameMode => instance.Gamemode.Value;

    public static void RespawnPlayer()
    {
        instance.RespawnPlayerRPC(NetworkManager.Singleton.LocalClientId);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void RespawnPlayerRPC(ulong id)
    {
        if (NetworkManager.Singleton.ConnectedClients[id].PlayerObject) { NetworkManager.Singleton.ConnectedClients[id].PlayerObject.Despawn(true); }

        //StartCoroutine(FinishRespawn(id));
        var p = Instantiate(ThePlayer, Vector3.zero, Quaternion.identity); //todo add the other spawnpoint ranges
        p.NetworkObject.SpawnAsPlayerObject(id);
        p.Teleport(Gamemode.Value == GameModeEnum.Survival ? Extensions.GetSurvivalSpawnPoint : (Gamemode.Value == GameModeEnum.TeamDeathmatch ? Extensions.GetTeamDeathmatchSpawnPoint : Extensions.GetDeathmatchSpawnPoint));
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void DespawnPlayerRPC(ulong id)
    {
        if (NetworkManager.Singleton.ConnectedClients[id].PlayerObject) { NetworkManager.Singleton.ConnectedClients[id].PlayerObject.Despawn(true); }
    }

    private IEnumerator FinishRespawn(ulong id )
    {
        yield return new WaitForSeconds(0.05f); //btw arena uses Deathmatch Spawn Points
        //Instantiate(ThePlayer, Gamemode.Value == GameModeEnum.Survival ? Extensions.GetSurvivalSpawnPoint : (Gamemode.Value == GameModeEnum.TeamDeathmatch ? Extensions.GetTeamDeathmatchSpawnPoint : Extensions.GetDeathmatchSpawnPoint), Quaternion.identity).SpawnAsPlayerObject(id); //todo add the other spawnpoint ranges
    }

    private void Update()
    {
        if (!NetworkManager.Singleton) { return; }

        switch (GetGamestate)
        {
            case GameStateEnum.Lobby:
                if (Input.GetKeyDown(KeyCode.V) && inlobbychannel) { VivoxManager.ToggleInputMute(); }
                UIManager.SetMuteIcon(VivoxManager.initialised && !VivoxManager.GetisMuted && inlobbychannel);

                UIManager.SetReadyTimerText(startTimer.Value >= 5 ? "" : System.Math.Round(startTimer.Value, 2).ToString());
                UIManager.SetReadyUpButtonText(readied ? "Cancel Ready" : "Ready Up");
                if (!IsServer) { return; }
                if (readiedPlayers.Count >= NetworkManager.Singleton.ConnectedClients.Count)
                {
                    startTimer.Value -= Time.deltaTime;

                    if (startTimer.Value <= 0)
                    {
                        Gamestate.Value = GameStateEnum.Ingame;
                        currWorld = Instantiate(world, Vector3.zero, Quaternion.identity);
                        currWorld.NetworkObject.Spawn();
                        var loading = SavingManager.WorldExists(GetSaveFileLocation, currentSaveFile);
                        if (loading) { SavingManager.Load(GetSaveFileLocation, currentSaveFile); }
                        else { currWorld.Init(currentSeed); }
                        ExitLobbyRPC(!loading);
                    }
                }
                else { startTimer.Value = 5; }
                break;
            case GameStateEnum.Ingame:
                if (inlobbychannel) { VivoxManager.LeaveLobbyChannel(); }
                if (!Player.LocalPlayer) {

                    if (inspectatorchannel)
                    {
                        if (Input.GetKeyDown(KeyCode.V)) { VivoxManager.ToggleInputMute(); }
                        UIManager.SetMuteIcon(VivoxManager.initialised && !VivoxManager.GetisMuted);
                    }
                    else { UIManager.SetMuteIcon(false); }
                }
                break;
        }
    }

    public static void ResetGamemode()
    {
        instance.Gamemode.Value = GameModeEnum.Survival;
        instance.currentSaveFile = 0;
        //UIManager.SetGamemodeIndicator(GameModeEnum.Survival);
        //UIManager.SetSaveIndicator(0);
        UIManager.SetWorldInfo(SavingManager.GetWorldInfo(SavingManager.SaveFileLocationEnum.Survival, 0, out string info), info);
    }

    [Rpc(SendTo.Everyone)]
    private void ExitLobbyRPC(bool spawnplayer)
    {
        if (spawnplayer) { RespawnPlayerRPC(NetworkManager.LocalClientId); UIManager.FadeToGame(); }
        if(!VivoxManager.GetisMuted) { VivoxManager.ToggleInputMute(); }
        if (inlobbychannel) { VivoxManager.LeaveLobbyChannel(); inlobbychannel = false; }
    }

    public static void EnableSpectator()
    {
        VivoxManager.JoinSpectateChannel(() => { instance.inspectatorchannel = true; });
        if (!VivoxManager.GetisMuted) { VivoxManager.ToggleInputMute(); }
        IsSpectating = true;
    }

    private static void CleanUp()
    {
        foreach (var ob in GameObject.FindGameObjectsWithTag("WorldObject"))
        {
            if(ob.TryGetComponent(out NetworkObject netob))
            {
                netob.Despawn();
            }
        }
    }

    public static void BackToLobby()
    {
        instance.readiedPlayers.Clear();
        instance.startTimer.Value = 5;
        SavingManager.Save(GetSaveFileLocation, instance.currentSaveFile);
        instance.Gamestate.Value = GameStateEnum.Lobby;
        instance.currWorld.NetworkObject.Despawn();
        SavingManager.GetWorldInfo(GetSaveFileLocation, instance.currentSaveFile, out string worldinfo);
        CleanUp();
        instance.ToLobbyRPC(worldinfo);
    }

    [Rpc(SendTo.Everyone)]
    private void ToLobbyRPC(string worldinfo)
    {
        readied = false;
        UIManager.ShowLobby();
        UIManager.HideGameOverScreen();
        if(!VivoxManager.GetisMuted) { VivoxManager.ToggleInputMute(); }
        DespawnPlayerRPC(NetworkManager.LocalClientId);
        VivoxManager.JoinLobbyChannel(() => { inlobbychannel = true; });
        if(inspectatorchannel) { VivoxManager.LeaveSpectateChannel(); inspectatorchannel = false; }
        IsSpectating = false;
        UIManager.SetWorldInfo(true, worldinfo);
    }

    public static void ReadyUp()
    {
        instance.readied = !instance.readied;
        instance.ReadiedRPC(instance.readied, Extensions.LocalClientID);
    }

    [Rpc(SendTo.Server)]
    private void ReadiedRPC(bool ready, ulong id)
    {
        if (ready) { readiedPlayers.Add(id); }
        else { readiedPlayers.Remove(id); }
        UpdateReadiedRPC(readiedPlayers.ToArray());
    }

    [Rpc(SendTo.Everyone)]  
    void UpdateReadiedRPC(ulong[] readied)
    {
        var rlist = readied.ToList();
        UIManager.UpdatePlayerList(localUserNames, rlist);
        readiedPlayers = rlist;
    }

    public static void SetGamemode(GameModeEnum mode)
    {
        instance.Gamemode.Value = mode;
        instance.currentSaveFile = 0;
        instance.UpdateGamemodeRPC(mode);
        instance.UpdateSaveRPC(0, SavingManager.GetWorldInfo(GetSaveFileLocation, 0, out string info), info);
    }

    [Rpc(SendTo.Everyone)]
    private void UpdateGamemodeRPC(GameModeEnum mode)
    {
        UIManager.SetGamemodeIndicator(mode);
    }

    public static void SetSave(int saveslot)
    {
        instance.currentSaveFile = saveslot;
        instance.UpdateSaveRPC(saveslot, SavingManager.GetWorldInfo(GetSaveFileLocation, saveslot, out string info), info);
    }

    [Rpc(SendTo.Everyone)]
    private void UpdateSaveRPC(int slot, bool exists, string worldinfo)
    {
        UIManager.SetSaveIndicator(slot);
        UIManager.SetWorldInfo(exists, worldinfo);
    }

    public static void SetSeed(string seed)
    {
        instance.UpdateSeedRPC(seed);
        if (string.IsNullOrEmpty(seed)) { instance.currentSeed = -1; return; }
        instance.currentSeed = int.Parse(seed);
    }

    [Rpc(SendTo.Everyone)]
    private void UpdateSeedRPC(string seed)
    {
        UIManager.SetSeedText(seed);
    }

    public static void DeleteSave()
    {
        SavingManager.DeleteWorld(GetSaveFileLocation, instance.currentSaveFile);
        instance.UpdateSaveRPC(instance.currentSaveFile, false, "Create new world");
    }
}
