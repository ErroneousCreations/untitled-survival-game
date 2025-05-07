using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Netcode;

public class PickupableItem : Interactible
{
    public string itemCode;
    public NetworkList<FixedString64Bytes> CurrentSavedData = new(writePerm: NetworkVariableWritePermission.Server, readPerm: NetworkVariableReadPermission.Everyone);
    public Rigidbody rb;
    public List<Collider> colliders;
    public float DamagePerVelocity = 2, MaxThrowFlyTime;
    public Vector3 HitRegSize;
    public bool FaceThrown, StickIntoPlayers;
    public DamageType Type;
    public NetworkVariable<ulong> thrower = new(0);

    private bool IsThrown;
    private float currDisableThrowTime = 0, ignoreThrowerTime;
    private Vector3 lastPos;

    public static List<PickupableItem> ITEMS = new();

    protected override void Enabled()
    {
        ITEMS.Add(this);
    }

    protected override void Disabled()
    {
        ITEMS.Remove(this);
    }

    public void InitSavedData(List<FixedString64Bytes> saveddata)
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
        IsThrown = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
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
                SavedData = new List<FixedString64Bytes>(CurrentSavedData.Count)
            };
            foreach (var data in CurrentSavedData)
            {
                itemData.SavedData.Add(new FixedString64Bytes(data));
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
                if (hit.collider.TryGetComponent(out PlayerHealthController ph) && (ph.OwnerClientId !=thrower.Value || ignoreThrowerTime <= 0))
                {
                    thrower.Value = 0;
                    IsThrown = false;
                    rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                    ph.ApplyDamage(rb.linearVelocity.magnitude * DamagePerVelocity, Type, hit.point, hit.normal, StickIntoPlayers, ItemDatabase.GetItem(itemCode).Name, ConvertToItemData);
                    if (StickIntoPlayers) { DestroyItem(); }
                    else { Vector3 vel = rb.linearVelocity; rb.linearVelocity = Vector3.zero; rb.linearVelocity = 0.1f * vel.magnitude * -hit.normal; }
                }
            }
            lastPos = transform.position;
        }
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
