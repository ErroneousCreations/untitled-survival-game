using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LegWalkerController : MonoBehaviour
{
    [System.Serializable]
    public struct Leg
    {
        public Transform EndOfLeg;
        public Transform target;               // Target the IK uses
        public Transform defaultAnchor;        // Where the leg "should" be from the body
        public float stepDistance;
        public float stepAheadLength;
        public float stepHeight;
        public float footHeightOffset;
        public float stepSpeed;
        public float raycastOffset;
        public bool alignToGround;
    }
    public LayerMask groundMask;
    /// <summary>
    /// with blender models the axis can be fucked so this defines something with CORRECT axis
    /// </summary>
    public Transform direction;
    public List<Leg> legs = new List<Leg>();
    private List<Vector3> currTargPositions = new();
    private List<bool> stepping = new();
    public float stepCooldown = 0.1f;
    public float ungroundedLegFlailAmplitude = 0.2f, ungroundedLegFlailSpeed = 4, groundcastLength;
    public Vector3 groundcastHeightOffset;

    private int currentLegIndex = 0;
    private float lastStepTime;
    private Vector3 bodyLastPosition;
    private float flyingTime;

    void Start()
    {
        bodyLastPosition = transform.position;

        foreach (Leg leg in legs)
        {
            currTargPositions.Add(leg.target.position);
            stepping.Add(false);
        }
    }

    void Update()
    {
        Vector3 velocity = (transform.position - bodyLastPosition) / Time.deltaTime;
        bodyLastPosition = transform.position;

        var IsGrounded = Physics.Raycast(transform.position + groundcastHeightOffset, Vector3.down, out _, groundcastLength, groundMask);

        if (!IsGrounded) { flyingTime += Time.deltaTime; }
        else { flyingTime = 0; }

        for (int i = 0; i < legs.Count; i++)
        {
            Leg leg = legs[i];

            if (!IsGrounded)
            {
                leg.target.position = leg.defaultAnchor.position + Mathf.Sin((flyingTime * ungroundedLegFlailSpeed * Mathf.Clamp(flyingTime/5,0,2)) + (Mathf.PI*i)) * ungroundedLegFlailAmplitude * direction.forward;
                currTargPositions[i] = leg.target.position;
                continue;
            }

            if (!stepping[i]) { leg.target.position = currTargPositions[i]; }

            Vector3 rayOrigin = leg.defaultAnchor.position + Vector3.up * leg.raycastOffset + velocity.normalized * (leg.stepSpeed + leg.stepAheadLength * velocity.magnitude);
            Vector3 worldTarget = leg.target.position;

            // Raycast to find new target
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 5f, groundMask))
            {
                worldTarget = hit.point + hit.normal * leg.footHeightOffset;
                float dist = Vector3.Distance(leg.target.position, worldTarget);

                if (dist > leg.stepDistance && Time.time - lastStepTime > stepCooldown && i == currentLegIndex)
                {
                    StartCoroutine(StepLeg(i, leg, worldTarget, hit.normal));
                    currentLegIndex = (currentLegIndex + 1) % legs.Count;
                    lastStepTime = Time.time;
                    break;
                }
            }
        }
    }

    IEnumerator StepLeg(int index, Leg leg, Vector3 finalPosition, Vector3 groundNormal)
    {
        stepping[index] = true;
        Vector3 startPos = leg.target.position;
        Quaternion startRot = leg.target.rotation;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / leg.stepSpeed;
            Vector3 pos = Vector3.Lerp(startPos, finalPosition, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * leg.stepHeight;

            leg.target.position = pos;
            leg.target.rotation = Quaternion.LookRotation((leg.EndOfLeg.position - pos).normalized, direction.right);

            yield return null;
        }
        stepping[index] = false;

        currTargPositions[index] = finalPosition;

        leg.target.position = finalPosition;
        leg.target.rotation = Quaternion.LookRotation(groundNormal, direction.right);
    }
}