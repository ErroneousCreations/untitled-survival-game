using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class CutTreeTrunk : NetworkBehaviour
{
    public int LogCount;
    public float TreeHeight, InitialForce, ContactDamage, MinDamageSpeed;
    public SavedNetObject saver;
    public float Health;
    public Rigidbody rb;

    private float CurrentHealth;

    private void Start()
    {
        if (!IsOwner) { return; }
        rb.AddForceAtPosition(transform.position + transform.up * TreeHeight, Extensions.RandomCircle * InitialForce);
        CurrentHealth = Health;
        saver.OnDataLoaded_Data += OnLoaded;
    }

    private void OnCollisionEnter(Collision collision)
    {
        var m = rb.angularVelocity.magnitude;
        if (m <= MinDamageSpeed || Vector3.Angle(transform.up, Vector3.up) > 70) { return; }

        if(collision.collider.CompareTag("Player") && collision.collider.TryGetComponent(out PlayerHealthController ph))
        {
            ph.ApplyDamage(ContactDamage * m, DamageType.Blunt, collision.contacts[0].point, collision.contacts[0].normal);
        }
        else if(collision.collider.TryGetComponent(out HealthBodyPart h)) {
            var d = ContactDamage * m;
            h.TakeDamage(d, d * 0.1f, DamageType.Blunt, collision.contacts[0].point, collision.contacts[0].normal);
        }
    }

    private void OnLoaded(List<string> data)
    {
        CurrentHealth = float.Parse(data[0]);
    }

    public void Attack(float damage)
    {
        AttackRPC(damage);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void AttackRPC(float damage)
    {
        CurrentHealth -= damage;
        if(CurrentHealth <= 0) { Die(); }
    }

    private void Die()
    {
        for (int i = 0; i < LogCount; i++)
        {
            var item = Random.value <= 0.25f ? "plank" : "log";
            var ob = Instantiate(ItemDatabase.GetItem(item).ItemPrefab, Vector3.Lerp(transform.position, transform.position + transform.up * TreeHeight, (i + 1) / (float)LogCount), transform.rotation);
            ob.NetworkObject.Spawn();
        }
        NetworkObject.Despawn();
    }

    private void OnDrawGizmosSelected()
    {
        for (int i = 0; i < LogCount; i++)
        {
            Gizmos.DrawWireSphere(Vector3.Lerp(transform.position, transform.position + transform.up * TreeHeight, (i + 1) / (float)LogCount), 0.25f);
        }
    }
}
