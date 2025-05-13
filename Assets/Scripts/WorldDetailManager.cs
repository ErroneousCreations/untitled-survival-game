using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class WorldDetailManager : NetworkBehaviour
{
    private static Dictionary<int, DestructibleWorldDetail> objectsByID = new();

    public static void RegisterObject(DestructibleWorldDetail obj)
    {
        objectsByID[obj.ObjectID] = obj;
    }

    public static void UnregisterObject(DestructibleWorldDetail obj)
    {
        objectsByID.Remove(obj.ObjectID);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    private void DoDamageRPC(int objectID, float damage)
    {
        if (objectsByID.TryGetValue(objectID, out var obj))
        {
            obj._RemoveHealth(damage);
        }
    }

    public static void DoDamage(int objectID, float damage)
    {
        instance.DoDamageRPC(objectID, damage);
    }

    private static WorldDetailManager instance;

    private void Awake()
    {
        if (instance == null) instance = this;
    }
}
