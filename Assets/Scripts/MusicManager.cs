using UnityEngine;
using System.Collections.Generic;
using EditorAttributes;
using UnityEngine.Audio;
using DG.Tweening;

public class MusicManager : MonoBehaviour
{
    [SerializeField, Header("Threat Music")] private float fadeSpeed = 2f;
    [SerializeField] private float threatDecaySpeed = 2f, threatdecayCooldown = 20;
    [SerializeField] private AudioMixerGroup mixer;
    [SerializeField, Space, Header("Music")]
    private AudioSource musicSourceA;
    [SerializeField] private AudioSource musicSourceB;
    [SerializeField] private float musicFadeDuration;
    [SerializeField] private float musicBaseVolume, musicFadeOutLength = 2f;

    private float volume, currDecayCd;

    private class LayerData
    {
        public AudioSource source;
        public float targetVolume;
        public float threshold;
    }

    private List<LayerData> layers = new();
    [SerializeField, ReadOnly, Space] private float threatLevel;
    [SerializeField, ReadOnly] private MusicLayerProfile currentProfile;
    private static MusicManager instance;
    private bool playingThreatMusic;
    private bool usingsource = false;
    private bool playingMusic => musicSourceA.isPlaying || musicSourceB.isPlaying;
    private float currMusicVolume = 0;
    private bool transitioning;

    public static void PlayMusicTrack(AudioClip newclip, bool loop = false, float volume = -1)
    {
        if (volume <= 0) { volume = instance.musicBaseVolume; }
        if (instance.playingThreatMusic) { return; } //threatmusic overrides regular music

        //fade out music
        if (!newclip)
        {
            instance.transitioning = true;
            instance.musicSourceA.DOKill();
            instance.musicSourceB.DOKill();
            instance.musicSourceA.DOFade(0, instance.musicFadeDuration).onComplete += () => { instance.musicSourceA.Stop(); instance.transitioning = false; };
            instance.musicSourceB.DOFade(0, instance.musicFadeDuration).onComplete += () => { instance.musicSourceA.Stop(); };
        }

        if (!instance.usingsource)
        {
            instance.transitioning = true;
            instance.usingsource = true;
            instance.musicSourceA.DOFade(0, instance.musicFadeDuration).onComplete += () => { instance.musicSourceA.Stop(); instance.transitioning = false; };
            instance.musicSourceB.volume = volume;
            instance.musicSourceB.clip = newclip;
            instance.musicSourceB.loop = loop;
            instance.currMusicVolume = volume;
            instance.musicSourceB.Play();
            instance.musicSourceB.DOFade(1, instance.musicFadeDuration);
        }
        else
        {
            instance.transitioning = true;
            instance.usingsource = false;
            instance.musicSourceB.DOFade(0, instance.musicFadeDuration).onComplete += () => { instance.musicSourceB.Stop(); instance.transitioning = false; };
            instance.musicSourceA.volume = volume;
            instance.musicSourceA.clip = newclip;
            instance.musicSourceA.loop = loop;
            instance.currMusicVolume = volume;
            instance.musicSourceA.Play();
            instance.musicSourceA.DOFade(1, instance.musicFadeDuration);
        }
    }

    private void Awake()
    {
        instance = this;
    }

    public static void Init(MusicLayerProfile newProfile)
    {
        instance.currentProfile = newProfile;
        instance.ResetLayers();
        instance.playingThreatMusic = false;
        foreach (var layer in instance.layers)
        {
            layer.targetVolume = instance.threatLevel >= layer.threshold ? instance.volume : 0f;
            if(instance.threatLevel >= layer.threshold) { instance.playingThreatMusic = true; }
        }
        if (instance.playingThreatMusic) { PlayMusicTrack(null); }
    }

    private void ResetLayers()
    {
        foreach (var data in layers)
            Destroy(data.source.gameObject);

        layers.Clear();

        if (currentProfile == null) return;
        volume = currentProfile.volume;

        int k = 0;
        foreach (var layer in currentProfile.layers)
        {
            GameObject go = new GameObject("Layer" + (k + 1));
            go.transform.parent = transform;

            var src = go.AddComponent<AudioSource>();
            src.clip = layer;
            src.loop = true;
            src.playOnAwake = false;
            src.volume = 0f;
            src.spatialBlend = 0f; // 2D
            src.Play();
            src.outputAudioMixerGroup = mixer;

            layers.Add(new LayerData
            {
                source = src,
                targetVolume = 0f,
                threshold = (((float)k / currentProfile.layers.Length) + (1f / currentProfile.layers.Length * 0.5f)) * 100f
            });
            k++;
        }
    }

    public static void SetThreatLevel(float newThreat)
    {
        instance.threatLevel = Mathf.Clamp(newThreat, 0f, 100f);
        instance.currDecayCd = instance.threatdecayCooldown;
        instance.playingThreatMusic = false;
        foreach (var layer in instance.layers)
        {
            layer.targetVolume = instance.threatLevel >= layer.threshold ? instance.volume : 0f;
            if (instance.threatLevel >= layer.threshold) { instance.playingThreatMusic = true; }
        }
        if (instance.playingThreatMusic) { PlayMusicTrack(null); }
    }

    public static void AddThreatLevel(float amount)
    {
        instance.threatLevel = Mathf.Clamp(instance.threatLevel + amount, 0f, 100f);
        instance.currDecayCd = instance.threatdecayCooldown;
        instance.playingThreatMusic = false;
        foreach (var layer in instance.layers)
        {
            layer.targetVolume = instance.threatLevel >= layer.threshold ? instance.volume : 0f;
            if (instance.threatLevel >= layer.threshold) { instance.playingThreatMusic = true; }
        }
        if (instance.playingThreatMusic) { PlayMusicTrack(null); }
    }

    private void Update()
    {
        if (currDecayCd > 0) { currDecayCd -= Time.deltaTime; }
        else if(threatLevel>0) { 
            threatLevel -= Time.deltaTime * threatDecaySpeed;
            foreach (var layer in instance.layers)
            {
                layer.targetVolume = instance.threatLevel >= layer.threshold ? instance.volume : 0f;
            }
        }

        foreach (var layer in layers)
        {
            if (Mathf.Approximately(layer.source.volume, layer.targetVolume)) continue;

            layer.source.volume = Mathf.Lerp(
                layer.source.volume,
                layer.targetVolume,
                Time.deltaTime / fadeSpeed
            );
        }

        if(!transitioning && musicSourceA.isPlaying && !musicSourceA.loop && (musicSourceA.clip.length- musicSourceA.time > musicFadeOutLength))
        {
            var timeleft = musicSourceA.clip.length - musicSourceA.time;
            musicSourceA.volume = Mathf.Lerp(currMusicVolume, 0, timeleft / musicFadeOutLength);
        }

        if (!transitioning && musicSourceB.isPlaying && !musicSourceB.loop && (musicSourceB.clip.length - musicSourceB.time > musicFadeOutLength))
        {
            var timeleft = musicSourceB.clip.length - musicSourceB.time;
            musicSourceB.volume = Mathf.Lerp(currMusicVolume, 0, timeleft / musicFadeOutLength);
        }
    }
}