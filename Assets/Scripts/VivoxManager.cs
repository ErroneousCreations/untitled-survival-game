using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Unity.Services.Vivox.AudioTaps;

public class VivoxManager : MonoBehaviour
{
    public static string DEFAULTCHANNEL = "MAIN", SPECTATECHANNEL = "SPECTATE", LOBBYCHANNEL = "LOBBY";
    public static System.Action InputDevicesChanged;
    public static System.Action InitialisationComplete;
    public static bool initialised, allocated;
    private static bool wantstoQuit, quitting;

    public static bool LeavingChannel;

    private void Awake()
    {
        UnityServicesManager.InitialisationComplete += () => { InitializeAsync(); };
        Application.wantsToQuit += CanQuit;
        Relay.AllocationCreated += AllocationCreated;
    }

    private void AllocationCreated(string allocation)
    {
        DEFAULTCHANNEL = allocation + "_MAIN";
        SPECTATECHANNEL = allocation + "_SPECTATE";
        LOBBYCHANNEL = allocation + "_LOBBY";
        allocated = true;
    }

    public static void OverwriteAllocationID(string allocation)
    {
        DEFAULTCHANNEL = allocation + "_MAIN";
        SPECTATECHANNEL = allocation + "_SPECTATE";
        LOBBYCHANNEL = allocation + "_LOBBY";
        allocated = true;
    }

    public static void SetAudioTaps()
    {
        foreach (var tap in FindObjectsByType<VivoxChannelAudioTap>(FindObjectsSortMode.None))
        {
            tap.ChannelName = DEFAULTCHANNEL;
        }
    }

    async void InitializeAsync()
    {
        await VivoxService.Instance.InitializeAsync();

        LoginOptions options = new()
        {
            DisplayName = Extensions.UniqueIdentifier,
        };
        await VivoxService.Instance.LoginAsync(options);
        if (!GetisMuted) { ToggleInputMute(); }
        VivoxService.Instance.AvailableInputDevicesChanged += () => { InputDevicesChanged?.Invoke(); };

        initialised = true;
        InitialisationComplete?.Invoke();
    }

    public static bool InMainChannel => VivoxService.Instance.ActiveChannels.ContainsKey(DEFAULTCHANNEL);
    public static bool InLobbyChannel => VivoxService.Instance.ActiveChannels.ContainsKey(LOBBYCHANNEL);
    public static bool InSpectatorChannel => VivoxService.Instance.ActiveChannels.ContainsKey(SPECTATECHANNEL);

    public static ReadOnlyDictionary<string, ReadOnlyCollection<VivoxParticipant>> GetActiveChannels => VivoxService.Instance.ActiveChannels;

    public static int GetChannelCount => VivoxService.Instance.ActiveChannels.Count;

    public static async void JoinMainChannel(System.Action calledonComplete = null)
    {
        if (InMainChannel) { return; }
        await VivoxService.Instance.JoinPositionalChannelAsync(DEFAULTCHANNEL, ChatCapability.AudioOnly, new(40, 15, 1f, AudioFadeModel.ExponentialByDistance));
        calledonComplete?.Invoke();
    }

    public static async void JoinLobbyChannel(System.Action calledonComplete = null)
    {
        if (InLobbyChannel) { return; }
        await VivoxService.Instance.JoinGroupChannelAsync(LOBBYCHANNEL, ChatCapability.AudioOnly);
        calledonComplete?.Invoke();
    }

    public static void JoinSpectateChannel(System.Action calledonComplete = null)
    {
        if (InSpectatorChannel) { return; }
        VivoxService.Instance.JoinGroupChannelAsync(SPECTATECHANNEL, ChatCapability.AudioOnly);
        calledonComplete?.Invoke();
    }

    public static async void LeaveMainChannel(System.Action calledonComplete = null)
    {
        if (!InMainChannel) { return; }
        await VivoxService.Instance.LeaveChannelAsync(DEFAULTCHANNEL);
        calledonComplete?.Invoke();
    }

    public static async void LeaveLobbyChannel (System.Action calledonComplete = null)
    {
        if (!InLobbyChannel) { return; }
        await VivoxService.Instance.LeaveChannelAsync(LOBBYCHANNEL);
        calledonComplete?.Invoke();
    }

    public static void LeaveSpectateChannel()
    {
        if (!InSpectatorChannel) { return; }
        VivoxService.Instance.LeaveChannelAsync(SPECTATECHANNEL);
    }

    public static void SetAudioInputVolume(int volume)
    {
        VivoxService.Instance.SetInputDeviceVolume(volume);
    }

    public static void SetAudioOutputVolume(int volume)
    {
        VivoxService.Instance.SetOutputDeviceVolume(volume);
    }

    public static void SetAudioInputDevice(VivoxInputDevice device)
    {
        VivoxService.Instance.SetActiveInputDeviceAsync(device);
    }

    public static void SetAudioOutputDevice(VivoxOutputDevice device)
    {
        VivoxService.Instance.SetActiveOutputDeviceAsync(device);
    }

    public static float GetInputVolume => VivoxService.Instance.InputDeviceVolume;
    public static float GetOutputVolume => VivoxService.Instance.OutputDeviceVolume;

    public static ReadOnlyCollection<VivoxInputDevice> GetInputDevices => VivoxService.Instance.AvailableInputDevices;
    public static VivoxInputDevice ActiveInputDevice => VivoxService.Instance.ActiveInputDevice;

    public static void SetPosition(GameObject ob)
    {
        VivoxService.Instance.Set3DPosition(ob, DEFAULTCHANNEL);
    }

    public static void ToggleInputMute()
    {
        if(VivoxService.Instance.IsInputDeviceMuted)
        {
            VivoxService.Instance.UnmuteInputDevice();
        }
        else
        {
            VivoxService.Instance.MuteInputDevice();
        }
    }   

    public static bool GetisMuted => VivoxService.Instance.IsInputDeviceMuted;

    private static bool CanQuit()
    {
        if (quitting) { return false; }
        if (!wantstoQuit) { QuitProcess(); }
        return wantstoQuit;
    }

    private static async void QuitProcess()
    {
        quitting = true;
        wantstoQuit = false;
        await VivoxService.Instance.LogoutAsync();
        wantstoQuit = true;
        quitting = false;
        Application.Quit();
    }

    public static void LeaveAllChannels()
    {
        VivoxService.Instance.LeaveAllChannelsAsync();
    }
}
