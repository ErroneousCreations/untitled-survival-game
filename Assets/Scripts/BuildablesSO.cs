using UnityEngine;
using AYellowpaper.SerializedCollections;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Buildables", menuName = "ScriptableObjects/Buildables")]
public class BuildablesSO : ScriptableObject
{
    [System.Serializable]
    public struct Buildable
    {
        public string itemID;
        public GameObject Prefab;
        public Mesh placementMesh;
        public Vector3 Offset, EulerRotation;
        public bool OnlyPlaceOnFloor;
    }

    public SerializedDictionary<string, Buildable> Buildables;

    private List<string> Items;

    public bool GetItemBuildable(string item)
    {
        if (Items == null)
        {
            Items = new();
            foreach (var b in Buildables)
            {
                Items.Add(b.Value.itemID);
            }
        }

        return Items.Contains(item);
    }
}
