using Unity.Netcode;
using UnityEngine;

public class Spike : MonoBehaviour
{
    public float DamagePerVelocity = 1f;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Player") && collision.collider.TryGetComponent(out PlayerHealthController ph))
        {
            ph.ApplyDamage(collision.relativeVelocity.magnitude * DamagePerVelocity, DamageType.Stab, collision.contacts[0].point, collision.contacts[0].normal);
        }

        if(NetworkManager.Singleton && NetworkManager.Singleton.IsServer && collision.collider.TryGetComponent(out HealthBodyPart part))
        {
            part.TakeDamage(collision.relativeVelocity.magnitude * DamagePerVelocity, collision.relativeVelocity.magnitude * DamagePerVelocity * 0.1f, DamageType.Stab, collision.contacts[0].point, collision.contacts[0].normal);
        }
    }
}
