using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using System.Collections;
using Unity.Collections;
using System.Text;
using System.Linq;
using UnityEngine.UI;

public class GameManager : NetworkBehaviour
{
    public NetworkObject ThePlayer;
    public World world;
    public enum GameStateEnum { Lobby, Ingame }
    public enum GameModeEnum { Survival, Deathmatch }
    private NetworkVariable<GameModeEnum> Gamemode = new();
    private NetworkVariable<GameStateEnum> Gamestate = new();
    private Dictionary<ulong, FixedString128Bytes> USERNAMES = new(); //for the server to keep track and stuff
    private Dictionary<ulong, string> localUserNames = new(); //for clients
    private static GameManager instance;

    //lobby
    private List<ulong> readiedPlayers = new();
    private NetworkVariable<float> startTimer = new(0);
    private bool readied, inlobbychannel;
    private World currWorld;

    public static World GetWorld => instance.currWorld;

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
    }

    private void ApproveClient(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        USERNAMES.Add(request.ClientNetworkId, Encoding.UTF8.GetString(request.Payload));
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
            SyncConnectedsRPC(usernames, ids, readiedPlayers.ToArray(), RpcTarget.Single(obj, RpcTargetUse.Temp));
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
    private void SyncConnectedsRPC(FixedString128Bytes[] usernames, ulong[] ids, ulong[] readied, RpcParams @params)
    {
        localUserNames = new();
        for (int i = 0; i < ids.Length; i++) {
            localUserNames.Add(ids[i], usernames[i].ToString());
        }
        var rlist = readied.ToList();
        UIManager.UpdatePlayerList(localUserNames, rlist);
        readiedPlayers = rlist;
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

        StartCoroutine(FinishRespawn(id));
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void DespawnPlayerRPC(ulong id)
    {
        if (NetworkManager.Singleton.ConnectedClients[id].PlayerObject) { NetworkManager.Singleton.ConnectedClients[id].PlayerObject.Despawn(true); }
    }

    private IEnumerator FinishRespawn(ulong id )
    {
        yield return new WaitForSeconds(0.01f);
        Instantiate(ThePlayer, Extensions.GetCloseSpawnPoint, Quaternion.identity).SpawnAsPlayerObject(id); //todo add the other spawnpoint ranges
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
                        currWorld.Init(-1); //todo seed thingy
                        ExitLobbyRPC();
                    }
                }
                else { startTimer.Value = 5; }
                break;
            case GameStateEnum.Ingame:
                if(!Player.LocalPlayer) { UIManager.SetMuteIcon(false); }
                break;
        }
    }

    [Rpc(SendTo.Everyone)]
    private void ExitLobbyRPC()
    {
        RespawnPlayerRPC(NetworkManager.LocalClientId);
        inlobbychannel = false;
        if(!VivoxManager.GetisMuted) { VivoxManager.ToggleInputMute(); }
        VivoxManager.LeaveLobbyChannel();
        UIManager.FadeToGame();
    }

    public static void BackToLobby()
    {
        instance.Gamestate.Value = GameStateEnum.Lobby;
        instance.currWorld.NetworkObject.Despawn();

        instance.ToLobbyRPC();
    }

    [Rpc(SendTo.Everyone)]
    private void ToLobbyRPC()
    {
        UIManager.ShowLobby();
        UIManager.HideGameOverScreen();
        if(!VivoxManager.GetisMuted) { VivoxManager.ToggleInputMute(); }
        DespawnPlayerRPC(NetworkManager.LocalClientId);
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
}
