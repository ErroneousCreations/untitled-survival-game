using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using EditorAttributes;

[CreateAssetMenu(fileName = "New ShieldBehaviour", menuName = "ItemBehaviours/ShieldBehaviour")]
public class Shield : ScriptableObject, IItemBehaviour
{
    [Header("Melee Animation")]
    public float AttackLength = 0.75f;
    public float ForwardTime = 0.15f;
    public float AttackForwardAmount = 0.75f;

    [Header("Melee Stats")]
    public float Damage;
    [Tooltip("From camera, ray length")] public float Range = 1f;

    [Header("Shield Stats")]
    public Vector3 colliderSize;

    public ItemData InInventoryUpdate(InventoryItemStatus status, ItemData item)
    {
        return item;
    }

    //clear shield collider
    public ItemData OnDrop(ItemData item, HandSideEnum side)
    {
        if(side == HandSideEnum.Left) { PlayerInventory.SetLeftHandCollSize(Vector3.zero); }
        else { PlayerInventory.SetRightHandCollSize(Vector3.zero); }    
        return item;
    }

    public ItemData OnEquip(ItemData item)
    {
        PlayerInventory.SetRightHandCollSize(colliderSize);
        return item;
    }

    public ItemData OnHandsSwapped(ItemData item, HandSideEnum side)
    {
        if(side == HandSideEnum.Right)
        {
            PlayerInventory.SetRightHandCollSize(colliderSize);
            PlayerInventory.SetLeftHandCollSize(Vector3.zero);
        }
        else
        {
            PlayerInventory.SetLeftHandCollSize(colliderSize);
            PlayerInventory.SetRightHandCollSize(Vector3.zero);
        }
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
        hand.DOLocalMove(new Vector3(0, 0, AttackForwardAmount), ForwardTime);
        yield return new WaitForSeconds(ForwardTime);
        DoMeleeDamage();
        hand.DOLocalMove(Vector3.zero, AttackLength - ForwardTime);
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
                    ph.player.DamageEffects(0.25f, 1);
                    return;
                }
                ph.ApplyDamage(Damage, DamageType.Blunt, hit.point, hit.normal, false);
            }
        }
    }

    public ItemData OnPickup(ItemData item, HandSideEnum side)
    {
        if (side == HandSideEnum.Left) { PlayerInventory.SetLeftHandCollSize(colliderSize); }
        else { PlayerInventory.SetRightHandCollSize(colliderSize); }
        return item;
    }

    public ItemData OnPutInBackpack(ItemData item)
    {
        PlayerInventory.SetRightHandCollSize(Vector3.zero);
        return item;
    }

    public ItemData OnRemoveFromBackpack(ItemData item)
    {
        PlayerInventory.SetRightHandCollSize(colliderSize);
        return item;
    }

    public ItemData OnUnequip(ItemData item)
    {
        PlayerInventory.SetRightHandCollSize(Vector3.zero);
        return item;
    }

    public ItemData OnWasCrafted(ItemData item)
    {
        PlayerInventory.SetRightHandCollSize(colliderSize);
        return item;
    }

    public ItemData OnWasStolen(ItemData item)
    {
        return item;
    }

    public ItemData OnLoaded(ItemData item, LoadedLocationEnum location)
    {
        if(location == LoadedLocationEnum.Lefthand)
        {
            PlayerInventory.SetLeftHandCollSize(colliderSize);
        }
        else if (location == LoadedLocationEnum.Righthand)
        {
            PlayerInventory.SetRightHandCollSize(colliderSize);
        }
        return item;
    }
}
