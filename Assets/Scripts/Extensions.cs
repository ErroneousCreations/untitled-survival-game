using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public static class Extensions
{
    public static float GetEstimatedRTT => (NetworkManager.Singleton.LocalTime - NetworkManager.Singleton.ServerTime).TimeAsFloat;

    public static ulong LocalClientID => NetworkManager.Singleton.LocalClientId;

    public static string UniqueIdentifier => Application.isEditor ? "EDITOR" : SystemInfo.deviceUniqueIdentifier;

    // Fisher-Yates shuffle
    public static void ShuffleList<T>(List<T> ts)
    {
        var count = ts.Count;
        var last = count - 1;
        for (var i = 0; i < last; ++i)
        {
            var r = UnityEngine.Random.Range(i, count);
            var tmp = ts[i];
            ts[i] = ts[r];
            ts[r] = tmp;
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

    public static int DMSPAWNINDEX = 0;

    public static void RandomiseDeathmatchSpawnIndex()
    {
        var points = GameObject.FindGameObjectsWithTag("DMSpawn");
        DMSPAWNINDEX = Random.Range(0, points.Length);
    }

    public static Vector3 GetDeathmatchSpawnPoint
    {
        get
        {
            var points = GameObject.FindGameObjectsWithTag("DMSpawn");
            if (points.Length == 0) { return Vector3.zero; }
            if (DMSPAWNINDEX >= points.Length) { DMSPAWNINDEX = 0; }
            int ind = DMSPAWNINDEX;
            DMSPAWNINDEX++;
            return points[ind].transform.position;
        }
    }

    public static int TEAMASPAWNINDEX = -1;
    public static int TEAMBSPAWNINDEX = -1;

    public static void RandomiseTeamSpawnIndexes()
    {
        var points = GameObject.FindGameObjectsWithTag("TDMSpawn");
        TEAMASPAWNINDEX = Random.Range(0, points.Length);
        TEAMBSPAWNINDEX = Random.Range(0, points.Length);
        while (TEAMASPAWNINDEX == TEAMBSPAWNINDEX)
        {
            TEAMBSPAWNINDEX = Random.Range(0, points.Length);
        }
    }

    public static Vector3 GetTeamASpawnPoint
    {
        get
        {
            var points = GameObject.FindGameObjectsWithTag("TDMSpawn");
            if (points.Length == 0) { return Vector3.zero; }
            var rand = Random.insideUnitCircle * 2;
            return points[TEAMASPAWNINDEX].transform.position + new Vector3(rand.x, 0, rand.y);
        }
    }

    public static Vector3 GetTeamBSpawnPoint
    {
        get
        {
            var points = GameObject.FindGameObjectsWithTag("TDMSpawn");
            if (points.Length == 0) { return Vector3.zero; }
            var rand = Random.insideUnitCircle * 2;
            return points[TEAMBSPAWNINDEX].transform.position + new Vector3(rand.x, 0, rand.y);
        }
    }

    public static LayerMask DefaultMeleeLayermask => LayerMask.GetMask("Player", "Creature", "Terrain");
    public static LayerMask DefaultThrownHitregLayermask => LayerMask.GetMask("Player", "Creature");

}
