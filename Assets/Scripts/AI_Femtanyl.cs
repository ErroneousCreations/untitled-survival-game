using UnityEngine;
using Unity.Netcode;
using Unity.VisualScripting;
using System.Collections.Generic;
using DG.Tweening;

public class AI_Femtanyl : NetworkBehaviour
{
    public enum FemtanylAIState { Idle, Scouting, ReturningWithNews, Searching, FightingTarget, LostTarget, GrabbingItem, Retreating, Fleeing }

    [Header("AI Settings")]
    public FemtanylAIState state;
    public AILocomotor locomotor;
    public AITargeter targeter;
    public float IdleWanderRange;
    public float ScoutRange, GetItemRange, FleeFearThreshold, SearchForTargetRange, ThrowRange = 5, ThrowVelocity;

    [Header("Visuals")]
    public Gradient SkinColourGradient;
    public Gradient ClothesColourGradient;
    public Transform model;
    public Renderer BodyRenderer;
    public Renderer[] HandRends;

    [Header("Inventory")]
    public MeshFilter LefthandF;
    public MeshFilter RighthandF;
    public MeshRenderer LefthandR, RighthandR;
    public Transform LeftHand, RightHand;
    private NetworkVariable<ItemData> LefthandItem = new(), RighthandItem = new();
    private NetworkVariable<Vector3> LeftHandPosition = new(), RightHandPosition = new();
    private NetworkVariable<Vector3> LeftHandRotation = new(), RightHandRotation = new();

    public ItemData GetLefthandItem => LefthandItem.Value;
    public ItemData GetRighthandItem => RighthandItem.Value;

    public void SetLefthandItem(ItemData item)
    {
        if (IsOwner) { LefthandItem.Value = item; }
    }

    public void SetRighthandItem(ItemData item)
    {
        if (IsOwner) { RighthandItem.Value = item; }
    }

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
    private float scoutcooldown, checkItemCooldown, currThrowWindup;
    private Vector3 homelocation, wanderposition, newsPosition;
    private bool scout, scoutcheckedforitems;
    private AITarget currFightingTarget, currFleeingTarget;
    private Vector3 lastseenpos;
    private float losttargetcooldown = 0;

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
            scoutcooldown = Random.Range(10f, 30f);
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

    private void UpdateSyncing()
    {
        LefthandF.mesh = LefthandItem.Value.IsValid ? ItemDatabase.GetItem(LefthandItem.Value.ID.ToString()).HeldMesh : null;
        RighthandF.mesh = RighthandItem.Value.IsValid ? ItemDatabase.GetItem(RighthandItem.Value.ID.ToString()).HeldMesh : null;
        if(LefthandItem.Value.IsValid) { LefthandR.materials = ItemDatabase.GetItem(LefthandItem.Value.ID.ToString()).HeldMats; }
        if (RighthandItem.Value.IsValid) { RighthandR.materials = ItemDatabase.GetItem(RighthandItem.Value.ID.ToString()).HeldMats; }
        LeftHand.gameObject.SetActive(LefthandItem.Value.IsValid);
        RightHand.gameObject.SetActive(RighthandItem.Value.IsValid);

        if (IsOwner)
        {
            if (LeftHandPosition.Value != LeftHand.localPosition) { LeftHandPosition.Value = LeftHand.localPosition; }
            if (RightHandPosition.Value != RightHand.localPosition) { RightHandPosition.Value = RightHand.localPosition; }
            if (LeftHandRotation.Value != LeftHand.localEulerAngles) { LeftHandRotation.Value = LeftHand.localEulerAngles; }
            if (RightHandRotation.Value != RightHand.localEulerAngles) { RightHandRotation.Value = RightHand.localEulerAngles; }
        }
        else
        {
            LeftHand.localPosition = LeftHandPosition.Value;
            RightHand.localPosition = RightHandPosition.Value;
            LeftHand.localEulerAngles = LeftHandRotation.Value;
            RightHand.localEulerAngles = RightHandRotation.Value;
        }
    }

    private float GetFearFromItems
    {
        get
        {
            var amountofitems = 0 + (LefthandItem.Value.IsValid ? 1 : 0) + (RighthandItem.Value.IsValid ? 1 : 0);
            return amountofitems switch
            {
                0 => 2,
                1 => 0.5f,
                2 => 0f,
                _ => 0,
            };
        }
    }

    private void Update()
    {
        if(!IsSpawned) { return; }
        UpdateSyncing();
        if (!IsOwner) { return; }
        targeter.FearModifier = fear + GetFearFromItems; //update the fear modifier based on if we are holding items (scaredy if we dont have any)
        targeter.AggressionModifier = aggression;
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
            case FemtanylAIState.LostTarget:
                LostTargetUpdate();
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
        LeftHand.localPosition = Vector3.zero;
        RightHand.localPosition = Vector3.zero;
        LeftHand.localRotation = Quaternion.identity;
        RightHand.localRotation = Quaternion.identity;
        if ((locomotor.transform.position - wanderposition).sqrMagnitude < 0.5f)
        {
            locomotor.Stop();
            wanderposition = homelocation + Extensions.RandomCircle * IdleWanderRange;
            locomotor.SetDestination(wanderposition);
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
            if(!LefthandItem.Value.IsValid || !RighthandItem.Value.IsValid) {
                var obs = Physics.OverlapSphere(transform.position, GetItemRange, Extensions.ItemLayermask);
                if (obs.Length > 0)
                {
                    float dist = Mathf.Infinity;
                    PickupableItem closest = null;
                    foreach (var ob in obs)
                    {
                        var thisdist = (transform.position - ob.transform.position).sqrMagnitude;
                        if (ob.TryGetComponent(out PickupableItem item) && item.IsSpawned && (item.ItemType == ItemTypeEnum.ThrowingBluntWeapon || item.ItemType == ItemTypeEnum.ThrowingSharpWeapon) && thisdist < dist)
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
        if (targeter.GetScaredTargets.Count > 0 && targeter.GetFearFactor(targeter.TopScaredTarget) > FleeFearThreshold)
        {
            state = FemtanylAIState.Fleeing;
            currFleeingTarget = targeter.TopScaredTarget;
            losttargetcooldown = Random.Range(5f, 10f);
            return;
        }

        //check for offensive targets
        if (targeter.TopAggroTarget && (RighthandItem.Value.IsValid || LefthandItem.Value.IsValid))
        {
            state = FemtanylAIState.FightingTarget;
            currFightingTarget = targeter.TopAggroTarget;
            losttargetcooldown = Random.Range(5f, 10f);
            return;
        }
    }

    private void ScoutUpdate()
    {
        LeftHand.localPosition = Vector3.zero;
        RightHand.localPosition = Vector3.zero;
        LeftHand.localRotation = Quaternion.identity;
        RightHand.localRotation = Quaternion.identity;
        if (scoutcooldown > 0 && (transform.position - wanderposition).sqrMagnitude < 1) { 
            scoutcooldown -= Time.deltaTime;

            if (!scoutcheckedforitems)
            {
                scoutcheckedforitems = true;
                var obs = Physics.OverlapSphere(transform.position, GetItemRange*1.5f, Extensions.ItemLayermask);
                if (obs.Length > 3) {
                    scoutcheckedforitems = false;
                    newsPosition = transform.position;
                    state = FemtanylAIState.ReturningWithNews;
                    scoutcooldown = Random.Range(120f, 180f);
                    locomotor.SetDestination(homelocation);
                    return;
                }
            }
        }
        if (scoutcooldown <= 0)
        {
            scoutcheckedforitems = false;
            state = FemtanylAIState.ReturningWithNews;
            scoutcooldown = Random.Range(120f, 180f);
            locomotor.SetDestination(homelocation);
            return;
        }

        if (targeter.TopAggroTarget)
        {
            scoutcheckedforitems = false;
            state = FemtanylAIState.ReturningWithNews;
            newsPosition = targeter.TopAggroTarget.transform.position;
            scoutcooldown = Random.Range(120f, 180f);
            return;
        }

        if (targeter.GetScaredTargets.Count > 0 && targeter.GetFearFactor(targeter.TopScaredTarget) > FleeFearThreshold)
        {
            scoutcheckedforitems = false;
            scoutcooldown = Random.Range(120f, 180f);
            state = FemtanylAIState.Fleeing;
            currFleeingTarget = targeter.TopScaredTarget;
            return;
        }
    }

    private void ReturningWithNewsUpdate()
    {
        LeftHand.localPosition = Vector3.zero;
        RightHand.localPosition = Vector3.zero;
        LeftHand.localRotation = Quaternion.identity;
        RightHand.localRotation = Quaternion.identity;
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
                state = FemtanylAIState.Idle;
                newsPosition = homelocation; //reset the news position
            }
            return;
        }

        if (targeter.GetScaredTargets.Count > 0 && targeter.GetFearFactor(targeter.TopScaredTarget) > FleeFearThreshold)
        {
            state = FemtanylAIState.Fleeing;
            currFleeingTarget = targeter.TopScaredTarget;
            return;
        }
    }

    private void RetreatingUpdate()
    {
        LeftHand.localPosition = Vector3.zero;
        RightHand.localPosition = Vector3.zero;
        LeftHand.localRotation = Quaternion.identity;
        RightHand.localRotation = Quaternion.identity;
        if (targeter.TopAggroTarget && (RighthandItem.Value.IsValid || LefthandItem.Value.IsValid))
        {
            state = FemtanylAIState.FightingTarget;
            currFightingTarget = targeter.TopAggroTarget;
            losttargetcooldown = Random.Range(5f, 10f);
            return;
        }
        if (targeter.GetScaredTargets.Count > 0 && targeter.GetFearFactor(targeter.TopScaredTarget) > FleeFearThreshold)
        {
            state = FemtanylAIState.Fleeing;
            currFleeingTarget = targeter.TopScaredTarget;
            return;
        }

        locomotor.SetDestination(homelocation);
        if ((transform.position - homelocation).sqrMagnitude < IdleWanderRange*IdleWanderRange)
        {
            state = FemtanylAIState.Idle;
            wanderposition = homelocation + Extensions.RandomCircle * IdleWanderRange;
            locomotor.SetDestination(wanderposition);
            return;
        }

        //look for items to grab
        checkItemCooldown -= Time.deltaTime;
        if (checkItemCooldown <= 0)
        {
            checkItemCooldown = Random.Range(1.5f, 3f);
            //make sure we have a free hand
            if (!LefthandItem.Value.IsValid || !RighthandItem.Value.IsValid)
            {
                var obs = Physics.OverlapSphere(transform.position, GetItemRange, Extensions.ItemLayermask);
                if (obs.Length > 0)
                {
                    float nearestdist = Mathf.Infinity;
                    PickupableItem closest = null;
                    foreach (var ob in obs)
                    {
                        var thisdist = (transform.position - ob.transform.position).sqrMagnitude;
                        if (ob.TryGetComponent(out PickupableItem item) && item.IsSpawned && (item.ItemType == ItemTypeEnum.ThrowingBluntWeapon || item.ItemType == ItemTypeEnum.ThrowingSharpWeapon) && thisdist < nearestdist)
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

    private void SearchingUpdate()
    {
        LeftHand.localPosition = Vector3.zero;
        RightHand.localPosition = Vector3.zero;
        LeftHand.localRotation = Quaternion.identity;
        RightHand.localRotation = Quaternion.identity;
        if (targeter.TopAggroTarget && (RighthandItem.Value.IsValid || LefthandItem.Value.IsValid))
        {
            state = FemtanylAIState.FightingTarget;
            currFightingTarget = targeter.TopAggroTarget;
            losttargetcooldown = Random.Range(5f, 10f);
            return;
        }
        if (targeter.GetScaredTargets.Count > 0 && targeter.GetFearFactor(targeter.TopScaredTarget) > FleeFearThreshold)
        {
            state = FemtanylAIState.Fleeing;
            currFleeingTarget = targeter.TopScaredTarget;
            losttargetcooldown = Random.Range(5f, 10f);
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
            if (!LefthandItem.Value.IsValid || !RighthandItem.Value.IsValid)
            {
                var obs = Physics.OverlapSphere(transform.position, GetItemRange, Extensions.ItemLayermask);
                if (obs.Length > 0)
                {
                    float nearestdist = Mathf.Infinity;
                    PickupableItem closest = null;
                    foreach (var ob in obs)
                    {
                        var thisdist = (transform.position - ob.transform.position).sqrMagnitude;
                        if (ob.TryGetComponent(out PickupableItem item) && item.IsSpawned && (item.ItemType == ItemTypeEnum.ThrowingBluntWeapon || item.ItemType == ItemTypeEnum.ThrowingSharpWeapon) && thisdist < nearestdist)
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
        if (!currFightingTarget)
        {
            currFightingTarget = targeter.TopAggroTarget;
            if (!currFightingTarget)
            {
                state = FemtanylAIState.Retreating;
                return;
            }
            losttargetcooldown = Random.Range(5f, 10f);
        }

        //retarget if we can see a better target
        if (targeter.TopAggroTarget && targeter.TopAggroTarget != currFightingTarget)
        {
            var adist = (targeter.TopAggroTarget.transform.position - transform.position).sqrMagnitude;
            var currdist = (currFightingTarget.transform.position - transform.position).sqrMagnitude;
            var fear = targeter.GetAggroFactor(targeter.TopAggroTarget);
            var currfear = targeter.GetAggroFactor(currFightingTarget);
            if (fear - (adist * 0.1f) > currfear - (currdist * 0.1f))
            {
                currFightingTarget = targeter.TopAggroTarget;
                losttargetcooldown = Random.Range(5f, 10f);
            }
        }

        //no more items... RUN!!!!
        if (!LefthandItem.Value.IsValid && !RighthandItem.Value.IsValid)
        {
            state = FemtanylAIState.Retreating;
            return;
        }

        if (targeter.GetScaredTargets.Count > 0 && targeter.GetFearFactor(targeter.TopScaredTarget) > FleeFearThreshold*1.25f)
        {
            state = FemtanylAIState.Fleeing;
            losttargetcooldown = Random.Range(5f, 10f);
            currFleeingTarget = targeter.TopScaredTarget;
            return;
        }

        bool visible = targeter.GetAggroTargets.Contains(currFightingTarget);
        if (visible) //if we can see them
        {
            lastseenpos = currFightingTarget.transform.position;
        }
        else { losttargetcooldown -= Time.deltaTime; }

        if (losttargetcooldown <= 0)
        {
            state = FemtanylAIState.LostTarget;
            losttargetcooldown = Random.Range(10f, 15f);
            wanderposition = lastseenpos + Extensions.RandomCircle * SearchForTargetRange;
            locomotor.SetDestination(wanderposition);
            return;
        }

        locomotor.SetDestination(lastseenpos);
        var dist = (transform.position - currFightingTarget.transform.position).sqrMagnitude;
        if (dist < ThrowRange * ThrowRange && visible)
        {
            var hand = RighthandItem.Value.IsValid ? 0 : 1;
            currThrowWindup -= Time.deltaTime;
            (hand == 0 ? RightHand : LeftHand).SetLocalPositionAndRotation(new Vector3(0, Mathf.Lerp(0.1f, 0, currThrowWindup / throwwindup), Mathf.Lerp(-0.5f, 0, currThrowWindup / throwwindup)), Quaternion.Lerp(Quaternion.Euler(90, 0, 0), Quaternion.identity, currThrowWindup / throwwindup));
            if (currThrowWindup <= 0)
            {
                var velocity = ((currFightingTarget.CentreOffset) - (transform.position + Vector3.up + 0.45f * transform.forward)).normalized * ThrowVelocity;
                var go = Instantiate(ItemDatabase.GetItem((hand == 0 ? RighthandItem.Value : LefthandItem.Value).ID.ToString()).ItemPrefab, transform.position + Vector3.up + 0.45f * transform.forward, Quaternion.identity);
                go.NetworkObject.Spawn();
                go.transform.up = velocity.normalized;
                go.rb.linearVelocity = velocity;
                go.rb.angularVelocity = Vector3.zero;
                go.InitThrown();
                go.InitSavedData((hand == 0 ? RighthandItem.Value : LefthandItem.Value).SavedData);
                (hand == 0 ? RighthandItem : LefthandItem).Value = ItemData.Empty;
                (hand == 0 ? RightHand : LeftHand).DOLocalMove(Vector3.zero, 0.25f);
                currThrowWindup = throwwindup;
            }
        }
        else {
            currThrowWindup = throwwindup;
            LeftHand.localPosition = Vector3.zero;
            RightHand.localPosition = Vector3.zero;
        }
    }

    private void LostTargetUpdate()
    {
        LeftHand.localPosition = Vector3.zero;
        RightHand.localPosition = Vector3.zero;
        LeftHand.localRotation = Quaternion.identity;
        RightHand.localRotation = Quaternion.identity;
        if ((wanderposition - transform.position).sqrMagnitude <= 1)
        {
            wanderposition = lastseenpos + Extensions.RandomCircle * SearchForTargetRange;
        }

        losttargetcooldown -= Time.deltaTime;

        //give up
        if (losttargetcooldown <= 0)
        {
            state = FemtanylAIState.Retreating;
            wanderposition = homelocation + Extensions.RandomCircle * IdleWanderRange;
            locomotor.SetDestination(wanderposition);
            return;
        }

        if (targeter.TopAggroTarget && (RighthandItem.Value.IsValid || LefthandItem.Value.IsValid))
        {
            state = FemtanylAIState.FightingTarget;
            currFightingTarget = targeter.TopAggroTarget;
            losttargetcooldown = Random.Range(5f, 10f);
            return;
        }

        //look for items to grab
        checkItemCooldown -= Time.deltaTime;
        if (checkItemCooldown <= 0)
        {
            checkItemCooldown = Random.Range(1.5f, 3f);
            //make sure we have a free hand
            if (!LefthandItem.Value.IsValid || !RighthandItem.Value.IsValid)
            {
                var obs = Physics.OverlapSphere(transform.position, GetItemRange, Extensions.ItemLayermask);
                if (obs.Length > 0)
                {
                    float nearestdist = Mathf.Infinity;
                    PickupableItem closest = null;
                    foreach (var ob in obs)
                    {
                        var thisdist = (transform.position - ob.transform.position).sqrMagnitude;
                        if (ob.TryGetComponent(out PickupableItem item) && item.IsSpawned && (item.ItemType == ItemTypeEnum.ThrowingBluntWeapon || item.ItemType == ItemTypeEnum.ThrowingSharpWeapon) && thisdist < nearestdist)
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

    private void GrabbingItemUpdate()
    {
        LeftHand.localPosition = Vector3.zero;
        RightHand.localPosition = Vector3.zero;
        LeftHand.localRotation = Quaternion.identity;
        RightHand.localRotation = Quaternion.identity;
        if (targeter.GetScaredTargets.Count > 0 && targeter.GetFearFactor(targeter.TopScaredTarget) > FleeFearThreshold)
        {
            state = FemtanylAIState.Fleeing;
            currFleeingTarget = targeter.TopScaredTarget;
            losttargetcooldown = Random.Range(5f, 10f);
            return;
        }

        if (targeter.TopAggroTarget && (RighthandItem.Value.IsValid || LefthandItem.Value.IsValid))
        {
            state = FemtanylAIState.FightingTarget;
            currFightingTarget = targeter.TopAggroTarget;
            return;
        }

        if (currItemTarget == null) { state = FemtanylAIState.Retreating; return; }
        var dist = (transform.position - currItemTarget.transform.position).sqrMagnitude;
        if (dist < 1)
        {
            if (!RighthandItem.Value.IsValid) { RighthandItem.Value = currItemTarget.ConvertToItemData; }
            else if (!LefthandItem.Value.IsValid) { LefthandItem.Value = currItemTarget.ConvertToItemData; }
            else
            {
                state = FemtanylAIState.Idle;
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

    private void FleeingUpdate()
    {
        LeftHand.localPosition = Vector3.zero;
        RightHand.localPosition = Vector3.zero;
        LeftHand.localRotation = Quaternion.identity;
        RightHand.localRotation = Quaternion.identity;
        if (currFleeingTarget == null)
        {
            currFleeingTarget = targeter.TopScaredTarget;
            if (!currFleeingTarget)
            {
                state = FemtanylAIState.Retreating;
                wanderposition = homelocation + Extensions.RandomCircle * IdleWanderRange;
                locomotor.SetDestination(wanderposition);
                return;
            }
            losttargetcooldown = Random.Range(5f, 10f);
        }

        if (targeter.TopScaredTarget!=null && targeter.TopScaredTarget != currFleeingTarget)
        {
            var dist = (targeter.TopScaredTarget.transform.position - transform.position).sqrMagnitude;
            var currdist = (currFleeingTarget.transform.position - transform.position).sqrMagnitude;
            var fear = targeter.GetFearFactor(targeter.TopScaredTarget);
            var currfear = targeter.GetFearFactor(currFleeingTarget);
            if (fear - (dist*0.1f) > currfear - (currdist*0.1f))
            {
                currFleeingTarget = targeter.TopScaredTarget;
                losttargetcooldown = Random.Range(5f, 10f);
            }
        }

        if (targeter.GetScaredTargets.Count <= 0)
        {
            losttargetcooldown -= Time.deltaTime;
            if (losttargetcooldown<= 0)
            {
                state = FemtanylAIState.Retreating;
                wanderposition = homelocation + Extensions.RandomCircle * IdleWanderRange;
                locomotor.SetDestination(wanderposition);
                return;
            }
        }

        var dir = (currFleeingTarget.transform.position - transform.position);
        dir.y = 0;
        locomotor.SetDestination(transform.position - dir.normalized * 5); //runn!!!!
        //make us spam jump
    }
}