using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class NetworkAudioManager : NetworkBehaviour
{
    [System.Serializable]
    public struct AudioClipStruct
    {
        public string Key;
        public AudioClip Value;
    }
    [SerializeField]
    private AudioClipStruct[] NetworkedAudioClips;
    private Dictionary<string, AudioClip> ClipsDictionary = new Dictionary<string, AudioClip>();
    [SerializeField]private AudioSource DefAudioOb; 
    public static NetworkAudioManager instance;

    private void Awake()
    {
        instance = this;
        foreach (var clip in NetworkedAudioClips)
        {
            ClipsDictionary.Add(clip.Key, clip.Value);
        }
    }

    public static void PlayNetworkedAudioClip(float pitch, float volume, float doppler, Vector3 position, string clip)
    {
        if (!instance.ClipsDictionary.ContainsKey(clip)) { return; }
        instance.PlayClip_ServerRPC(pitch, volume, doppler, position, clip, NetworkManager.Singleton.LocalClientId);

        //play locally
        AudioSource source = Instantiate(instance.DefAudioOb, position, Quaternion.identity);
        source.pitch = pitch;
        source.volume = volume;
        source.clip = instance.ClipsDictionary[clip];
        source.dopplerLevel = doppler;
        source.Play();
        Destroy(source.gameObject, instance.ClipsDictionary[clip].length);
    }

    [ServerRpc(RequireOwnership =false)]
    private void PlayClip_ServerRPC(float pitch, float volume, float doppler, Vector3 position, string clip, ulong except)
    {
        PlayClip_ClientRPC(pitch, volume, doppler, position, clip, except);
    }

    [ClientRpc]
    private void PlayClip_ClientRPC(float pitch, float volume, float doppler, Vector3 position, string clip, ulong except)
    {
        if(NetworkManager.Singleton.LocalClientId == except) { return; }
        AudioSource source = Instantiate(DefAudioOb, position, Quaternion.identity);
        source.pitch = pitch;
        source.volume = volume;
        source.clip = ClipsDictionary[clip];
        source.dopplerLevel = doppler;
        source.Play();
        Destroy(source.gameObject, ClipsDictionary[clip].length);
    }
}
