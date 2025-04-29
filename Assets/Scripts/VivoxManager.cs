using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

public class VivoxManager : MonoBehaviour
{
    public const string DEFAULTCHANNEL = "MAIN", SPECTATECHANNEL = "SPECTATE", LOBBYCHANNEL = "LOBBY";
    public static System.Action InputDevicesChanged;
    public static System.Action InitialisationComplete;
    public static bool initialised;

    public static bool LeavingChannel;

    private void Awake()
    {
        UnityServicesManager.InitialisationComplete += () => { InitializeAsync(); };
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

    public static bool InMainChannel => VivoxService.Instance.ActiveChannels.ContainsKey(DEFAULTCHANNEL) && VivoxService.Instance.ActiveChannels[DEFAULTCHANNEL].FirstOrDefault(part => part.DisplayName==Extensions.UniqueIdentifier)!=null;

    public static int GetChannelCount => VivoxService.Instance.ActiveChannels.Count;

    public static async void JoinMainChannel(System.Action calledonComplete = null)
    {
        await VivoxService.Instance.JoinPositionalChannelAsync(DEFAULTCHANNEL, ChatCapability.AudioOnly, new(30, 10, 0.1f, AudioFadeModel.ExponentialByDistance));
        calledonComplete?.Invoke();
    }

    public static async void JoinLobbyChannel(System.Action calledonComplete = null)
    {
        await VivoxService.Instance.JoinGroupChannelAsync(LOBBYCHANNEL, ChatCapability.AudioOnly);
        calledonComplete?.Invoke();
    }

    public static void JoinSpectateChannel()
    {
        VivoxService.Instance.JoinGroupChannelAsync(SPECTATECHANNEL, ChatCapability.AudioOnly);
    }

    public static async void JoinTestChannel(System.Action calledonComplete = null)
    {
        await VivoxService.Instance.JoinEchoChannelAsync("TEST", ChatCapability.AudioOnly);
        calledonComplete?.Invoke();
    }

    public static async void LeaveMainChannel(System.Action calledonComplete = null)
    {
        await VivoxService.Instance.LeaveChannelAsync(DEFAULTCHANNEL);
        calledonComplete?.Invoke();
    }

    public static async void LeaveLobbyChannel (System.Action calledonComplete = null)
    {
        await VivoxService.Instance.LeaveChannelAsync(LOBBYCHANNEL);
        calledonComplete?.Invoke();
    }

    public static void LeaveSpectateChannel()
    {
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

    private void OnApplicationQuit()
    {
        VivoxService.Instance.LogoutAsync();
    }

    public static void LeaveAllChannels()
    {
        VivoxService.Instance.LeaveAllChannelsAsync();
    }
}
