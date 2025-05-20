using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using EditorAttributes;
using UnityEngine.Events;
using AYellowpaper.SerializedCollections;

public class AITargeter : MonoBehaviour
{
    public struct RelationshipStruct
    {
        public float Fear, Aggression;
        public bool Ignore;
    }

    public bool CanSee = true;
    [ShowField(nameof(CanSee))] public Transform EyesPosition;
    [ShowField(nameof(CanSee))] public float ViewRange, ViewAngle;
    [ShowField(nameof(CanSee))] public LayerMask ViewMask;

    public bool CanHear = true;
    [ShowField(nameof(CanHear))] public float HearRange, HearingPower;
    [ShowField(nameof(CanHear))] public UnityEvent<float, Vector3> HeardSound;
    public System.Action<float, Vector3> OnHeardSound;

    [ReadOnly]public float FearModifier = 1, AggressionModifier = 1;

    public RelationshipStruct DefaultRelationship;
    public SerializedDictionary<string, RelationshipStruct> Relationships;

    private List<AITarget> aggressiveTo, scaredOf;

    public List<AITarget> GetAggroTargets => aggressiveTo;
    public List<AITarget> GetScaredTargets => scaredOf;

    public AITarget TopAggroTarget => aggressiveTo.Count > 0 ? aggressiveTo[0] : null;
    public AITarget TopScaredTarget => scaredOf.Count > 0 ? scaredOf[0] : null;

    public void FindTarget()
    {
        aggressiveTo.Clear();
        scaredOf.Clear();
        Dictionary<AITarget, Vector3> foundvalues = new();

        foreach (var target in AITarget.targets)
        {
            var relation = DefaultRelationship;
            if (Relationships.ContainsKey(target.TargetType)) { relation = Relationships[target.TargetType]; }
            if (relation.Ignore) { continue; }

            var dist = (target.transform.position - transform.position).sqrMagnitude;
            var dir = (target.transform.position - EyesPosition.transform.position).normalized;
            //seeing is believing
            if (CanSee && dist < ViewRange*ViewRange &&
                Vector3.Angle(dir, EyesPosition.transform.forward) < ViewAngle &&
                Physics.Raycast(EyesPosition.position, dir, out RaycastHit hit, Mathf.Sqrt(dist), ViewMask, QueryTriggerInteraction.Ignore) && hit.transform == target.transform) {
                var fear = relation.Fear * FearModifier * target.ScarinessModifier;
                var aggression = relation.Aggression * AggressionModifier * target.AggroModifier;
                if (fear > aggression) { scaredOf.Add(target); }
                else { aggressiveTo.Add(target); }
                foundvalues.Add(target, new Vector3(dist, fear, aggression));
            }

            if(CanHear && dist < HearRange && (target.AudioEnergy / (1 + (dist * 0.1f))) > HearingPower) {

            }
        }
    }
}
