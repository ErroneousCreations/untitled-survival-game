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

    private void Awake()
    {
        instance = this;
    }

    public async Task CreateRelay()
    {
        if(!UnityServicesManager.initialised)
        {
            Debug.Log("Services not initialised");
            return;
        }
        try
        {
            Allocation allj = await RelayService.Instance.CreateAllocationAsync(99);
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
        }
        catch(RelayServiceException ex)
        {
            Debug.Log(ex);
        }
    }

    public async Task JoinRelay(string joincode)
    {
        if (!UnityServicesManager.initialised)
        {
            Debug.Log("Services not initialised");
            return;
        }
        try
        {
            Debug.Log("Joining relay " + joincode);
            JoinAllocation allj = await RelayService.Instance.JoinAllocationAsync(joincode);
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetClientRelayData(
                allj.RelayServer.IpV4,
                (ushort)allj.RelayServer.Port,
                allj.AllocationIdBytes,
                allj.Key,
                allj.ConnectionData,
                allj.HostConnectionData
            );
        }
        catch(RelayServiceException e)
        {
            Debug.Log(e);
        }
    }
}
