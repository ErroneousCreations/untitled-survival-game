using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Collections;

public class PlayerCorpseProxy : NetworkBehaviour
{
    [Header("Values")]
    public float baseDespawnTime = 300;
    public float fastDespawnRange = 50;
    public int playerSkinMatIndex, playerScarfMatIndex;

    [Header("Visuals")]
    public ParticleSystem bleedingEffect;
    public Renderer PlayerRend, ScarfRend, EyeRend;
    public List<Texture2D> skinTextures;
    public Texture2D eyeTex;

    [Header("Physics and Colliders")]
    public Rigidbody rb;
    public List<Collider> bodyColliders;

    //statics
    public static List<PlayerCorpseProxy> CORPSES = new();

    //privates
    private float blood;
    private float losingbloodrate;
    private List<bool> embeddeds = new();
    private List<ItemData> embeddedItems = new();
    private float despawnTimer;
    private float checkRangesTimer = 0;
    private float despawnSpeedMult = 1;
    private Material skinMat;

    private Transform GetClosestCollider(Vector3 startpos, out Vector3 newPos)
    {
        float closestDist = Mathf.Infinity;
        Transform closest = null;
        newPos = startpos;

        foreach (var coll in bodyColliders)
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

    private void OnEnable()
    {
        CORPSES.Add(this);
    }

    private void OnDisable()
    {
        CORPSES.Remove(this);
    }

    private string VecToString(Vector3 pos)
    {
        return System.Math.Round(pos.x, 2).ToString() + "," + System.Math.Round(pos.y, 2).ToString() + "," + System.Math.Round(pos.z, 2).ToString();
    }
    private string SavedItemDataToString(List<FixedString128Bytes> list)
    {
        if (list.Count <= 0) { return ""; }
        string final = "";
        foreach (var item in list)
        {
            final += item.ToString() + ",";
        }
        return final[..^1];
    }

    public string GetSavedItemsFromCorpse
    {
        get
        {
            if(embeddedItems.Count <= 0) { return ""; }
            string saveditems = "";
            foreach (var item in embeddedItems)
            {
                if (!item.IsValid) { continue; }
                var rand = Random.insideUnitCircle * 0.5f;
                var pos = transform.position + Vector3.up*0.1f + new Vector3(rand.x, 0, rand.y);
                var saveddata = SavedItemDataToString(item.SavedData);
                saveditems += $"{item.ID},{VecToString(pos)},{VecToString(Vector3.zero)}{(saveddata.Length > 0 ? ","+saveddata : "")}\\";
            }
            return saveditems;
        }
    }

    public void Init(List<WoundObject> woundObjects, float blood, float losingbloodrate, int skintex, float scarfr, float scarfg, float scarfb)
    {
        //sync this
        SyncBleedRateRPC(blood, losingbloodrate);

        //spawn the objects
        foreach (var wound in woundObjects)
        {
            SpawnWoundObjectRPC(wound.particles.transform.position, wound.particles.transform.forward, wound.severity, wound.embedded, wound.hasembedded);
        }
        despawnTimer = baseDespawnTime;
        checkRangesTimer = 10;
        despawnSpeedMult = 1;

        SyncVisualsRPC(skintex, scarfr, scarfg, scarfb);
    }

    [Rpc(SendTo.Everyone)]
    private void SyncVisualsRPC(int skintex, float scarfr, float scarfg, float scarfb)
    {
        PlayerRend.materials[playerScarfMatIndex].color = new Color(scarfr, scarfg, scarfb);
        ScarfRend.material.color = new Color(scarfr, scarfg, scarfb);
        skinMat = PlayerRend.materials[playerSkinMatIndex];
        skinMat.SetTexture("_MainTex", skinTextures[skintex]);
        EyeRend.material.mainTexture = eyeTex;
    }

    [Rpc(SendTo.Everyone)]
    private void SyncBleedRateRPC(float blood, float losingbloodrate)
    {
        this.blood = blood;
        this.losingbloodrate = losingbloodrate;
    }

    [Rpc(SendTo.Everyone)]
    private void SpawnWoundObjectRPC(Vector3 pos, Vector3 normal, float intensity, ItemData item, bool embedded)
    {
        var index = embeddeds.Count;
        embeddeds.Add(embedded);
        embeddedItems.Add(item);
        //spawn the objects
        var bleed = Instantiate(bleedingEffect, Vector3.zero, Quaternion.identity);
        var ob = new GameObject("Wound");
        var coll = GetClosestCollider(pos, out var newPos);
        ob.transform.parent = coll;
        ob.transform.SetPositionAndRotation(newPos, Quaternion.LookRotation(normal));
        GameObject embeddedgraphic = null;
        if (embedded)
        {
            embeddedgraphic = new GameObject("EmbeddedGraphic");
            embeddedgraphic.transform.parent = ob.transform;
            embeddedgraphic.transform.localPosition = Vector3.zero;
            embeddedgraphic.transform.up = -normal;
            embeddedgraphic.AddComponent<MeshFilter>().mesh = ItemDatabase.GetItem(item.ID.ToString()).HeldMesh;
            embeddedgraphic.AddComponent<MeshRenderer>().materials = ItemDatabase.GetItem(item.ID.ToString()).HeldMats;
        }
        bleed.transform.parent = ob.transform;
        bleed.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        var initialpos = ob.transform.localPosition;

        var inter = ob.AddComponent<Interactible>();
        inter.OnUpdate += () =>
        {
            var i = index;
            //todo make it change depending on what your holding and stuff
            inter.Banned = !Player.LocalPlayer || !embeddeds[i];
            inter.Description = embeddeds[i] ? $"Remove {ItemDatabase.GetItem(item.ID.ToString()).Name}" : "";
            inter.InteractLength = 2f;
            inter.InteractDistance = 1.5f;
            if (embeddedgraphic && !embeddeds[i])
            {
                Destroy(embeddedgraphic);
            }
            var emission = bleed.emission;
            emission.rateOverTime = 25 * (blood>0 ? intensity : 0);
            if (embeddeds[i] && Player.LocalPlayer) { inter.OnInteractedLocal = () => RemoveEmbeddedObject(i, item); }
            else { inter.OnInteractedLocal = null; }
        };
    }

    public void RemoveEmbeddedObject(int id, ItemData embeddedItem)
    {
        RemoveEmbeddedObjectRPC(id);
        SpawnEmbeddedWoundObjectRPC(embeddedItem, Player.GetLocalPlayerCentre + Player.LocalPlayer.transform.forward);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    private void RemoveEmbeddedObjectRPC(int id)
    {
        embeddeds[id] = false;
        embeddedItems[id] = ItemData.Empty;
    }

    [Rpc(SendTo.Server)]
    private void SpawnEmbeddedWoundObjectRPC(ItemData item, Vector3 position)
    {
        // Spawn logic should be synced
        var go = Instantiate(ItemDatabase.GetItem(item.ID.ToString()).ItemPrefab, position, Quaternion.identity);
        go.NetworkObject.Spawn();
        go.CurrentSavedData = new();
        foreach (var data in item.SavedData)
        {
            go.CurrentSavedData.Add(data.ToString());
        }
    }

    private void Update()
    {
        if(blood > 0)
        {
            blood -= losingbloodrate * Time.deltaTime;
        }
        skinMat.SetFloat("_Paleness", 1-blood);
        if (!IsOwner) { return; }
        despawnTimer -= Time.deltaTime*despawnSpeedMult;
        if(despawnTimer <= 0) { DespawnCorpse(); }
        if (checkRangesTimer > 0)
        {
            checkRangesTimer -= Time.deltaTime;
        }
        else
        {
            checkRangesTimer = 5;
            var players = Player.PLAYERBYID.Values;
            int inrange = 0;
            foreach (var player in players)
            {
                if ((transform.position - player.transform.position).sqrMagnitude < fastDespawnRange * fastDespawnRange) { inrange++; }
            }
            if (inrange > 0) { despawnSpeedMult = 1; }
            else { despawnSpeedMult = 5; }
        }
    }

    private void DespawnCorpse()
    {
        foreach (var item in embeddedItems)
        {
            if (item.IsValid)
            {
                var rand = Random.insideUnitCircle * 0.5f;
                var go = Instantiate(ItemDatabase.GetItem(item.ID.ToString()).ItemPrefab, transform.position + Vector3.up * 0.1f + new Vector3(rand.x, 0, rand.y), Quaternion.identity);
                go.NetworkObject.Spawn();
                go.InitSavedData(item.SavedData);
            }
        }
        NetworkObject.Despawn();
    }

    private bool StandingUp => Vector3.Angle(transform.up, Vector3.up) < 10;

    private void FixedUpdate()
    {
        if(!IsOwner) { return; }

        if (StandingUp) { rb.AddForceAtPosition(transform.forward, transform.TransformPoint(new Vector3(0, 1.7f, 0)), ForceMode.Force); }
        else
        {
            // Simulate friction to reduce sliding
            Vector3 lateralVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
            Vector3 frictionForce = -lateralVelocity * 5;
            rb.AddForce(frictionForce, ForceMode.Acceleration);

            rb.angularVelocity *= 1f / (1f + 5 * Time.fixedDeltaTime);
        }
    }
}
