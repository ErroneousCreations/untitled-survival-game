using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

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
    private List<IInteractible> withinRange = new();

    public static Interactor instance;

    public static bool LookingAtItem => instance != null && instance.currentTarget is PickupableItem;

    private void Awake()
    {
        instance = this;
    }

    void Update()
    {
        if (GameManager.IsSpectating) { UsernameDisplay.text = ""; SelectorUI.gameObject.SetActive(false); return; }

        if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out RaycastHit hit, PlayerUsernameDisplayLength, PlayerLayer) && hit.collider.TryGetComponent(out Player p))
        {
            UsernameDisplay.text = p.GetUsername + (Player.LocalPlayer ? (GameManager.GetGameMode == GameModeEnum.TeamDeathmatch ? (p.GetIsTeamA == Player.LocalPlayer.GetIsTeamA ? "(Ally)" : "(Enemy)") : "") : "");
        }
        else { UsernameDisplay.text = ""; }

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
    }

    private void LateUpdate()
    {
        GetAllValid();
    }

    void GetAllValid()
    {
        // Destroy old UI elements
        withinRange.Clear();
        foreach (Transform child in InteractibleUIBase.transform.parent)
        {
            if (child.gameObject.activeSelf)
            {
                Destroy(child.gameObject);
            }
        }
        if(GameManager.IsSpectating) { return; }

        //TODO
        //foreach (var item in Extensions.GetNearbySpacial(transform.position, 5, IInteractible.PARTITIONGRID, IInteractible.PARTITIONSIZE))
        //{

        //}
    }

    Interactible GetBestInteractible(out NonNetInteractible nonnet)
    {
        Interactible best = null;
        NonNetInteractible bestNonNet = null;
        float closestScreenDist = float.MaxValue;

        foreach (var inter in withinRange)
        {
            if(!inter) { continue; }
            var pos = Extensions.TransformToHUDSpace(inter.transform.position);
            if (pos.z < 0) { continue; }
            var dist = (UIcentre.localPosition - pos).sqrMagnitude;
            if (dist < closestScreenDist)
            {
                closestScreenDist = dist;
                best = inter;
            }
        }

        foreach (var inter in withinRangeNonNet)
        {
            if (!inter) { continue; }
            var pos = Extensions.TransformToHUDSpace(inter.transform.position);
            if (pos.z < 0) { continue; }
            var dist = (UIcentre.localPosition - pos).sqrMagnitude;
            if (dist < closestScreenDist)
            {
                closestScreenDist = dist;
                bestNonNet = inter;
            }
        }
        nonnet = closestScreenDist < maxUIInteractionRange*maxUIInteractionRange ? bestNonNet : null;
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
            SelectorUI.transform.localPosition = Extensions.TransformToHUDSpace(currentTarget.transform.position);
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
            SelectorUI.transform.localPosition = Extensions.TransformToHUDSpace(altCurrentTarget.transform.position);
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

    public float GetHoldProgressNormalized
    {
        get
        {
            if (!currentTarget && !altCurrentTarget) return 0f;
            return Mathf.Clamp01(currentTarget ? holdProgress / currentTarget.InteractLength : (altCurrentTarget ? holdProgress / altCurrentTarget.InteractLength : 0));
        }
    }

    public Interactible GetCurrentTarget() => currentTarget;
}