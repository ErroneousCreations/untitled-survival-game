using UnityEngine;
using System.Collections.Generic;
using EditorAttributes;
using UnityEngine.Audio;

public class MusicManager : MonoBehaviour
{
    [SerializeField] private float fadeSpeed = 2f;
    [SerializeField] private float threatDecaySpeed = 2f, threatdecayCooldown = 20;
    [SerializeField] private AudioMixerGroup mixer;
    private float volume, currDecayCd;

    private class LayerData
    {
        public AudioSource source;
        public float targetVolume;
        public float threshold;
    }

    private List<LayerData> layers = new();
    [SerializeField, ReadOnly, Space]private float threatLevel;
    [SerializeField, ReadOnly] private MusicLayerProfile currentProfile;
    private static MusicManager instance;

    private void Awake()
    {
        instance = this;
    }

    public static void Init(MusicLayerProfile newProfile)
    {
        instance.currentProfile = newProfile;
        instance.ResetLayers();
        foreach (var layer in instance.layers)
        {
            layer.targetVolume = instance.threatLevel >= layer.threshold ? instance.volume : 0f;
        }
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
        foreach (var layer in instance.layers)
        {
            layer.targetVolume = instance.threatLevel >= layer.threshold ? instance.volume : 0f;
        }
    }

    public static void AddThreatLevel(float amount)
    {
        instance.threatLevel = Mathf.Clamp(instance.threatLevel + amount, 0f, 100f);
        instance.currDecayCd = instance.threatdecayCooldown;
        foreach (var layer in instance.layers)
        {
            layer.targetVolume = instance.threatLevel >= layer.threshold ? instance.volume : 0f;
        }
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
    }
}