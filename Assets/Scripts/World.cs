using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using Unity.Jobs;
using System.Collections;
using Unity.AI.Navigation;

public class World : NetworkBehaviour
{
    //world features spawned locally (they are NOT networked) 
    [System.Serializable]
    public struct RandomWorldFeature
    {
        public string name; //just for editor identification
        public List<WorldFeature> FeatureTypes;
        public Transform spawnsParent;
        public bool RandomiseRotation;
        [Tooltip("-1 is empty btw")]public List<int> SpawnPool;
    }

    //world features spawned by server (they ARE networked) 
    [System.Serializable]
    public struct RandomNetWorldFeature
    {
        public string name; //just for editor identification
        public List<NetWorldFeature> FeatureTypes;
        public Transform spawnsParent;
        public bool RandomiseRotation;
        [Tooltip("-1 is empty btw")] public List<int> SpawnPool;
    }

    //for grass or something, still worldfeatures
    [System.Serializable]
    public struct RaycastedRandomWorldFeature
    {
        public string name; //just for editor identification
        public List<WorldFeatureSavePos> FeatureTypes; //saves raycasting later by saving the position
        public List<Transform> SpawnCentres;
        public Vector2 SpawnRectangleSize;
        public int Amount;
        public string RequiredTag;
        public LayerMask Mask;
        public bool RandomiseRotation;
        [Tooltip("-1 is empty btw")] public List<int> SpawnPool;
    }

    [System.Serializable]
    public struct SpawnedWorldObject
    {
        public string SpawnLocationIndex;
        public List<GameObject> spawnPool;
        public Vector2Int SpawnAmount;
        public bool RandomiseRotation;
        public bool OnlyBeforeSave, OnlyForServer;
    }

    [Header("World Features")]
    public List<RandomWorldFeature> worldFeatures; //local features
    public List<RandomNetWorldFeature> netWorldFeatures; //networked features
    public List<RaycastedRandomWorldFeature> raycastedWorldFeatures;
    [Header("Navigation")]
    public NavMeshSurface[] navMeshSurfaces; //navmesh surfaces to bake when the world is generated
    [Header("Spawning")]
    public List<SpawnedWorldObject> Spawned;

    //privates
    private List<List<WorldFeature>> spawnedWorldFeatures = new();
    private List<List<NetWorldFeature>> spawnedNetWorldFeatures = new();
    private Dictionary<string, List<Transform>> spawnLocations = new();
    private System.Random rand;
    private static World instance;
    private int seed;

    public static bool LoadingFromSave = false;
    public static bool Loading = false;

    public static Vector3 WindDirection { get; private set; }
    public static float WindIntensity { get; private set; }

    public static void RegisterSpawnLocation(string id, Transform pos)
    {
        if (instance.spawnLocations.ContainsKey(id)) { instance.spawnLocations[id].Add(pos); }
        else { instance.spawnLocations.Add(id, new() { pos }); }
    }

    /// <summary>
    /// Multiplier for the sway intensity of foliage mateirals (mostly on trees)
    /// </summary>
    public static void SetSwayIntensity(float intensity)
    {
        Shader.SetGlobalFloat("_GLOBALSWAYINTENSITY", intensity);
        WindIntensity = intensity;
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
        WindDirection = dir;
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
        GameManager.SetWorld(this);
    }

    public static List<List<WorldFeature>> GetWorldFeatures => instance.spawnedWorldFeatures;
    public static List<List<NetWorldFeature>> GetNetWorldFeatures => instance.spawnedNetWorldFeatures;

    /// <summary>
    /// Wont work for raycasted ones im not bothered lol
    /// </summary>
    public static WorldFeature GetWorldFeaturePrefab(int wftype, int typeindex)
    {
        return instance.worldFeatures[wftype].FeatureTypes[typeindex];
    }

    public static NetWorldFeature GetNetWorldFeaturePrefab(int wftype, int typeindex)
    {
        return instance.netWorldFeatures[wftype].FeatureTypes[typeindex];
    }

    public void Init(int seed)
    {
        if(seed == -1) { seed = Random.Range(0, 999999); }
        InitSeedRPC(seed);

        //initialise tree swaying stuff
        Random.InitState(seed);
        var dir = Random.insideUnitCircle.normalized;
        SetSwayDir(new Vector3(dir.x, 0, dir.y));
        SetSwayIntensity(1f);
        SetSwaySpeed(1f);

        if (!IsOwner) { return; }
        LoadingFromSave = false;
        GenerateWorld();
    }

    /// <summary>
    /// called on client from the saving manager
    /// </summary>
    public void Init(int seed, string[] worldfeatures)
    {
        //initialise tree swaying stuff
        Random.InitState(seed);
        var dir = Random.insideUnitCircle.normalized;
        SetSwayDir(new Vector3(dir.x, 0, dir.y));
        SetSwayIntensity(1f);
        SetSwaySpeed(1f);

        LoadingFromSave = true;
        rand = new System.Random(seed);
        Loading = true;
        MenuController.ToggleLoadingScreen(true);

        LoadWorldFeatures(worldfeatures);
        LoadRaycastedWorldFeatures(worldfeatures);

        StartCoroutine(DoWorldSpawned());
    }

    public void LoadNetWorldFeatures(string[] loadedfeatures)
    {
        if (!IsOwner) { return; }
        for (int i = 0; i < netWorldFeatures.Count; i++)
        {
            var split = loadedfeatures[i].Split('|');
            spawnedNetWorldFeatures.Add(new());
            for (int j = 0; j < split.Length; j++)
            {
                var wfsplit = split[j].Split(',');
                int typeindex = int.Parse(wfsplit[0]);
                if (typeindex == -1) { spawnedNetWorldFeatures[i].Add(null); continue; }
                var savedataarray = new string[wfsplit.Length - 1];
                System.Array.Copy(wfsplit, 1, savedataarray, 0, wfsplit.Length - 1);
                string savedata = string.Join(",", savedataarray);
                if (worldFeatures[i].FeatureTypes[typeindex] != null)
                {
                    Random.InitState(seed + (int)netWorldFeatures[i].spawnsParent.GetChild(j).position.x + (int)netWorldFeatures[i].spawnsParent.GetChild(j).position.z); //this should be deterministic
                    var spawnedFeature = Instantiate(netWorldFeatures[i].FeatureTypes[typeindex], netWorldFeatures[i].spawnsParent.GetChild(j).position, netWorldFeatures[i].spawnsParent.GetChild(j).rotation);
                    spawnedFeature.NetworkObject.Spawn();
                    spawnedFeature.Init(i, j, typeindex);
                    spawnedFeature.LoadFromSavedData(savedata);
                    spawnedFeature.name = worldFeatures[i].name + "_" + j + "_" + typeindex;
                    spawnedNetWorldFeatures[i].Add(spawnedFeature);
                }
            }
        }
    }

    private void LoadWorldFeatures(string[] loadedfeatures)
    {
        for (int i = 0; i < worldFeatures.Count; i++)
        {
            var split = loadedfeatures[i].Split('|');
            spawnedWorldFeatures.Add(new());
            for (int j = 0; j < split.Length; j++)
            {
                var wfsplit = split[j].Split(',');
                int typeindex = int.Parse(wfsplit[0]);
                if (typeindex == -1) { spawnedWorldFeatures[i].Add(null); continue; }
                var savedataarray = new string[wfsplit.Length - 1];
                System.Array.Copy(wfsplit, 1, savedataarray, 0, wfsplit.Length - 1);
                string savedata = string.Join(",", savedataarray);
                if (worldFeatures[i].FeatureTypes[typeindex] != null)
                {
                    Random.InitState(seed + (int)worldFeatures[i].spawnsParent.GetChild(j).position.x + (int)worldFeatures[i].spawnsParent.GetChild(j).position.z); //this should be deterministic
                    var spawnedFeature = Instantiate(worldFeatures[i].FeatureTypes[typeindex], worldFeatures[i].spawnsParent.GetChild(j).position, worldFeatures[i].spawnsParent.GetChild(j).rotation, worldFeatures[i].spawnsParent.GetChild(j));
                    spawnedFeature.Init(i, j, typeindex);
                    spawnedFeature.LoadFromSavedData(savedata);
                    spawnedFeature.name = worldFeatures[i].name + "_" + j + "_" + typeindex;
                    spawnedWorldFeatures[i].Add(spawnedFeature);
                }
            }
        }
    }

    private void LoadRaycastedWorldFeatures(string[] loadedfeatures)
    {
        for (int i = worldFeatures.Count, x = 0; i < loadedfeatures.Length; i++, x++)
        {
            var split = loadedfeatures[i].Split('|');
            spawnedWorldFeatures.Add(new());
            for (int j = 0; j < split.Length; j++)
            {
                var wfsplit = split[j].Split(',');
                int typeindex = int.Parse(wfsplit[0]);
                if (typeindex == -1) { spawnedWorldFeatures[i].Add(null); continue; }
                var savedataarray = new string[wfsplit.Length - 1];
                System.Array.Copy(wfsplit, 1, savedataarray, 0, wfsplit.Length - 1);
                string savedata = string.Join(",", savedataarray);
                if (raycastedWorldFeatures[x].FeatureTypes[typeindex] != null)
                {
                    var spawnindex = j % raycastedWorldFeatures[x].SpawnCentres.Count;
                    var spawnedFeature = Instantiate(raycastedWorldFeatures[x].FeatureTypes[typeindex], raycastedWorldFeatures[x].SpawnCentres[spawnindex].position, raycastedWorldFeatures[x].SpawnCentres[spawnindex].rotation, raycastedWorldFeatures[x].SpawnCentres[spawnindex]);
                    spawnedFeature.Init(i, j, typeindex);
                    spawnedFeature.LoadFromSavedData(savedata);
                    spawnedFeature.name = raycastedWorldFeatures[x].name + "_" + j + "_" + typeindex;
                    spawnedWorldFeatures[i].Add(spawnedFeature);
                }
            }
        }
    }

    private void GenerateWorld()
    {
        DoNormalWorldFeaturesRPC();
        StartCoroutine(GenerateNetWorldFeaturesCoroutine());
    }

    private IEnumerator GenerateNavMeshesCoroutine()
    {
        for (int i = 0; i < navMeshSurfaces.Length; i++)
        {
            navMeshSurfaces[i].BuildNavMesh();
            yield return null;
        }
    }

    [Rpc(SendTo.Everyone)]
    private void InitSeedRPC(int seed)
    {
        Loading = true;
        MenuController.ToggleLoadingScreen(true);
        rand = new System.Random(seed);
        this.seed = seed;
        spawnLocations = new();

        Random.InitState(seed);
        var dir = Random.insideUnitCircle.normalized;
        SetSwayDir(new Vector3(dir.x, 0, dir.y));
        SetSwayIntensity(1f);
        SetSwaySpeed(1f);
    }

    private IEnumerator GenerateNetWorldFeaturesCoroutine()
    {
        yield return new WaitForSeconds(0.75f);
        for (int i = 0; i < netWorldFeatures.Count; i++)
        {
            spawnedNetWorldFeatures.Add(new());
            RandomNetWorldFeature feature = netWorldFeatures[i];
            for (int j = 0; j < feature.spawnsParent.childCount; j++)
            {
                var spawnedindex = rand.Next(0, feature.SpawnPool.Count);
                if (feature.SpawnPool[spawnedindex] != -1)
                {
                    Random.InitState(seed + (int)feature.spawnsParent.GetChild(j).position.x + (int)feature.spawnsParent.GetChild(j).position.z); //this should be deterministic
                    var spawnedFeature = Instantiate(feature.FeatureTypes[feature.SpawnPool[spawnedindex]], feature.spawnsParent.GetChild(j).position, feature.RandomiseRotation ? Quaternion.Euler(0, Random.Range(0, 360), 0) : feature.spawnsParent.GetChild(j).rotation);
                    spawnedFeature.name = feature.name + "_" + i + "_" + j + "_" + feature.SpawnPool[spawnedindex];
                    spawnedFeature.Init(i, j, feature.SpawnPool[spawnedindex]);
                    spawnedNetWorldFeatures[i].Add(spawnedFeature);
                }
                else { spawnedWorldFeatures[i].Add(null); }
            }
            yield return null;
        }
    }

    private IEnumerator GenerateWorldFeaturesCoroutine()
    {
        for (int i = 0; i < worldFeatures.Count; i++)
        {
            spawnedWorldFeatures.Add(new());
            RandomWorldFeature feature = worldFeatures[i];
            for (int j = 0; j < feature.spawnsParent.childCount; j++)
            {
                var spawnedindex = rand.Next(0, feature.SpawnPool.Count);
                if (feature.SpawnPool[spawnedindex] != -1)
                {
                    Random.InitState(seed + (int)feature.spawnsParent.GetChild(j).position.x + (int)feature.spawnsParent.GetChild(j).position.z); //this should be deterministic
                    var spawnedFeature = Instantiate(feature.FeatureTypes[feature.SpawnPool[spawnedindex]], feature.spawnsParent.GetChild(j).position, feature.RandomiseRotation ? Quaternion.Euler(0, Random.Range(0, 360), 0) : feature.spawnsParent.GetChild(j).rotation, feature.spawnsParent.GetChild(j));
                    spawnedFeature.name = feature.name + "_" + i + "_" + j + "_" + feature.SpawnPool[spawnedindex];
                    spawnedFeature.Init(i, j, feature.SpawnPool[spawnedindex]);
                    spawnedWorldFeatures[i].Add(spawnedFeature);
                }
                else { spawnedWorldFeatures[i].Add(null); }
            }
            yield return null;
        }
    }

    [Rpc(SendTo.Everyone)]
    private void DoNormalWorldFeaturesRPC()
    {
        StartCoroutine(DoNormalWorldFeatures());
    }

    private IEnumerator DoNormalWorldFeatures()
    {
        yield return StartCoroutine(GenerateWorldFeaturesCoroutine()); //wait for it to be done btw
        yield return null;
        yield return StartCoroutine(GenerateRaycastedWorldFeaturesCoroutine());
        if (IsOwner)
        {
            yield return null;
            yield return StartCoroutine(GenerateNavMeshesCoroutine());
            StartCoroutine(DoWorldSpawned());
        }
        else { StartCoroutine(DoWorldSpawned()); }
    }

    private IEnumerator DoWorldSpawned()
    {
        var k = 0;
        foreach (var spawn in Spawned)
        {
            if (!spawnLocations.ContainsKey(spawn.SpawnLocationIndex)) { k++; continue; }
            if(!NetworkManager.Singleton.IsServer && spawn.OnlyForServer) { k++; continue; }
            if (LoadingFromSave && spawn.OnlyBeforeSave) { k++; continue; }
            Random.InitState(seed + k);
            var amount = Random.Range(spawn.SpawnAmount.x, spawn.SpawnAmount.y + 1);
            List<int> usedindexes = new();
            for (int i = 0; i < amount; i++)
            {
                if(usedindexes.Count >= spawnLocations[spawn.SpawnLocationIndex].Count) { break; }
                var spawnindex = Random.Range(0, spawnLocations[spawn.SpawnLocationIndex].Count);
                while (usedindexes.Contains(spawnindex)) { spawnindex = Random.Range(0, spawnLocations[spawn.SpawnLocationIndex].Count); }
                usedindexes.Add(spawnindex);
                Instantiate(spawn.spawnPool[Random.Range(0, spawn.spawnPool.Count)], spawnLocations[spawn.SpawnLocationIndex][spawnindex].position, spawn.RandomiseRotation ? Quaternion.Euler(0, Random.Range(0, 360), 0) : spawnLocations[spawn.SpawnLocationIndex][spawnindex].rotation);
            }
            k++;
            yield return null;
        }

        Loading = false;
        MenuController.ToggleLoadingScreen(false);
    }

    private IEnumerator GenerateRaycastedWorldFeaturesCoroutine()
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
            while (!handle.IsCompleted)
            {
                yield return null;
            }

            //actually spawn them
            bool needstag = feature.RequiredTag != "";
            k = 0;
            foreach (var point in feature.SpawnCentres)
            {
                for (int j = 0; j < feature.Amount; j++)
                {
                    if (results[k].collider == null || (needstag && !results[k].collider.CompareTag(feature.RequiredTag))) { continue; }

                    var spawnedindex = rand.Next(0, feature.SpawnPool.Count);
                    if (feature.SpawnPool[spawnedindex] != -1)
                    {
                        Random.InitState(seed + (int)results[k].point.x + (int)results[k].point.z);
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

    public static void SpawnItemAtWorldfeature(string id, int worldfeatureid, int index, Vector3 localoffset = default)
    {
        instance.SpawnItemAtWorldfeatureRPC(id, worldfeatureid, index, localoffset);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void SpawnItemAtWorldfeatureRPC(string id, int worldfeatureid, int index, Vector3 offset)
    {
        if (!spawnedWorldFeatures[worldfeatureid][index]) { return; }
        var curr = Instantiate(ItemDatabase.GetItem(id).ItemPrefab, spawnedWorldFeatures[worldfeatureid][index].transform.TransformPoint(offset), Quaternion.identity);
        curr.NetworkObject.Spawn();
        curr.InitSavedData();
    }

    public static void WorldFeatureDestroyed(int featureID, int index)
    {
        instance.WorldFeatureDestroyedRPC(featureID, index);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
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

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    private void WorldFeatureSwappedRPC(int featureID, int index, int newtype)
    {
        if (spawnedWorldFeatures[featureID][index] != null)
        {
            Destroy(spawnedWorldFeatures[featureID][index].gameObject);
            spawnedWorldFeatures[featureID][index] = Instantiate(worldFeatures[featureID].FeatureTypes[newtype], worldFeatures[featureID].spawnsParent.GetChild(index).position, worldFeatures[featureID].spawnsParent.GetChild(index).rotation, worldFeatures[featureID].spawnsParent.GetChild(index));
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
        if(raycastedWorldFeatures != null && raycastedWorldFeatures.Count > 0)
        {
            Gizmos.color = Color.green;
            foreach (var feature in raycastedWorldFeatures)
            {
                foreach (var spawn in feature.SpawnCentres)
                {
                    Gizmos.matrix = spawn.localToWorldMatrix;
                    Gizmos.DrawCube(Vector3.zero, new Vector3(feature.SpawnRectangleSize.x * 2, 0.25f, feature.SpawnRectangleSize.y * 2));
                }
            }
        }

        if (worldFeatures != null && worldFeatures.Count > 0)
        {
            Gizmos.color = Color.red;
            foreach (var feature in worldFeatures)
            {
                foreach (Transform spawn in feature.spawnsParent)
                {
                    Gizmos.matrix = spawn.localToWorldMatrix;
                    var rend = feature.FeatureTypes[0].GetComponentInChildren<MeshRenderer>();
                    var offset = rend.transform.localPosition;
                    var extents = rend.bounds.extents;
                    var centre = rend.bounds.center;
                    Gizmos.DrawWireCube(offset + centre, extents * 2);
                }
            }
        }

        if(netWorldFeatures != null && netWorldFeatures.Count > 0)
        {
            Gizmos.color = Color.blue;
            foreach (var feature in netWorldFeatures)
            {
                foreach (Transform spawn in feature.spawnsParent)
                {
                    Gizmos.matrix = spawn.localToWorldMatrix;
                    var rend = feature.FeatureTypes[0].GetComponentInChildren<MeshRenderer>();
                    var offset = rend.transform.localPosition;
                    var extents = rend.bounds.extents;
                    var centre = rend.bounds.center;
                    Gizmos.DrawWireCube(offset + centre, extents * 2);
                }
            }
        }
    }
}
