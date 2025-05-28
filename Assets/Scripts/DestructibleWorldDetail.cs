using UnityEngine;
using EditorAttributes;
using DG.Tweening;
using System.Collections.Generic;
using Unity.Netcode;

public class DestructibleWorldDetail : MonoBehaviour
{
    public static float ELECTRICITY_FALLOFF = 0.9f;

    public int ObjectID { get; private set; }

    [ReadOnly, SerializeField]private float CurrHealth;
    [SerializeField]private List<LootDropStruct> drops;
    public float Health;
    public string BreakParticle;
    public SavedObject mySaver;
    public bool IsBuilding;
    [ShowField(nameof(IsBuilding))] public float BaseElectricity;
    [ShowField(nameof(IsBuilding))] public bool Conductive;
    [ShowField(nameof(isconductive))] public Transform ElectricEffect;
    private float currElectricity;
    private int connWfTypeIndex, connWfGenIndex;
    private enum ConnectedTypeEnum { None, WorldFeature, DWD }
    private ConnectedTypeEnum connected;
    private int connBuildingUID;

    private float breaktime = 0;
    private bool isconductive => IsBuilding && Conductive;

    public float GetCurrElectricity => currElectricity;

    public void SetConnection(string conn)
    {
        var split = conn.Split('~');
        if (split.Length <= 1)
        { //then its connected to a DWD
            connected = ConnectedTypeEnum.DWD;
            connBuildingUID = int.Parse(split[0]);
        }
        else
        {
            connected = ConnectedTypeEnum.WorldFeature;
            connWfTypeIndex = int.Parse(split[0]);
            connWfGenIndex = int.Parse(split[1]);
        }

        mySaver.SavedData[1] = conn;
    }

    private void Start()
    {
        CurrHealth = Health;
        mySaver.OnDataLoaded_Data += SetHealth;
        ObjectID = Extensions.HashVector3ToInt(transform.position);
        WorldDetailManager.RegisterObject(this);
        breaktime = Random.Range(0.05f, 0.2f);
    }

    void SetHealth(List<string> data)
    {
        if (mySaver && data.Count > 0)
        {
            CurrHealth = float.Parse(data[0]);
            if (data.Count > 1) {
                var split = data[1].Split('~');
                if (split.Length <= 1) { //then its connected to a DWD
                    connected = ConnectedTypeEnum.DWD;
                    connBuildingUID = int.Parse(split[0]);
                } 
                else
                {
                    connected = ConnectedTypeEnum.WorldFeature;
                    connWfTypeIndex = int.Parse(split[0]);
                    connWfGenIndex = int.Parse(split[1]);
                }
            }
            else { connected = ConnectedTypeEnum.None; }
        }
    }

    private void OnDestroy()
    {
        WorldDetailManager.UnregisterObject(this);
    }

    /// <summary>
    /// ONLY CALL THIS FROM WORLDDETAILMANAGER not from anywhere else. thats why it has an underscore!!!
    /// </summary>
    public void _RemoveHealth(float damage)
    {
        CurrHealth -= damage;
        if (mySaver) { mySaver.SavedData[0] = System.Math.Round(CurrHealth, 2).ToString(); }
        if (CurrHealth <= 0)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                for (int i = 0; i < drops.Count; i++)
                {
                    if (Random.value <= drops[i].Chance)
                    {
                        var curr = Instantiate(ItemDatabase.GetItem(drops[i].ItemID).ItemPrefab, transform.position + Vector3.up * 0.75f, Quaternion.identity);
                        curr.NetworkObject.Spawn();
                        curr.InitSavedData();
                    }
                }
            }
            Destroy(gameObject);
        }
        //else
        //{
        //    transform.DOPunchScale(new Vector3(0.5f, 0.5f, 0.5f), 0.2f);
        //}
    }

    public void Attack(float damage)
    {
        WorldDetailManager.DoDamage(ObjectID, damage);
    }

    private void Update()
    {
        if (!IsBuilding) { return; }
        if (connected == ConnectedTypeEnum.None) { currElectricity = BaseElectricity; return; }

        //electricity
        currElectricity = Conductive ? (BaseElectricity + (connected == ConnectedTypeEnum.WorldFeature && WorldDetailManager.TryGetOb(connBuildingUID, out var det) ? det.BaseElectricity*ELECTRICITY_FALLOFF : 0)) : 0;
        if (ElectricEffect)
        {
            ElectricEffect.gameObject.SetActive(currElectricity > 0);
            ElectricEffect.localScale = Vector3.one * currElectricity;
        }

        //breaking check on the server
        if(!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer) { return; }

        breaktime -= Time.deltaTime;
        if (breaktime <= 0)
        {
            breaktime = Random.Range(0.05f, 0.2f);

            //destroy if not connected
            switch (connected)
            {
                case ConnectedTypeEnum.DWD:
                    if (!WorldDetailManager.GetIDExists(connBuildingUID)) { Attack(1000); return; }
                    return;
                case ConnectedTypeEnum.WorldFeature:
                    if (!World.GetWorldFeatures[connWfTypeIndex][connWfGenIndex]) { Attack(1000); return; }
                    return;
            }
        }
    }
}