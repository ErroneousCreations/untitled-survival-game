using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.Rendering.Universal;
using Unity.Collections;
using Unity.Jobs;

public class World : NetworkBehaviour
{
    //world features spawned locally (they are NOT networked) 
    [System.Serializable]
    public struct RandomWorldFeature
    {
        public string name; //just for editor identification
        public List<WorldFeature> FeatureTypes;
        public List<Transform> SpawnPoints;
        public bool RandomiseRotation;
        [Tooltip("-1 is empty btw")]public List<int> SpawnPool;
    }

    //world features spawned by server (they ARE networked) 
    [System.Serializable]
    public struct RandomNetWorldFeature
    {
        public string name; //just for editor identification
        public List<NetWorldFeature> FeatureTypes;
        public List<Transform> SpawnPoints;
        public bool RandomiseRotation;
        [Tooltip("-1 is empty btw")] public List<int> SpawnPool;
    }

    //for grass or something, still worldfeatures
    [System.Serializable]
    public struct RaycastedRandomWorldFeature
    {
        public string name; //just for editor identification
        public List<WorldFeature> FeatureTypes;
        public List<Transform> SpawnCentres;
        public Vector2 SpawnRectangleSize;
        public int Amount;
        public string RequiredTag;
        public LayerMask Mask;
        public bool RandomiseRotation;
        [Tooltip("-1 is empty btw")] public List<int> SpawnPool;
    }

    [Header("World Features")]
    public List<RandomWorldFeature> worldFeatures; //local features
    public List<RandomNetWorldFeature> netWorldFeatures; //networked features
    public List<RaycastedRandomWorldFeature> raycastedWorldFeatures;

    //privates
    private List<List<WorldFeature>> spawnedWorldFeatures = new();
    private List<List<NetWorldFeature>> spawnedNetWorldFeatures = new();
    private System.Random rand;
    private static World instance;
    private int seed;

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

    public static int CurrentSeed => instance.seed;
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
        DoRaycastedWorldFeaturesRPC();
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
                    Random.InitState(seed + (int)feature.SpawnPoints[j].position.x + (int)feature.SpawnPoints[j].position.y); //this should be deterministic
                    var spawnedFeature = Instantiate(feature.FeatureTypes[feature.SpawnPool[spawnedindex]], feature.SpawnPoints[j].position, feature.RandomiseRotation ? Quaternion.Euler(0, Random.Range(0, 360), 0) : feature.SpawnPoints[j].rotation, feature.SpawnPoints[j]);
                    spawnedFeature.name = feature.name + "_" + i + "_" + j + "_" + feature.SpawnPool[spawnedindex];
                    spawnedFeature.Init(i, j, feature.SpawnPool[spawnedindex]);
                    spawnedNetWorldFeatures[i].Add(spawnedFeature);
                }
                else { spawnedWorldFeatures[i].Add(null); }
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
                    Random.InitState(seed + (int)feature.SpawnPoints[j].position.x + (int)feature.SpawnPoints[j].position.y); //this should be deterministic
                    var spawnedFeature = Instantiate(feature.FeatureTypes[feature.SpawnPool[spawnedindex]], feature.SpawnPoints[j].position, feature.RandomiseRotation ? Quaternion.Euler(0, Random.Range(0, 360), 0) : feature.SpawnPoints[j].rotation, feature.SpawnPoints[j]);
                    spawnedFeature.name = feature.name + "_" + i + "_" + j + "_" + feature.SpawnPool[spawnedindex];
                    spawnedFeature.Init(i, j, feature.SpawnPool[spawnedindex]);
                    spawnedWorldFeatures[i].Add(spawnedFeature);
                }
                else { spawnedWorldFeatures[i].Add(null); }
            }
        }
    }

    [Rpc(SendTo.Everyone)]
    private void DoRaycastedWorldFeaturesRPC()
    {
        var initiallength = spawnedWorldFeatures.Count;
        for (int i = 0; i < raycastedWorldFeatures.Count; i++)
        {
            spawnedWorldFeatures.Add(new());
            RaycastedRandomWorldFeature feature = raycastedWorldFeatures[i];

            //prepare the raycastcommand stuff
            var results = new NativeArray<RaycastHit>(feature.Amount * feature.SpawnCentres.Count, Allocator.TempJob);
            var commands = new NativeArray<RaycastCommand>(feature.Amount * feature.SpawnCentres.Count, Allocator.TempJob);

            //get the start points
            int k = 0;
            foreach (var point in feature.SpawnCentres)
            {
                for (int j = 0; j < feature.Amount; j++)
                {
                    var pos = point.TransformPoint(new Vector3(RandomFloatRange(0, feature.SpawnRectangleSize.x * 2) - feature.SpawnRectangleSize.x, 0, RandomFloatRange(0, feature.SpawnRectangleSize.y * 2) - feature.SpawnRectangleSize.y));
                    commands[k] = new RaycastCommand(pos, Vector3.down, new QueryParameters(feature.Mask));
                    k++;
                }
            }

            //do the job
            JobHandle handle = RaycastCommand.ScheduleBatch(commands, results, feature.Amount * feature.SpawnCentres.Count, 1, default);
            handle.Complete();

            //actually spawn them
            bool needstag = feature.RequiredTag != "";
            k = 0;
            foreach (var point in feature.SpawnCentres)
            {
                for (int j = 0; j < feature.Amount; j++)
                {
                    if (results[k].collider==null || (needstag && !results[k].collider.CompareTag(feature.RequiredTag))) { continue; }

                    var spawnedindex = rand.Next(0, feature.SpawnPool.Count);
                    if(feature.SpawnPool[spawnedindex] != -1)
                    {
                        Random.InitState(seed + (int)results[k].point.x + (int)results[k].point.y);
                        var spawnedFeature = Instantiate(feature.FeatureTypes[feature.SpawnPool[spawnedindex]], results[k].point, Quaternion.identity, point);
                        if (feature.RandomiseRotation) { spawnedFeature.transform.localRotation = Quaternion.AngleAxis(Random.Range(0, 360), results[k].normal); }
                        else { spawnedFeature.transform.up = results[k].normal; }
                        spawnedFeature.name = feature.name + "_" + i + "_" + j + "_" + feature.SpawnPool[spawnedindex];
                        spawnedFeature.Init(initiallength + i, k, feature.SpawnPool[spawnedindex]);
                        spawnedWorldFeatures[initiallength + i].Add(spawnedFeature);
                    }
                    else { spawnedWorldFeatures[initiallength + i].Add(null); }
                    k++;
                }
            }

            // Dispose the buffers
            results.Dispose();
            commands.Dispose();
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
        var curr = Instantiate(ItemDatabase.GetItem(id).ItemPrefab, spawnedWorldFeatures[worldfeatureid][index].transform.position + Vector3.up*0.5f, Quaternion.identity);
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

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        if (raycastedWorldFeatures==null || raycastedWorldFeatures.Count<=0) { return; }
        foreach (var feature in raycastedWorldFeatures)
        {
            foreach (var spawn in feature.SpawnCentres)
            {
                Gizmos.matrix = spawn.localToWorldMatrix;
                Gizmos.DrawCube(Vector3.zero, new Vector3(feature.SpawnRectangleSize.x, 0.25f, feature.SpawnRectangleSize.y));
            }
        }
    }
}
