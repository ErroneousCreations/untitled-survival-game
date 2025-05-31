using UnityEngine;
using EditorAttributes;
using Unity.Netcode;

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
                    bool doeffects =false;
                    var pos = Vector3.zero;
                    if (other.CompareTag("Player") && other.TryGetComponent(out Player p))
                    {
                        doeffects = true;
                        pos = other.ClosestPoint(transform.position);
                        p.KnockOver(myDetail.GetCurrElectricity * 2.5f, true);
                        p.ph.ApplyDamage(myDetail.GetCurrElectricity * 2, DamageType.Blunt, pos, Vector3.up);
                    }
                    else if (other.TryGetComponent(out HealthBodyPart hp))
                    {
                        doeffects = true;
                        pos = other.ClosestPoint(transform.position);
                        hp.TakeDamage(myDetail.GetCurrElectricity * 2, myDetail.GetCurrElectricity * 4f, DamageType.Blunt, pos, Vector3.up);
                    }

                    if (doeffects)
                    {
                        Destroy(Instantiate(NetPrefabsList.GetNetPrefab("particle_electroshock"), pos, Quaternion.identity).gameObject, 1);
                        NetPrefabsList.SpawnObjectExcept("particle_electroshock", pos, Quaternion.identity, NetworkManager.Singleton.LocalClientId, 1);
                        NetworkAudioManager.PlayNetworkedAudioClip(Random.Range(0.8f, 1.2f), 1, 1, pos, "electrocute");
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
