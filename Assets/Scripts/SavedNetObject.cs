using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class SavedNetObject : NetworkBehaviour
{
    public static List<SavedNetObject> SAVEDNETOBJECTS = new();

    public string SavedObjectID;
    public NetworkList<FixedString128Bytes> SavedData;

    public System.Action OnDataLoaded;
    public UnityEngine.Events.UnityEvent DataLoaded;

    private void OnEnable()
    {
        SAVEDNETOBJECTS.Add(this);
    }

    private void OnDisable()
    {
        SAVEDNETOBJECTS.Remove(this);
    }

    public void Init(List<string> saveddata, Vector3 pos, Vector3 rot)
    {
        transform.SetPositionAndRotation(pos, Quaternion.Euler(rot));
        SavedData = new(new List<FixedString128Bytes>());
        foreach (var item in saveddata)
        {
            SavedData.Add(item);
        }
        OnDataLoaded?.Invoke();
        DataLoaded?.Invoke();
    }
}
