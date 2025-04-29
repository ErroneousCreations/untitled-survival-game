using Unity.Netcode;
using UnityEngine;

public static class Extensions
{
    public static float GetEstimatedRTT => (NetworkManager.Singleton.LocalTime - NetworkManager.Singleton.ServerTime).TimeAsFloat;

    public static ulong LocalClientID => NetworkManager.Singleton.LocalClientId;

    public static string UniqueIdentifier => Application.isEditor ? "EDITOR" : SystemInfo.deviceUniqueIdentifier;

    public static int GetDeterministicStringIndex(string input, int range)
    {
        if (range <= 0) return 0;

        int hash = 0;
        foreach (char c in input)
            hash += c;

        // Make sure it's positive and within range
        return Mathf.Abs(hash % range);
    }

    public static Vector3 GetCloseSpawnPoint
    {
        get
        {
            var points = GameObject.FindGameObjectsWithTag("CloseSpawn");
            if (points.Length == 0) { return Vector3.zero; }
            return points[Random.Range(0, points.Length)].transform.position;
        }
    }

    public static Vector3 GetMidSpawnPoint
    {
        get
        {
            var points = GameObject.FindGameObjectsWithTag("MidSpawn");
            if (points.Length == 0) { return Vector3.zero; }
            return points[Random.Range(0, points.Length)].transform.position;
        }
    }

    public static Vector3 GetFarSpawnPoint
    {
        get
        {
            var points = GameObject.FindGameObjectsWithTag("FarSpawn");
            if (points.Length == 0) { return Vector3.zero; }
            return points[Random.Range(0, points.Length)].transform.position;
        }
    }

    public static LayerMask DefaultMeleeLayermask => LayerMask.GetMask("Player", "Creature", "Terrain");
    public static LayerMask DefaultThrownHitregLayermask => LayerMask.GetMask("Player", "Creature");

}
