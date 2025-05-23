using System.Collections.Generic;
using UnityEngine;

public class AITarget : MonoBehaviour
{
    public static List<AITarget> targets = new List<AITarget>();
    public string TargetType;
    [SerializeField]private Vector3 CentreOffset;
    [SerializeField]private Rigidbody rb;

    [EditorAttributes.ReadOnly]public float ScarinessModifier = 1, AggroModifier = 1, AudioEnergy = 0;

    public Rigidbody GetRigidbody => rb;
    public Vector3 GetVelocity => rb.linearVelocity;
    public Vector3 GetPosition => transform.TransformPoint(CentreOffset);

    private void OnEnable()
    {
        targets.Add(this);
    }

    private void OnDisable()
    {
        targets.Remove(this);
    }
}
