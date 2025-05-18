using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using EditorAttributes;
using Unity.Collections;

[CreateAssetMenu(fileName = "New MeleeThrowBehaviour", menuName = "ItemBehaviours/MeleeThrowBehaviour")]
public class MeleeThrowBehaviour : ScriptableObject, IItemBehaviour
{
    [Header("Melee Animation")]
    public float AttackLength = 0.75f;
    public float ForwardTime = 0.15f;
    public float AttackForwardAmount = 0.75f, AttackJumpPower = 0.4f;
    public bool RotateForwardsMelee;

    [Header("Melee Stats")]
    public float Damage;
    public DamageType Type;
    [Tooltip("From camera, ray length")]public float Range = 1f;
    public bool HitWorldBreakables;
    [ShowField(nameof(HitWorldBreakables))] public float WorldBreakableDamage;
    public bool Sharpen;
    [ShowField(nameof(Sharpen))] public string SharpenResult, SharpenParticle;

    [Header("Throw")]
    public float MaxCharge = 1;
    public float VibrateIntensity = 0.1f, ThrowchargeUpwardAmount = 0.5f;
    public float MaximumThrowForce = 1, ThrowAngularForce = 0;
    public float ThrowAnimationBack = 0.25f, ThrowAnimationForward = 0.5f, ThrowAnimationJump = 0.25f, ThrowAnimationLength = 0.4f;
    public bool RotateForwardsThrow;

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
        item.TempData = new()
        {
            0,
            0
        };
        return item;
    }

    public ItemData OnHandsSwapped(ItemData item, HandSideEnum side)
    {
        return item;
    }

    public ItemData OnHeldUpdate(Transform hand, HandSideEnum side, ItemData item)
    {
        if(Player.GetCanStand && side==HandSideEnum.Right && !PlayerInventory.GetIsBusy && Input.GetMouseButtonDown(0))
        {
            PlayerInventory.AddItemBusyTime(AttackLength);
            Player.LocalPlayer.StartCoroutine(AttackAnimation(hand, item));
        }

        if(Player.GetCanStand && side==HandSideEnum.Right && !PlayerInventory.GetIsBusy && Input.GetMouseButtonDown(1))
        {
            PlayerInventory.AddItemBusyTime(0.1f);
            item.TempData[1] = 1;
        }

        if (Player.GetCanStand && side == HandSideEnum.Right && item.TempData[1] > 0) {
            item.TempData[0] += Time.deltaTime;
            PlayerInventory.AddItemBusyTime(0.1f);
            hand.SetLocalPositionAndRotation(Vector3.Lerp(Vector3.zero, new Vector3(0, ThrowchargeUpwardAmount, 0) + Random.insideUnitSphere * VibrateIntensity, item.TempData[0] / MaxCharge), RotateForwardsThrow ? Quaternion.Lerp(Quaternion.identity, Quaternion.Euler(90, 0, 0), item.TempData[0] * 4 / MaxCharge) : Quaternion.identity);
        }

        if (Player.GetCanStand && side == HandSideEnum.Right && item.TempData[1] > 0 && (Input.GetMouseButtonUp(1) || item.TempData[0] >= MaxCharge))
        {
            PlayerInventory.AddItemBusyTime(ThrowAnimationLength);
            Player.LocalPlayer.StartCoroutine(ThrowCoroutine(item.TempData[0], hand, item));
            item.TempData[0] = 0;
            item.TempData[1] = 0;
        }

        if (!Player.GetCanStand) { Player.LocalPlayer.StopCoroutine(nameof(AttackAnimation)); Player.LocalPlayer.StopCoroutine(nameof(ThrowCoroutine)); item.TempData[0] = 0; item.TempData[1] = 0; hand.localPosition = Vector3.zero; hand.localRotation = Quaternion.identity; }

        return item;
    }

    IEnumerator ThrowCoroutine(float charge, Transform hand, ItemData item)
    {
        if (RotateForwardsThrow) { hand.localRotation = Quaternion.Euler(90, 0, 0); }
        Vector3 pos = hand.localPosition;
        float rtt = Extensions.GetEstimatedRTT;
        float timeminusRTT = ThrowAnimationLength - rtt - 0.2f;
        hand.DOLocalMove(pos + new Vector3(0, 0, -ThrowAnimationBack), ThrowAnimationLength - 0.2f);
        yield return new WaitForSeconds(timeminusRTT);
        PlayerInventory.SpawnItemWithVelocity(item, Camera.main.transform.position + Player.LocalPlayer.transform.forward, Player.LocalPlayer.pm.GetRigidbody.linearVelocity + (charge / MaxCharge) * MaximumThrowForce * Camera.main.transform.forward, Extensions.LocalClientID, Random.insideUnitSphere.normalized * ThrowAngularForce);
        yield return new WaitForSeconds(rtt);
        PlayerInventory.DeleteRighthandItem();
        hand.DOLocalJump(pos + new Vector3(0, 0, ThrowAnimationForward), ThrowAnimationJump, 1, 0.1f);
        yield return new WaitForSeconds(0.1f);
        hand.DOLocalRotate(Vector3.zero, 0.1f);
        hand.DOLocalMove(Vector3.zero, 0.1f);
    }

    IEnumerator AttackAnimation(Transform hand, ItemData item)
    {
        hand.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        if (RotateForwardsMelee) { hand.DOLocalRotate(new Vector3(90, 0, 0), ForwardTime * 0.3f); }
        hand.DOLocalJump(new Vector3(0, 0, AttackForwardAmount), AttackJumpPower, 1, ForwardTime);
        yield return new WaitForSeconds(ForwardTime);
        DoMeleeDamage(item);
        hand.DOLocalMove(Vector3.zero, AttackLength-ForwardTime);
        hand.DOLocalRotate(Vector3.zero, AttackLength - ForwardTime);
    }

    private void DoMeleeDamage(ItemData item)
    {
        if(Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out RaycastHit hit, Range, Extensions.DefaultMeleeLayermask, QueryTriggerInteraction.Collide))
        {
            if (hit.collider.attachedRigidbody && hit.collider.attachedRigidbody.TryGetComponent(out PlayerHealthController ph))
            {
                if (hit.collider.CompareTag("Shield"))
                {
                    NetworkAudioManager.PlayNetworkedAudioClip(Random.Range(0.9f, 1.1f), 0.2f, 1, hit.point, "shieldbonk");
                    ph.player.DamageEffects(0.35f, Damage / 7.5f);
                    return;
                }
                ph.ApplyDamage(Damage, Type, hit.point, hit.normal, false);
            }
            else if (HitWorldBreakables && hit.collider.transform.parent.TryGetComponent(out WorldFeature wf) && wf.Destroyable)
            {
                wf.Attack(WorldBreakableDamage);
                PlayerInventory.SpawnNetOb(wf.Breakparticle, hit.point, Quaternion.LookRotation(hit.normal));
            }
            else if (HitWorldBreakables && hit.collider.transform.parent.TryGetComponent(out DestructibleWorldDetail det))
            {
                det.Attack(WorldBreakableDamage);
                PlayerInventory.SpawnNetOb(det.BreakParticle, hit.point, Quaternion.LookRotation(hit.normal));
            }
            else if (Sharpen)
            {
                if (int.TryParse(item.SavedData[0].ToString(), out int sharphitsleft))
                {
                    PlayerInventory.SpawnNetOb(SharpenParticle, hit.point, Quaternion.LookRotation(hit.normal));
                    sharphitsleft--;
                    item.SavedData[0] = sharphitsleft.ToString();
                    if (sharphitsleft <= 0)
                    {
                        item.ID = SharpenResult;
                        item.SavedData = new List<FixedString128Bytes>();
                        foreach (var data in ItemDatabase.GetItem(SharpenResult).BaseSavedData)
                        {
                            item.SavedData.Add(data);
                        }
                    }
                    PlayerInventory.UpdateRightHandItemData(item);
                }
            }
        }
    }

    public ItemData OnPickup(ItemData item, HandSideEnum side)
    {
        item.TempData = new()
        {
            0,
            0
        };
        return item;
    }

    public ItemData OnPutInBackpack(ItemData item)
    {
        item.TempData = new()
        {
            0,
            0
        };
        return item;
    }

    public ItemData OnRemoveFromBackpack(ItemData item)
    {
        item.TempData = new()
        {
            0,
            0
        };
        return item;
    }

    public ItemData OnUnequip(ItemData item)
    {
        item.TempData = new()
        {
            0,
            0
        };
        return item;
    }

    public ItemData OnWasCrafted(ItemData item)
    {
        item.TempData = new()
        {
            0,
            0
        };
        return item;
    }

    public ItemData OnWasStolen(ItemData item)
    {
        return item;
    }

    public ItemData OnLoaded(ItemData item, LoadedLocationEnum location)
    {
        item.TempData = new()
        {
            0,
            0
        };
        return item;
    }
}
