using UnityEngine;
using System.Collections.Generic;

public class SavedObject : MonoBehaviour
{
    public static List<SavedObject> SAVEDOBJECTS = new();

    public string SavedObjectID;
    public List<string> SavedData;

    public System.Action OnDataLoaded;
    public System.Action<List<string>> OnDataLoaded_Data;
    public UnityEngine.Events.UnityEvent DataLoaded;
    public UnityEngine.Events.UnityEvent<List<string>> DataLoaded_Data;

    /// <summary>
    /// Gets if the Init function was called by SavingManager, never called when not loading from a save
    /// </summary>
    public bool GetLoaded { get; private set; } = false;

    private void OnEnable()
    {
        SAVEDOBJECTS.Add(this);
    }

    private void OnDisable()
    {
        SAVEDOBJECTS.Remove(this);
    }

    public void Init(List<string> saveddata, Vector3 pos, Vector3 rot)
    {
        GetLoaded = true;
        transform.SetPositionAndRotation(pos, Quaternion.Euler(rot));
        SavedData = new(saveddata);
        OnDataLoaded?.Invoke();
        DataLoaded?.Invoke();
        DataLoaded_Data?.Invoke(saveddata);
        OnDataLoaded_Data?.Invoke(saveddata);
    }
}
