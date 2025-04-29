using System.Collections.Generic;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using static Unity.VisualScripting.Member;
using UnityEngine.UIElements;

public class AudioTapManager : MonoBehaviour
{
    public AudioMixerGroup targetMixer;

    private void Start()
    {
        VivoxManager.InitialisationComplete += () =>
        {
            VivoxService.Instance.ParticipantAddedToChannel += ParticipantAddedToChannel;
            VivoxService.Instance.ParticipantRemovedFromChannel += ParticipantRemovedFromChannel;
        };
    }

    private Dictionary<string, AudioSource> participantAudioSources = new();

    public AudioSource GetParticipantAudioSource(string participantName)
    {
        if (participantAudioSources.TryGetValue(participantName, out var audioSource))
        {
            return audioSource;
        }
        else
        {
            Debug.LogWarning($"Audio source for participant {participantName} not found.");
            return null;
        }
    }

    private void ParticipantRemovedFromChannel(VivoxParticipant participant)
    {
        Debug.Log("Remove tap");
        participant.DestroyVivoxParticipantTap();
        participantAudioSources.Remove(participant.DisplayName);
    }

    private void ParticipantAddedToChannel(VivoxParticipant participant)
    {
        Debug.Log("Made tap");
        var ob = participant.CreateVivoxParticipantTap(participant.DisplayName, true);
        if (!ob) { return; }
        participant.ParticipantTapAudioSource.outputAudioMixerGroup = targetMixer;
        participantAudioSources.Add(participant.DisplayName, participant.ParticipantTapAudioSource);
    }
}
