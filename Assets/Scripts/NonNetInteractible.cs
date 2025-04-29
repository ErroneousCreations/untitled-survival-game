using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

public class NonNetInteractible : MonoBehaviour
{
    public static List<NonNetInteractible> INTERACTIBLES = new();

    public string Description;
    public float InteractLength = 1, InteractDistance = 1;
    [SerializeField] protected UnityEvent Interacted;
    [SerializeField] protected UnityEvent<ulong> Interacted_GetUser;
    public System.Action OnInteracted;
    public System.Action<ulong> OnInteracted_GetUser;
    public System.Action OnUpdate;
    public bool InteractOnce;
    private bool interacted;

    [HideInInspector] public bool Banned;

    public bool GetBannedFromInteracting => (InteractOnce && interacted) || Banned;

    private void OnEnable()
    {
        INTERACTIBLES.Add(this);
    }

    private void OnDisable()
    {
        INTERACTIBLES.Remove(this);
    }

    private void Update()
    {
        OnUpdate?.Invoke();
        VirtUpdate();
    }

    protected virtual void VirtUpdate()
    {

    }

    public virtual void InteractComplete()
    {
        interacted = true;
        Interacted?.Invoke();
        OnInteracted?.Invoke();
        Interacted_GetUser?.Invoke(NetworkManager.Singleton.LocalClientId);
        OnInteracted_GetUser?.Invoke(NetworkManager.Singleton.LocalClientId);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, InteractDistance);
    }
}