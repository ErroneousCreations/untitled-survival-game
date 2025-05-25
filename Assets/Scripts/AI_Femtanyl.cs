using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using DG.Tweening;

public class AI_Femtanyl : NetworkBehaviour
{
    public enum FemtanylAIState { Idle, Scouting, ReturningWithNews, Searching, FightingTarget, LostTarget, GrabbingItem, Retreating, Fleeing, Reacting }

    [Header("AI Settings")]
    public FemtanylAIState state;
    public AILocomotor locomotor;
    public AITargeter targeter;
    public float IdleWanderRange;
    public float ScoutRange, GetItemRange, FleeFearThreshold, SearchForTargetRange, ThrowRange = 5, ThrowVelocity;
    public Health health;
    public Collider coll;

    [Header("Visuals")]
    public Gradient SkinColourGradient;
    public Gradient ClothesColourGradient;
    public Transform model;
    public Renderer BodyRenderer, EyeRenderer;
    public Renderer[] HandRends;
    public Texture2D[] EyeTextures;
    public Transform Leftear, Rightear;

    [Header("Inventory")]
    public MeshFilter LefthandF;
    public MeshFilter RighthandF;
    public MeshRenderer LefthandR, RighthandR;
    public Transform LeftHand, RightHand;
    private NetworkVariable<ItemData> LefthandItem = new(), RighthandItem = new();
    private NetworkVariable<Vector3> LeftHandPosition = new(), RightHandPosition = new();
    private NetworkVariable<Vector3> LeftHandRotation = new(), RightHandRotation = new();
    private NetworkVariable<float> eyetex = new(), earrot = new();
    private float tookDamageTime;

    public ItemData GetLefthandItem => LefthandItem.Value;
    public ItemData GetRighthandItem => RighthandItem.Value;
    public bool GetIsScout => scout;
    public int GetID => ID; 

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

    public int GetFaceTexture
    {
        get
        {
            int face = 0;
            if(currBlinkTime <= 0.15f) { face = 1; }
            if(state == FemtanylAIState.Fleeing) { face = 1; }
            if(health.GetNormalisedHealth <= 0.1f) { face = 1; } //scared, close eyes
            if(tookDamageTime > 0) { face = 1; }
            if(health.GetStunned) { face = 1; } //stunned, eyes closed
            return face;
        }
    }

    public float GetEarRotation
    {
        get
        {
            float rot = 0;
            if (state == FemtanylAIState.Reacting) { rot = 20; }
            if (state == FemtanylAIState.Fleeing) { rot = 40; }
            if (tookDamageTime > 0) { rot = 30; }
            if (health.GetStunned) { rot = 30; } //stunned, lower ears
            if (health.GetNormalisedHealth <= 0.25f) { rot = 40; } //scared, lower ears
            return rot;
        }
    }

    //characteristics
    private float speedmod = 1, eyeblinktime = 2.5f, throwwindup = 2f, reactiontime = 0.5f, aggression = 1, fear = 1, inaccuracy, paintolerance;
    //other privates
    private FemtanylAIState nextState;
    private PickupableItem currItemTarget;
    private float scoutcooldown, checkItemCooldown, currThrowWindup, currBlinkTime, currReactionTime, currGiveUpTime, healingCooldown;
    private Vector3 homelocation, wanderposition, newsPosition, lookdirection, fightingOffset;
    private bool scout, scoutcheckedforitems;
    private AITarget currFightingTarget, currFleeingTarget;
    private Vector3 lastseenpos;
    private float losttargetcooldown = 0;
    private Material eyemat;
    private bool tooScaredToEngage;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        eyemat = EyeRenderer.material;
        if (IsOwner) {
            health.OnTakeDamage += (_, _, _) => OnTakeDamage();
            SyncIDRPC(ID);
            Random.InitState(ID);
            speedmod = Random.Range(0.96f, 1.05f);
            eyeblinktime = Random.Range(2f, 3f);
            throwwindup = Random.Range(0.9f, 1.4f);
            reactiontime = Random.Range(0.5f, 0.8f);
            aggression = Random.Range(0.8f, 1.2f);
            paintolerance = Random.Range(0.1f, 0.8f);
            fear = Random.Range(0.8f, 1.2f);
            inaccuracy = Random.Range(0.1f, 0.4f);
            targeter.FearModifier = fear;
            targeter.AggressionModifier = aggression;
            locomotor.maxSpeed *= speedmod;
            checkItemCooldown = Random.Range(1.5f, 3f);
            scoutcooldown = Random.Range(10f, 30f);
            fightingOffset = Extensions.RandomCircle * 2f; //offset for fighting target, so we dont just stand still and get hit
            currGiveUpTime = 30;
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
        eyemat.mainTexture = EyeTextures[(int)eyetex.Value];
        LefthandF.mesh = LefthandItem.Value.IsValid ? ItemDatabase.GetItem(LefthandItem.Value.ID.ToString()).HeldMesh : null;
        RighthandF.mesh = RighthandItem.Value.IsValid ? ItemDatabase.GetItem(RighthandItem.Value.ID.ToString()).HeldMesh : null;
        if(LefthandItem.Value.IsValid) { LefthandR.materials = ItemDatabase.GetItem(LefthandItem.Value.ID.ToString()).HeldMats; }
        if (RighthandItem.Value.IsValid) { RighthandR.materials = ItemDatabase.GetItem(RighthandItem.Value.ID.ToString()).HeldMats; }
        LeftHand.gameObject.SetActive(LefthandItem.Value.IsValid);
        RightHand.gameObject.SetActive(RighthandItem.Value.IsValid);
        Leftear.localRotation = Quaternion.Lerp(Leftear.localRotation, Quaternion.Euler(-earrot.Value, 0, 0), Time.deltaTime*5);
        Rightear.localRotation = Quaternion.Lerp(Rightear.localRotation, Quaternion.Euler(earrot.Value, 0, 0), Time.deltaTime * 5);

        if (IsOwner)
        {
            eyetex.Value = GetFaceTexture;
            currBlinkTime -= Time.deltaTime;
            earrot.Value = GetEarRotation;
            if (currBlinkTime <= 0) { currBlinkTime = eyeblinktime; }
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

    private float GetFearFromHealth => health.GetHealth < 0 ? 10 : Mathf.Lerp(3, 0, health.GetNormalisedHealth);

    private void OnTakeDamage()
    {
        tookDamageTime = 0.5f;
        if (health.GetNormalisedHealth < paintolerance) { state = FemtanylAIState.Retreating; tooScaredToEngage = true; }
        healingCooldown = 30;
    }

    private void Update()
    {
        if(!IsSpawned) { return; }
        UpdateSyncing();
        if (!IsOwner) { return; }
        locomotor.StandingUp = !health.GetStunned;
        targeter.FearModifier = fear + GetFearFromItems + GetFearFromHealth; //update the fear modifier based on if we are holding items (scaredy if we dont have any)
        targeter.AggressionModifier = aggression;
        if(tookDamageTime > 0) { tookDamageTime -= Time.deltaTime; }
        if(healingCooldown > 0) { healingCooldown -= Time.deltaTime; }
        else
        {
            health.Heal(health.MaxHealth*0.02f * Time.deltaTime); //heal 0.1% of health per second
        }
        if (health.GetStunned) { return; }
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
            case FemtanylAIState.Reacting:
                ReactingUpdate();
                break;
        }
    }

    private void ReactingUpdate()
    {
        currReactionTime -= Time.deltaTime;
        locomotor.Stop();
        locomotor.SetTurnDirection(lookdirection);
        if (currReactionTime <= 0) { locomotor.ResetTurnDirection(); state = nextState; }
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

        currGiveUpTime -= Time.deltaTime;
        if (currGiveUpTime <= 0)
        {
            wanderposition = homelocation + Extensions.RandomCircle * IdleWanderRange;
            locomotor.SetDestination(wanderposition);
            currGiveUpTime = 30;
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
                        currGiveUpTime = 30;
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
            state = FemtanylAIState.Reacting;
            nextState = FemtanylAIState.Fleeing;
            currReactionTime = reactiontime;
            lookdirection = (targeter.TopScaredTarget.transform.position - transform.position).normalized;
            currFleeingTarget = targeter.TopScaredTarget;
            losttargetcooldown = Random.Range(5f, 10f);
            return;
        }

        //check for offensive targets
        if (targeter.TopAggroTarget && (RighthandItem.Value.IsValid || LefthandItem.Value.IsValid))
        {
            state = FemtanylAIState.Reacting;
            nextState = FemtanylAIState.FightingTarget;
            currReactionTime = reactiontime;
            lookdirection = (targeter.TopAggroTarget.transform.position - transform.position).normalized;
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
            state = FemtanylAIState.Reacting;
            nextState = FemtanylAIState.ReturningWithNews;
            lookdirection = (targeter.TopAggroTarget.transform.position - transform.position).normalized;
            currReactionTime = reactiontime;
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
            state = FemtanylAIState.Reacting;
            currFleeingTarget = targeter.TopScaredTarget;
            nextState = FemtanylAIState.Fleeing;
            currReactionTime = reactiontime;
            lookdirection = (targeter.TopScaredTarget.transform.position - transform.position).normalized;
            return;
        }
    }

    private void RetreatingUpdate()
    {
        LeftHand.localPosition = Vector3.zero;
        RightHand.localPosition = Vector3.zero;
        LeftHand.localRotation = Quaternion.identity;
        RightHand.localRotation = Quaternion.identity;
        if (targeter.TopAggroTarget && (RighthandItem.Value.IsValid || LefthandItem.Value.IsValid) && !tooScaredToEngage)
        {
            state = FemtanylAIState.Reacting;
            currFightingTarget = targeter.TopAggroTarget;
            losttargetcooldown = Random.Range(5f, 10f);
            nextState = FemtanylAIState.FightingTarget;
            currReactionTime = reactiontime;
            lookdirection = (targeter.TopAggroTarget.transform.position - transform.position).normalized;
            return;
        }
        if (targeter.GetScaredTargets.Count > 0 && targeter.GetFearFactor(targeter.TopScaredTarget) > FleeFearThreshold)
        {
            state = FemtanylAIState.Reacting;
            currFleeingTarget = targeter.TopScaredTarget;
            nextState = FemtanylAIState.Fleeing;
            currReactionTime = reactiontime;
            lookdirection = (targeter.TopScaredTarget.transform.position - transform.position).normalized;
            return;
        }

        locomotor.SetDestination(homelocation);
        if ((transform.position - homelocation).sqrMagnitude < IdleWanderRange*IdleWanderRange)
        {
            tooScaredToEngage = false; //reset the too scared to engage flag
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
            if(tooScaredToEngage) { return; } //if we are too scared to engage, we dont look for items
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
                        currGiveUpTime = 30;
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
            state = FemtanylAIState.Reacting;
            currFightingTarget = targeter.TopAggroTarget;
            losttargetcooldown = Random.Range(5f, 10f);
            nextState = FemtanylAIState.FightingTarget;
            currReactionTime = reactiontime;
            lookdirection = (targeter.TopAggroTarget.transform.position - transform.position).normalized;
            return;
        }
        if (targeter.GetScaredTargets.Count > 0 && targeter.GetFearFactor(targeter.TopScaredTarget) > FleeFearThreshold)
        {
            state = FemtanylAIState.Reacting;
            currFleeingTarget = targeter.TopScaredTarget;
            losttargetcooldown = Random.Range(5f, 10f);
            nextState = FemtanylAIState.Fleeing;
            currReactionTime = reactiontime;
            lookdirection = (targeter.TopScaredTarget.transform.position - transform.position).normalized;
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
                        currGiveUpTime = 30;
                        state = FemtanylAIState.GrabbingItem;
                        currItemTarget = closest;
                        return;
                    }
                }
            }
        }
    }

    public bool GetEngagingPlayer => currFightingTarget && currFightingTarget.TargetType == "axolotl";

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
                locomotor.ResetTurnDirection();
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
            locomotor.ResetTurnDirection();
            return;
        }

        if (targeter.GetScaredTargets.Count > 0 && targeter.GetFearFactor(targeter.TopScaredTarget) > FleeFearThreshold*1.25f)
        {
            state = FemtanylAIState.Reacting;
            losttargetcooldown = Random.Range(5f, 10f);
            currFleeingTarget = targeter.TopScaredTarget;
            nextState = FemtanylAIState.Fleeing;
            currReactionTime = reactiontime;
            lookdirection = (targeter.TopScaredTarget.transform.position - transform.position).normalized;
            return;
        }

        bool visible = targeter.GetCanSee(currFightingTarget);
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
            locomotor.ResetTurnDirection();
            return;
        }
        var dir = (currFightingTarget.transform.position - transform.position);
        var dist = dir.sqrMagnitude;
        dir.Normalize();
        locomotor.SetTurnDirection(new Vector3(dir.x, 0, dir.z));
        locomotor.SetDestination(lastseenpos + (visible ? fightingOffset : Vector3.zero));
        if (dist < ThrowRange * ThrowRange && visible)
        {
            var hand = RighthandItem.Value.IsValid ? 0 : 1;
            var rootdist = Mathf.Sqrt(dist);
            currThrowWindup -= Time.deltaTime;
            if(currThrowWindup <= -throwwindup) { currThrowWindup = throwwindup*0.9f; }
            (hand == 0 ? RightHand : LeftHand).SetLocalPositionAndRotation(new Vector3(0, Mathf.Lerp(0.1f, 0, currThrowWindup / throwwindup), Mathf.Lerp(-0.5f, 0, currThrowWindup / throwwindup)), Quaternion.Lerp(Quaternion.Euler(90, 0, 0), Quaternion.identity, currThrowWindup / throwwindup));
            bool willfriendlyfire = Physics.Raycast(transform.position + Vector3.up + 0.45f * transform.forward, ((currFightingTarget.GetPosition + currFightingTarget.GetVelocity * 0.4f) - (transform.position + Vector3.up + 0.45f * transform.forward)).normalized, out RaycastHit hit, rootdist, Extensions.CreatureLayermask) && hit.collider.attachedRigidbody && hit.collider.attachedRigidbody.TryGetComponent(out AI_Femtanyl _);
            if (currThrowWindup <= 0 && !willfriendlyfire)
            {
                var id = (hand == 0 ? RighthandItem : LefthandItem).Value.ID.ToString();
                var isblunt = ItemDatabase.GetItem(id).ItemType == ItemTypeEnum.ThrowingBluntWeapon || id=="sharperrock" || id=="sharpstick"||id=="sharpshortstick";
                var aboveamount = (isblunt ? 3.5f : 1.6f) * (rootdist / 12) * Vector3.up;
                var forwardamount = (isblunt ? 0.86f : 0.25f) * (rootdist / 10) * currFightingTarget.GetVelocity;
                var targetpos = (currFightingTarget.GetPosition + forwardamount + aboveamount) + Random.insideUnitSphere * inaccuracy;
                var velocity = (isblunt ? 0.6f : 1) * ThrowVelocity * (targetpos - (transform.position + Vector3.up + 0.45f * transform.forward)).normalized;
                var go = Instantiate(ItemDatabase.GetItem((hand == 0 ? RighthandItem.Value : LefthandItem.Value).ID.ToString()).ItemPrefab, transform.position + Vector3.up + 0.45f * transform.forward, Quaternion.identity);
                go.NetworkObject.Spawn();
                go.transform.up = velocity.normalized;
                go.rb.linearVelocity = velocity;
                go.rb.angularVelocity = Vector3.zero;
                go.InitThrown(coll);
                go.InitSavedData((hand == 0 ? RighthandItem.Value : LefthandItem.Value).SavedData);
                (hand == 0 ? RighthandItem : LefthandItem).Value = ItemData.Empty;
                (hand == 0 ? RightHand : LeftHand).DOLocalMove(Vector3.zero, 0.25f);
                (hand == 0 ? RightHand : LeftHand).DOLocalRotate(Vector3.zero, 0.25f);
                currThrowWindup = throwwindup;
            }
        }
        else
        {
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
            state = FemtanylAIState.Reacting;
            currFightingTarget = targeter.TopAggroTarget;
            losttargetcooldown = Random.Range(5f, 10f);
            nextState = FemtanylAIState.FightingTarget;
            currReactionTime = reactiontime;
            lookdirection = (targeter.TopAggroTarget.transform.position - transform.position).normalized;
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
            state = FemtanylAIState.Reacting;
            nextState = FemtanylAIState.Fleeing;
            currReactionTime = reactiontime;
            lookdirection = (targeter.TopScaredTarget.transform.position - transform.position).normalized;
            currFleeingTarget = targeter.TopScaredTarget;
            losttargetcooldown = Random.Range(5f, 10f);
            return;
        }

        if (targeter.TopAggroTarget && (RighthandItem.Value.IsValid || LefthandItem.Value.IsValid))
        {
            state = FemtanylAIState.Reacting;
            currFightingTarget = targeter.TopAggroTarget;
            nextState = FemtanylAIState.FightingTarget;
            currReactionTime = reactiontime;
            lookdirection = (targeter.TopAggroTarget.transform.position - transform.position).normalized;
            return;
        }

        currGiveUpTime -= Time.deltaTime;
        if (currGiveUpTime <= 0)
        {
            state = FemtanylAIState.Retreating;
            return;
        }

        if (currItemTarget == null || !currItemTarget.IsSpawned) { state = FemtanylAIState.Retreating; return; }
        var dist = (transform.position - currItemTarget.transform.position).sqrMagnitude;
        if (dist < 1)
        {
            if (!RighthandItem.Value.IsValid) { RighthandItem.Value = currItemTarget.ConvertToItemData; }
            else if (!LefthandItem.Value.IsValid) { LefthandItem.Value = currItemTarget.ConvertToItemData; }
            else
            {
                state = FemtanylAIState.Retreating;
                return;
            }
            currItemTarget.NetworkObject.Despawn(true);

            currItemTarget = null;
            state = FemtanylAIState.Retreating;
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

        if (!targeter.GetScaredTargets.Contains(currFleeingTarget))
        {
            losttargetcooldown -= Time.deltaTime;
            if (losttargetcooldown<= 0)
            {
                state = FemtanylAIState.Reacting;
                nextState = FemtanylAIState.Retreating;
                currReactionTime = reactiontime;
                lookdirection = (currFleeingTarget.transform.position - transform.position).normalized;
                return;
            }
        }

        var dir = (currFleeingTarget.transform.position - transform.position);
        dir.y = 0;
        locomotor.SetDestination(transform.position - dir.normalized * 5); //runn!!!!
        //make us spam jump
    }
}