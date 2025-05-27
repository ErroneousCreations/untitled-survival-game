using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

public class BuildingManager : NetworkBehaviour
{
    public MeshFilter buildingPlacementMesh;
    public MeshRenderer buildingPlacementRend;
    public float BuildRange, PlacementRotSensitivity = 20;
    public BuildablesSO buildables;
    public Material validMat, invalidMat;
    private static BuildingManager instance;

    private BuildablesSO.Buildable currentBuildable;
    private bool currentlyPlacing;
    private float currRotation;
    private Vector3 currHitPos, currHitNormal;

    public static bool PlacementValid;

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

    public static void StopPlacement()
    {
        instance.currentlyPlacing = false;
        instance.buildingPlacementMesh.gameObject.SetActive(false);
    }

    public static void Place()
    {
        if(!instance.currentlyPlacing) return;
        var rot = Quaternion.FromToRotation(Vector3.up, instance.currHitNormal) * Quaternion.Euler(0f, instance.currRotation, 0f) * Quaternion.Euler(instance.currentBuildable.EulerRotation);
        var pos = instance.currHitPos + (rot * instance.currentBuildable.Offset);
        instance.PlaceStructureRPC(pos, rot, instance.currentBuildable.itemID);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    private void PlaceStructureRPC(Vector3 pos, Quaternion rot, string placed)
    {
        Instantiate(instance.buildables.Buildables[placed].Prefab, pos, rot);
    }

    private void Update()
    {
        if (currentlyPlacing)
        {
            if (!Player.LocalPlayer || !Player.GetCanStand) { StopPlacement(); return; }
            buildingPlacementRend.material = PlacementValid ? validMat : invalidMat;    
            bool didhit = Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out RaycastHit hit, BuildRange, Extensions.PlacementMask);
            currHitNormal = didhit ? hit.normal : Vector3.up;
            currHitPos = didhit ? hit.point : Camera.main.transform.position + Camera.main.transform.forward * BuildRange;
            buildingPlacementMesh.gameObject.SetActive(didhit);
            buildingPlacementMesh.mesh = currentBuildable.placementMesh;
            var rot = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, currRotation, 0f) * Quaternion.Euler(currentBuildable.EulerRotation);
            var pos = hit.point + (rot * currentBuildable.Offset);
            if (didhit) { buildingPlacementMesh.transform.SetPositionAndRotation(pos, rot); }
            currRotation = (currRotation + Input.mouseScrollDelta.y * PlacementRotSensitivity * Time.deltaTime) % 360;
            var overlapping = Physics.OverlapBox(buildingPlacementMesh.transform.position, buildingPlacementMesh.mesh.bounds.extents, buildingPlacementMesh.transform.rotation, Extensions.BannedConstructionMask);
            PlacementValid = didhit && (!currentBuildable.OnlyPlaceOnFloor || Vector3.Angle(hit.normal, Vector3.up) < 20) && overlapping.Length<=0;
        }
    }
}
