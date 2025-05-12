using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public static class Extensions
{
    public static float GetEstimatedRTT => (NetworkManager.Singleton.LocalTime - NetworkManager.Singleton.ServerTime).TimeAsFloat;

    public static ulong LocalClientID => NetworkManager.Singleton.LocalClientId;

    public static string UniqueIdentifier => Application.isEditor ? "EDITOR" : SystemInfo.deviceUniqueIdentifier;

    // Fisher-Yates shuffle
    public static void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[j], list[i]) = (list[i], list[j]);
        }
    }

    public static int GetDeterministicStringIndex(string input, int range)
    {
        if (range <= 0) return 0;

        int hash = 0;
        foreach (char c in input)
            hash += c;

        // Make sure it's positive and within range
        return Mathf.Abs(hash % range);
    }

    public static Vector3 GetSurvivalSpawnPoint
    {
        get
        {
            var points = GameObject.FindGameObjectsWithTag("SVSpawn");
            if (points.Length == 0) { return Vector3.zero; }
            var result = points[Random.Range(0, points.Length)].transform.position;
            return result;
        }
    }

    public static Vector3 GetDeathmatchSpawnPoint
    {
        get
        {
            var points = GameObject.FindGameObjectsWithTag("DMSpawn");
            if (points.Length == 0) { return Vector3.zero; }
            return points[Random.Range(0, points.Length)].transform.position;
        }
    }

    public static Vector3 GetTeamDeathmatchSpawnPoint
    {
        get
        {
            var points = GameObject.FindGameObjectsWithTag("TDMSpawn");
            if (points.Length == 0) { return Vector3.zero; }
            return points[Random.Range(0, points.Length)].transform.position;
        }
    }

    public static LayerMask DefaultMeleeLayermask => LayerMask.GetMask("Player", "Creature", "Terrain");
    public static LayerMask DefaultThrownHitregLayermask => LayerMask.GetMask("Player", "Creature");

}
