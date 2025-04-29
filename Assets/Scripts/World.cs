using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.Rendering.Universal;

public class World : NetworkBehaviour
{
    //world features spawned locally (they are NOT networked) 
    [System.Serializable]
    public struct RandomWorldFeature
    {
        public string name; //just for editor identification
        public List<WorldFeature> FeatureTypes;
        public List<Transform> SpawnPoints;
        [Tooltip("-1 is empty btw")]public List<int> SpawnPool;
    }

    //world features spawned by server (they ARE networked) 
    [System.Serializable]
    public struct RandomNetWorldFeature
    {
        public string name; //just for editor identification
        public List<NetWorldFeature> FeatureTypes;
        public List<Transform> SpawnPoints;
        [Tooltip("-1 is empty btw")] public List<int> SpawnPool;
    }

    [Header("World Features")]
    public List<RandomWorldFeature> worldFeatures; //local features
    public List<RandomNetWorldFeature> netWorldFeatures; //networked features

    //privates
    private List<List<WorldFeature>> spawnedWorldFeatures = new();
    private List<List<NetWorldFeature>> spawnedNetWorldFeatures = new();
    private System.Random rand;
    private static World instance;

    public static bool LoadingFromSave = false;

    /// <summary>
    /// Multiplier for the sway intensity of foliage mateirals (mostly on trees)
    /// </summary>
    public static void SetSwayIntensity(float intensity)
    {
        Shader.SetGlobalFloat("_GLOBALSWAYINTENSITY", intensity);
    }

    /// <summary>
    /// Multiplier for the sway speed of foliage mateirals (mostly on trees)
    /// </summary>
    public static void SetSwaySpeed(float intensity)
    {
        Shader.SetGlobalFloat("_GLOBALSWAYSPEED", intensity);
    }

    /// <summary>
    /// Direction of the sway of foliage mateirals (mostly on trees)
    /// </summary>
    public static void SetSwayDir(Vector3 dir)
    {
        Shader.SetGlobalVector("_GLOBALSWAYDIR", dir);
    }

    public static int RandomIntRange(int min, int max)
    {
        return instance.rand.Next(min, max);
    }

    /// <summary>
    /// max and min MUST BE above 0 lol
    /// </summary>
    public static float RandomFloatRange(float min, float max)
    {
        return (float)instance.rand.NextDouble() * (max - min) + min;
    }

    public static float RandomValue => (float)instance.rand.NextDouble();

    private void Awake()
    {
        instance = this;
    }

    public void Init(int seed) //todo feed savefile to load from
    {
        //initialise tree swaying stuff
        var dir = Random.insideUnitCircle.normalized;
        SetSwayDir(new Vector3(dir.x, 0, dir.y));
        SetSwayIntensity(1f);
        SetSwaySpeed(1f);

        if (!IsOwner) { return; }
        LoadingFromSave = false;
        GenerateWorld(seed == -1 ? Random.Range(0, 999999) : seed);
    }

    private void GenerateWorld(int seed)
    {
        InitSeedRPC(seed);
        DoWorldFeaturesRPC();
        DoNetWorldFeatures();
    }

    [Rpc(SendTo.Everyone)]
    private void InitSeedRPC(int seed)
    {
        rand = new System.Random(seed);
    }

    private void DoNetWorldFeatures()
    {
        for (int i = 0; i < netWorldFeatures.Count; i++)
        {
            spawnedNetWorldFeatures.Add(new());
            RandomNetWorldFeature feature = netWorldFeatures[i];
            for (int j = 0; j < feature.SpawnPoints.Count; j++)
            {
                var spawnedindex = rand.Next(0, feature.SpawnPool.Count);
                if (feature.SpawnPool[spawnedindex] != -1)
                {
                    var spawnedFeature = Instantiate(feature.FeatureTypes[feature.SpawnPool[spawnedindex]], feature.SpawnPoints[j].position, feature.SpawnPoints[j].rotation, feature.SpawnPoints[j]);
                    spawnedFeature.name = feature.name + "_" + i + "_" + j + "_" + feature.SpawnPool[spawnedindex];
                    spawnedFeature.Init(i, j, feature.SpawnPool[spawnedindex]);
                    spawnedNetWorldFeatures[i].Add(spawnedFeature);
                }
            }
        }
    }

    [Rpc(SendTo.Everyone)]
    private void DoWorldFeaturesRPC()
    {
        for (int i = 0; i < worldFeatures.Count; i++)
        {
            spawnedWorldFeatures.Add(new());
            RandomWorldFeature feature = worldFeatures[i];
            for (int j = 0; j < feature.SpawnPoints.Count; j++)
            {
                var spawnedindex = rand.Next(0, feature.SpawnPool.Count);
                if (feature.SpawnPool[spawnedindex] != -1)
                {
                    var spawnedFeature = Instantiate(feature.FeatureTypes[feature.SpawnPool[spawnedindex]], feature.SpawnPoints[j].position, feature.SpawnPoints[j].rotation, feature.SpawnPoints[j]);
                    spawnedFeature.name = feature.name + "_" + i + "_" + j + "_" + feature.SpawnPool[spawnedindex];
                    spawnedFeature.Init(i, j, feature.SpawnPool[spawnedindex]);
                    spawnedWorldFeatures[i].Add(spawnedFeature);
                }
            }
        }
    }

    public static void SpawnItemAtWorldfeature(string id, int worldfeatureid, int index)
    {
        instance.SpawnItemAtWorldfeatureRPC(id, worldfeatureid, index);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void SpawnItemAtWorldfeatureRPC(string id, int worldfeatureid, int index)
    {
        if (!spawnedWorldFeatures[worldfeatureid][index]) { return; }
        var curr = Instantiate(ItemDatabase.GetItem(id).ItemPrefab, spawnedWorldFeatures[worldfeatureid][index].transform.position, Quaternion.identity);
        curr.NetworkObject.Spawn();
        curr.InitSavedData();
    }

    public static void WorldFeatureDestroyed(int featureID, int index)
    {
        instance.WorldFeatureDestroyedRPC(featureID, index);
    }

    private void WorldFeatureDestroyedRPC(int featureID, int index)
    {
        if (spawnedWorldFeatures[featureID][index] != null)
        {
            Destroy(spawnedWorldFeatures[featureID][index].gameObject);
            spawnedWorldFeatures[featureID][index] = null;
        }
    }

    public static void WorldFeatureSwapped(int featureID, int index, int newtype)
    {
        instance.WorldFeatureSwappedRPC(featureID, index, newtype);
    }

    private void WorldFeatureSwappedRPC(int featureID, int index, int newtype)
    {
        if (spawnedWorldFeatures[featureID][index] != null)
        {
            Destroy(spawnedWorldFeatures[featureID][index].gameObject);
            spawnedWorldFeatures[featureID][index] = Instantiate(worldFeatures[featureID].FeatureTypes[newtype], worldFeatures[featureID].SpawnPoints[index].position, worldFeatures[featureID].SpawnPoints[index].rotation, worldFeatures[featureID].SpawnPoints[index]);
        }
    }

    public static void WorldFeatureDamaged(int featureID, int index, float damage)
    {
        instance.WorldFeatureDamagedRPC(featureID, index, damage);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    private void WorldFeatureDamagedRPC(int featureID, int index, float damage)
    {
        if (spawnedWorldFeatures[featureID][index] != null)
        {
            spawnedWorldFeatures[featureID][index].__RemoveHealth(damage);
        }
    }
}
