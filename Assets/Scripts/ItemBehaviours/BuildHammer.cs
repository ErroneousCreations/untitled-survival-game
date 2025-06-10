using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

[CreateAssetMenu(fileName = "New AxeBehaviour", menuName = "ItemBehaviours/BuildHammer")]
public class BuildHammer : ScriptableObject, IItemBehaviour
{
    [Header("Melee Animation")]
    public float AttackLength = 0.75f;
    public float ForwardTime = 0.15f;
    public float AttackForwardAmount = 0.75f;

    [Header("Building")]
    public BuildablesSO buildables;
    public int AnimationAmount = 3;

    [Header("Melee Stats")]
    public float Damage;
    public float Stun;
    public DamageType Type;
    [Tooltip("From camera, ray length")] public float Range = 1f;
    public float WorldBreakableDamage;

    public ItemData InInventoryUpdate(InventoryItemStatus status, ItemData item)
    {
        item.TempData = new List<float> { 0 };
        return item;
    }

    public ItemData OnDrop(ItemData item, HandSideEnum side)
    {
        if(side == HandSideEnum.Right && item.TempData[0] == 1)
        {
            BuildingManager.StopPlacement();
        }
        return item;
    }

    public ItemData OnEquip(ItemData item)
    {
        item.TempData = new List<float> { 0 };
        return item;
    }

    public ItemData OnHandsSwapped(ItemData item, HandSideEnum side)
    {
        if (side == HandSideEnum.Left && item.TempData[0] == 1)
        {
            BuildingManager.StopPlacement();
        }
        item.TempData = new List<float> { 0 };
        return item;
    }

    public ItemData OnHeldUpdate(Transform hand, HandSideEnum side, ItemData item)
    {
        if (item.TempData[0] == 0)
        {
            if (Player.GetCanStand && side == HandSideEnum.Right && !PlayerInventory.GetIsBusy && Input.GetMouseButtonDown(0))
            {
                PlayerInventory.AddItemBusyTime(AttackLength);
                Player.LocalPlayer.StartCoroutine(AttackAnimation(hand));
            }

            if (Player.GetCanStand && side == HandSideEnum.Right && !PlayerInventory.GetIsBusy && Input.GetMouseButtonDown(1) && PlayerInventory.GetLeftHandItem.IsValid && buildables.Buildables.ContainsKey(PlayerInventory.GetLeftHandItem.ID.ToString()))
            {
                item.TempData[0] = 1;
                BuildingManager.SetPlacement(buildables.Buildables[PlayerInventory.GetLeftHandItem.ID.ToString()]);
            }
        }
        else if(item.TempData[0] == 1)
        {
            if (!PlayerInventory.GetLeftHandItem.IsValid || !buildables.Buildables.ContainsKey(PlayerInventory.GetLeftHandItem.ID.ToString()) || Input.GetMouseButtonDown(0) || !Player.GetCanStand) { BuildingManager.StopPlacement(); item.TempData[0] = 0; return item; }

            if (Player.GetCanStand && side == HandSideEnum.Right && !PlayerInventory.GetIsBusy && Input.GetMouseButtonDown(1))
            {
                PlayerInventory.AddItemBusyTime(AttackLength*AnimationAmount);
                Player.LocalPlayer.StartCoroutine(BuildAnimation(hand));
                item.TempData[0] = 2; // Reset the temp data to stop building placement
            }
        }
        else
        {
            if (!PlayerInventory.GetLeftHandItem.IsValid || !buildables.Buildables.ContainsKey(PlayerInventory.GetLeftHandItem.ID.ToString()) || Input.GetMouseButtonDown(0) || !Player.GetCanStand) { Player.LocalPlayer.StopCoroutine(nameof(BuildAnimation)); BuildingManager.StopPlacement(); item.TempData[0] = 0; return item; }
            if (!PlayerInventory.GetIsBusy) { item.TempData[0] = 0; } // Reset the temp data if not busy
            if (!BuildingManager.PlacementValid) { Player.LocalPlayer.StopCoroutine(nameof(BuildAnimation)); hand.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity); BuildingManager.StopPlacement(); item.TempData[0] = 0; return item; }
        }

        if (!Player.GetCanStand) { Player.LocalPlayer.StopCoroutine(nameof(BuildAnimation)); Player.LocalPlayer.StopCoroutine(nameof(AttackAnimation)); hand.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity); }

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

    IEnumerator BuildAnimation(Transform hand)
    {
        for (int i = 0; i < AnimationAmount; i++)
        {
            hand.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            hand.DOLocalRotate(new Vector3(90, 0, 0), ForwardTime);
            hand.DOLocalMove(new Vector3(0, 0, AttackForwardAmount), ForwardTime);
            if (!BuildingManager.PlacementValid) { PlayerInventory.AddItemBusyTime(0.25f); ResetHandPos(hand); yield break; }
            yield return new WaitForSeconds(ForwardTime);
            if (i == AnimationAmount-1 && BuildingManager.PlacementValid) { Construct(); }
            hand.DOLocalMove(Vector3.zero, AttackLength - ForwardTime);
            hand.DOLocalRotate(Vector3.zero, AttackLength - ForwardTime);
            if (!BuildingManager.PlacementValid) { PlayerInventory.AddItemBusyTime(0.25f); ResetHandPos(hand); yield break; }
            yield return new WaitForSeconds(AttackLength - ForwardTime);
        }
    }

    private void ResetHandPos(Transform hand)
    {
        hand.DOLocalMove(Vector3.zero, 0.25f);
        hand.DOLocalRotate(Vector3.zero, 0.25f);
    }

    private void Construct()
    {
        BuildingManager.Place();
        BuildingManager.StopPlacement();
        PlayerInventory.DeleteLefthandItem();
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
            else if (hit.collider.transform.parent && hit.collider.transform.parent.TryGetComponent(out WorldFeature wf) && wf.Destroyable)
            {
                wf.Attack(WorldBreakableDamage);
                PlayerInventory.SpawnNetOb(wf.Breakparticle, hit.point, Quaternion.LookRotation(hit.normal));
            }
            else if (hit.collider.transform.parent && hit.collider.transform.parent.TryGetComponent(out DestructibleWorldDetail det))
            {
                det.Attack(WorldBreakableDamage);
                PlayerInventory.SpawnNetOb(det.BreakParticle, hit.point, Quaternion.LookRotation(hit.normal));
            }
        }
    }

    public ItemData OnPickup(ItemData item, HandSideEnum side)
    {
        item.TempData = new List<float> {  0 };
        return item;
    }

    public ItemData OnPutInBackpack(ItemData item)
    {
        if (item.TempData[0] == 1) { BuildingManager.StopPlacement(); }
        return item;
    }

    public ItemData OnRemoveFromBackpack(ItemData item)
    {
        item.TempData = new List<float> { 0 };
        return item;
    }

    public ItemData OnUnequip(ItemData item)
    {
        if (item.TempData[0] == 1) { BuildingManager.StopPlacement(); }
        return item;
    }

    public ItemData OnWasCrafted(ItemData item)
    {
        item.TempData = new List<float> { 0 };
        return item;
    }

    public ItemData OnWasStolen(ItemData item)
    {
        return item;
    }

    public ItemData OnLoaded(ItemData item, LoadedLocationEnum location)
    {
        item.TempData = new List<float> { 0 };
        return item;
    }
}
