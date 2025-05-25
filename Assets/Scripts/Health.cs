using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[System.Serializable]
public struct SyncedMaterialStruct
{
    public Renderer rend;
    public int index;
}

public class Health : NetworkBehaviour
{
    public float MaxHealth = 100f;

    private float _currentHealth = 0;
    public float GetHealth => _currentHealth;
    public float GetNormalisedHealth => Mathf.Clamp01(_currentHealth / MaxHealth);
    public AnimationCurve DeathChanceCurve;
    public CreatureCorpseProxy Corpse;
    public float StunResistance, StunThreshold;
    public List<HealthBodyPart> Parts;
    /// <summary>
    /// For if the creature has colour variation so we can apply that to the corpse as well!!!
    /// </summary>
    public List<SyncedMaterialStruct> SyncedMats;

    /// <summary>
    /// Damage, Stunamount, Is Embedded
    /// </summary>
    public System.Action<float, float, bool> OnTakeDamage;

    public bool GetStunned => stunTime > StunThreshold;
    private float stunTime;
    private Dictionary<int,Wound> _wounds = new Dictionary<int,Wound>();
    private int next;

    public struct Wound
    {
        public ItemData embeddedItem;
        public int Part;
        public Vector3 localPosition, localRotation;

        public Wound(ItemData embeddedItem, int part, Vector3 localPosition, Vector3 localRotation)
        {
            this.embeddedItem = embeddedItem;
            Part = part;
            this.localPosition = localPosition;
            this.localRotation = localRotation;
        }

        public Wound(string data)
        {
            var split = data.Split('`');
            embeddedItem = new ItemData(split[0]);
            Part = int.Parse(split[1]);
            localPosition = new Vector3(float.Parse(split[2]), float.Parse(split[3]), float.Parse(split[4]));
            localRotation = new Vector3(float.Parse(split[5]), float.Parse(split[6]), float.Parse(split[7]));
        }

        public override string ToString()
        {
            return $"{embeddedItem.ID}`{Part}`{Extensions.VecToString(localPosition, "`")}`{Extensions.VecToString(localRotation, "`")}";
        }
    }

    private string WoundsToString
    {
        get
        {
            var r = "";
            foreach (var wound in _wounds.Values)
            {
                r += wound.ToString() + "~";
            }
            return r.Length>0 ? r[..^1] : r;
        }
    }

    public string GetSavedData => $"{System.Math.Round(_currentHealth,1)}~{System.Math.Round(stunTime, 1)}{(_wounds.Count > 0 ? "~"+WoundsToString : "")}";

    public void ApplySavedData(string data)
    {
        if(data == "null" || string.IsNullOrEmpty(data)) { return; } // No data to apply

        var split = data.Split('~');
        _currentHealth = float.Parse(split[0]);
        stunTime = float.Parse(split[1]);
        _wounds = new();
        if(split.Length < 3) { return; } // No wounds to apply
        for (int i = 2; i < split.Length; i++)
        {
            var wound = new Wound(split[i]);
            ApplyNewWoundRPC(split[i]); // Apply the wound via RPC to ensure all clients are updated
        }
    }

    //spawn the wound visuals and interactor and whatever
    [Rpc(SendTo.Everyone)]
    private void ApplyNewWoundRPC(string data)
    {
        var wound = new Wound(data);
        _wounds.Add(next, wound);
        var index = next; ;
        next++;

        //spawn the objects
        var ob = new GameObject("Wound");
        ob.transform.parent = Parts[wound.Part].transform;
        ob.transform.SetLocalPositionAndRotation(wound.localPosition, Quaternion.LookRotation(wound.localRotation));
        GameObject embeddedgraphic = null;

        embeddedgraphic = new GameObject("EmbeddedGraphic");
        embeddedgraphic.transform.parent = ob.transform;
        embeddedgraphic.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(-90,0,0));
        embeddedgraphic.AddComponent<MeshFilter>().mesh = ItemDatabase.GetItem(wound.embeddedItem.ID.ToString()).HeldMesh;
        embeddedgraphic.AddComponent<MeshRenderer>().materials = ItemDatabase.GetItem(wound.embeddedItem.ID.ToString()).HeldMats;

        var initialpos = ob.transform.localPosition;

        var inter = ob.AddComponent<Interactible>();
        inter.OnUpdate += () =>
        {
            var i = index;
            if (!_wounds.ContainsKey(i)) { Destroy(ob); return; } // Ensure the wound still exists
            inter.Banned = !Player.LocalPlayer || !_wounds.ContainsKey(i);
            inter.Description = $"Remove {ItemDatabase.GetItem(wound.embeddedItem.ID.ToString()).Name}";
            inter.InteractLength = 2f;
            inter.InteractDistance = 1.5f;
            if (Player.LocalPlayer) { inter.OnInteractedLocal = () => DeleteWound(i); }
            else { inter.OnInteractedLocal = null; }
        };
    }

    private void DeleteWound(int wound)
    {
        SpawnEmbeddedWoundObjectRPC(_wounds[wound].embeddedItem, Player.LocalPlayer.transform.position + Vector3.up + Player.LocalPlayer.transform.forward*0.5f);
        DeleteWoundRPC(wound);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    private void DeleteWoundRPC(int wound)
    {
        if (_wounds.ContainsKey(wound))
        {
            _wounds.Remove(wound);
        }
    }

    [Rpc(SendTo.Server)]
    private void SpawnEmbeddedWoundObjectRPC(ItemData item, Vector3 position)
    {
        // Spawn logic should be synced
        var go = Instantiate(ItemDatabase.GetItem(item.ID.ToString()).ItemPrefab, position, Quaternion.identity);
        go.NetworkObject.Spawn();
        go.InitSavedData(item.SavedData);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
        {
            _currentHealth = MaxHealth;
        }
    }

    /// <summary>
    /// should only be called from healthbodypart
    /// </summary>
    public void TakeDamage(float damage, float stun, DamageType type, int part, bool embedded = false, Vector3 pos = default, Vector3 localrot = default, ItemData embeddeditem = default)
    {
        DamageRPC(damage, stun, type, part, embedded, pos, localrot, embeddeditem);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void DamageRPC(float damage, float stun, DamageType type, int part, bool embedded, Vector3 pos, Vector3 localrot, ItemData embeddeditem)
    {
        _currentHealth -= damage;
        stunTime += stun * (1 - StunResistance);
        if (embedded) {
            // Add a new wound to the list
            var newWound = new Wound(embeddeditem, part, pos, localrot);
            ApplyNewWoundRPC(newWound.ToString()); // Apply the wound visuals and interactor
        }
        OnTakeDamage?.Invoke(damage, stun, embedded);
    }

    public void Heal(float amount)
    {
        HealRPC(amount);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void HealRPC(float amount)
    {
        _currentHealth = Mathf.Min(_currentHealth + amount, MaxHealth);
    }

    private void Update()
    {
        if(!IsServer) return;

        if (stunTime > 0) { stunTime -= Time.deltaTime; }

        if (_currentHealth <= 0)
        {
            if(Random.value < DeathChanceCurve.Evaluate(Mathf.Clamp01(Mathf.Abs(_currentHealth) / MaxHealth)))
            {
                var corpse = Instantiate(Corpse, transform.position, transform.rotation);
                corpse.NetworkObject.Spawn();
                List<Color > colours = new List<Color>();
                foreach (var synced in SyncedMats)
                {
                    colours.Add(synced.rend.materials[synced.index].color);
                }
                corpse.Init(_wounds.Values.ToList(), Parts, colours);
                NetworkObject.Despawn();
            }
        }
    }
}
