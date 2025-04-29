using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public class UnityServicesManager : MonoBehaviour
{
    public static bool initialised;
    public static System.Action InitialisationComplete;

    private void Awake()
    {
        initialised = false;
    }

    private async void Start()
    {
        await UnityServices.InitializeAsync();

        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        initialised = true;

        InitialisationComplete?.Invoke();
    }
}
