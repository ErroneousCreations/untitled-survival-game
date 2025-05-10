using UnityEngine;
using System.Collections.Generic;
using Unity.Collections;

public enum InventoryItemStatus { InHotbar, InBackpack }
public enum HandSideEnum { Left, Right }

public interface IItemBehaviour
{
    /// <summary>
    /// Called when the item is loaded into the inventory, no matter where it is (backpack, hotbar, offhand)
    /// </summary>
    public ItemData OnLoaded(ItemData item);
    /// <summary>
    /// Called every frame on an item that is in either of your hands. side variable is which hand it is in, owner defines whether this is being called on owner or a client.
    /// </summary>
    public ItemData OnHeldUpdate(Transform hand, HandSideEnum side, ItemData item);
    /// <summary>
    /// Called every frame that an item is sitting not in one of your hands. Where it is is the status variable. Always called only on owner.
    /// </summary>
    public ItemData InInventoryUpdate(InventoryItemStatus status, ItemData item);
    /// <summary>
    /// Called right when a right hand item has been retrieved from the hotbar and is now in your hand.
    /// </summary>
    public ItemData OnEquip(ItemData item);
    /// <summary>
    /// Called right when a right hand item has been placed back into your hotbar.
    /// </summary>
    public ItemData OnUnequip(ItemData item);
    /// <summary>
    /// Called right after the item has been picked up.
    /// </summary>
    public ItemData OnPickup(ItemData item, HandSideEnum side);
    /// <summary>
    /// Called when the item is dropped just before it is actually spawned on the ground.
    /// </summary>
    public ItemData OnDrop(ItemData item, HandSideEnum side);
    /// <summary>
    /// Called when you press X and add an item to your backpack. Called after is placed in the backpack.
    /// </summary>
    public ItemData OnPutInBackpack(ItemData item);
    /// <summary>
    /// Called when you press X and remove the top item from your backpack. Called after it is retrieved.
    /// </summary>
    public ItemData OnRemoveFromBackpack(ItemData item);
    /// <summary>
    /// Called when the hands are swapped for the items in both hands. It is called after the swap, so the side means the new side it is in e.g. left hand item moves to right and this is called to say its now on the right.
    /// </summary>
    public ItemData OnHandsSwapped(ItemData item, HandSideEnum side);
    /// <summary>
    /// Called on the result of a crafting recipe after its completion
    /// </summary>
    public ItemData OnWasCrafted(ItemData item);
    /// <summary>
    /// Called when an item was stolen from the backpack by another player.
    /// </summary>
    public ItemData OnWasStolen(ItemData item);
}
