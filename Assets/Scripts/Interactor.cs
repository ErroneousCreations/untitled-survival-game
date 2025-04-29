using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;

public class Interactor : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float maxUIInteractionRange = 10; //i really dont kbnow what this should be set to lol
    public KeyCode interactKey = KeyCode.F;

    [Header("UI")]
    public Canvas canvas;
    public Transform UIcentre;
    public GameObject InteractibleUIBase; // optional: to instantiate one per interactible
    public Transform SelectorUI;
    public TMP_Text InteractorTitle;
    public Slider InteractionSlider;
    public TMP_Text UsernameDisplay;
    public float PlayerUsernameDisplayLength = 1.5f;
    public LayerMask PlayerLayer;
    public Transform followSelector;

    private bool interacted = false;
    private Interactible currentTarget;
    private NonNetInteractible altCurrentTarget;
    private float holdProgress = 0f;
    private List<Interactible> withinRange = new();
    private List<NonNetInteractible> withinRangeNonNet = new();

    public static Interactor instance;

    public static bool LookingAtItem => instance != null && instance.currentTarget is PickupableItem;

    private void Awake()
    {
        instance = this;
    }

    void Update()
    {
        followSelector.localPosition = currentTarget ? SelectorUI.transform.localPosition : Vector3.zero;
        Interactible closest = GetBestInteractible(out NonNetInteractible nonnetclosest);
        if (nonnetclosest) {  //this means the closest one at all is a NON NET
            if (nonnetclosest != altCurrentTarget || (!altCurrentTarget && currentTarget))
            {
                altCurrentTarget = nonnetclosest;
                currentTarget = null;
                holdProgress = 0f;
                interacted = false;
            }
        }
        else
        {
            if (closest != currentTarget || (!currentTarget && altCurrentTarget))
            {
                currentTarget = closest;
                altCurrentTarget = null;
                holdProgress = 0f;
                interacted = false;
            }
        }
        
        UpdateInteraction();

        if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out RaycastHit hit, PlayerUsernameDisplayLength, PlayerLayer) && hit.collider.TryGetComponent(out Player p))
        {
            UsernameDisplay.text = p.GetUsername;
        }
        else { UsernameDisplay.text = ""; } 
    }

    private void LateUpdate()
    {
        GetAllValid();
    }

    void GetAllValid()
    {
        // Destroy old UI elements
        withinRange.Clear();
        withinRangeNonNet.Clear();
        foreach (Transform child in InteractibleUIBase.transform.parent)
        {
            if (child.gameObject.activeSelf)
            {
                Destroy(child.gameObject);
            }
        }

        foreach (var inter in Interactible.INTERACTIBLES)
        {
            if(!inter.GetBannedFromInteracting && (inter.transform.position - transform.position).sqrMagnitude < inter.InteractDistance * inter.InteractDistance && Player.LocalPlayer.ph.isConscious.Value)
            {
                withinRange.Add(inter);
                var pos = TransformToHUDSpace(inter.transform.position);
                if(pos.z < 0) { continue; }
                var ob = Instantiate(InteractibleUIBase, InteractibleUIBase.transform.parent);
                ob.SetActive(true);
                ob.transform.localPosition = pos;
            }
        }

        foreach (var inter in NonNetInteractible.INTERACTIBLES)
        {
            if (!inter.GetBannedFromInteracting && (inter.transform.position - transform.position).sqrMagnitude < inter.InteractDistance * inter.InteractDistance && Player.LocalPlayer.ph.isConscious.Value)
            {
                withinRangeNonNet.Add(inter);
                var pos = TransformToHUDSpace(inter.transform.position);
                if (pos.z < 0) { continue; }
                var ob = Instantiate(InteractibleUIBase, InteractibleUIBase.transform.parent);
                ob.SetActive(true);
                ob.transform.localPosition = pos;
            }
        }
    }

    Interactible GetBestInteractible(out NonNetInteractible nonnet)
    {
        Interactible best = null;
        NonNetInteractible bestNonNet = null;
        float closestScreenDist = float.MaxValue;

        foreach (var inter in withinRange)
        {
            if(!inter) { continue; }
            var dist = (UIcentre.localPosition - TransformToHUDSpace(inter.transform.position)).sqrMagnitude;
            if (dist < closestScreenDist)
            {
                closestScreenDist = dist;
                best = inter;
            }
        }

        foreach (var inter in withinRangeNonNet)
        {
            if (!inter) { continue; }
            var dist = (UIcentre.localPosition - TransformToHUDSpace(inter.transform.position)).sqrMagnitude;
            if (dist < closestScreenDist)
            {
                closestScreenDist = dist;
                bestNonNet = inter;
            }
        }
        nonnet = bestNonNet;
        return closestScreenDist < maxUIInteractionRange*maxUIInteractionRange ? best: null;
    }

    void UpdateInteraction()
    {
        //im very sorry
        if (!altCurrentTarget)
        {
            if (!currentTarget)
            {
                SelectorUI.gameObject.SetActive(false);
                return;
            }
            SelectorUI.transform.localPosition = TransformToHUDSpace(currentTarget.transform.position);
            SelectorUI.gameObject.SetActive(true);
            InteractorTitle.text = currentTarget.Description;
            InteractionSlider.value = GetHoldProgressNormalized;
            if (currentTarget is PickupableItem && !Player.GetLocalPlayerInvBusy)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    Player.LocalPlayer.pi.PickupItemL((currentTarget as PickupableItem).ConvertToItemData);
                    (currentTarget as PickupableItem).DestroyItem();
                    return;
                }
                else if (Input.GetMouseButtonDown(1))
                {
                    Player.LocalPlayer.pi.PickupItemR((currentTarget as PickupableItem).ConvertToItemData);
                    (currentTarget as PickupableItem).DestroyItem();
                    return;
                }
            }
            else
            {
                if (Input.GetKey(interactKey))
                {
                    if (!interacted) { holdProgress += Time.deltaTime; }
                    if (holdProgress >= currentTarget.InteractLength)
                    {
                        interacted = true;
                        currentTarget.InteractComplete();
                        holdProgress = 0f; // reset to allow repeat, or set to -1 to disable further interaction
                    }
                }
                else
                {
                    interacted = false;
                    if (holdProgress > 0) { holdProgress -= Time.deltaTime * 0.75f; }
                }
            }
        }
        else
        {
            SelectorUI.transform.localPosition = TransformToHUDSpace(altCurrentTarget.transform.position);
            SelectorUI.gameObject.SetActive(true);
            InteractorTitle.text = altCurrentTarget.Description;
            InteractionSlider.value = GetHoldProgressNormalized;
            if (Input.GetKey(interactKey))
            {
                if (!interacted) { holdProgress += Time.deltaTime; }
                if (holdProgress >= altCurrentTarget.InteractLength)
                {
                    interacted = true;
                    altCurrentTarget.InteractComplete();
                    holdProgress = 0f; // reset to allow repeat, or set to -1 to disable further interaction
                }
            }
            else
            {
                interacted = false;
                if (holdProgress > 0) { holdProgress -= Time.deltaTime * 0.75f; }
            }
        }
    }

    private Vector3 TransformToHUDSpace(Vector3 worldSpace)
    {
        var scalefactor = canvas.transform.localScale.x;
        var screenSpace = Camera.main.WorldToScreenPoint(worldSpace);
        return (screenSpace - new Vector3(Screen.width / 2, Screen.height / 2)) / scalefactor;
    }

    public float GetHoldProgressNormalized
    {
        get
        {
            if (!currentTarget) return 0f;
            return Mathf.Clamp01(holdProgress / currentTarget.InteractLength);
        }
    }

    public Interactible GetCurrentTarget() => currentTarget;
}