using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

public class PlayerCorpseProxy : NetworkBehaviour
{
    public ParticleSystem bleedingEffect;
    private float blood;
    private float losingbloodrate;
    private List<bool> embeddeds = new();

    public void Init(List<WoundObject> woundObjects, float blood, float losingbloodrate)
    {
        //sync this
        SyncBleedRateRPC(blood, losingbloodrate);

        //spawn the objects
        foreach (var wound in woundObjects)
        {
            SpawnWoundObjectRPC(wound.particles.transform.position, wound.particles.transform.forward, wound.particles.emission.rateOverTime.constant, wound.embedded, wound.hasembedded);
        }
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
        //spawn the objects
        var bleed = Instantiate(bleedingEffect, Vector3.zero, Quaternion.identity);
        var ob = new GameObject("Wound");
        ob.transform.parent = transform;
        ob.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(normal));
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
            inter.Banned = IsOwner || !Player.LocalPlayer || !embeddeds[i];
            if (!Player.LocalPlayer) { return; }
            inter.Description = embeddeds[i] ? $"Remove {ItemDatabase.GetItem(item.ID.ToString()).Name}" : "";
            inter.InteractLength = 2f;
            inter.InteractDistance = 1.5f;
            if (embeddedgraphic && !embeddeds[i])
            {
                Destroy(embeddedgraphic);
            }
            var emission = bleed.emission;
            emission.rateOverTime = 25 * (blood>0 ? intensity : 0);
            if (embeddeds[i]) { inter.OnInteractedLocal = () => RemoveEmbeddedObject(i, item); }
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
    }
}
