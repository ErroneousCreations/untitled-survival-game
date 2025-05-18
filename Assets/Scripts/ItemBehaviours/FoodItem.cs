using DG.Tweening;
using UnityEngine;
using System.Collections;

[CreateAssetMenu(fileName = "New FoodItem", menuName = "ItemBehaviours/FoodItem")]
public class FoodItem : ScriptableObject, IItemBehaviour
{
    public float VibrateIntensity, EatLength, HungerGiven;

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
        item.TempData = new()
        {
            0,
            0
        };
        return item;
    }

    public ItemData OnHeldUpdate(Transform hand, HandSideEnum side, ItemData item)
    {
        if (Player.GetCanStand && side == HandSideEnum.Right && !PlayerInventory.GetIsBusy && Input.GetMouseButtonDown(0))
        {
            PlayerInventory.AddItemBusyTime(0.1f);
            item.TempData[1] = 1;
        }

        if (Player.GetCanStand && side == HandSideEnum.Right && item.TempData[1] > 0)
        {
            item.TempData[0] += Time.deltaTime;
            PlayerInventory.AddItemBusyTime(0.1f);
            hand.SetLocalPositionAndRotation(Vector3.Lerp(Vector3.zero, new Vector3(-3.15f, 0.903f, -0.94f) + Random.insideUnitSphere * VibrateIntensity, item.TempData[0]*4 / EatLength), Quaternion.identity);
        }

        if (Player.GetCanStand && side == HandSideEnum.Right && item.TempData[1] > 0 && (Input.GetMouseButtonUp(0) || item.TempData[0] >= EatLength))
        {
            PlayerInventory.AddItemBusyTime(0.25f);
            ResetPosition(hand);
            bool ate = item.TempData[0] >= EatLength;
            item.TempData[0] = 0;
            item.TempData[1] = 0;
            if (ate) { Player.LocalPlayer.ph.AddHunger(HungerGiven); item.SavedData[0] = (int.Parse(item.SavedData[0].ToString()) - 1).ToString(); if (int.Parse(item.SavedData[0].ToString()) <= 0) { PlayerInventory.DeleteRighthandItem(); return ItemData.Empty; } }
            
        }

        if (!Player.GetCanStand) { item.TempData[0] = 0; item.TempData[1] = 0; hand.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity); }
        return item;

    }

    void ResetPosition(Transform hand)
    {
        hand.DOLocalRotate(Vector3.zero, 0.2f);
        hand.DOLocalMove(Vector3.zero, 0.2f);
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
