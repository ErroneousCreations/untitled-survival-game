using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using System.Collections;
using Unity.Collections;
using System.Text;
using System.Linq;
using UnityEngine.UI;
using System.IO;
using Unity.Services.Vivox;

public enum GameStateEnum { Lobby, Ingame, Winnerscreen }
public enum GameModeEnum { Survival, Deathmatch, TeamDeathmatch, Arena }

public class GameManager : NetworkBehaviour
{
    public Player ThePlayer;
    public World world;

    private NetworkVariable<GameModeEnum> Gamemode = new();
    private NetworkVariable<GameStateEnum> Gamestate = new();
    private Dictionary<ulong, FixedString128Bytes> USERNAMES = new(); //for the server to keep track and stuff
    private Dictionary<ulong, string> UNIQUEUSERIDS = new(); //for the server to keep track and stuff
    private Dictionary<string, ulong> CLIENTIDFROMUUID = new(); //for the server to keep track and stuff
    private Dictionary<ulong, string> localUserNames = new(); //for clients
    private Dictionary<string, ulong> localUUIDtoID = new(); //for clients
    private static GameManager instance;
    [EditorAttributes.ReadOnly]public List<string> TEAMA = new(), TEAMB = new();

    //lobby
    private List<ulong> readiedPlayers = new();
    private NetworkVariable<bool> allPlayersInit = new(false);
    public static bool ALL_PLAYERS_INITIALISED => instance.allPlayersInit.Value;
    private NetworkVariable<float> startTimer = new(0);
    private bool readied;
    private World currWorld;
    private int currentSaveFile, currentSeed = -1;
    private VivoxParticipant lobbyParticipant, spectatorMainParticipant, spectatorParticipant;
    private bool isSpeaking;
    private NetworkVariable<FixedString64Bytes> talkingPlayers = new();
    private List<ulong> serverSpeakingList = new();
    private NetworkVariable<float> timeSinceLastDeath = new(0);
    private float doxCooldown, hauntCooldown;

    public static bool IsSpectating = false;

    public static World GetWorld => instance.currWorld;

    public static Dictionary<ulong, string> GetUUIDS => instance.UNIQUEUSERIDS;

    public static SavingManager.SaveFileLocationEnum GetSaveFileLocation => GetGameMode == GameModeEnum.Arena ? SavingManager.SaveFileLocationEnum.Survival : (SavingManager.SaveFileLocationEnum)GetGameMode;

    public static bool InTeamA(string uuid) => instance.TEAMA.Contains(uuid);
    public static bool InTeamB(string uuid) => instance.TEAMB.Contains(uuid);

    private NetworkVariable<FixedString512Bytes> allocID = new();

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
        Invoke(nameof(Wait), 0.01f); //wait for the UI to load
        readied = false;
        UIManager.SetGamemodeIndicator(GameModeEnum.Survival);
        UIManager.SetSaveIndicator(0);
        UIManager.SetWorldInfo(SavingManager.GetWorldInfo(SavingManager.SaveFileLocationEnum.Survival, 0, out string info), info);
        if (IsOwner) { allocID.Value = Relay.CurrentAllocationId; }
    }

    void Wait() { if (!IsOwner) { VivoxManager.OverwriteAllocationID(allocID.Value.ToString()); } VivoxManager.JoinLobbyChannel(); }

    private void ApproveClient(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        if(Gamestate.Value != GameStateEnum.Lobby) { response.Approved = false; return; } //cant join if not in lobby

        var usernameandid = Encoding.UTF8.GetString(request.Payload);

        USERNAMES.Add(request.ClientNetworkId, usernameandid.Split('\v')[0]);
        UNIQUEUSERIDS.Add(request.ClientNetworkId, usernameandid.Split('\v')[1]);
        CLIENTIDFROMUUID.Add(usernameandid.Split('\v')[1], request.ClientNetworkId);
        response.Approved = true;
        response.CreatePlayerObject = false;
    }

    private void OnClientDisconnectCallback(ulong obj)
    {
        if (!IsServer) { return; }
        if(serverSpeakingList.Contains(obj)) { serverSpeakingList.Remove(obj); }
        DisconnectedRPC(obj, readiedPlayers.ToArray(), UNIQUEUSERIDS[obj]);
        USERNAMES.Remove(obj);
        CLIENTIDFROMUUID.Remove(UNIQUEUSERIDS[obj]);
        UNIQUEUSERIDS.Remove(obj);
        if (readiedPlayers.Contains(obj)) { readiedPlayers.Remove(obj); }
        //if we hav 1 player and are in a competitive gamemove then reset it
        if(NetworkManager.Singleton.ConnectedClients.Count < 2 && (Gamemode.Value != GameModeEnum.Survival))
        {
            ResetGamemode();
        }
    }

    [Rpc(SendTo.Everyone)]
    private void DisconnectedRPC(ulong id, ulong[] readied, string uuid)
    {
        if(Extensions.LocalClientID == id) { return; }
        if (localUserNames.ContainsKey(id)) { localUserNames.Remove(id); }
        if (localUUIDtoID.ContainsKey(uuid)) { localUUIDtoID.Remove(uuid); }
        var rlist = readied.ToList();
        UIManager.UpdatePlayerList(localUserNames, rlist);
        readiedPlayers = rlist;
    }

    private void OnClientConnectedCallback(ulong obj)
    {
        if (!IsServer) { return; }
        NewPlayerRPC(USERNAMES[obj].ToString(), readiedPlayers.ToArray(), obj, UNIQUEUSERIDS[obj]);
        if (obj != 0) {
            var usernames = new FixedString512Bytes[USERNAMES.Count];
            var ids = new ulong[USERNAMES.Count];
            var keys = USERNAMES.Keys.ToList();
            for (int i = 0; i < usernames.Length; i++)
            {
                usernames[i] = USERNAMES[keys[i]] + "\v" + UNIQUEUSERIDS[keys[i]];
                ids[i] = keys[i];   
            }
            SyncConnectedsRPC(usernames, ids, readiedPlayers.ToArray(), GetGameMode, currentSaveFile, SavingManager.GetWorldInfo(GetSaveFileLocation, currentSaveFile, out string info), info, RpcTarget.Single(obj, RpcTargetUse.Temp));
        }
    }

    [Rpc(SendTo.Everyone)]
    private void NewPlayerRPC(string username, ulong[] readied, ulong id, string uuid)
    {
        if (Extensions.LocalClientID == id && id != 0) { return; }
        localUserNames.Add(id, username);
        localUUIDtoID.Add(uuid, id);
        var rlist = readied.ToList();
        UIManager.UpdatePlayerList(localUserNames, rlist);
        readiedPlayers = rlist;
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void SyncConnectedsRPC(FixedString512Bytes[] usernames, ulong[] ids, ulong[] readied, GameModeEnum gamemode, int save, bool worldexists, string info, RpcParams @params)
    {
        localUserNames = new();
        for (int i = 0; i < ids.Length; i++) {
            localUserNames.Add(ids[i], usernames[i].ToString().Split('\v')[0]);
            localUUIDtoID.Add(usernames[i].ToString().Split('\v')[1], ids[i]);
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
        var spawnpoint = Vector3.zero;
        p.SetSpawnPos(spawnpoint);
        switch (GetGameMode)
        {
            case GameModeEnum.Survival:
                spawnpoint = Extensions.GetSurvivalSpawnPoint;
                break;
            case GameModeEnum.Deathmatch:
                spawnpoint = Extensions.GetDeathmatchSpawnPoint;
                break;
            case GameModeEnum.TeamDeathmatch:
                if (TEAMA.Contains(UNIQUEUSERIDS[id])) { spawnpoint = Extensions.GetTeamASpawnPoint; }
                else if (TEAMB.Contains(UNIQUEUSERIDS[id])) { spawnpoint = Extensions.GetTeamBSpawnPoint; }
                else { spawnpoint = Extensions.GetDeathmatchSpawnPoint; }
                break;
            case GameModeEnum.Arena:
                spawnpoint = Extensions.GetDeathmatchSpawnPoint;
                break;
        }
        p.NetworkObject.SpawnAsPlayerObject(id);
        p.Teleport(spawnpoint);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void DespawnPlayerRPC(ulong id)
    {
        if (NetworkManager.Singleton.ConnectedClients[id].PlayerObject) { NetworkManager.Singleton.ConnectedClients[id].PlayerObject.Despawn(true); }
    }

    private void Update()
    {
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsClient) { return; }

        if (IsServer && (GetGamestate != GameStateEnum.Ingame || IsSpectating))
        {
            talkingPlayers.Value = "";
            foreach (var id in serverSpeakingList)
            {
                talkingPlayers.Value += id + "|";
            }
            if (talkingPlayers.Value.Length > 0) { talkingPlayers.Value = talkingPlayers.Value.ToString()[..^1]; }
        }

        switch (GetGamestate)
        {
            case GameStateEnum.Lobby:
                if (Input.GetKeyDown(KeyCode.V) && VivoxManager.InLobbyChannel) { VivoxManager.ToggleInputMute(); }
                UIManager.SetMuteIcon(VivoxManager.initialised && !VivoxManager.GetisMuted && VivoxManager.InLobbyChannel);
                UIManager.SetReadyTimerText(startTimer.Value >= 5 ? "" : System.Math.Round(startTimer.Value, 2).ToString());
                UIManager.SetReadyUpButtonText(readied ? "Cancel Ready" : "Ready Up");
                UIManager.SetSpeakingIndicators(talkingPlayers.Value.ToString());
                if (VivoxManager.InSpectatorChannel) { VivoxManager.LeaveSpectateChannel(); spectatorParticipant = null; spectatorMainParticipant = null; }
                if (VivoxManager.InMainChannel) { VivoxManager.LeaveMainChannel(); }
                if (VivoxManager.InLobbyChannel) {
                    if(lobbyParticipant == null) { lobbyParticipant = VivoxManager.GetActiveChannels[VivoxManager.LOBBYCHANNEL].FirstOrDefault(x => { return x.DisplayName == Extensions.UniqueIdentifier; }); }
                    else {
                        if (lobbyParticipant.SpeechDetected && !isSpeaking)
                        {
                            isSpeaking = true; UpdateSpeakingRPC(NetworkManager.Singleton.LocalClientId, true);
                        }
                        if (!lobbyParticipant.SpeechDetected && isSpeaking)
                        {
                            isSpeaking = false; UpdateSpeakingRPC(NetworkManager.Singleton.LocalClientId, false);
                        }
                    } //no talking
                }

                if (!IsServer) { return; }
                if (readiedPlayers.Count >= NetworkManager.Singleton.ConnectedClients.Count)
                {
                    startTimer.Value -= Time.deltaTime;

                    if (startTimer.Value <= 0)
                    {
                        serverSpeakingList = new();
                        talkingPlayers.Value = "";
                        Gamestate.Value = GameStateEnum.Ingame;
                        allPlayersInit.Value = false;
                        initialisedPlayers = 0;
                        currWorld = Instantiate(world, Vector3.zero, Quaternion.identity);
                        currWorld.NetworkObject.Spawn();
                        Extensions.RandomiseDeathmatchSpawnIndex();
                        Extensions.RandomiseTeamSpawnIndexes();
                        var loading = SavingManager.WorldExists(GetSaveFileLocation, currentSaveFile);
                        if (loading) {
                            SavingManager.Load(GetSaveFileLocation, currentSaveFile);
                            if(Gamemode.Value == GameModeEnum.TeamDeathmatch)
                            {
                                TEAMA = new();
                                TEAMB = new();
                                List<string> allUUIDs = new(UNIQUEUSERIDS.Values);
                                List<string> uncontainedUUIDS = new(allUUIDs);
                                string loadeda = SavingManager.GetWorldSaveData("teamA"), loadedb = SavingManager.GetWorldSaveData("teamB");
                                var splitA = loadeda.Split('|').ToList();
                                var splitB = loadedb.Split('|').ToList();
                                foreach (var uuid in allUUIDs)
                                {
                                    if (splitA.Contains(uuid)) { TEAMA.Add(uuid); uncontainedUUIDS.Remove(uuid); }
                                    if (splitB.Contains(uuid)) { TEAMB.Add(uuid); uncontainedUUIDS.Remove(uuid); }
                                }
                                if(uncontainedUUIDS.Count > 0)
                                {
                                    Extensions.ShuffleList(ref uncontainedUUIDS);
                                    bool BLess = TEAMB.Count < TEAMA.Count;
                                    for (int i = 0; i < uncontainedUUIDS.Count; i++)
                                    {
                                        if (i % 2 == 0) { if (BLess) { TEAMB.Add(uncontainedUUIDS[i]); loadedb += "|" + uncontainedUUIDS[i]; } else { TEAMA.Add(uncontainedUUIDS[i]); loadeda += "|" + uncontainedUUIDS[i]; } }
                                        else { if (BLess) { TEAMA.Add(uncontainedUUIDS[i]); loadeda += "|" + uncontainedUUIDS[i]; } else { TEAMB.Add(uncontainedUUIDS[i]); loadedb += "|" + uncontainedUUIDS[i]; } }
                                    }
                                    SavingManager.SetWorldSaveData("teamA", loadeda);
                                    SavingManager.SetWorldSaveData("teamB", loadedb);
                                }
                                SyncTeamsRPC(loadeda, loadedb);
                            }
                        }
                        else {
                            currWorld.Init(currentSeed);
                            //create the teams
                            if(Gamemode.Value == GameModeEnum.TeamDeathmatch)
                            {
                                TEAMA = new();
                                TEAMB = new();

                                // Extract all UUIDs and shuffle them
                                List<string> allUUIDs = new(UNIQUEUSERIDS.Values);
                                Extensions.ShuffleList(ref allUUIDs);
                                string teamA = "";
                                string teamB = "";

                                // Assign alternately to balance teams
                                for (int i = 0; i < allUUIDs.Count; i++)
                                {
                                    if (i % 2 == 0)
                                    {
                                        TEAMA.Add(allUUIDs[i]);
                                        teamA += allUUIDs[i] + "|";
                                    }
                                    else
                                    {
                                        TEAMB.Add(allUUIDs[i]);
                                        teamB += allUUIDs[i] + "|";
                                    }
                                }

                                SavingManager.SetWorldSaveData("teamA", teamA .Length>0 ? teamA[..^1] : "");
                                SavingManager.SetWorldSaveData("teamB", teamB.Length > 0 ? teamB[..^1] : "");
                                SyncTeamsRPC(teamA, teamB);
                            }
                        }
                        ExitLobbyRPC(!loading);
                        return;
                    }
                }
                else { startTimer.Value = 5; }
                break;
            case GameStateEnum.Ingame:
                if (VivoxManager.InLobbyChannel) { VivoxManager.LeaveLobbyChannel(); }
                if (IsSpectating) {
                    if (doxCooldown > 0) { doxCooldown -= Time.deltaTime; }
                    if (hauntCooldown > 0) { hauntCooldown -= Time.deltaTime; }
                    var talkers = "";
                    if(talkingPlayers.Value.Length > 0) {
                        foreach (var id in talkingPlayers.Value.ToString().Split('|'))
                        {
                            var un = localUserNames[ulong.Parse(id)];
                            talkers += (un.Contains('\r') ? un.Split('\r')[0] : un.ToString()) + "\n";
                        }
                        if (talkers.Length > 0) { talkers = talkers[..^1]; }
                    }
                    else { talkers = ""; }
                    UIManager.SetSpectatorTalkingText(talkers);
                    SpectatorControls();
                    if (VivoxManager.InMainChannel)
                    {
                        VivoxManager.SetPosition(Camera.main.gameObject);
                        foreach (var participant in VivoxManager.GetActiveChannels[VivoxManager.DEFAULTCHANNEL])
                        {
                            participant.SetLocalVolume(0);
                        }
                        //if(spectatorMainParticipant == null) { spectatorMainParticipant = VivoxManager.GetActiveChannels[VivoxManager.DEFAULTCHANNEL].FirstOrDefault(x => { return x.DisplayName == Extensions.UniqueIdentifier; }); }
                        //else { spectatorMainParticipant.SetLocalVolume(-50); } //no talking
                    }
                    if (VivoxManager.InSpectatorChannel)
                    {
                        if(spectatorParticipant == null) { spectatorParticipant = VivoxManager.GetActiveChannels[VivoxManager.SPECTATECHANNEL].FirstOrDefault(x => { return x.DisplayName == Extensions.UniqueIdentifier; }); }
                        else { 
                            if (spectatorParticipant.SpeechDetected && !isSpeaking) { 
                                isSpeaking = true; UpdateSpeakingRPC(NetworkManager.Singleton.LocalClientId, true); 
                            } 
                            if(!spectatorParticipant.SpeechDetected && isSpeaking)
                            {
                                isSpeaking = false; UpdateSpeakingRPC(NetworkManager.Singleton.LocalClientId, false);
                            }
                        }

                        if (Input.GetKeyDown(KeyCode.V)) { VivoxManager.ToggleInputMute(); }
                        UIManager.SetMuteIcon(VivoxManager.initialised && !VivoxManager.GetisMuted);
                    }
                    else { UIManager.SetMuteIcon(false); }
                    UIManager.ToggleDamageIndicator(false);
                    ScreenEffectsManager.SetAberration(0);
                    ScreenEffectsManager.SetVignette(0);
                    ScreenEffectsManager.SetSaturation(0);
                    ScreenEffectsManager.SetMotionBlur(0);
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                else if (Player.LocalPlayer) {
                    UIManager.SetSpectatorTalkingText("");

                    //mute spectators as a player
                    if (VivoxManager.InMainChannel)
                    {
                        foreach (var participant in VivoxManager.GetActiveChannels[VivoxManager.DEFAULTCHANNEL])
                        {
                            participant.SetLocalVolume(Player.PLAYERBYID.ContainsKey(localUUIDtoID[participant.DisplayName]) ? 0 : (Player.LocalPlayer.ph.isConscious.Value ? -50 : -20)); //if you are dying you can hear the dead cuz ur losing it lol
                        }
                    }
                }

                if (SavingManager.LOADING) { return; }
                if (!IsServer) { return; }
                if (timeSinceLastDeath.Value > 0) { timeSinceLastDeath.Value -= Time.deltaTime; }
                allPlayersInit.Value = initialisedPlayers >= NetworkManager.Singleton.ConnectedClients.Count;
                if (!ALL_PLAYERS_INITIALISED) { return; }
                switch (Gamemode.Value)
                {
                    case GameModeEnum.Survival:
                        //nothing ever happens
                        break;
                    case GameModeEnum.Deathmatch:
                        //one player standing
                        if(Player.PLAYERS.Count == 1)
                        {
                            Gamestate.Value = GameStateEnum.Winnerscreen;
                            ShowVictoryScreenRPC($"{Player.PLAYERS[0].GetUsername} is victorious.", new ulong[] { Player.PLAYERS[0].OwnerClientId });
                            CleanUp();
                        }
                        break;
                    case GameModeEnum.TeamDeathmatch:
                        bool atleastoneplayeraliveA = false, atleastoneplayeraliveB = false;
                        foreach (var player in Player.PLAYERBYID.Keys)
                        {
                            if (TEAMA.Contains(UNIQUEUSERIDS[player]))
                            {
                                atleastoneplayeraliveA = true;
                            }

                            if (TEAMB.Contains(UNIQUEUSERIDS[player]))
                            {
                                atleastoneplayeraliveB = true;
                            }
                        }

                        if(atleastoneplayeraliveA && !atleastoneplayeraliveB)
                        {
                            var winners = "";
                            var winnerids = new List<ulong>();
                            foreach (var player in TEAMA)
                            {
                                if (!CLIENTIDFROMUUID.ContainsKey(player)) { continue; }
                                winners += USERNAMES[CLIENTIDFROMUUID[player]].ToString().Split('\r')[0] + ",";
                                winnerids.Add(CLIENTIDFROMUUID[player]);
                            }
                            Gamestate.Value = GameStateEnum.Winnerscreen;
                            ShowVictoryScreenRPC($"{winners[..^1]} are victorious.", winnerids.ToArray());
                            CleanUp();
                        }
                        else if(atleastoneplayeraliveB && !atleastoneplayeraliveA)
                        {
                            var winners = "";
                            var winnerids = new List<ulong>();
                            foreach (var player in TEAMB)
                            {
                                if (!CLIENTIDFROMUUID.ContainsKey(player)) { continue; }
                                winners += USERNAMES[CLIENTIDFROMUUID[player]].ToString().Split('\r')[0] + ",";
                                winnerids.Add(CLIENTIDFROMUUID[player]);
                            }
                            Gamestate.Value = GameStateEnum.Winnerscreen;
                            ShowVictoryScreenRPC($"{winners[..^1]} are victorious.", winnerids.ToArray());
                            CleanUp();
                        }
                        else if(!atleastoneplayeraliveA && !atleastoneplayeraliveB)
                        {
                            Gamestate.Value = GameStateEnum.Winnerscreen;   
                            ShowVictoryScreenRPC("Nobody is victorious", new ulong[0]);
                            CleanUp();
                        }
                        break;
                    case GameModeEnum.Arena:
                        break;
                }
                break;
            case GameStateEnum.Winnerscreen:
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (VivoxManager.InLobbyChannel)
                {
                    if (Input.GetKeyDown(KeyCode.V) && VivoxManager.InLobbyChannel) { VivoxManager.ToggleInputMute(); }
                    UIManager.SetMuteIcon(VivoxManager.initialised && !VivoxManager.GetisMuted && VivoxManager.InLobbyChannel);
                }
                else
                {
                    UIManager.SetMuteIcon(false);
                }
                break;
        }
    }

    [Rpc(SendTo.NotServer)]
    private void SyncTeamsRPC(string teama, string teamb)
    {
        TEAMA = new();
        TEAMB = new();
        foreach (var p in teama.Split('|'))
        {
            TEAMA.Add(p);
        }
        foreach (var p in teamb.Split('|'))
        {
            TEAMB.Add(p);
        }
    }

    private void SpectatorControls()
    {
        if (!SpectatorCamera.spectatingTarget) { UIManager.SetBottomscreenText(""); return; }

        UIManager.SetBottomscreenText((hauntCooldown <= 0 ? "F - Haunt" : "") + (doxCooldown <= 0 && timeSinceLastDeath.Value<=0 ? "\nSpace - Mark for Death" : ""));
        if(hauntCooldown <= 0 && Input.GetKeyDown(KeyCode.F))
        {
            var rand = Random.value;
            if (rand <= 0.3)
            {
                SpectatorCamera.spectatingTarget.ph.TryWakeUp();
            }
            else if(rand > 0.3 && rand <= 0.6)
            {
                SpectatorCamera.spectatingTarget.Haunted(false);
            }
            else if(rand > 0.6 && rand <= 0.74f)
            {
                SpectatorCamera.spectatingTarget.pm.GetRigidbody.AddForce(Vector3.up * 5, ForceMode.Impulse);
            }
            else if(rand > 0.74f && rand <= 0.8f)
            {
                PlayerInventory.DropRightHandItem();
            }
            else if(rand > 0.8f && rand <= 0.95f)
            {
                SpectatorCamera.spectatingTarget.Haunted(true);
            }
            else if(rand > 0.95f)
            {
                SpectatorCamera.spectatingTarget.KnockOver(1);
            }
            hauntCooldown = 5f;
        }

        if (doxCooldown <= 0 && Input.GetKeyDown(KeyCode.Space) && timeSinceLastDeath.Value <= 0)
        {
            MarkPlayerRPC(SpectatorCamera.spectatingTarget.OwnerClientId);
            doxCooldown = 15f;
        }
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    private void MarkPlayerRPC(ulong targ)
    {
        UIManager.SetMarkedForDeathIcon(targ);
    }

    [Rpc(SendTo.Server)]
    private void UpdateSpeakingRPC(ulong id, bool talking)
    {
        if (talking && !serverSpeakingList.Contains(id)) { serverSpeakingList.Add(id); }
        else if (!talking && serverSpeakingList.Contains(id)) { serverSpeakingList.Remove(id); }
    }

    [Rpc(SendTo.Everyone)]
    private void ShowVictoryScreenRPC(string winners, ulong[] winnerarray)
    {
        UIManager.ResetUI();
        UIManager.ShowWinScreen();
        UIManager.SetWinText(winners, winnerarray.Contains(NetworkManager.Singleton.LocalClientId) ? "You Win!" : "You Lose!");

        if (!VivoxManager.GetisMuted) { VivoxManager.ToggleInputMute(); }
        DespawnPlayerRPC(NetworkManager.LocalClientId);
        VivoxManager.JoinLobbyChannel();
        if (VivoxManager.InSpectatorChannel) { VivoxManager.LeaveSpectateChannel(); spectatorMainParticipant = null; spectatorParticipant = null; }
        if(VivoxManager.InMainChannel) { VivoxManager.LeaveMainChannel(); }
        IsSpectating = false;
        isSpeaking = false;
    }

    private static float initialisedPlayers = 0;
    public static void PlayerInitialisationComplete()
    {
        if (ALL_PLAYERS_INITIALISED) { return; }
        instance.PlayerInitCompleteRPC();
    }

    [Rpc(SendTo.Server)]
    private void PlayerInitCompleteRPC()
    {
        initialisedPlayers++;
    }

    public static void BackToLobbyFromWin()
    {
        instance.startTimer.Value = 5;
        instance.readiedPlayers.Clear();
        instance.LobbyFromWinRPC();
        if (instance.currWorld) { instance.currWorld.NetworkObject.Despawn(); }
        instance.timeSinceLastDeath.Value = 0;
    }

    [Rpc(SendTo.Everyone)]
    private void LobbyFromWinRPC()
    {
        readied = false;
        UIManager.SetReadyUpButtonText("Ready Up");
        UIManager.LobbyFromWinScreen();
        UIManager.UpdatePlayerList(localUserNames, new());
        instance.doxCooldown = 0;
        instance.hauntCooldown = 0;
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
        if (VivoxManager.InLobbyChannel) { VivoxManager.LeaveLobbyChannel(); }
        lobbyParticipant = null;
        isSpeaking = false;
    }

    public static void EnableSpectator()
    {
        if(GetGamestate != GameStateEnum.Ingame || (GetGameMode == GameModeEnum.Deathmatch && Player.PLAYERS.Count < 2)) { return; }
        VivoxManager.JoinSpectateChannel();
        if (!VivoxManager.GetisMuted) { VivoxManager.ToggleInputMute(); }
        IsSpectating = true;
        VivoxManager.JoinMainChannel(); //ok wait hear me out
        instance.isSpeaking = false;
        instance.ResetCooldownRPC();
        instance.doxCooldown = 15f;
        instance.hauntCooldown = 1f;
        instance.timeSinceLastDeath.Value = 30;
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void ResetCooldownRPC()
    {
        timeSinceLastDeath.Value = 70;
    }

    public static void EnsureLeaveChannels()
    {
        if (VivoxManager.InSpectatorChannel) { VivoxManager.LeaveSpectateChannel(); }
        if (VivoxManager.InLobbyChannel) { VivoxManager.LeaveLobbyChannel(); }
        if (VivoxManager.InMainChannel) { VivoxManager.LeaveMainChannel(); }
    }

    public static void CleanUp()
    {
        foreach (var ob in GameObject.FindGameObjectsWithTag("WorldObject"))
        {
            if(ob.TryGetComponent(out NetworkObject netob) && netob.IsSpawned)
            {
                netob.Despawn();
            }
        }

        instance.CleanupRPC();
    }

    [Rpc(SendTo.Everyone)]
    private void CleanupRPC()
    {
        foreach (var ob in GameObject.FindGameObjectsWithTag("WorldDetail"))
        {
            Destroy(ob);
        }
    }

    public static void LocalCleanup()
    {
        foreach (var ob in GameObject.FindGameObjectsWithTag("WorldDetail"))
        {
            Destroy(ob);
        }
    }

    public static void BackToLobby()
    {
        instance.readiedPlayers.Clear();
        instance.startTimer.Value = 5.1f;
        SavingManager.Save(GetSaveFileLocation, instance.currentSaveFile);
        instance.Gamestate.Value = GameStateEnum.Lobby;
        instance.currWorld.NetworkObject.Despawn();
        SavingManager.GetWorldInfo(GetSaveFileLocation, instance.currentSaveFile, out string worldinfo);
        CleanUp();
        instance.ToLobbyRPC(worldinfo);
        instance.timeSinceLastDeath.Value = 0;
    }

    [Rpc(SendTo.Everyone)]
    private void ToLobbyRPC(string worldinfo)
    {
        UIManager.ResetUI();
        readied = false;
        UIManager.ShowLobby();
        UIManager.HideGameOverScreen();
        UIManager.SetReadyUpButtonText("Ready Up");
        if (!VivoxManager.GetisMuted) { VivoxManager.ToggleInputMute(); }
        DespawnPlayerRPC(NetworkManager.LocalClientId);
        VivoxManager.JoinLobbyChannel();
        if(VivoxManager.InSpectatorChannel) { VivoxManager.LeaveSpectateChannel(); spectatorParticipant = null; spectatorMainParticipant = null; }
        if (VivoxManager.InMainChannel) { VivoxManager.LeaveMainChannel(); }
        IsSpectating = false;
        UIManager.SetWorldInfo(true, worldinfo);
        isSpeaking = false;
        UIManager.UpdatePlayerList(localUserNames, new());
        instance.doxCooldown = 0;
        instance.hauntCooldown = 0;
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
