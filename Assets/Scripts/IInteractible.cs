using UnityEngine;
using System.Collections.Generic;

public interface IInteractible
{
    public const float PARTITIONSIZE = 10;
    public static List<IInteractible> INTERACTIBLES = new();
    public static Dictionary<Vector2Int, List<IInteractible>> PARTITIONGRID = new();

    public string GetDescription { get; }
    public float GetInteractDist { get; }
    public float GetInteractLength { get; }
    public Vector3 GetPosition { get; }

    public abstract void InteractComplete();

    public bool GetBannedFromInteracting { get; }

}
