using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
public enum DamageType { Blunt, Stab }
public enum BodyPart { Head, Body, Arm, Leg }

[System.Serializable]
public struct Wound
{
    public float severity;
    public bool isEmbeddedObject;

    public Wound(float damage, bool embedded)
    {
        severity = damage;
        isEmbeddedObject = embedded;
    }
}

public struct WoundObject
{
    public ParticleSystem particles;
    public ItemData embedded;
    public bool hasembedded;
    public float severity;
    public Vector3 originalhitpos;

    public WoundObject(ParticleSystem particles, ItemData embedded, bool hasembedded, Vector3 hitpos, float severity)
    {
        this.particles = particles;
        this.embedded = embedded;
        this.hasembedded = hasembedded;
        originalhitpos = hitpos;
        this.severity = severity;
    }

    public override string ToString()
    {
        return $"{System.Math.Round(originalhitpos.x, 2)}~{System.Math.Round(originalhitpos.y)}~{System.Math.Round(originalhitpos.z)}~{System.Math.Round(severity,2)}~{(hasembedded ? 1 : 0)}~{embedded}~{System.Math.Round(particles.transform.forward.x, 2)}~{System.Math.Round(particles.transform.forward.y, 2)}~{System.Math.Round(particles.transform.forward.z, 2)}";
    }
}

public class PlayerHealthController : NetworkBehaviour
{
    public NetworkVariable<float> currentBlood = new(0, writePerm: NetworkVariableWritePermission.Owner);
    public NetworkVariable<float> shock = new(0, writePerm: NetworkVariableWritePermission.Owner);
    public NetworkVariable<float> consciousness = new(0, writePerm: NetworkVariableWritePermission.Owner);
    public NetworkVariable<bool> heartBeating = new(true, writePerm: NetworkVariableWritePermission.Owner);
    public NetworkVariable<bool> breathing = new (true, writePerm: NetworkVariableWritePermission.Owner);
    public NetworkVariable<bool> isConscious = new(true, writePerm: NetworkVariableWritePermission.Owner);
    public NetworkVariable<bool> isAlive = new(true, writePerm: NetworkVariableWritePermission.Owner);
    public NetworkVariable<float> legHealth = new(0, writePerm: NetworkVariableWritePermission.Owner), bodyHealth = new(0, writePerm: NetworkVariableWritePermission.Owner), headHealth = new(0, writePerm: NetworkVariableWritePermission.Owner);
    public NetworkVariable<float> hunger = new(0, writePerm: NetworkVariableWritePermission.Owner);

    private Dictionary<ushort, Wound> wounds = new();
    private Dictionary<ushort, WoundObject> woundObjects = new();

    public Player player;

    [Header("Vitals Tuning")]
    public float HungerDepleteTime = 600f;
    public float HungerMax = 1.25f;
    public float bleedRatePerWound = 0.001f;
    public float shockIncreasePerWound = 0.01f, instantShockIncrease = 0.001f;
    public float shockReduceRate = 0.02f;
    public float consciousnessRegenRate = 0.01f;
    public float bloodRegenRate = 0.003f;
    public float legHealthRegenRate, bodyHealthRegenRate, headHealthRegenRate;
    public float baseBleedParticleAmount = 25;
    public float maxShockPerSeverity = 35f;

    [Header("Effects to other parts")]
    public AnimationCurve LeghealthMovementMult;
    public AnimationCurve ConsciousnessMovementMult, ConsciousnessMouseTrippiness, ConsciousnessHeadbobMult, LeghealthHeadbobMult, LeghealthHeadbobSpeedMult, ConsciousnessStandDampeningMult, HungerStandDampeningMult, HungerMovementMult, HungerMouseTrippinessMult, HungerBlurriness;

    [Header("Body parts")]
    public Vector3 headPos;
    public float headRadius;
    public Vector3 legsPos;
    public float legsRadius;
    public Vector3 heartLocalPos;
    public float heartRadius;

    [Header("FX")]
    public ParticleSystem bleedingEffect;
    public PlayerCorpseProxy corpse;

    [Header("Interactors")]
    public List<Interactible> interactibles;
    public Interactible HealYourselfInteractor;

    [Header("Colliders")]
    public List<Collider> bodyColliders;

    private float leghealthRegenCd, headhealthRegenCd, bodyHealthRegenCd;
    private float currOxygen = 0, fullyDieTime, stopBreathignTime;
    private const float CPR_RATE_REQ = 0.576923077f, MAXIMUM_ALLOWED_DISTANCE = 0.2f;
    private float lastCPRTime = 0f, lastMouthMouthTime = 0f;
    private float combinedcprPoints = 0;
    private float cprPoints, cprFailPoints, mmPoints, mmFailPoints;
    private float recuscitationCooldown = 0;
    private ushort nextWoundID = 0;
    private float lastBleedAmount = 0;
    private float recentDamageCd = 0;
    private float shockTimer = 0;

    public bool GetCanSprint => hunger.Value > 0.2f && consciousness.Value > 0.25f && legHealth.Value > 0.5f && isConscious.Value;
    public float GetHunger => hunger.Value;
    public float GetMovespeedMult => LeghealthMovementMult.Evaluate(legHealth.Value) * ConsciousnessMovementMult.Evaluate(consciousness.Value) * HungerMovementMult.Evaluate(hunger.Value);
    public float MouseTrippyness => Mathf.Max(ConsciousnessMouseTrippiness.Evaluate(consciousness.Value), HungerMouseTrippinessMult.Evaluate(hunger.Value));
    public float GetHeadbobMult => ConsciousnessHeadbobMult.Evaluate(consciousness.Value) * LeghealthHeadbobMult.Evaluate(legHealth.Value);
    public float GetHeadbobSpeedMult =>  LeghealthHeadbobSpeedMult.Evaluate(legHealth.Value);
    public bool GetForceCrouch => consciousness.Value <= 0.3f || legHealth.Value <= 0.1f || hunger.Value <= 0.2f;
    public float GetWallrunDropChance => (1f-consciousness.Value+0.3f)*0.1f * (1f - hunger.Value + 0.3f) * 0.1f;
    public float GetStandDampeningMult => ConsciousnessStandDampeningMult.Evaluate(consciousness.Value) * HungerStandDampeningMult.Evaluate(hunger.Value);
    public bool GetCanStand => isConscious.Value;
    public float GetBleedSpeed => lastBleedAmount;
    public bool GetRecentDamage => recentDamageCd > 0;
    public float GetBlurriness => HungerBlurriness.Evaluate(hunger.Value);

    public string GetSavedWounds
    {
        get
        {
            if(woundObjects.Count <= 0) { return "null"; }
            string str = "";
            foreach (var item in woundObjects)
            {
                str += item.Value.ToString() + "|";
            }
            return str[..^1];
        }
    }

    private ushort GetMostSevereWound
    {
        get
        {
            if (wounds == null || wounds.Count == 0)
                return 0;

            float maxSeverity = float.MinValue;
            ushort mostSevereKey = 0;
            bool found = false;

            foreach (var pair in wounds)
            {
                if (pair.Value.severity > maxSeverity)
                {
                    maxSeverity = pair.Value.severity;
                    mostSevereKey = pair.Key;
                    found = true;
                }
            }

            return found ? mostSevereKey : (ushort)0;
        }
    }

    public void ApplySavedWounds(string saved)
    {
        if (saved == "null") { return; }
        var split = saved.Split('|');
        foreach (var item in split)
        {
            var wound = item.Split('~');
            var pos = new Vector3(float.Parse(wound[0]), float.Parse(wound[1]), float.Parse(wound[2]));
            var severity = float.Parse(wound[3]);
            var embedded = wound[4] == "1";
            var embeddeditem = new ItemData(wound[5]);
            SpawnWoundInteractorRPC(nextWoundID, pos, Vector3.up, severity, embedded, ItemDatabase.GetItem(embeddeditem.ID.ToString()).Name, embeddeditem);
            nextWoundID++;
        }
    }

    public void AddHunger(float amount)
    {
        hunger.Value = Mathf.Clamp(hunger.Value + amount, 0, HungerMax);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            currentBlood.Value = 1;
            heartBeating.Value = true;
            breathing.Value = true;
            isAlive.Value = true;
            isConscious.Value = true;
            shock.Value = 0f;
            consciousness.Value = 1f;
            legHealth.Value = 1f;
            currOxygen = 1;
            bodyHealth.Value = 1f;
            headHealth.Value = 1f;
            hunger.Value = 1;
        }
    }

    void Update()
    {
        List<ushort> removing = new();
        foreach (var item in woundObjects)
        {
            if (!item.Value.particles) { removing.Add(item.Key); }
        }
        foreach (var item in removing)
        {
            woundObjects.Remove(item);
        }

        foreach (var inter in interactibles)
        {
            inter.Banned = IsOwner;
        }

        if (recuscitationCooldown > 0) { 
            recuscitationCooldown -= Time.deltaTime;
            if(recuscitationCooldown <= 0)
            {
                mmPoints = 0;
                mmFailPoints = 0;
                cprPoints = 0;
                cprFailPoints = 0;
                lastCPRTime = -1;
                lastMouthMouthTime = -1;
                combinedcprPoints = 0;
            }
        }
        if(combinedcprPoints > 0) { combinedcprPoints -= Time.deltaTime; }

        //stop bleeding when out of blood
        if(currentBlood.Value <= 0 && woundObjects.Count>0) {
            var keys = woundObjects.Keys;
            foreach (var key in keys)
            {
                var ob = woundObjects[key];
                ob.severity = 0;
                woundObjects[key] = ob;
            }
        }
        if (!IsOwner) { HealYourselfInteractor.Banned = true; return; }

        if (wounds.Count > 0 && GameManager.GetGameMode == GameModeEnum.Deathmatch)
        {
            var targ = GetMostSevereWound;
            if (wounds[targ].isEmbeddedObject)
            {
                HealYourselfInteractor.Banned = false;
                HealYourselfInteractor.Description = $"Remove {ItemDatabase.GetItem(woundObjects[targ].embedded.ID.ToString()).Name}";
                HealYourselfInteractor.InteractLength = 2f;
                HealYourselfInteractor.InteractDistance = 1.5f;
                HealYourselfInteractor.OnInteractedLocal = () => { RemoveEmbeddedObject(targ, woundObjects[targ].embedded); };
            }
            else
            {
                HealYourselfInteractor.Banned = PlayerInventory.GetRightHandItem.ID != "bandage";
                HealYourselfInteractor.InteractLength = 2f;
                HealYourselfInteractor.InteractDistance = 1.5f;
                if (PlayerInventory.GetRightHandItem.ID == "bandage")
                {
                    HealYourselfInteractor.Description = $"Bandage Wound ({PlayerInventory.GetRightHandItem.SavedData[0]})";
                    HealYourselfInteractor.OnInteractedLocal = () =>
                    {
                        var currdurab = int.Parse(PlayerInventory.GetRightHandSaveData[0].ToString()) - 1;
                        var durab = currdurab;
                        if (durab <= 0) { PlayerInventory.DeleteRighthandItem(); }
                        else { PlayerInventory.UpdateRightHandSaveData(new() { durab.ToString() }); }
                        Bandage(targ);
                    };
                }
                else
                {
                    HealYourselfInteractor.Description = "";
                    HealYourselfInteractor.OnInteractedLocal = null;
                }
            }
        }
        else { HealYourselfInteractor.Banned = true; }

        hunger.Value = Mathf.Clamp(hunger.Value - (Time.deltaTime / HungerDepleteTime), 0, HungerMax);
        if (hunger.Value <= 0) { Die("Hunger"); return; }

        if (recentDamageCd > 0) { recentDamageCd -= Time.deltaTime; } 
        HandleWounds();
        HandleVitals();
    }

    void HandleWounds()
    {
        if(currentBlood.Value <= 0) { return; }
        float totalBleed = 0f;

        foreach (var wound in wounds)
        {
            totalBleed += bleedRatePerWound * wound.Value.severity;
        }
        lastBleedAmount = totalBleed;

        currentBlood.Value -= totalBleed * Time.deltaTime;
        if (lastBleedAmount > 0) { shockTimer += shockTimer >= (lastBleedAmount / bleedRatePerWound) * maxShockPerSeverity ? 0 : Time.deltaTime * (lastBleedAmount / (bleedRatePerWound*1.25f)); }
        else if(shockTimer>0) { shockTimer -= Time.deltaTime * 1.5f; }
        float shockIncreaseMult = Mathf.Pow(1.02f, shockTimer) - 1;
        shock.Value += shockIncreasePerWound * shockIncreaseMult * Time.deltaTime;
    }

    void HandleVitals()
    {
        if (!isAlive.Value) { return; }

        if(wounds.Count > 254)
        {
            Die("Death by a thousand cuts");
            return;
        }

        if (currentBlood.Value <= 0.1f)
        {
            Die("Blood loss");
            return;
        }

        if(currentBlood.Value <= 0.75f)
        {
            consciousness.Value -= ((1-(currentBlood.Value + 0.25f)) / 1) * 0.5f * Time.deltaTime;
            if (consciousness.Value < -0.9f) { consciousness.Value = -0.9f; } //to prevent going too low
        }

        if (shock.Value >= 1f)
        {
            StopHeart("Shock overload");
            return;
        }

        if (consciousness.Value <= 0)
        {
            isConscious.Value = false;
        }

        if (consciousness.Value >= 0.2f && !isConscious.Value) { 
            isConscious.Value = true;
        }

        if (bodyHealth.Value <= -0.9f) { Die("Fatal bodily trauma"); }
        if(headHealth.Value <= -0.25f) { Die("Fatal head trauma"); }

        if (!isConscious.Value)
        {
            if (stopBreathignTime <= 0)
            {
                breathing.Value = false;
                return;
            }
            else { stopBreathignTime -= Time.deltaTime / 60; }
        }
        else { stopBreathignTime = 1; }

        if (!breathing.Value)
        {
            if(currOxygen <= 0 && heartBeating.Value)
            {
                StopHeart("Oxygen supply lost");
                return;
            }
            else { currOxygen -= Time.deltaTime / 45; }
        }
        else { currOxygen = Mathf.Min(1, currOxygen + Time.deltaTime / 10); }

        if (!heartBeating.Value)
        {
            if (fullyDieTime <= 0)
            {
                Die("Cardiac Arrest");
                return;
            }
            else { fullyDieTime -= Time.deltaTime / 45; }
        }
        else { fullyDieTime = 1; }

        // Passive recovery
        if (heartBeating.Value)
        {
            if (leghealthRegenCd > 0) { leghealthRegenCd -= Time.deltaTime; }
            legHealth.Value = Mathf.Min(1, legHealth.Value + (leghealthRegenCd <= 0 && lastBleedAmount <= bleedRatePerWound ? (Time.deltaTime/legHealthRegenRate)*hunger.Value : 0));
            if (legHealth.Value < -1) { legHealth.Value = -1; }

            if (bodyHealthRegenCd > 0) { bodyHealthRegenCd -= Time.deltaTime; }
            bodyHealth.Value = Mathf.Min(1, bodyHealth.Value + (bodyHealthRegenCd <= 0 && lastBleedAmount <= bleedRatePerWound ? (Time.deltaTime/bodyHealthRegenRate)*hunger.Value : 0));
            if (bodyHealth.Value < -1) { bodyHealth.Value = -1; }

            if (headhealthRegenCd > 0) { headhealthRegenCd -= Time.deltaTime; }
            headHealth.Value = Mathf.Min(1, headHealth.Value + (headhealthRegenCd <= 0 && lastBleedAmount <= bleedRatePerWound ? (Time.deltaTime / headHealthRegenRate) * hunger.Value : 0));
            if (headHealth.Value < -1) { headHealth.Value = -1; }

            shock.Value = Mathf.Max(0, shock.Value - ((Time.deltaTime/shockReduceRate) / (wounds.Count+1)));
            currentBlood.Value = Mathf.Min(1, currentBlood.Value + (Time.deltaTime / bloodRegenRate) / (wounds.Count + 1));
            if(consciousness.Value < -0.6f) { consciousness.Value = -0.6f; } //to prevent going too low
            consciousness.Value = Mathf.Min(Mathf.Min(headHealth.Value+0.2f, 1), consciousness.Value + ((Time.deltaTime / consciousnessRegenRate) * currentBlood.Value));
        }
    }

    private Transform GetClosestCollider(Vector3 startpos, out Vector3 newPos)
    {
        float closestDist = Mathf.Infinity;
        Transform closest = null;
        newPos = startpos;

        foreach(var coll in bodyColliders)
        {
            var closestpos = coll.ClosestPoint(startpos);
            float dist = (startpos - closestpos).sqrMagnitude;
            if (dist < closestDist)
            {
                closest = coll.transform;
                closestDist = dist;
                newPos = closestpos;
            }
        }
        return closest;
    }

    public void ApplyDamage(float damage, DamageType type, Vector3 pos, Vector3 normal, bool embeddedob = false, string embeededobname = "", ItemData embeddeditem = default)
    {
        DamagedRPC(damage, type, pos, normal, embeddedob, embeededobname, embeddeditem);
    }

    [Rpc(SendTo.Owner, RequireOwnership = false)]
    private void DamagedRPC(float damage, DamageType type, Vector3 pos, Vector3 normal, bool embeddedob = false, string embeededobname = "", ItemData embeddeditem = default)
    {
        if (damage > 1000) { Die("Lethal damage"); return; }
        Debug.Log("dmg: " + damage);

        MusicManager.AddThreatLevel(damage*1.35f);
        bool inlegs = false;

        if ((pos - transform.TransformPoint(new Vector3(heartLocalPos.x, heartLocalPos.y * Player.LocalPlayer.pm.GetCrouchHeightMult, heartLocalPos.z))).sqrMagnitude < heartRadius * heartRadius && type == DamageType.Stab && damage >= 40)
        {
            Die("Heart pierced");
            return;
        }

        bool wasinHead = false;
        if ((pos - transform.TransformPoint(new Vector3(headPos.x, headPos.y * Player.LocalPlayer.pm.GetCrouchHeightMult, headPos.z))).sqrMagnitude < headRadius*headRadius)
        {
            wasinHead = true;
            consciousness.Value -= damage / 45f;
            headHealth.Value -= (damage / 70f) * (embeddedob ? 1.8f : 1);
            headhealthRegenCd = 30;
        }
        else if ((pos - transform.TransformPoint(new Vector3(legsPos.x, legsPos.y * Player.LocalPlayer.pm.GetCrouchHeightMult, legsPos.z))).sqrMagnitude < legsRadius * legsRadius)
        {
            inlegs = true;
            legHealth.Value -= damage / 100; 
            leghealthRegenCd = 30;
        }
        else
        {
            bodyHealth.Value -= damage / 150;
            bodyHealthRegenCd = 30;
        }

        player.MouseJitterIntensity = damage * 0.23f;

        if(damage > (type == DamageType.Stab ? 55 : 65) * (inlegs ? 0.8f : 1) && Random.value <= 0.8f)
        {
            player.KnockOver(damage * 0.08f * (inlegs ? 1.3f : 1));
        }

        recentDamageCd = 0.5f;

        shock.Value += damage * instantShockIncrease * (wasinHead ? 3 : 1) * (type == DamageType.Blunt?0.5f:1f);
        consciousness.Value -= damage * 0.003f;

        if (type != DamageType.Stab) { return; }

        var wound = new Wound(damage/20f * (embeddedob?0.25f:1f) * (wasinHead ? 3 : 1), embeddedob);
        var id = nextWoundID++;
        wounds.Add(id, wound);
        SpawnWoundInteractorRPC(id, pos, normal, wound.severity, embeddedob, embeededobname, embeddeditem);
    }

    [Rpc(SendTo.Everyone)]
    private void SpawnWoundInteractorRPC(ushort id, Vector3 pos, Vector3 normal, float intensity, bool embedded, string embeddedname, ItemData embeddedItem)
    {
                //create the wound interactor
        var bleed = Instantiate(bleedingEffect, Vector3.zero, Quaternion.identity);
        woundObjects.Add(id, new WoundObject(bleed, embeddedItem, embedded, pos, intensity));
        var ob = new GameObject("Wound" + wounds.Count);
        var coll = GetClosestCollider(pos, out Vector3 newPos);
        ob.transform.parent = coll;
        ob.transform.SetPositionAndRotation(newPos, Quaternion.LookRotation(normal));
        GameObject embeddedgraphic = null;
        if(embedded)
        {
            embeddedgraphic = new GameObject("EmbeddedGraphic");
            embeddedgraphic.transform.parent = ob.transform;
            embeddedgraphic.transform.localPosition = Vector3.zero;
            embeddedgraphic.transform.up = -normal;
            embeddedgraphic.AddComponent<MeshFilter>().mesh = ItemDatabase.GetItem(embeddedItem.ID.ToString()).HeldMesh;
            embeddedgraphic.AddComponent<MeshRenderer>().materials = ItemDatabase.GetItem(embeddedItem.ID.ToString()).HeldMats;
        }
        bleed.transform.parent = ob.transform;
        bleed.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        var initialpos = ob.transform.localPosition;

        var inter = ob.AddComponent<Interactible>();
        inter.OnUpdate += () =>
        {
            //todo make it change depending on what your holding and stuff
            inter.Banned = IsOwner || !Player.LocalPlayer || ((!woundObjects.ContainsKey(id) || !woundObjects[id].hasembedded) && PlayerInventory.GetRightHandItem.ID.ToString() != "bandage");
            if (!Player.LocalPlayer) { return; }
            if (!woundObjects.ContainsKey(id)) { Destroy(ob); return; }
            inter.Description = woundObjects[id].hasembedded ? $"Remove {embeddedname}" : (PlayerInventory.GetRightHandItem.ID.ToString()=="bandage" ? $"Bandage Wound ({PlayerInventory.GetRightHandItem.SavedData[0]})" : "");
            inter.InteractLength = 2f;
            inter.InteractDistance = 1.5f;
            if(embeddedgraphic && !woundObjects[id].hasembedded) { 
                Destroy(embeddedgraphic);
            }
            var emission = bleed.emission;
            emission.rateOverTime = baseBleedParticleAmount * woundObjects[id].severity;
            if (woundObjects[id].hasembedded) { inter.OnInteractedLocal = () => RemoveEmbeddedObject(id, embeddedItem); }
            else if (PlayerInventory.GetRightHandItem.ID.ToString() == "bandage") { inter.OnInteractedLocal = () => { 
                var currdurab = int.Parse(PlayerInventory.GetRightHandSaveData[0].ToString()) - 1;
                if(currdurab <= 0) { PlayerInventory.DeleteRighthandItem(); }
                else { PlayerInventory.UpdateRightHandSaveData(new() { currdurab.ToString() }); }
                Bandage(id); 
            }; }
            else { inter.OnInteractedLocal = null; }
        };
    }

    [Rpc(SendTo.Server)]
    private void SpawnEmbeddedWoundObjectRPC(ItemData item, Vector3 position)
    {
        // Spawn logic should be synced
        var go = Instantiate(ItemDatabase.GetItem(item.ID.ToString()).ItemPrefab, position, Quaternion.identity);
        go.NetworkObject.Spawn();
        go.InitSavedData(item.SavedData);
    }

    public void CheckPulse()
    {
        UIManager.ShowVitalsIndication(heartBeating.Value);
    }

    public void CheckBreathing()
    {
        UIManager.ShowVitalsIndication(breathing.Value);
    }

    public void Bandage(ushort wound)
    {
        BandageRPC(wound);
    }

    [Rpc(SendTo.Owner, RequireOwnership = false)]
    private void BandageRPC(ushort wound)
    {
        wounds.Remove(wound);
        RemoveWoundIntensityRPC(wound);
    }

    [Rpc(SendTo.Everyone)]
    private void RemoveWoundIntensityRPC(ushort wound)
    {
        woundObjects.Remove(wound);
    }

    public void RemoveEmbeddedObject(ushort wound, ItemData embeddedItem)
    {
        RemoveEmbeddedObjectRPC(wound);
        SpawnEmbeddedWoundObjectRPC(embeddedItem, Player.GetLocalPlayerCentre + Player.LocalPlayer.transform.forward);
    }

    [Rpc(SendTo.Owner, RequireOwnership = false)]
    private void RemoveEmbeddedObjectRPC(ushort wound)
    {
        if (!wounds[wound].isEmbeddedObject) return;

        var w = wounds[wound];
        w.isEmbeddedObject = false;
        w.severity *= 4; //you bleed a bit more after its removed
        shock.Value += w.severity / 90;
        wounds[wound] = w;
        IncreaseWoundIntensityRPC(wound, 2);
    }

    [Rpc(SendTo.Everyone)]
    private void IncreaseWoundIntensityRPC(ushort wound, float mult)
    {
        var ob = woundObjects[wound];
        ob.hasembedded = false;
        ob.embedded = ItemData.Empty;
        ob.severity *= mult;
        woundObjects[wound] = ob;
    }

    public void StopHeart(string reason)
    {
        if (!heartBeating.Value) return;

        mmFailPoints = 0;
        mmPoints = 0;
        cprFailPoints = 0;
        cprPoints = 0;
        heartBeating.Value = false;
        breathing.Value = false;
        consciousness.Value = 0f;
        isConscious.Value = false;

        Debug.Log($"cardiac arrest: {reason}");
    }

    public void TryWakeUp()
    {
        TryWakeUpRPC();
    }

    [Rpc(SendTo.Owner, RequireOwnership = false)]
    private void TryWakeUpRPC()
    {
        recentDamageCd = 0.25f;
        player.MouseJitterIntensity = 2f;
        if(isConscious.Value) return;
        if (heartBeating.Value && breathing.Value)
        {
            consciousness.Value += Random.Range(0.01f, 0.025f);
            if(consciousness.Value > 0 && Random.value <= 0.02f) {
                consciousness.Value = 0.2f;
                isConscious.Value = true;
                Debug.Log($"slapped to consciousness");
            }
        }
    }

    public void DoMouthtoMouth()
    {
        if (breathing.Value) return;
        DoMouthToMouthRPC();
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    private void DoMouthToMouthRPC()
    {
        recuscitationCooldown = 2;
        float difference = lastMouthMouthTime>0 ? Mathf.Abs(Mathf.Abs(Time.time - lastMouthMouthTime) - (CPR_RATE_REQ * (!heartBeating.Value ? 2.1f : 1))) : 0; //2x cuz you have to go back and forth
        if (difference > MAXIMUM_ALLOWED_DISTANCE)
        {
            if (IsOwner) { currOxygen -= Time.deltaTime; }
            mmFailPoints += 1f / 10f;
            Debug.Log("bad MtM, points are " + mmFailPoints);
            if (mmFailPoints >= 1f)
            {
                mmFailPoints = 0f;
                mmPoints = 0;
                lastMouthMouthTime = -1;
                Debug.Log($"failed mouth to mouth");
            }
        }
        else
        {
            if (IsOwner) { currOxygen += Time.deltaTime; }
            mmPoints += 1f / 15f;
            Debug.Log("good MtM, points are " + mmPoints);
            if (mmPoints >= 1f)
            {
                Debug.Log("succeeded mouth to mouth");
                if (heartBeating.Value)
                {
                    MtMRecuscitateRPC();
                }
                else { combinedcprPoints = 1 + CPR_RATE_REQ + 0.3f; }
                lastMouthMouthTime = -1;
                mmPoints = 0f;
                mmFailPoints = 0f;
            }
            else
            {
                player.GetRigidbody.AddForce(Vector3.up * 0.5f, ForceMode.Impulse);
            }
        }
        lastMouthMouthTime = Time.time;
    }

    [Rpc(SendTo.Owner, RequireOwnership = false)]
    private void MtMRecuscitateRPC()
    {
        player.GetRigidbody.AddForce(Vector3.up * 3, ForceMode.Impulse);
        stopBreathignTime = 30f;
        breathing.Value = true;
        consciousness.Value = 0.15f;
    }

    public void DoCPR()
    {
        recuscitationCooldown = 2;
        float difference = Mathf.Abs(Mathf.Abs(Time.time - lastCPRTime) - (CPR_RATE_REQ * (!heartBeating.Value ? 2.1f : 1)));
        if (difference > MAXIMUM_ALLOWED_DISTANCE)
        {
            cprFailPoints += 1f / 10f;
            Debug.Log("bad cpr, points are " + cprFailPoints);
            if (cprFailPoints >= 1f)
            {
                cprFailPoints = 0f;
                cprPoints = 0;
                lastCPRTime = -1;
                Debug.Log($"failed CPR");
            }
        }
        else
        {
            
            cprPoints += 1f / 15f;
            Debug.Log("good cpr, points are " + cprPoints);
            if (cprPoints >= 1f && combinedcprPoints > 1)
            {
                Debug.Log("succeeded CPR"); 
                CPRRecuscitateRPC();
                cprFailPoints = 0f;
                cprPoints = 0;
                combinedcprPoints = 0;
                lastCPRTime = -1;
            }
            else
            {
                player.GetRigidbody.AddForce(Vector3.up * 0.5f, ForceMode.Impulse);
            }
        }
        lastMouthMouthTime = Time.time;
    }

    [Rpc(SendTo.Owner, RequireOwnership = false)]
    private void CPRRecuscitateRPC()
    {
        if(heartBeating.Value) return;
        player.GetRigidbody.AddForce(Vector3.up * 3, ForceMode.Impulse);
        heartBeating.Value = true;
        breathing.Value = true;
        consciousness.Value = 0.15f;
    }

    public void Die(string cause)
    {
        UIManager.ShowGameOverScreen();
        VivoxManager.LeavingChannel = true;
        VivoxManager.LeaveMainChannel(() => { VivoxManager.LeavingChannel = false; });
        if(!VivoxManager.GetisMuted) { VivoxManager.ToggleInputMute(); }
        isAlive.Value = false;
        heartBeating.Value = false;
        breathing.Value = false;
        isConscious.Value = false;
        MusicManager.SetThreatLevel(0);
        Debug.Log($"biological death: {cause}");
        player.OnDied?.Invoke();
        ServerDieRPC(PlayerPrefs.GetInt("SKINTEX", 0), PlayerPrefs.GetFloat("SCARFCOL_R", 1), PlayerPrefs.GetFloat("SCARFCOL_G", 0), PlayerPrefs.GetFloat("SCARFCOL_B", 0), lastBleedAmount);
    }

    [Rpc(SendTo.Server)]
    private void ServerDieRPC(int skintex, float scarfr, float scarfg, float scarfb, float bleed)
    {
        var corpseobj = Instantiate(corpse, transform.position, transform.rotation);
        corpseobj.NetworkObject.Spawn();
        corpseobj.Init(woundObjects.Values.ToList(), currentBlood.Value, bleed, skintex, scarfr, scarfg, scarfb);
        NetworkObject.Despawn();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.TransformPoint(new Vector3(heartLocalPos.x, heartLocalPos.y * player.pm.GetCrouchHeightMult, heartLocalPos.z)), heartRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(transform.TransformPoint(new Vector3(headPos.x, headPos.y * player.pm.GetCrouchHeightMult, headPos.z)), headRadius);
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(transform.TransformPoint(new Vector3(legsPos.x, legsPos.y * player.pm.GetCrouchHeightMult, legsPos.z)), legsRadius);
    }
}