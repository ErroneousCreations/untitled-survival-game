using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using EditorAttributes;
using Unity.Collections;

[CreateAssetMenu(fileName = "New CameraBehaviour", menuName = "ItemBehaviours/CameraBehaviour")]
public class CameraBehaviour : ScriptableObject, IItemBehaviour
{
    public Camera photoTaker;
    public int W = 1600, H = 1200, D = 24;

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
            TakePhoto(hand.GetChild(0).position + hand.forward*0.1f + hand.up * 0.07f, hand.forward);
            item.SavedData[0] = (shotsleft - 1).ToString();
            PlayerInventory.SpawnNetOb("cameraflash", hand.GetChild(0).position + hand.forward * 0.1f + hand.up * 0.07f, Quaternion.LookRotation(hand.forward), 0.1f);
            NetworkAudioManager.PlayNetworkedAudioClip(Random.Range(0.9f, 1.1f), 0.2f, 1, Camera.main.transform.position, "cameraclick");
            PlayerInventory.AddItemBusyTime(0.1f);
        }
        return item;
    }

    private void TakePhoto(Vector3 pos, Vector3 forward)
    {
        var camera = Instantiate(photoTaker, pos, Quaternion.LookRotation(forward));
        var rt = new RenderTexture(W, H, D);
        Texture2D screenshot = new Texture2D(W, H, TextureFormat.RGB24, false);
        camera.targetTexture = rt;
        RenderTexture.active = camera.targetTexture;
        camera.Render();
        screenshot.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        screenshot.Apply();
        camera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(camera.gameObject);
        Destroy(rt);
        ApplyBasicFilter(ref screenshot);
        var bytes = screenshot.EncodeToPNG();
        var path = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop) + "/" + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".png";
        System.IO.File.WriteAllBytes(path, bytes);
    }

    void ApplyBasicFilter(ref Texture2D texture)
    {
        int width = texture.width;
        int height = texture.height;

        Color32[] pixels = texture.GetPixels32();
        Vector2 center = new Vector2(width / 2f, height / 2f);
        float maxDist = center.magnitude;

        System.Random rng = new System.Random();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                Color32 c = pixels[index];

                // === Convert to float for manipulation ===
                float r = c.r / 255f;
                float g = c.g / 255f;
                float b = c.b / 255f;

                // === Desaturate slightly ===
                float gray = (r + g + b) / 3f;
                r = Mathf.Lerp(gray, r, 0.6f);
                g = Mathf.Lerp(gray, g, 0.6f);
                b = Mathf.Lerp(gray, b, 0.6f);

                // === Warm Tint ===
                r += 0.05f;
                g += 0.02f;

                // === Vignette ===
                float dist = Vector2.Distance(center, new Vector2(x, y));
                float vignette = Mathf.Clamp01(dist / maxDist);
                float vignetteStrength = 0.4f;
                float darken = 1f - vignette * vignetteStrength;

                r *= darken;
                g *= darken;
                b *= darken;

                // === Film Grain ===
                float grain = (float)rng.NextDouble() * 0.05f - 0.025f; // ±2.5%
                r = Mathf.Clamp01(r + grain);
                g = Mathf.Clamp01(g + grain);
                b = Mathf.Clamp01(b + grain);

                // === Convert back to Color32 ===
                pixels[index] = new Color(r, g, b);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
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
