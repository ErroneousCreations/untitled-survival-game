using UnityEngine;
using AYellowpaper.SerializedCollections;
using System.Collections.Generic;
using Unity.Netcode;

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
}
