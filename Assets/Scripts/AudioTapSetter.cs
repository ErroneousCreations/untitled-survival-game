using UnityEngine;
using Unity.Services.Vivox.AudioTaps;
using Unity.Netcode;

public class AudioTapSetter : MonoBehaviour
{
    public enum AudioChannelEnum { Main, Lobby, Spectator }
    public AudioChannelEnum Channel;
    public VivoxChannelAudioTap Tap;

    private void Update()
    {
        switch (Channel)
        {
            case AudioChannelEnum.Main:
                if (NetworkManager.Singleton && NetworkManager.Singleton.IsConnectedClient && VivoxManager.allocated && VivoxManager.InMainChannel) {
                    if (!Tap.enabled) { Tap.enabled = true; Tap.ChannelName = VivoxManager.DEFAULTCHANNEL; }
                }
                else {
                    if (Tap.enabled) { Tap.enabled = false; }
                }
                break;
            case AudioChannelEnum.Lobby:
                if (NetworkManager.Singleton && NetworkManager.Singleton.IsConnectedClient && VivoxManager.allocated && VivoxManager.InLobbyChannel)
                {
                    if (!Tap.enabled) { Tap.enabled = true; Tap.ChannelName = VivoxManager.LOBBYCHANNEL; }
                }
                else
                {
                    if (Tap.enabled) { Tap.enabled = false; }
                }
                break;
            case AudioChannelEnum.Spectator:
                if (NetworkManager.Singleton && NetworkManager.Singleton.IsConnectedClient && VivoxManager.allocated && VivoxManager.InSpectatorChannel)
                {
                    if (!Tap.enabled) { Tap.enabled = true; Tap.ChannelName = VivoxManager.SPECTATECHANNEL; }
                }
                else
                {
                    if (Tap.enabled) { Tap.enabled = false; }
                }
                break;
        }
    }
}
