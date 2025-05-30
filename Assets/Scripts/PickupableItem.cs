using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Netcode;

/// <summary>
/// For AI to know what item it is.
/// </summary>
public enum ItemTypeEnum { CraftingMaterial, ThrowingBluntWeapon, ThrowingSharpWeapon, MeleeWeapon }

public class PickupableItem : Interactible
{
    public string itemCode;
    public NetworkList<FixedString128Bytes> CurrentSavedData = new(writePerm: NetworkVariableWritePermission.Server, readPerm: NetworkVariableReadPermission.Everyone);
    public Rigidbody rb;
    public List<Collider> colliders;
    public float DamagePerVelocity = 2, StunPerVelocity = 0.1f, MaxThrowFlyTime;
    public Vector3 HitRegSize;
    public bool FaceThrown, StickIntoPlayers;
    public DamageType Type;
    public NetworkVariable<ulong> thrower = new(0);
    public ItemTypeEnum ItemType;

    private bool IsThrown;
    private float currDisableThrowTime = 0, ignoreThrowerTime;
    private Vector3 lastPos;
    private Collider creatureThrower;

    public static List<PickupableItem> ITEMS = new();

    protected override void Enabled()
    {
        ITEMS.Add(this);
    }

    protected override void Disabled()
    {
        ITEMS.Remove(this);
    }

    public void InitSavedData(List<FixedString128Bytes> saveddata)
    {
        foreach (var data in saveddata)
        {
            CurrentSavedData.Add(data);
        }
    }

    public void InitThrown(ulong thrower)
    {
        IsThrown = true;
        currDisableThrowTime = MaxThrowFlyTime;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        this.thrower.Value = thrower;
        ignoreThrowerTime = 0.65f;
    }

    /// <summary>
    /// For when a creature throws it
    /// </summary>
    public void InitThrown(Collider coll)
    {
        IsThrown = true;
        currDisableThrowTime = MaxThrowFlyTime;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        thrower.Value = ulong.MaxValue;
        ignoreThrowerTime = 0f;
        creatureThrower = coll;
    }

    public void InitSavedData()
    {
        foreach (var data in ItemDatabase.GetItem(itemCode).BaseSavedData)
        {
            CurrentSavedData.Add(data);
        }
    }

    public void OnCollisionEnter(Collision collision)
    {
        if (!IsThrown || !IsOwner) { return; }
        AddThreatRPC(rb.linearVelocity.magnitude * DamagePerVelocity * 1f, collision.contacts[0].point);
        IsThrown = false;
        rb.angularVelocity = Random.insideUnitSphere * 15f;
        thrower.Value = 0;
        //if(collision.collider.TryGetComponent(out PlayerHealthController ph))
        //{
        //    ph.ApplyDamage(collision.relativeVelocity.magnitude * DamagePerVelocity, Type, collision.contacts[0].point, collision.contacts[0].normal, StickIntoPlayers, ItemDatabase.GetItem(itemCode).Name, ConvertToItemData);
        //    if (StickIntoPlayers) { NetworkObject.Despawn(); }
        //}
    }

    public override void InteractComplete()
    {
        //nothing here
    }

    public ItemData ConvertToItemData
    {
        get
        {
            ItemData itemData = new ItemData
            {
                ID = itemCode,
                SavedData = new List<FixedString128Bytes>(CurrentSavedData.Count)
            };
            foreach (var data in CurrentSavedData)
            {
                itemData.SavedData.Add(new FixedString128Bytes(data));
            }
            return itemData;
        }
    }

    protected override void VirtUpdate()
    {
        if(!IsOwner) { return; }
        if (IsThrown) {
            currDisableThrowTime -= Time.deltaTime;
            if (FaceThrown) { transform.up = rb.linearVelocity.normalized; }
            if (currDisableThrowTime < 0) { IsThrown = false; rb.angularVelocity += Random.insideUnitSphere * 20; return; }
            if(ignoreThrowerTime > 0) { ignoreThrowerTime -= Time.deltaTime; }

            if (Physics.BoxCast(lastPos, HitRegSize, (transform.position - lastPos).normalized, out RaycastHit hit, transform.rotation, Vector3.Distance(lastPos, transform.position)+0.1f, Extensions.DefaultThrownHitregLayermask))
            {
                if (hit.collider.attachedRigidbody.TryGetComponent(out PlayerHealthController ph) && (ph.OwnerClientId !=thrower.Value || ignoreThrowerTime <= 0))
                {
                    if (hit.collider.CompareTag("Shield"))
                    {
                        NetworkAudioManager.PlayNetworkedAudioClip(Random.Range(0.9f, 1.1f), 0.2f, 1, hit.point, "shieldbonk");
                        if(rb.linearVelocity.magnitude * DamagePerVelocity >= 40) { ph.player.KnockOver(1, true); }
                        Vector3 vel = rb.linearVelocity; rb.linearVelocity = Vector3.zero; rb.linearVelocity = 0.25f * vel.magnitude * -hit.normal;
                        thrower.Value = 0;
                        IsThrown = false;
                        creatureThrower = null; //reset creature thrower so we dont hit the same creature again
                        return;
                    }

                    thrower.Value = 0;
                    IsThrown = false;
                    ph.ApplyDamage(rb.linearVelocity.magnitude * DamagePerVelocity, Type, hit.point, hit.normal, StickIntoPlayers, ItemDatabase.GetItem(itemCode).Name, ConvertToItemData);
                    if (StickIntoPlayers) { DestroyItem(); }
                    else { Vector3 vel = rb.linearVelocity; rb.linearVelocity = Vector3.zero; rb.linearVelocity = 0.25f * vel.magnitude * -hit.normal; }
                }
                else if((hit.collider != creatureThrower || currDisableThrowTime <= 0) && hit.collider.TryGetComponent(out HealthBodyPart hp))
                {
                    var spd = rb.linearVelocity.magnitude;
                    hp.TakeDamage(spd * DamagePerVelocity, spd * StunPerVelocity, Type, hit.point, hit.normal, StickIntoPlayers, ConvertToItemData);
                    thrower.Value = 0;
                    IsThrown = false;
                    creatureThrower = null; //reset creature thrower so we dont hit the same creature again
                    if (StickIntoPlayers) { DestroyItem(); }
                    else { Vector3 vel = rb.linearVelocity; rb.linearVelocity = Vector3.zero; rb.linearVelocity = 0.25f * vel.magnitude * -hit.normal; }
                }
            }
            lastPos = transform.position;
        }
    }

    [Rpc(SendTo.Everyone)]
    private void AddThreatRPC(float amount, Vector3 impactpos)
    {
        if (!Player.LocalPlayer) { return; }    
        MusicManager.AddThreatLevel(amount * Mathf.Lerp(1, 0, (impactpos - Player.GetLocalPlayerCentre).magnitude / 5));
    }

    public void DestroyItem() {
        DestroyRPC();
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void DestroyRPC()
    {
        NetworkObject.Despawn(true);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireCube(transform.position, HitRegSize*2);
    }
}
