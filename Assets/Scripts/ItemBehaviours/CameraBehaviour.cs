using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using EditorAttributes;
using Unity.Collections;

[CreateAssetMenu(fileName = "New CameraBehaviour", menuName = "ItemBehaviours/CameraBehaviour")]
public class CameraBehaviour : ScriptableObject, IItemBehaviour
{


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
        if (item.SavedData.Count > 0 && int.TryParse(item.SavedData[0].ToString(), out int shotsleft) && shotsleft > 0 && Player.GetCanStand && side == HandSideEnum.Right && !PlayerInventory.GetIsBusy && Input.GetMouseButtonDown(0))
        {
            Player.LocalPlayer.StartCoroutine(CaptureCoroutine());
            item.SavedData[0] = (shotsleft - 1).ToString();
            NetworkAudioManager.PlayNetworkedAudioClip(Random.Range(0.9f, 1.1f), 0.2f, 1, Camera.main.transform.position, "cameraclick");
            PlayerInventory.AddItemBusyTime(0.1f);
        }
        return item;
    }

    private IEnumerator CaptureCoroutine()
    {
        UIManager.ToggleCanvasEnabled(false);
        Player.LocalPlayer.pm.ToggleViewmodel(false);
        yield return null;
        ScreenCapture.CaptureScreenshot(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop) + "/" + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".png", 1);
        yield return null;
        Player.LocalPlayer.pm.ToggleViewmodel(true);
        UIManager.ToggleCanvasEnabled(true);

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
