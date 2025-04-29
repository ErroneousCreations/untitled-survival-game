using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

public class Interactible : NetworkBehaviour
{
    public static List<Interactible> INTERACTIBLES = new();

    public string Description;
    public float InteractLength = 1, InteractDistance = 1;
    [SerializeField] protected UnityEvent InteractedLocal, InteractedClient, InteractedServer;
    [SerializeField] protected UnityEvent<ulong> InteractedLocal_GetUser, InteractedClient_GetUser, InteractedServer_GetUser;
    public System.Action OnInteractedLocal, OnInteractedClient, OnInteractedServer;
    public System.Action<ulong> OnInteractedLocal_GetUser, OnInteractedClient_GetUser, OnInteractedServer_GetUser;
    public System.Action OnUpdate;
     public bool DestroyOnInteract, InteractOnce, DoRpcs = true;
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
        InteractedLocal?.Invoke();
        OnInteractedLocal?.Invoke();
        InteractedLocal_GetUser?.Invoke(NetworkManager.LocalClientId);
        OnInteractedLocal_GetUser?.Invoke(NetworkManager.LocalClientId);
        if (!DoRpcs) { return; }
        Interact_ServerRPC(NetworkManager.LocalClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    protected virtual void Interact_ServerRPC(ulong user)
    {
        if (DestroyOnInteract) { NetworkObject.Despawn(true); }
        InteractedServer?.Invoke();
        InteractedServer_GetUser?.Invoke(user);
        OnInteractedServer?.Invoke();
        OnInteractedServer_GetUser?.Invoke(user);
        Interact_ClientRPC(user);
        interacted = true;
    }

    [ClientRpc]
    protected virtual void Interact_ClientRPC(ulong user)
    {
        if (IsServer) { return; }
        interacted = true;
        InteractedClient?.Invoke();
        InteractedClient_GetUser?.Invoke(user);
        OnInteractedClient?.Invoke();
        OnInteractedClient_GetUser?.Invoke(user);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, InteractDistance);
    }

    public void InteractFunc_SpawnItem(string id)
    {
        if (!IsServer) { return; }
        var curr = Instantiate(ItemDatabase.GetItem(id).ItemPrefab, transform.position, transform.rotation);
        curr.NetworkObject.Spawn();
        curr.InitSavedData();
    }
}