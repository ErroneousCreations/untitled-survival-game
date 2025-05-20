using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using EditorAttributes;
using UnityEngine.Events;

public class AITargeter : MonoBehaviour
{
    public bool CanSee = true;
    [ShowField(nameof(CanSee))] public Transform EyesPosition;
    [ShowField(nameof(CanSee))] public float ViewRange, ViewAngle;
    [ShowField(nameof(CanSee))] public LayerMask ViewMask;

    public bool CanHear = true;
    [ShowField(nameof(CanHear))] public float HearRange, HearingPower;
    [ShowField(nameof(CanHear))] public UnityEvent<float, Vector3> HeardSound;
    public System.Action<float, Vector3> OnHeardSound;

    public static List<AITargeter> listeners = new List<AITargeter>();  

    void OnEnable()
    {
        if (!CanHear) return;
        listeners.Add(this);
    }

    void OnDisable()
    {
        if (!CanHear) return;
        listeners.Remove(this);
    }

    public static void SoundEmitted(float intensity, Vector3 position)
    {
        foreach (var targeter in listeners)
        {
            targeter.OnHeardSound?.Invoke(intensity, position);
            targeter.HeardSound?.Invoke(intensity, position);
        }
    }
}
