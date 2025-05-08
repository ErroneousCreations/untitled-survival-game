using UnityEngine;
using System.Collections.Generic;

public class SavedObject : MonoBehaviour
{
    public static List<SavedObject> SAVEDOBJECTS = new();

    public string SavedObjectID;
    public List<string> SavedData;

    public System.Action OnDataLoaded;
    public UnityEngine.Events.UnityEvent DataLoaded;

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
        transform.SetPositionAndRotation(pos, Quaternion.Euler(rot));
        SavedData = new(saveddata);
        OnDataLoaded?.Invoke();
        DataLoaded?.Invoke();
    }
}
