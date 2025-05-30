using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

public class Interactible : NetworkBehaviour,IInteractible
{

    public string Description;
    public float InteractLength = 1, InteractDistance = 1;
    [SerializeField] protected UnityEvent InteractedLocal, InteractedClient, InteractedServer;
    [SerializeField] protected UnityEvent<ulong> InteractedLocal_GetUser, InteractedClient_GetUser, InteractedServer_GetUser;
    public System.Action OnInteractedLocal, OnInteractedClient, OnInteractedServer;
    public System.Action<ulong> OnInteractedLocal_GetUser, OnInteractedClient_GetUser, OnInteractedServer_GetUser;
    public System.Action OnUpdate;
    public bool DestroyOnInteract, InteractOnce, DoRpcs = true;
    private bool interacted;
    private Vector3 lastPosition;
    private Vector2Int currCell;

    [HideInInspector] public bool Banned;

    public bool GetBannedFromInteracting { get { return (InteractOnce && interacted) || Banned; } }
    public string GetDescription { get => Description; }
    public float GetInteractDist { get => InteractDistance; }
    public float GetInteractLength { get => InteractLength; }
    public Vector3 GetPosition { get => transform.position; }

    private void OnEnable()
    {
        IInteractible.INTERACTIBLES.Add(this);
        lastPosition = transform.position;
        currCell = Extensions.GetSpacialCell(transform.position, IInteractible.PARTITIONSIZE);
        if (!IInteractible.PARTITIONGRID.ContainsKey(currCell)) { IInteractible.PARTITIONGRID.Add(currCell, new() { this }); }
        else { IInteractible.PARTITIONGRID[currCell].Add(this); }
        Enabled();
    }

    protected virtual void Enabled()
    {

    }

    private void OnDisable()
    {
        IInteractible.INTERACTIBLES.Remove(this);
        IInteractible.PARTITIONGRID[currCell].Remove(this);
        Disabled();
    }

    protected virtual void Disabled()
    {

    }

    private void Update()
    {
        OnUpdate?.Invoke();
        VirtUpdate();

        if(Mathf.Abs(transform.position.x - lastPosition.x) > 0.1f || Mathf.Abs(transform.position.y - lastPosition.y) > 0.1f || Mathf.Abs(transform.position.z - lastPosition.z) > 0.1f)
        {
            lastPosition = transform.position;
            var newcell = Extensions.GetSpacialCell(transform.position, IInteractible.PARTITIONSIZE);
            if (newcell != currCell)
            {
                IInteractible.PARTITIONGRID[currCell].Remove(this);
                currCell = newcell;
                if (!IInteractible.PARTITIONGRID.ContainsKey(newcell)) { IInteractible.PARTITIONGRID.Add(newcell, new() { this }); }
                else { IInteractible.PARTITIONGRID[newcell].Add(this); }
            }
        }
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