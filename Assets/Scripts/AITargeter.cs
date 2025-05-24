using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using EditorAttributes;
using UnityEngine.Events;
using AYellowpaper.SerializedCollections;
using Unity.Netcode;

public class AITargeter : MonoBehaviour
{
    public const float AudioEnergyFalloffDistanceModifer = 0.036f;

    [System.Serializable]
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

    [Space]
    public float DistanceWeight = 0.1f;

    [ReadOnly]public float FearModifier = 1, AggressionModifier = 1;

    public RelationshipStruct DefaultRelationship;
    public SerializedDictionary<string, RelationshipStruct> Relationships;

    protected List<AITarget> aggressiveTo = new(), scaredOf = new();

    public List<AITarget> GetAggroTargets => aggressiveTo;
    public List<AITarget> GetScaredTargets => scaredOf;

    public AITarget TopAggroTarget => aggressiveTo.Count > 0 ? aggressiveTo[0] : null;
    public AITarget TopScaredTarget => scaredOf.Count > 0 ? scaredOf[0] : null;

    public virtual bool GetDoUpdate => Player.LocalPlayer ? (transform.position - Player.LocalPlayer.transform.position).sqrMagnitude < 150*150 : true;

    protected float updateInterval;

    private void Start()
    {
        updateInterval = Random.Range(0.09f, 0.2f);
    }

    private void Update()
    {
        if (!NetworkManager.Singleton) { return; } //if we are not networked, just return
        if (!GetDoUpdate || !NetworkManager.Singleton.IsServer) { return; }

        updateInterval -= Time.deltaTime;
        if (updateInterval <= 0)
        {
            updateInterval = Random.Range(0.09f, 0.2f);
            FindTargets();
        }
    }

    //maybe dont run this every frame lol
    protected virtual void FindTargets()
    {
        aggressiveTo.Clear();
        scaredOf.Clear();
        Dictionary<AITarget, Vector3> foundvalues = new();

        foreach (var target in AITarget.targets)
        {
            if(target.transform == transform) { continue; } //skip self

            var relation = DefaultRelationship;
            if (Relationships.ContainsKey(target.TargetType)) { relation = Relationships[target.TargetType]; }
            if (relation.Ignore) { continue; }

            var dist = (target.transform.position - transform.position).sqrMagnitude;
            var dir = (target.GetPosition - EyesPosition.transform.position).normalized;

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

            if(CanHear && dist < HearRange && (target.AudioEnergy / (1 + (dist * AudioEnergyFalloffDistanceModifer))) > HearingPower) {
                OnHeardSound?.Invoke(target.AudioEnergy, target.transform.position);
                HeardSound?.Invoke(target.AudioEnergy, target.transform.position);
                var fear = relation.Fear * FearModifier * target.ScarinessModifier;
                var aggression = relation.Aggression * AggressionModifier * target.AggroModifier;
                if (fear > aggression) { scaredOf.Add(target); }
                else { aggressiveTo.Add(target); }
                if(!foundvalues.ContainsKey(target)) //if we haven't already added this target from vision
                    foundvalues.Add(target, new Vector3(dist, fear, aggression));
            }

            aggressiveTo.Sort((a,b) => { return SortAggressiveTargets(a, b, foundvalues); });

            scaredOf.Sort((a, b) => { return SortScaredTargets(a, b, foundvalues); });
        }
    }

    public float GetFearFactor(AITarget target)
    {
        return FearModifier * target.ScarinessModifier * (Relationships.ContainsKey(target.TargetType) ? Relationships[target.TargetType].Fear : DefaultRelationship.Fear);
    }

    public float GetAggroFactor(AITarget target)
    {
        return AggressionModifier * target.AggroModifier * (Relationships.ContainsKey(target.TargetType) ? Relationships[target.TargetType].Aggression : DefaultRelationship.Aggression);
    }

    protected virtual int SortAggressiveTargets(AITarget a, AITarget b, Dictionary<AITarget, Vector3> foundvalues)
    {
        Vector3 va = foundvalues[a];
        Vector3 vb = foundvalues[b];

        float aScore = va.z + va.x * DistanceWeight; // aggression - dist influence
        float bScore = vb.z + vb.x * DistanceWeight;

        return bScore.CompareTo(aScore); // Descending (higher aggression = higher priority)
    }

    protected virtual int SortScaredTargets(AITarget a, AITarget b, Dictionary<AITarget, Vector3> foundvalues)
    {
        Vector3 va = foundvalues[a];
        Vector3 vb = foundvalues[b];

        float aScore = va.y + va.x * DistanceWeight; // fear + dist influence (closer = scarier)
        float bScore = vb.y + vb.x * DistanceWeight;

        return bScore.CompareTo(aScore); // Descending (higher fear = higher priority)
    }
}
