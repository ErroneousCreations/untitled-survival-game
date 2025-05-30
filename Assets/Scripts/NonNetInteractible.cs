using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

public class NonNetInteractible : MonoBehaviour, IInteractible
{
    public string Description;
    public float InteractLength = 1, InteractDistance = 1;
    [SerializeField] protected UnityEvent Interacted;
    [SerializeField] protected UnityEvent<ulong> Interacted_GetUser;
    public System.Action OnInteracted;
    public System.Action<ulong> OnInteracted_GetUser;
    public System.Action OnUpdate;
    public bool InteractOnce;
    private bool interacted;
    private Vector3 lastPos;
    private Vector2Int currCell;

    [HideInInspector] public bool Banned;

    public bool GetBannedFromInteracting => (InteractOnce && interacted) || Banned;

    public string GetDescription => Description;

    public float GetInteractDist => InteractDistance;

    public float GetInteractLength => InteractLength;
    public Vector3 GetPosition { get => transform.position; }


    private void OnEnable()
    {
        IInteractible.INTERACTIBLES.Add(this);
        lastPos = transform.position;
        currCell = Extensions.GetSpacialCell(transform.position, IInteractible.PARTITIONSIZE);
        if (!IInteractible.PARTITIONGRID.ContainsKey(currCell)) { IInteractible.PARTITIONGRID.Add(currCell, new() { this }); }
        else { IInteractible.PARTITIONGRID[currCell].Add(this); }
    }

    private void OnDisable()
    {
        IInteractible.INTERACTIBLES.Remove(this);
        IInteractible.PARTITIONGRID[currCell].Remove(this);
    }

    private void Update()
    {
        OnUpdate?.Invoke();
        VirtUpdate();

        if (Mathf.Abs(transform.position.x - lastPos.x) > 0.1f || Mathf.Abs(transform.position.y - lastPos.y) > 0.1f || Mathf.Abs(transform.position.z - lastPos.z) > 0.1f)
        {
            lastPos = transform.position;
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
        Interacted.Invoke();
        OnInteracted?.Invoke();
        Interacted_GetUser.Invoke(NetworkManager.Singleton.LocalClientId);
        OnInteracted_GetUser?.Invoke(NetworkManager.Singleton.LocalClientId);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, InteractDistance);
    }
}