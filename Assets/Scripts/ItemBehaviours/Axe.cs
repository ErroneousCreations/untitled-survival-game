using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using EditorAttributes;

[CreateAssetMenu(fileName = "New AxeBehaviour", menuName = "ItemBehaviours/AxeBehaviour")]
public class Axe : ScriptableObject, IItemBehaviour
{
    [Header("Melee Animation")]
    public float AttackLength = 0.75f;
    public float ForwardTime = 0.15f;
    public float AttackForwardAmount = 0.75f;

    [Header("Melee Stats")]
    public float Damage, Stun;
    public DamageType Type;
    [Tooltip("From camera, ray length")] public float Range = 1f;
    public float WorldBreakableDamage;

    public ItemData InInventoryUpdate(InventoryItemStatus status, ItemData item)
    {
        return item;
    }

    public ItemData OnDrop(ItemData item, HandSideEnum side)
    {
        return item;
    }

    public ItemData OnEquip(ItemData item)
    {
        return item;
    }

    public ItemData OnHandsSwapped(ItemData item, HandSideEnum side)
    {
        return item;
    }

    public ItemData OnHeldUpdate(Transform hand, HandSideEnum side, ItemData item)
    {
        if (Player.GetCanStand && side == HandSideEnum.Right && !PlayerInventory.GetIsBusy && Input.GetMouseButtonDown(0))
        {
            PlayerInventory.AddItemBusyTime(AttackLength);
            Player.LocalPlayer.StartCoroutine(AttackAnimation(hand));
        }

        if (!Player.GetCanStand) { Player.LocalPlayer.StopCoroutine(nameof(AttackAnimation)); hand.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity); }

        return item;
    }

    IEnumerator AttackAnimation(Transform hand)
    {
        hand.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        hand.DOLocalRotate(new Vector3(90, 0, 0), ForwardTime);
        hand.DOLocalMove(new Vector3(0, 0, AttackForwardAmount), ForwardTime);
        yield return new WaitForSeconds(ForwardTime);
        DoMeleeDamage();
        hand.DOLocalMove(Vector3.zero, AttackLength - ForwardTime);
        hand.DOLocalRotate(Vector3.zero, AttackLength - ForwardTime);
    }

    private void DoMeleeDamage()
    {
        if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out RaycastHit hit, Range, Extensions.DefaultMeleeLayermask, QueryTriggerInteraction.Collide))
        {
            if (hit.collider.attachedRigidbody && hit.collider.attachedRigidbody.TryGetComponent(out PlayerHealthController ph))
            {
                if (hit.collider.CompareTag("Shield"))
                {
                    NetworkAudioManager.PlayNetworkedAudioClip(Random.Range(0.9f, 1.1f), 0.2f, 1, hit.point, "shieldbonk");
                    ph.player.DamageEffects(0.35f, Damage / 2);
                    return;
                }
                ph.ApplyDamage(Damage, Type, hit.point, hit.normal, false);
            }
            else if (hit.collider.TryGetComponent(out HealthBodyPart hp))
            {
                hp.TakeDamage(Damage, Stun, Type, hit.point, hit.normal);
            }
            else if (hit.collider.transform.parent.TryGetComponent(out WorldFeature wf) && wf.Destroyable)
            {
                wf.Attack(WorldBreakableDamage);
                PlayerInventory.SpawnNetOb(wf.Breakparticle, hit.point, Quaternion.LookRotation(hit.normal));
            }
            else if (hit.collider.transform.parent.TryGetComponent(out DestructibleWorldDetail det))
            {
                det.Attack(WorldBreakableDamage);
                PlayerInventory.SpawnNetOb(det.BreakParticle, hit.point, Quaternion.LookRotation(hit.normal));
            }
        }
    }

    public ItemData OnPickup(ItemData item, HandSideEnum side)
    {
        return item;
    }

    public ItemData OnPutInBackpack(ItemData item)
    {
        return item;
    }

    public ItemData OnRemoveFromBackpack(ItemData item)
    {
        return item;
    }

    public ItemData OnUnequip(ItemData item)
    {
        return item;
    }

    public ItemData OnWasCrafted(ItemData item)
    {
        return item;
    }

    public ItemData OnWasStolen(ItemData item)
    {
        return item;
    }

    public ItemData OnLoaded(ItemData item, LoadedLocationEnum location)
    {
        return item;
    }
}
