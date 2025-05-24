using System.Collections.Generic;
using UnityEngine;

public class HealthBodyPart : MonoBehaviour
{
    public float DamageModifier, StunModifier;
    public Health health;
    public int PartIndex;

    [System.Serializable]
    public struct ModifierAreaStruct
    {
        public Vector3 Position;
        public float Size;
        public float DamageModifier, StunModifier;
    }
    public List<ModifierAreaStruct> ModifierAreas;

    public void TakeDamage(float damage, float stun, DamageType type, Vector3 position, Vector3 normal, bool embedded = false, ItemData embeddeditem = default)
    {
        float dmod = DamageModifier, smod = StunModifier;
        foreach (var area in ModifierAreas)
        {
            if ((transform.TransformPoint(area.Position) - position).sqrMagnitude <= area.Size * area.Size)
            {
                dmod *= area.DamageModifier;
                smod *= area.StunModifier;
            }
        }

        health.TakeDamage(damage * dmod, stun * smod, type, PartIndex, embedded, transform.InverseTransformPoint(position), transform.InverseTransformDirection(normal), embeddeditem);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        foreach (var area in ModifierAreas)
        {
            Gizmos.DrawWireSphere(transform.TransformPoint(area.Position), area.Size);
        }
    }
}
