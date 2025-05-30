using UnityEngine;
using EditorAttributes;

public class ConstructedDamager : MonoBehaviour
{
    public enum DamagerType { Electric, Spike }
    public DamagerType MyType;
    public DestructibleWorldDetail myDetail;
    public float MinimumDamageDot = 1;

    [ShowField(nameof(MyType), DamagerType.Spike)] public float DamagePerVelocity, DurabilityLoss;

    private void OnTriggerEnter(Collider other)
    {
        switch (MyType)
        {
            case DamagerType.Electric:
                {
                    if(myDetail.GetCurrElectricity <= 0.05f) { return; }
                    if (other.CompareTag("Player") && other.TryGetComponent(out Player p))
                    {
                        p.KnockOver(myDetail.GetCurrElectricity * 2.5f, true);
                        p.ph.ApplyDamage(myDetail.GetCurrElectricity * 10, DamageType.Blunt, other.ClosestPoint(transform.position), Vector3.up);
                    }
                    else if (other.TryGetComponent(out HealthBodyPart hp))
                    {
                        hp.TakeDamage(myDetail.GetCurrElectricity * 10, myDetail.GetCurrElectricity * 4f, DamageType.Blunt, other.ClosestPoint(transform.position), Vector3.up);
                    }
                }
                break;
            case DamagerType.Spike:
                {
                    var norm = transform.position - other.transform.position;
                    norm.y = 0;
                    norm.Normalize();

                    var relativevelocity = -Vector3.Dot(other.attachedRigidbody.linearVelocity, transform.up);
                    if (relativevelocity <= MinimumDamageDot) { return; }
                    if (other.CompareTag("Player") && other.TryGetComponent(out Player p))
                    {
                        p.ph.ApplyDamage(relativevelocity * DamagePerVelocity, DamageType.Stab, other.ClosestPoint(transform.position), norm);
                        myDetail.Attack(DurabilityLoss);
                    }
                    else if (other.TryGetComponent(out HealthBodyPart hp))
                    {
                        hp.TakeDamage(relativevelocity * DamagePerVelocity, relativevelocity*DamagePerVelocity * 0.2f, DamageType.Stab, other.ClosestPoint(transform.position), norm);
                        myDetail.Attack(DurabilityLoss);
                    }

                }
                break;
        }
    }
}
