using UnityEngine;

[CreateAssetMenu(fileName = "New Layers Profile", menuName = "ScriptableObjects/MusicProfile")]
public class MusicLayerProfile : ScriptableObject
{
    public AudioClip[] layers;
    public float volume = 1;
}