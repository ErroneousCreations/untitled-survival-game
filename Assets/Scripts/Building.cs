using EditorAttributes;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Building : MonoBehaviour
{
    public const float ELECTRICITY_FALLOFF = 0.8f;

    public DestructibleWorldDetail myDwd;
    public SavedObject mySaver;
    public float BaseElectricity;
    public bool Conductive;
    [ShowField(nameof(Conductive))] public Renderer ElectricEffect;
    private float currElectricity;
    private int connWfTypeIndex, connWfGenIndex;
    private Material elecmat;
    private int propID;
    private enum ConnectedTypeEnum { None, WorldFeature, DWD }
    private ConnectedTypeEnum connected;
    private int connBuildingUID;

    private float breaktime = 0;

    public float GetCurrElectricity => currElectricity;

    private void Start()
    {
        elecmat = ElectricEffect ? ElectricEffect.material : null;
        if (elecmat) { propID = Shader.PropertyToID("_AlphaNoiseThresh"); }
        mySaver.OnDataLoaded_Data += SetData;
    }

    public void SetConnection(string conn)
    {
        if (conn.Length <= 0) { connected = ConnectedTypeEnum.None; return; }
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

    void SetData(List<string> data)
    {
        if (data.Count > 1)
        {
            if (data[1].Length <= 0) { connected = ConnectedTypeEnum.None; return; }
            var split = data[1].Split('~');
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
        }
        else { myDwd.Attack(1000); }
    }

    public void Attack(float damage)
    {
        myDwd.Attack(damage);
    }

    private void Update()
    {
        if (connected == ConnectedTypeEnum.None || connected == ConnectedTypeEnum.WorldFeature) { currElectricity = BaseElectricity; }
        else
        {
            //electricity
            currElectricity = Conductive ? (BaseElectricity + (connected == ConnectedTypeEnum.DWD && WorldDetailManager.TryGetOb(connBuildingUID, out var det) ? det.GetCurrElectricity * ELECTRICITY_FALLOFF : 0)) : 0;
        }

        //electricity effect
        if (elecmat)
        {
            ElectricEffect.gameObject.SetActive(currElectricity > 0.05f);
            if (currElectricity > 0.05f) { elecmat.SetFloat(propID, Mathf.Lerp(0.87f, 0.45f, currElectricity)); }
        }

        //breaking check on the server
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer) { return; }

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
