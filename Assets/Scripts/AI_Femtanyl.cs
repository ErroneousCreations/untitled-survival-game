using UnityEngine;
using Unity.Netcode;
using Unity.VisualScripting;
using System.Collections.Generic;
using System.Linq;

public class AI_Femtanyl : NetworkBehaviour
{
    public enum FemtanylAIState { Idle, Scouting, ReturningWithNews, Searching, FightingTarget, GrabbingItem, Retreating, Fleeing }

    [Header("AI Settings")]
    public FemtanylAIState state;
    public AILocomotor locomotor;
    public AITargeter targeter;
    public float IdleWanderRange;
    public float ScoutRange, GetItemRange;

    [Header("Visuals")]
    public Gradient SkinColourGradient, ClothesColourGradient;
    public Transform model;
    public Renderer BodyRenderer;
    public Renderer[] HandRends;

    [Header("Inventory")]
    public MeshFilter LefthandF, RighthandF;
    public MeshRenderer LefthandR, RighthandR;
    public Transform LeftHand, RightHand;
    public ItemData LefthandItem, RighthandItem;

    //seed for rng appearance and behaviour and stuff
    private int ID;
    public void InitialiseCreature(int id, Vector3 nestpos, bool scout)
    {
        homelocation = nestpos;
        ID = id;
        this.scout = scout;
    }

    //characteristics
    private float speedmod = 1, eyeblinktime = 2.5f, throwwindup = 2f, reactiontime = 0.5f, aggression = 1, fear = 1;
    //other privates
    private PickupableItem currItemTarget;
    private float wandercooldown, scoutcooldown, checkItemCooldown, currThrowWindup;
    private Vector3 homelocation, wanderposition, newsPosition;
    private bool scout;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsOwner) { 
            SyncIDRPC(ID);
            Random.InitState(ID);
            speedmod = Random.Range(0.96f, 1.05f);
            eyeblinktime = Random.Range(2f, 3f);
            throwwindup = Random.Range(1.9f, 2.2f);
            reactiontime = Random.Range(0.4f, 0.6f);
            aggression = Random.Range(0.8f, 1.2f);
            fear = Random.Range(0.8f, 1.2f);
            targeter.FearModifier = fear;
            targeter.AggressionModifier = aggression;
            locomotor.maxSpeed *= speedmod;
            checkItemCooldown = Random.Range(1.5f, 3f);
        }
    }

    [Rpc(SendTo.Everyone)]
    private void SyncIDRPC(int id) { ID = id; UpdateRandomCharacteristics(); }

    private void UpdateRandomCharacteristics()
    {
        Random.InitState(ID);
        var skincol = SkinColourGradient.Evaluate(Random.Range(0f, 1f));
        BodyRenderer.materials[0].color = skincol;
        BodyRenderer.materials[1].color = ClothesColourGradient.Evaluate(Random.Range(0f, 1f));
        for (int i = 0; i < HandRends.Length; i++)
        {
            HandRends[i].material.color = skincol;
        }
    }

    private void Update()
    {
        if (!IsOwner || !IsSpawned) { return; }
        switch (state)
        {
            case FemtanylAIState.Idle:
                IdleUpdate();
                break;
            case FemtanylAIState.Scouting:
                ScoutUpdate();
                break;
            case FemtanylAIState.ReturningWithNews:
                ReturningWithNewsUpdate();
                break;
            case FemtanylAIState.Searching:
                SearchingUpdate();
                break;
            case FemtanylAIState.FightingTarget:
                FightingUpdate();
                break;
            case FemtanylAIState.GrabbingItem:
                GrabbingItemUpdate();
                break;
            case FemtanylAIState.Retreating:
                RetreatingUpdate();
                break;
            case FemtanylAIState.Fleeing:
                FleeingUpdate();
                break;
        }
    }

    void IdleUpdate()
    {
        wandercooldown -= Time.deltaTime;
        if (wandercooldown <= 0)
        {
            wanderposition = homelocation + Extensions.RandomCircle * IdleWanderRange;
            locomotor.SetDestination(wanderposition);
            wandercooldown = Random.Range(1f, 3f);
        }
        if ((locomotor.transform.position - wanderposition).sqrMagnitude < 0.25f)
        {
            locomotor.Stop();
            wandercooldown = Random.Range(1f, 3f);
        }

        if (scoutcooldown > 0) { scoutcooldown -= Time.deltaTime; }
        if(scout && scoutcooldown <= 0)
        {
            state = FemtanylAIState.Scouting;
            scoutcooldown = Random.Range(10f, 15f);
            newsPosition = homelocation;
            wanderposition = homelocation + Extensions.RandomCircle * ScoutRange;
            locomotor.SetDestination(wanderposition);
            return;
        }

        //look for items to grab
        checkItemCooldown -= Time.deltaTime;
        if(checkItemCooldown <= 0)
        {
            checkItemCooldown = Random.Range(1.5f, 3f);
            //make sure we have a free hand
            if(!LefthandItem.IsValid || !RighthandItem.IsValid) {
                var obs = Physics.OverlapSphere(transform.position, GetItemRange, Extensions.ItemLayermask);
                if (obs.Length > 0)
                {
                    float dist = Mathf.Infinity;
                    PickupableItem closest = null;
                    foreach (var ob in obs)
                    {
                        var thisdist = (transform.position - ob.transform.position).sqrMagnitude;
                        if (ob.TryGetComponent(out PickupableItem item) && (item.ItemType == ItemTypeEnum.ThrowingBluntWeapon || item.ItemType == ItemTypeEnum.ThrowingSharpWeapon) && thisdist < dist)
                        {
                            dist = thisdist;
                            closest = item;
                        }
                    }
                    if (closest != null)
                    {
                        state = FemtanylAIState.GrabbingItem;
                        currItemTarget = closest;
                        return;
                    }
                }
            }
        }

        //check if we need to RUN TF AWAY
        if (targeter.GetScaredTargets.Count > 0)
        {
            state = FemtanylAIState.Fleeing;
            return;
        }

        //check for offensive targets
        if (targeter.TopAggroTarget && (RighthandItem.IsValid || LefthandItem.IsValid))
        {
            state = FemtanylAIState.FightingTarget;
            return;
        }
    }

    private void ScoutUpdate()
    {
        if (scoutcooldown > 0 && (transform.position - wanderposition).sqrMagnitude < 1) { scoutcooldown -= Time.deltaTime; }
        if (scoutcooldown <= 0)
        {
            state = FemtanylAIState.ReturningWithNews;
            scoutcooldown = Random.Range(120f, 180f);
            locomotor.SetDestination(homelocation);
            return;
        }

        if (targeter.TopAggroTarget)
        {
            state = FemtanylAIState.ReturningWithNews;
            newsPosition = targeter.TopAggroTarget.transform.position;
            scoutcooldown = Random.Range(120f, 180f);
            return;
        }

        if (targeter.GetScaredTargets.Count > 0)
        {
            state = FemtanylAIState.Fleeing;
            return;
        }
    }

    private void ReturningWithNewsUpdate()
    {
        locomotor.SetDestination(homelocation);
        if ((transform.position - homelocation).sqrMagnitude < 25)
        {
            if(newsPosition != homelocation)
            {
                //send the news to our allies
                var targets = Physics.OverlapSphere(homelocation, IdleWanderRange*1.5f, Extensions.CreatureLayermask);
                foreach (var target in targets)
                {
                    if(target.transform == transform) { continue; } //skip self
                    if (target.TryGetComponent(out AI_Femtanyl femtanyl))
                    {
                        femtanyl.InformOfNews(newsPosition);
                    }
                }
                state = FemtanylAIState.Searching;
                wanderposition = newsPosition + Extensions.RandomCircle * 5;
                locomotor.SetDestination(wanderposition);
                newsPosition = homelocation; //reset the news position
            }
            else
            {
                //we have no news to report, so we can go back to idling
                wandercooldown = 0;
                state = FemtanylAIState.Idle;
                newsPosition = homelocation; //reset the news position
            }
            return;
        }

        if (targeter.TopAggroTarget && (RighthandItem.IsValid || LefthandItem.IsValid))
        {
            state = FemtanylAIState.FightingTarget;
            return;
        }
    }

    private void RetreatingUpdate()
    {
        if (targeter.TopAggroTarget && (RighthandItem.IsValid || LefthandItem.IsValid))
        {
            state = FemtanylAIState.FightingTarget;
            return;
        }
        if (targeter.GetScaredTargets.Count > 0)
        {
            state = FemtanylAIState.Retreating;
            return;
        }

        locomotor.SetDestination(homelocation);
        if ((transform.position - homelocation).sqrMagnitude < IdleWanderRange*IdleWanderRange)
        {
            if (RighthandItem.IsValid) { RighthandItem = ItemData.Empty; }
            else if (LefthandItem.IsValid) { LefthandItem = ItemData.Empty; }
            state = FemtanylAIState.Idle;
            wanderposition = homelocation + Extensions.RandomCircle * IdleWanderRange;
            locomotor.SetDestination(wanderposition);
            return;
        }
    }

    private void SearchingUpdate()
    {
        if (targeter.TopAggroTarget && (RighthandItem.IsValid || LefthandItem.IsValid))
        {
            state = FemtanylAIState.FightingTarget;
            return;
        }
        if (targeter.GetScaredTargets.Count > 0)
        {
            state = FemtanylAIState.Retreating;
            return;
        }
        var dist = (transform.position - wanderposition).sqrMagnitude;
        if(dist < 144)
        {
            scoutcooldown -= Time.deltaTime;
            if (scoutcooldown <= 0)
            {
                state = FemtanylAIState.Retreating;
                wanderposition = homelocation + Extensions.RandomCircle * IdleWanderRange;
                locomotor.SetDestination(wanderposition);
                return;
            }
        }
        if (dist < 1)
        {
            locomotor.Stop();
            wanderposition = wanderposition + Extensions.RandomCircle * 5;
            locomotor.SetDestination(wanderposition);
        }
        //look for items to grab
        checkItemCooldown -= Time.deltaTime;
        if (checkItemCooldown <= 0)
        {
            checkItemCooldown = Random.Range(1.5f, 3f);
            //make sure we have a free hand
            if (!LefthandItem.IsValid || !RighthandItem.IsValid)
            {
                var obs = Physics.OverlapSphere(transform.position, GetItemRange, Extensions.ItemLayermask);
                if (obs.Length > 0)
                {
                    float nearestdist = Mathf.Infinity;
                    PickupableItem closest = null;
                    foreach (var ob in obs)
                    {
                        var thisdist = (transform.position - ob.transform.position).sqrMagnitude;
                        if (ob.TryGetComponent(out PickupableItem item) && (item.ItemType == ItemTypeEnum.ThrowingBluntWeapon || item.ItemType == ItemTypeEnum.ThrowingSharpWeapon) && thisdist < nearestdist)
                        {
                            nearestdist = thisdist;
                            closest = item;
                        }
                    }
                    if (closest != null)
                    {
                        state = FemtanylAIState.GrabbingItem;
                        currItemTarget = closest;
                        return;
                    }
                }
            }
        }
    }

    public void InformOfNews(Vector3 position)
    {
        if (state == FemtanylAIState.Idle || state == FemtanylAIState.GrabbingItem)
        {
            state = FemtanylAIState.Searching;
            wanderposition = position + Extensions.RandomCircle * 5;
            locomotor.SetDestination(wanderposition);
            scoutcooldown = Random.Range(10f, 15f);
        }
    }

    private void FightingUpdate()
    {
        if(!targeter.TopAggroTarget && (RighthandItem.IsValid || LefthandItem.IsValid))
        {
            state = FemtanylAIState.Retreating;
            wanderposition = homelocation + Extensions.RandomCircle * IdleWanderRange;
            locomotor.SetDestination(wanderposition);
            return;
        }

        if (targeter.GetScaredTargets.Count > 0)
        {
            state = FemtanylAIState.Retreating;
            wanderposition = homelocation + Extensions.RandomCircle * IdleWanderRange;
            locomotor.SetDestination(wanderposition);
            return;
        }

        //no more items... RUN!!!!
        if(!LefthandItem.IsValid && !RighthandItem.IsValid)
        {
            state = FemtanylAIState.Retreating;
        }

        locomotor.SetDestination(targeter.TopAggroTarget.transform.position );
        if (targeter.TopAggroTarget)
        {
            var dist = (transform.position - targeter.TopAggroTarget.transform.position).sqrMagnitude;
            if (dist < 25)
            {
                //bro idk figure it out
            }
            else { currThrowWindup = throwwindup; }
        }
    }

    private void GrabbingItemUpdate()
    {
        if (targeter.GetScaredTargets.Count > 0)
        {
            state = FemtanylAIState.Fleeing;
            return;
        }

        if (targeter.TopAggroTarget && (RighthandItem.IsValid || LefthandItem.IsValid))
        {
            state = FemtanylAIState.FightingTarget;
            return;
        }

        if (currItemTarget == null) { state = FemtanylAIState.Idle; wandercooldown = 0; return; }
        var dist = (transform.position - currItemTarget.transform.position).sqrMagnitude;
        if (dist < 1)
        {
            if (!RighthandItem.IsValid) { RighthandItem = currItemTarget.ConvertToItemData; }
            else if (!LefthandItem.IsValid) { LefthandItem = currItemTarget.ConvertToItemData; }
            else
            {
                state = FemtanylAIState.Idle;
                wandercooldown = 0;
                return;
            }
            currItemTarget.NetworkObject.Despawn(true);

            currItemTarget = null;
            state = FemtanylAIState.Idle;
            return;
        }
        else
        {
            locomotor.SetDestination(currItemTarget.transform.position);
        }
    }

    Vector3 GetFleeDirection(Vector3 selfPosition, out bool isSurrounded)
    {
        // Sort scared targets by fear (descending)
        var topScared = targeter.GetScaredTargets
            .Take(3)
            .ToList();

        Vector3 fleeVector = Vector3.zero;

        foreach (var target in topScared)
        {
            Vector3 directionAway = (selfPosition - target.transform.position).normalized;

            fleeVector += directionAway * targeter.GetFearFactor(target);
        }

        // Check magnitude to see if we're surrounded (low net direction vector)
        float fleeStrength = fleeVector.sqrMagnitude;
        isSurrounded = fleeStrength < 0.1f*0.1f; // tweak this threshold if needed

        return fleeStrength > 0f ? fleeVector.normalized : Vector3.zero;
    }

    private void FleeingUpdate()
    {
        if(targeter.GetScaredTargets.Count <= 0)
        {
            state = FemtanylAIState.Idle;
            wanderposition = homelocation + Extensions.RandomCircle * IdleWanderRange;
            locomotor.SetDestination(wanderposition);
            return;
        }

        var fleedirection = GetFleeDirection(transform.position, out bool isSurrounded);
        if (isSurrounded)
        {
            // If surrounded, just run in a random direction
            fleedirection = Extensions.RandomCircle;
        }
        locomotor.SetDestination(transform.position + fleedirection * 5); //runn!!!!
        //make us spam jump
    }
}