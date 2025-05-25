using UnityEngine;

public class RegionTrigger : MonoBehaviour
{
    public string RegionName;
    public float RegionTitleCountdown = 3;
    public MusicLayerProfile threatMusicProfile;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && other.transform == Player.LocalPlayer.transform)
        {
            UIManager.ShowRegionTitle(RegionName, RegionTitleCountdown);
            MusicManager.Init(threatMusicProfile);
        }
    }
}
