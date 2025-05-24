using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using EditorAttributes;

[RequireComponent(typeof(Rigidbody), typeof(NavMeshAgent))]
public class AILocomotor : MonoBehaviour
{
    [Header("Navigation")]
    public float maxSpeed = 3f;
    public float acceleration = 15f;
    public float linkTraverseSpeed = 2f;
    public float cornerTolerance = 0.5f;

    [Header("Rotation")]
    public bool faceMovement = false;
    public float turnSpeed = 10f, turnDrag = 0.9f;

    [Header("Standing up")]
    public bool alwaysStanding;
    public bool applyStandupForce;
    [ShowField(nameof(applyStandupForce))]public float standForce, standDamping;
    [HideField(nameof(alwaysStanding))]public float knockedOverDrag, knockedOverAngularDrag, fallOverForce;

    [Header("Ground Check")]
    public float groundCheckDistance = 0.3f;
    public LayerMask groundLayer;
    public float slopeLimit = 45f;

    private Rigidbody rb;
    private NavMeshAgent agent;
    private Vector3 groundNormal = Vector3.up;
    private bool isGrounded;
    private Vector3 destination;
    private bool isNavigating;
    private Vector3 turndestination;
    private bool isTurning;
    private bool traversingLink;

    [HideInInspector] public bool StandingUp = true;

    public void SetDestination(Vector3 target)
    {
        destination = target;
        isNavigating = true;
        agent.SetDestination(target);
    }

    private Vector3 ClosestOnNavmesh(Vector3 start)
    {
        if (NavMesh.SamplePosition(start, out NavMeshHit hit, 1f, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return start;
    }

    public void Stop()
    {
        isNavigating = false;
        agent.ResetPath();
    }

    public void SetTurnDirection(Vector3 direction)
    {
        turndestination = direction;
        isTurning = true;
    }

    public void ResetTurnDirection()
    {
        turndestination = Vector3.zero;
        isTurning = false;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        transform.position = ClosestOnNavmesh(transform.position);
        agent.enabled = false;
    }

    private bool GetUpright => Vector3.Angle(transform.up, Vector3.up) < 10;

    private void FixedUpdate()
    {
        agent.enabled = StandingUp;
        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.updateUpAxis = false;

        if (!StandingUp && !alwaysStanding)
        {
            if (GetUpright)
            {
                rb.AddForceAtPosition(transform.forward * fallOverForce, transform.TransformPoint(new Vector3(0, 1.5f, 0)), ForceMode.Force);
            }
            else { ApplyFriction(); }
            return;
        }

        if (applyStandupForce) { ApplyUprightTorque(); }

        GroundCheck();

        if (agent.path == null) { return; }

        if (agent.isOnOffMeshLink)
        {
            if(traversingLink) { return; }
            StartCoroutine(TraverseLink(agent.currentOffMeshLinkData));
        }

        if (isTurning) { TurnTowards(); }
        else { rb.angularVelocity *= turnDrag; }
        if (traversingLink) { return; }
        if (isNavigating) { MoveTowards(agent.steeringTarget); }
        else { Deccelerate(); }
        agent.nextPosition = transform.position;
    }

    void TurnTowards()
    {
        Quaternion targetRotation = Quaternion.LookRotation(turndestination);
        Quaternion deltaRotation = targetRotation * Quaternion.Inverse(transform.rotation);
        deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180) angle -= 360;
        rb.AddTorque(angle * turnSpeed * axis);
        rb.angularVelocity *= turnDrag;
    }

    void Deccelerate()
    {
        Vector3 velocityChange = -rb.linearVelocity;
        velocityChange.y = 0f;
        Vector3 force = Vector3.ClampMagnitude(velocityChange * acceleration, maxSpeed * acceleration);
        rb.AddForce(force, ForceMode.Acceleration);
    }

    void MoveTowards(Vector3 nextpos)
    {
        Vector3 dir = (nextpos - transform.position);
        dir.y = 0f;

        Vector3 desiredDir = dir.normalized;

        // Project onto ground normal (slope-aware movement)
        Vector3 slopeDir = Vector3.ProjectOnPlane(desiredDir, groundNormal).normalized;
        Vector3 targetVelocity = slopeDir * maxSpeed;
        Vector3 velocityChange = (targetVelocity - rb.linearVelocity);
        velocityChange.y = 0f;

        Vector3 force = Vector3.ClampMagnitude(velocityChange * acceleration, maxSpeed * acceleration);
        rb.AddForce(force, ForceMode.Acceleration);

        // Rotate to face movement
        if (faceMovement && !isTurning && dir.sqrMagnitude > 0.25f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(desiredDir);
            Quaternion deltaRotation = targetRotation * Quaternion.Inverse(transform.rotation);
            deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180) angle -= 360;
            rb.AddTorque(angle * turnSpeed * axis);
            rb.angularVelocity *= turnDrag;
        }
    }

    void ApplyFriction()
    {
        // Simulate friction to reduce sliding
        Vector3 lateralVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
        Vector3 frictionForce = -lateralVelocity * knockedOverDrag;
        rb.AddForce(frictionForce, ForceMode.Acceleration);

        rb.angularVelocity *= 1f / (1f + knockedOverAngularDrag * Time.fixedDeltaTime);
    }

    void ApplyUprightTorque()
    {
        Vector3 desiredUp = Vector3.up;
        Vector3 currentUp = transform.up;

        Vector3 torqueAxis = Vector3.Cross(currentUp, desiredUp);
        float angle = Vector3.Angle(currentUp, desiredUp) * Mathf.Deg2Rad;

        Vector3 correctiveTorque = (torqueAxis.normalized * angle * standForce) - (standDamping * rb.angularVelocity);
        rb.AddTorque(correctiveTorque, ForceMode.Acceleration);
    }

    void GroundCheck()
    {
        Ray ray = new Ray(transform.position + Vector3.up * 0.05f, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, groundCheckDistance + 0.1f, groundLayer))
        {
            groundNormal = hit.normal;
            isGrounded = Vector3.Angle(Vector3.up, groundNormal) <= slopeLimit;
        }
        else
        {
            isGrounded = false;
            groundNormal = Vector3.up;
        }
    }

    IEnumerator TraverseLink(OffMeshLinkData data)
    {
        traversingLink = true;
        rb.isKinematic = true;
        agent.isStopped = true;

        float t = 0f;
        while (t < 1.01f)
        {
            t += Time.deltaTime * linkTraverseSpeed;
            transform.position = Vector3.Lerp(data.startPos, data.endPos, t);
            yield return null;
        }

        rb.isKinematic = false;
        traversingLink = false;
        agent.isStopped = false;
    }
}