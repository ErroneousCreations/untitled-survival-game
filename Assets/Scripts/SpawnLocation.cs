using UnityEngine;

public class SpawnLocation : MonoBehaviour
{
    public string SpawnIndex;

    private void Start()
    {
        World.RegisterSpawnLocation(SpawnIndex, transform);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.25f);
    }
}
