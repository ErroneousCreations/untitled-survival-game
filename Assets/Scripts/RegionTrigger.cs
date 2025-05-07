using UnityEngine;

public class RegionTrigger : MonoBehaviour
{
    public string RegionName;
    public float RegionTitleCountdown = 3;
    public MusicLayerProfile threatMusicProfile;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            UIManager.ShowRegionTitle(RegionName, RegionTitleCountdown);
            MusicManager.Init(threatMusicProfile);
        }
    }
}
