using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using Unity.Collections;

public class CreatureCorpseProxy : NetworkBehaviour
{
    [Header("Values")]
    public float baseDespawnTime = 150;
    public float fastDespawnRange = 50;

    [Header("Physics and Colliders")]
    public Rigidbody rb;
    public List<Rigidbody> HealthParts;

    [Header("Visuals")]
    public List<SyncedMaterialStruct> syncedMats;
    public Renderer eyesRend;
    public Texture2D eyesTex;

    //statics
    public static List<CreatureCorpseProxy> CORPSES = new();

    //privates
    private List<ItemData> embeddedItems = new();
    private List<bool> embeddeds = new();
    private float despawnTimer;
    private float checkRangesTimer = 0;
    private float despawnSpeedMult = 1;
    private Material skinMat;


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
            if (embeddedItems.Count <= 0) { return ""; }
            string saveditems = "";
            foreach (var item in embeddedItems)
            {
                if (!item.IsValid) { continue; }
                var rand = Random.insideUnitCircle * 0.5f;
                var pos = transform.position + Vector3.up * 0.1f + new Vector3(rand.x, 0, rand.y);
                var saveddata = SavedItemDataToString(item.SavedData);
                saveditems += $"{item.ID},{VecToString(pos)},{VecToString(Vector3.zero)}{(saveddata.Length > 0 ? "," + saveddata : "")}\\";
            }
            return saveditems;
        }
    }

    public void Init(List<Health.Wound> woundObjects, List<HealthBodyPart> parts, List<Color> syncedCols)
    {
        var k = 0;
        foreach (var part in parts)
        {
            if (!part.transform.parent) { k++; continue; }
            HealthParts[k].transform.SetLocalPositionAndRotation(part.transform.localPosition, part.transform.localRotation);
        }
        //spawn the objects
        foreach (var wound in woundObjects)
        {
            SpawnWoundObjectRPC(wound.Part, wound.localPosition, wound.localRotation, wound.embeddedItem);
        }
        for (var i = 0; i< syncedMats.Count; i++)
        {
            if(i >= syncedCols.Count) { break; }
            syncedMats[i].rend.materials[syncedMats[i].index].color = syncedCols[i];
        }
        despawnTimer = baseDespawnTime;
        checkRangesTimer = Random.Range(4.5f, 6.5f);
        despawnSpeedMult = 1;
        if(eyesRend && eyesTex)
        {
            eyesRend.material.mainTexture = eyesTex;
        }
    }

    [Rpc(SendTo.Everyone)]
    private void SpawnWoundObjectRPC(int part, Vector3 pos, Vector3 normal, ItemData item)
    {
        var index = embeddedItems.Count;
        embeddedItems.Add(item);
        embeddeds.Add(true);
        //spawn the objects
        var ob = new GameObject("Wound");
        ob.transform.parent = HealthParts[part].transform;
        ob.transform.SetLocalPositionAndRotation(pos, Quaternion.LookRotation(normal));

        GameObject embeddedgraphic = null;
        embeddedgraphic = new GameObject("EmbeddedGraphic");
        embeddedgraphic.transform.parent = ob.transform;
        embeddedgraphic.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(-90, 0, 0));
        embeddedgraphic.AddComponent<MeshFilter>().mesh = ItemDatabase.GetItem(item.ID.ToString()).HeldMesh;
        embeddedgraphic.AddComponent<MeshRenderer>().materials = ItemDatabase.GetItem(item.ID.ToString()).HeldMats;


        var inter = ob.AddComponent<Interactible>();
        inter.OnUpdate += () =>
        {
            var i = index;
            if (!embeddeds[i]) { Destroy(ob); return; } // Ensure the wound still exists
            //todo make it change depending on what your holding and stuff
            inter.Banned = !Player.LocalPlayer || !embeddeds[i];
            inter.Description = $"Remove {ItemDatabase.GetItem(item.ID.ToString()).Name}";
            inter.InteractLength = 2f;
            inter.InteractDistance = 1.5f;
            if ( Player.LocalPlayer) { inter.OnInteractedLocal = () => RemoveEmbeddedObject(i, item); }
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
        go.InitSavedData(item.SavedData);

    }

    private void Update()
    {
        if (!IsOwner) { return; }
        despawnTimer -= Time.deltaTime * despawnSpeedMult;
        if (despawnTimer <= 0) { DespawnCorpse(); }
        if (checkRangesTimer > 0)
        {
            checkRangesTimer -= Time.deltaTime;
        }
        else
        {
            checkRangesTimer = Random.Range(4.5f, 6.5f);
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
        if (!IsOwner) { return; }

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
