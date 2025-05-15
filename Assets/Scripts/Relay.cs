using System.Collections;
using System.Collections.Generic;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Threading.Tasks;

public class Relay : MonoBehaviour
{
    public static Relay instance;
    public static string CurrentJoinCode { get; private set; }
    public static string CurrentAllocationId;
    public static System.Action<string> AllocationCreated;

    private void Awake()
    {
        instance = this;
    }

    public async Task<bool> CreateRelay()
    {
        if(!UnityServicesManager.initialised)
        {
            Debug.Log("Services not initialised");
            return false;
        }
        try
        {
            Allocation allj = await RelayService.Instance.CreateAllocationAsync(99);
            CurrentAllocationId = allj.AllocationId.ToString();
            AllocationCreated?.Invoke(CurrentAllocationId);
            string code = await RelayService.Instance.GetJoinCodeAsync(allj.AllocationId);
            Debug.Log(code);
            CurrentJoinCode = code;

            //set the stuff on the network manager
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(
                allj.RelayServer.IpV4,
                (ushort)allj.RelayServer.Port,
                allj.AllocationIdBytes,
                allj.Key,
                allj.ConnectionData
            );
            return true;
        }
        catch(RelayServiceException ex)
        {
            Debug.Log(ex);
            return false;
        }
    }

    public async Task<bool> JoinRelay(string joincode)
    {
        if (!UnityServicesManager.initialised)
        {
            Debug.Log("Services not initialised");
            return false;
        }
        try
        {
            Debug.Log("Joining relay " + joincode);
            JoinAllocation allj = await RelayService.Instance.JoinAllocationAsync(joincode);
            CurrentAllocationId = allj.AllocationId.ToString();
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetClientRelayData(
                allj.RelayServer.IpV4,
                (ushort)allj.RelayServer.Port,
                allj.AllocationIdBytes,
                allj.Key,
                allj.ConnectionData,
                allj.HostConnectionData
            );
            return true;
        }
        catch(RelayServiceException e)
        {
            Debug.Log(e);
            return false;
        }
    }
}
