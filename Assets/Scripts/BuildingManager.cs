using UnityEngine;
using System.Collections.Generic;

public class BuildingManager : MonoBehaviour
{
    public MeshFilter buildingPlacementMesh;
    public float BuildRange;

    private static BuildingManager instance;

    private BuildablesSO.Buildable currentBuildable;
    private bool currentlyPlacing;
    private float currRotation;

    private void Awake()
    {
        instance = this;
    }

    public static void SetPlacement(BuildablesSO.Buildable current)
    {
        instance.currentBuildable = current;
        instance.currentlyPlacing = true;
        instance.currRotation = 0;
    }

    private void Update()
    {
        if (currentlyPlacing)
        {
            bool didhit = Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out RaycastHit hit, BuildRange, Extensions.TerrainMask);
            buildingPlacementMesh.gameObject.SetActive(didhit);
            buildingPlacementMesh.mesh = currentBuildable.placementMesh;
            buildingPlacementMesh.transform.position = hit.point + Quaternion.LookRotation(Quaternion.AngleAxis(90, Vector3.forward) * hit.normal, hit.normal) * currentBuildable.Offset;
        }
    }
}
