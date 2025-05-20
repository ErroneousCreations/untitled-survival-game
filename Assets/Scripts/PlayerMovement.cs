using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 6f;
    public float acceleration = 20f;
    public float deceleration = 25f;
    public float jumpForce = 7f;
    public float slopeLimit = 45f;
    public float downForce;
    public LayerMask groundLayer;
    public float groundCheckDistance = 0.5f;

    [Header("Air Control")]
    public float airControl = 0.25f;
    public float airMaxSpeed = 10f;
    public float airAcceleration = 30f;

    [Header("Headbob Settings")]
    public float bobFrequency = 1.8f;
    public float bobHorizontalAmplitude = 0.05f;
    public float bobVerticalAmplitude = 0.08f;
    public float rotationAmplitude = 2f;
    public float bobSpeedMultiplier = 1f;
    public Transform CameraParent, ViewmodelParent;

    [Header("Crouch Settings")]
    public CapsuleCollider crouchCollider;
    public Vector3 forceCrouchOffset, crouchCamOffset;
    public float forceCrouchRadius;
    public float crouchSpeedMult = 0.6f, crouchHeadbobMult = 0.5f;
    public Vector2 crouchColliderSize = new(0.5f, 0.5f);
    private float targCollHeight, targCollOffset;
    private Vector2 origCollSize;

    [Header("Slide settings")]
    public float slideInitialSpeed;
    public float slideSlopeIncrease, slideDeceleration, maxSlideSpeed;
    [Tooltip("Note this is the percentage of horizontal force turned into jump")]public float slideJumpModifier;
    public float slideJumpCap, slideFromFallDist, slideFromFallSpeedMod = 0.75f;
    private Vector2 currSlideDir;

    [Header("Wall Running")]
    public float wallCheckDistance = 0.1f;
    public float wallRunDuration = 0.75f;
    public float wallRunForce = 5f;
    public float wallRunUpForce = 0.5f, wallRunInitUpForce;
    public float wallJumpForce = 6f;
    public float wallJumpAngle = 0.6f;
    [Header("Chimney Climbing")]
    public float chimneyClimbSpeed = 2f;
    public float chimneyExitHeight = 2.5f, chimneyExitCheckDistF = 0.4f, chimneyExitCheckDistD = 0.5f, chimneyExitSpeed, chimneyExitSpeedForward, chimneyExitTime;
    private bool isTouchingWallLeft, isTouchingWallRight, isTouchingWallFront;
    private int surroundingWalls;
    private float wallRunTimer;
    private Vector3 wallNormal, exitChimneyDir, exitChimneyTarg;
    private float currChimneyExitTime;
    [Header("Clamber on Ledge")]
    public float clamberCheckHeight = 2.5f;
    public float clamberCheckDistF = 0.4f, clamberCheckDistD = 0.5f, clamberExitSpeed, clamberExitSpeedForward, clamberExitTime;
    private float currClamberTime;
    [Header("Ladder")]
    public float ladderCheckDistance = 0.2f;
    public float ladderClimbSpeed = 2f, ladderDescendSpeed = 5f, ladderJumpoffForce =6f;
    public float ladderStayForce = 40f, ladderJumpAngle = 0.9f, ladderSecondCheckAbove = 0.4f;
    private Vector3 currladderNormal;
    private float ladderFalloffTime = 0.1f;

    private float bobTimer = 0f;
    private float shakeTimer = 0f;
    private float currentShakeIntensity = 0f;
    private Vector3 shakeOffset;
    private Vector3 initialLocalPos;
    private float distToGround;

    private Rigidbody rb;
    private Vector3 moveInput;
    private Vector2 rawMoveInput;
    private bool isGrounded;
    private Vector3 groundNormal;
    private float groundAngle;
    private float slopeDirectionDot = 0f;
    private float coyoteTime = 0f;
    private float jumpCd = 0, slideforceCd;
    private float impactSlideValue = 0;
    private bool wasGrounded;

    public enum MoveStateEnum { Walking, Sliding, Wallrunning, ChinmeyClimbing, LadderClimbing }
    public MoveStateEnum currMoveState;

    //networking
    private NetworkVariable<bool> NetCrouching = new(
       writePerm: NetworkVariableWritePermission.Owner);

    public bool GetCrouching => NetCrouching.Value;
    public float GetCrouchHeightMult => NetCrouching.Value ? (crouchColliderSize.x / origCollSize.x) : 1f;
    public bool GetGrounded => isGrounded;  
    public Rigidbody GetRigidbody => rb;
    public MoveStateEnum GetMovestate => currMoveState;

    private bool GetHoldingCrouch => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
    private bool GetHoldingSprint => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

    private bool CanSprint => GetHoldingSprint && Player.LocalPlayer.ph.GetCanSprint && Player.GetCanStand && !GetCrouching;

    public bool GetIsSprinting => GetHoldingSprint && Player.LocalPlayer.ph.GetCanSprint && Player.GetCanStand && rawMoveInput.sqrMagnitude > 0.1f;

    private bool GetMovestateAllowsCrouch => currMoveState == MoveStateEnum.Walking || currMoveState == MoveStateEnum.Sliding;

    public void ToggleViewmodel(bool value)
    {
        ViewmodelParent.gameObject.SetActive(value);
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        origCollSize.x = crouchCollider.height;
        origCollSize.y = crouchCollider.center.y;
        if (IsOwner) {
            
            initialLocalPos = CameraParent.localPosition;
        }

        rb.isKinematic = !IsOwner;
    }

    void CheckWallSides()
    {
        RaycastHit leftHit, rightHit;
        Vector3 origin = transform.position + crouchCollider.center;
        
        //reset stuff
        isTouchingWallLeft = false;
        isTouchingWallRight = false;
        isTouchingWallFront = false;

        //check for the wallrunning
        if (Physics.Raycast(origin, -transform.right, out leftHit, wallCheckDistance + crouchCollider.radius, groundLayer)) { surroundingWalls++; isTouchingWallLeft = true; }
        if (Physics.Raycast(origin, transform.right, out rightHit, wallCheckDistance + crouchCollider.radius, groundLayer)) { surroundingWalls++; isTouchingWallRight = true; }
        if( Physics.Raycast(origin, transform.forward, out _, wallCheckDistance + crouchCollider.radius, groundLayer)) { surroundingWalls++; isTouchingWallFront = true; }

        //check for the wall climbing
        Vector3[] directions =
        {
            transform.forward,
            -transform.forward,
            transform.right,
            -transform.right,
            (transform.forward + transform.right).normalized,
            (transform.forward - transform.right).normalized,
            (-transform.forward + transform.right).normalized,
            (-transform.forward - transform.right).normalized
        };
        List<Vector3> directionsList = new();
        foreach (Vector3 direction in directions)
        {
            if (Physics.Raycast(origin, direction, out RaycastHit hit, wallCheckDistance + crouchCollider.radius, groundLayer))
            {
                if(directionsList.Contains(hit.normal)) { continue; }   
                directionsList.Add(hit.normal);
            }
        }
        surroundingWalls = directionsList.Count;

        if (isTouchingWallLeft)
            wallNormal = leftHit.normal;
        else if (isTouchingWallRight)
            wallNormal = rightHit.normal;
    }

    void Update()
    {
        //here is run on both client and owner
        targCollHeight = GetCrouching ? crouchColliderSize.x : origCollSize.x;
        targCollOffset = GetCrouching ? crouchColliderSize.y : origCollSize.y;
        crouchCollider.height = Mathf.Lerp(crouchCollider.height, targCollHeight, Time.deltaTime * 8f);
        crouchCollider.center = new Vector3(crouchCollider.center.x, Mathf.Lerp(crouchCollider.center.y, targCollOffset, Time.deltaTime * 8f), crouchCollider.center.z);

        if (!IsOwner) { return; }
        //here is done only on owner

        // Get raw input
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        rawMoveInput = new Vector2(x, z);

        //coyotetime
        if (isGrounded) { coyoteTime = 0.1f; }
        else if (coyoteTime > 0f) { coyoteTime -= Time.deltaTime; }

            Vector3 camForward = Camera.main.transform.forward;
        Vector3 camRight = Camera.main.transform.right;
        camForward.y = 0; camRight.y = 0;

        moveInput = (camForward.normalized * z + camRight.normalized * x).normalized;

        if (jumpCd > 0) { jumpCd -= Time.deltaTime; }

        //ladder climb above jump :)))
        if (Physics.Raycast(transform.position + crouchCollider.center, transform.forward, out RaycastHit ladderhit, crouchCollider.radius + ladderCheckDistance, groundLayer) && ladderhit.collider.CompareTag("Ladder") && currMoveState == MoveStateEnum.Walking && Input.GetButtonDown("Jump") && jumpCd <= 0)
        {
            jumpCd = 0.25f;
            rb.useGravity = false;
            currMoveState = MoveStateEnum.LadderClimbing;
            currladderNormal = ladderhit.normal;
        }

        if (Input.GetButtonDown("Jump") && coyoteTime>0 && currMoveState != MoveStateEnum.Wallrunning && jumpCd <=0 && surroundingWalls < 2 && (!GetCrouching || currMoveState==MoveStateEnum.Sliding))
        {
            Jump();
            jumpCd = 0.25f;
        }

        Vector3 topCheckPos = transform.position + crouchCollider.center + Vector3.up * clamberCheckHeight + transform.forward * (crouchCollider.radius + clamberCheckDistF);
        bool cleartoClamber = Physics.Raycast(topCheckPos, Vector3.down, out RaycastHit hit, clamberCheckDistD, groundLayer) && !Physics.CheckSphere(topCheckPos, 0.1f);
        if(cleartoClamber && Input.GetButtonDown("Jump") && currMoveState == MoveStateEnum.Walking && !isGrounded && surroundingWalls < 2)
        {
            exitChimneyDir = transform.forward;
            jumpCd = 0.25f;
            currClamberTime = clamberExitTime;
            exitChimneyTarg = hit.point;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        }

        NetCrouching.Value = (GetHoldingCrouch && GetMovestateAllowsCrouch) || Physics.CheckSphere(transform.position + forceCrouchOffset, forceCrouchRadius, groundLayer) || Player.LocalPlayer.ph.GetForceCrouch;

        if(slideforceCd > 0) { slideforceCd -= Time.deltaTime; }
        if ((Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl)) && rb.linearVelocity.magnitude > 0 && currMoveState == MoveStateEnum.Walking && GetGrounded)
        {
            currSlideDir = moveInput;
            currMoveState = MoveStateEnum.Sliding;
            Vector3 slopeDir = Vector3.ProjectOnPlane(moveInput, groundNormal).normalized;
            if(slideforceCd <= 0 && rb.linearVelocity.magnitude < maxSlideSpeed) { rb.AddForce(slopeDir * slideInitialSpeed, ForceMode.Force); slideforceCd = 0.175f; }
            impactSlideValue = 0;
        }

        if((Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl)) && rawMoveInput != Vector2.zero && currMoveState == MoveStateEnum.Walking && distToGround <= slideFromFallDist)
        {
            currSlideDir = moveInput;
            impactSlideValue = rb.linearVelocity.magnitude * slideFromFallSpeedMod;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, rb.linearVelocity.y * 1 - slideFromFallSpeedMod, rb.linearVelocity.z);
        }
        GroundCheck();

        if (impactSlideValue > 0 && isGrounded && !wasGrounded) {
            if (GetHoldingCrouch)
            {
                currMoveState = MoveStateEnum.Sliding;
                if(new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude < maxSlideSpeed)
                {
                    Vector3 slopeDir = Vector3.ProjectOnPlane(moveInput, groundNormal).normalized;
                    rb.AddForce(slopeDir * impactSlideValue, ForceMode.Impulse);
                }
                impactSlideValue = 0;
                slideforceCd = 0.25f;
            }
            else
            {
                impactSlideValue = 0;
            }
        }
        wasGrounded = isGrounded;

        if(currMoveState == MoveStateEnum.Sliding && !GetHoldingCrouch) { currMoveState = MoveStateEnum.Walking; }

        if((isTouchingWallLeft || isTouchingWallRight) && z>0 && (isTouchingWallLeft ? x<0 : x > 0) && currMoveState == MoveStateEnum.Walking && !isGrounded)
        {
            currMoveState = MoveStateEnum.Wallrunning;
            wallRunTimer = wallRunDuration;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            rb.AddForce(Vector3.up*wallRunInitUpForce, ForceMode.Force);
        }

        if(Input.GetButtonDown("Jump") && (isTouchingWallLeft || isTouchingWallRight) && !isGrounded && jumpCd <= 0 && currMoveState != MoveStateEnum.ChinmeyClimbing && surroundingWalls < 2)
        {
            jumpCd = 0.25f;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            Vector3 jumpDir = wallNormal + Vector3.up * wallJumpAngle;
            rb.AddForce((currMoveState == MoveStateEnum.Wallrunning ? 0.6f : 1) * wallJumpForce * jumpDir.normalized, ForceMode.Impulse);
        }

        if (Input.GetButtonDown("Jump") && currMoveState == MoveStateEnum.ChinmeyClimbing && jumpCd <= 0)
        {
            currMoveState = MoveStateEnum.Walking;
            rb.useGravity = true;
            jumpCd = 0.25f;
        }

        if (Input.GetButtonDown("Jump") && currMoveState == MoveStateEnum.LadderClimbing && jumpCd <= 0)
        {
            currMoveState = MoveStateEnum.Walking;
            rb.useGravity = true;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            Vector3 jumpDir = currladderNormal + Vector3.up * ladderJumpAngle;
            rb.AddForce(ladderJumpoffForce * jumpDir.normalized, ForceMode.Impulse);
            jumpCd = 0.25f;
        }

        if (surroundingWalls > 1 && Input.GetButtonDown("Jump") && jumpCd <= 0)
        {
            currMoveState = MoveStateEnum.ChinmeyClimbing;
            rb.useGravity = false;
            jumpCd = 0.25f;
        }

        CheckWallSides();
        Headbob();
    }

    private void Headbob()
    {
        var disabled = PlayerPrefs.GetInt("HEADBOB", 0) == 1 ? 0 : 1;
        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        float speed = horizontalVelocity.magnitude;

        switch (currMoveState)
        {
            case MoveStateEnum.Walking:
                // Bobbing
                if (isGrounded && speed > 0.1f)
                {
                    bobTimer += Time.deltaTime * bobFrequency * (speed * bobSpeedMultiplier * (GetCrouching ? crouchHeadbobMult : 1) * Player.LocalPlayer.ph.GetHeadbobSpeedMult) * (CanSprint ? 1.75f : 1);
                    float bobX = Mathf.Sin(bobTimer) * bobHorizontalAmplitude * (GetCrouching ? crouchHeadbobMult : 1);
                    float bobY = Mathf.Cos(bobTimer * 2) * bobVerticalAmplitude * (GetCrouching ? crouchHeadbobMult : 1) * (CanSprint ? 1.75f : 1) ;

                    float rotZ = Mathf.Sin(bobTimer) * rotationAmplitude * (GetCrouching ? crouchHeadbobMult : 1) * Player.LocalPlayer.ph.GetHeadbobMult;

                    CameraParent.SetLocalPositionAndRotation(Vector3.Lerp(CameraParent.localPosition, (GetCrouching ? crouchCamOffset : initialLocalPos) + new Vector3(bobX*disabled, bobY*disabled, 0), Time.deltaTime * 5), Quaternion.Slerp(CameraParent.localRotation, Quaternion.Euler(0, 0, rotZ*disabled), Time.deltaTime * 8f));

                    ViewmodelParent.SetLocalPositionAndRotation(Vector3.Lerp(ViewmodelParent.localPosition, new Vector3(bobX * 0.5f, bobY * 0.5f, 0), Time.deltaTime * 5), Quaternion.Slerp(ViewmodelParent.localRotation, Quaternion.Euler(0, 0, -rotZ * 0.5f), Time.deltaTime * 8f));
                }
                else
                {
                    // Return to rest
                    CameraParent.SetLocalPositionAndRotation(Vector3.Lerp(CameraParent.localPosition, (GetCrouching ? crouchCamOffset : initialLocalPos) + shakeOffset, Time.deltaTime * 5f), Quaternion.Slerp(CameraParent.localRotation, Quaternion.identity, Time.deltaTime * 8f));
                    ViewmodelParent.SetLocalPositionAndRotation(Vector3.Lerp(ViewmodelParent.localPosition, new Vector3(0, 0, 0), Time.deltaTime * 5f), Quaternion.Slerp(ViewmodelParent.localRotation, Quaternion.identity, Time.deltaTime * 8f));
                }
                break;
            case MoveStateEnum.Wallrunning:
                CameraParent.localRotation = Quaternion.Slerp(CameraParent.localRotation,
                    Quaternion.Euler(0, 0, isTouchingWallLeft ? -10f : 10f),
                    Time.deltaTime * 5f);
                break;
            default:
                CameraParent.SetLocalPositionAndRotation(Vector3.Lerp(CameraParent.localPosition, (GetCrouching ? crouchCamOffset : initialLocalPos) + shakeOffset, Time.deltaTime * 5f), Quaternion.Slerp(CameraParent.localRotation, Quaternion.identity, Time.deltaTime * 8f));
                break;

        }

        // Apply shake if active
        if (shakeTimer > 0)
        {
            shakeTimer -= Time.deltaTime;
            shakeOffset = Random.insideUnitSphere * currentShakeIntensity;
        }
        else
        {
            shakeOffset = Vector3.Lerp(shakeOffset, Vector3.zero, Time.deltaTime * 5f);
        }
    }

    void FixedUpdate()
    {
        if(!IsOwner) { return; }
        if (!Player.GetCanStand) { return; }
        switch(currMoveState)
        {
            case MoveStateEnum.Walking:
                WalkFixedUpdate();
                break;
            case MoveStateEnum.Sliding:
                SlideFixedUpdate();
                break;
            case MoveStateEnum.Wallrunning:
                WallrunFixedUpdate();
                break;
            case MoveStateEnum.ChinmeyClimbing:
                ChimneyClimbFixedUpdate();
                break;
            case MoveStateEnum.LadderClimbing:
                LadderClimbFixedUpdate();
                break;
        }
    }

    void LadderClimbFixedUpdate()
    {
        //we check above so we dont just drop off if theres 2 ladders or soimething
        if((Physics.Raycast(transform.position + crouchCollider.center, -currladderNormal, out RaycastHit ladderhit, crouchCollider.radius + ladderCheckDistance, groundLayer) && ladderhit.collider.CompareTag("Ladder")) || (Physics.Raycast(transform.position + crouchCollider.center + Vector3.up*ladderSecondCheckAbove , -currladderNormal, out RaycastHit ladderhit2, crouchCollider.radius + ladderCheckDistance, groundLayer) && ladderhit2.collider.CompareTag("Ladder")))
        {
            ladderFalloffTime = 0.3f;
        }
        else { ladderFalloffTime -= Time.deltaTime; }

        if (ladderFalloffTime <= 0)
        {
            rb.useGravity = true;
            currMoveState = MoveStateEnum.Walking;
            return;
        }
        float climbVelocity = 0f;
        rb.useGravity = false;
        if (rawMoveInput.y > 0)
        {
            climbVelocity = ladderClimbSpeed * Time.fixedDeltaTime;
        }
        else if (rawMoveInput.y < 0)
        {
            climbVelocity = -ladderDescendSpeed * Time.fixedDeltaTime;
        }
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, new Vector3(0, climbVelocity, 0), Time.fixedDeltaTime * 5);
        rb.AddForce(-currladderNormal * ladderStayForce, ForceMode.Acceleration);
        // Detect chimney top (space above, but no walls around)
        Vector3 topCheckPos = transform.position + crouchCollider.center + Vector3.up * chimneyExitHeight + transform.forward * (crouchCollider.radius + chimneyExitCheckDistF);
        bool cleartoExit = Physics.Raycast(topCheckPos, Vector3.down, out RaycastHit hit, chimneyExitCheckDistD, groundLayer) && !hit.collider.CompareTag("Ladder") && !Physics.CheckSphere(topCheckPos, 0.3f);

        if (cleartoExit && rawMoveInput.y > 0)
        {
            rb.useGravity = true;
            exitChimneyDir = transform.forward;
            exitChimneyTarg = hit.point;
            currChimneyExitTime = chimneyExitTime;
            currMoveState = MoveStateEnum.Walking;
            return;
        }
    }

    void ChimneyClimbFixedUpdate()
    {
        if (surroundingWalls < 2)
        {
            currMoveState = MoveStateEnum.Walking;
            rb.useGravity = true;
            return;
        }

        float climbVelocity = 0f;

        if (rawMoveInput.y > 0)
        {
            climbVelocity = chimneyClimbSpeed*Time.fixedDeltaTime;
        }
        else if (rawMoveInput.y < 0)
        {
            climbVelocity = -chimneyClimbSpeed*Time.fixedDeltaTime;
        }

        // Apply climb movement
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, new Vector3(0f, climbVelocity, 0f), Time.fixedDeltaTime * 5);

        // Detect chimney top (space above, but no walls around)
        Vector3 topCheckPos = transform.position + crouchCollider.center + Vector3.up*chimneyExitHeight + transform.forward*(crouchCollider.radius+ chimneyExitCheckDistF);
        bool cleartoExit = Physics.Raycast(topCheckPos, Vector3.down, out RaycastHit hit, chimneyExitCheckDistD, groundLayer) && !Physics.CheckSphere(topCheckPos, 0.1f);

        if (cleartoExit && rawMoveInput.y > 0)
        {
            rb.useGravity = true;
            exitChimneyDir = transform.forward;
            exitChimneyTarg = hit.point;
            currChimneyExitTime = chimneyExitTime;
            currMoveState = MoveStateEnum.Walking;
            return;
        }
    }

    void WallrunFixedUpdate()
    {
        if (surroundingWalls >= 2) {
            currMoveState = MoveStateEnum.ChinmeyClimbing;
            return;
        }

        if (isGrounded || (isTouchingWallLeft && isTouchingWallRight) || !(isTouchingWallLeft || isTouchingWallRight) || isTouchingWallFront || wallRunTimer <= 0 || GetHoldingCrouch || rawMoveInput.y <0.5f || (isTouchingWallLeft ? rawMoveInput.x > -0.5f : rawMoveInput.x < 0.5f) || Random.value < Player.LocalPlayer.ph.GetWallrunDropChance)
        {
            currMoveState = MoveStateEnum.Walking;
            rb.AddForce(0.25f * wallJumpForce * wallNormal, ForceMode.Impulse);
            return;
        }

        wallRunTimer -= Time.deltaTime;

        // Weak forward boost + reduce gravity
        Vector3 runDir;

        if (isTouchingWallRight)
            runDir = Vector3.Cross(Vector3.up, wallNormal); // right wall > forward
        else
            runDir = Vector3.Cross(wallNormal, Vector3.up); // left wall > forward
        runDir.Normalize();
        rb.AddForce(runDir * wallRunForce, ForceMode.Acceleration);
        rb.AddForce(Vector3.up * wallRunUpForce, ForceMode.Acceleration);
    }

    private void WalkFixedUpdate()
    {
        if (coyoteTime > 0) { rb.AddForce(-groundNormal * downForce); }

        if (currChimneyExitTime > 0)
        {
            currChimneyExitTime -= Time.deltaTime;
            rb.AddForce(exitChimneyDir * chimneyExitSpeedForward, ForceMode.Force);
            rb.AddForce(Vector3.up * chimneyExitSpeed, ForceMode.Force);
            if (isGrounded || transform.position.y > exitChimneyTarg.y+0.25f) { currChimneyExitTime = 0; }
            return;
        }

        if (currClamberTime > 0)
        {
            currClamberTime -= Time.deltaTime;
            rb.AddForce(exitChimneyDir * clamberExitSpeedForward, ForceMode.Force);
            rb.AddForce(Vector3.up * clamberExitSpeed, ForceMode.Force);
            if (isGrounded || transform.position.y > exitChimneyTarg.y + 0.25f) { currClamberTime = 0; }
            return;
        }

        if (isGrounded)
        {
            Vector3 slopeDir = Vector3.ProjectOnPlane(moveInput, groundNormal).normalized;
            Vector3 targetVelocity = (GetCrouching ? crouchSpeedMult : 1) * moveSpeed * Player.LocalPlayer.ph.GetMovespeedMult * (CanSprint ? 2.1f : 1) * slopeDir;
            Vector3 velocity = rb.linearVelocity;
            Vector3 velocityChange = targetVelocity - velocity;

            Vector3 force = Vector3.ClampMagnitude(velocityChange * acceleration, acceleration);
            rb.AddForce(force, ForceMode.Force);

            // Apply friction when grounded and not moving
            if (isGrounded && moveInput.magnitude < 0.1f)
            {
                Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                rb.AddForce(-horizontalVel * deceleration, ForceMode.Force);
            }
        }
        else
        {
            if (rawMoveInput.sqrMagnitude == 0f) return;

            // Convert input to world space relative to camera
            Vector3 camForward = Camera.main.transform.forward;
            Vector3 camRight = Camera.main.transform.right;
            camForward.y = 0;
            camRight.y = 0;
            Vector3 moveDir = (camForward.normalized * rawMoveInput.y + camRight.normalized * rawMoveInput.x).normalized;

            Vector3 velocity = rb.linearVelocity;
            Vector3 flatVel = new(velocity.x, 0f, velocity.z);
            float currentSpeedInMoveDir = Vector3.Dot(flatVel, moveDir);

            // Only accelerate if not over speed cap in that direction
            if (currentSpeedInMoveDir < airMaxSpeed)
            {
                float accelMultiplier = Mathf.Clamp01(1f - (currentSpeedInMoveDir / airMaxSpeed));
                Vector3 force = moveDir * airAcceleration * airControl * accelMultiplier;
                rb.AddForce(force, ForceMode.Acceleration);
            }
        }
    }

    private void SlideFixedUpdate()
    {
        Vector3 slopeDir = Vector3.ProjectOnPlane(currSlideDir, groundNormal).normalized;
        if(groundAngle >= 5) { 
            if(slopeDirectionDot > 0)
            {
                rb.AddForce(slopeDir * (groundAngle / slopeLimit * slideSlopeIncrease), ForceMode.Force);
            }
            else
            {
                rb.AddForce(-rb.linearVelocity.normalized * (groundAngle / slopeLimit * slideSlopeIncrease * 0.3f), ForceMode.Force);
            }
        }
        else
        {
            rb.AddForce(-rb.linearVelocity.normalized * slideDeceleration, ForceMode.Force);
        }

        float speed = rb.linearVelocity.magnitude;
        if (speed < 0.25f) { currMoveState = MoveStateEnum.Walking; }
        if(speed > maxSlideSpeed) { rb.AddForce(10 * slideDeceleration * -rb.linearVelocity.normalized, ForceMode.Force); }

        if (!isGrounded) { currMoveState = MoveStateEnum.Walking; }
    }

    public void TriggerShake(float intensity, float duration)
    {
        currentShakeIntensity = intensity;
        shakeTimer = duration;
    }

    void Jump()
    {
        coyoteTime = 0;
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        rb.AddForce(groundNormal * (currMoveState == MoveStateEnum.Sliding ? Mathf.Clamp(rb.linearVelocity.magnitude * slideJumpModifier, jumpForce, slideJumpCap) : jumpForce), ForceMode.Impulse);
    }

    void GroundCheck()
    {
        RaycastHit hit;
        Vector3 origin = transform.position + Vector3.up * (crouchCollider.radius + 0.1f); // slight lift

        if (Physics.SphereCast(origin, crouchCollider.radius, Vector3.down, out hit, Mathf.Infinity, groundLayer, QueryTriggerInteraction.Ignore))
        {
            groundAngle = Vector3.Angle(Vector3.up, hit.normal);
            distToGround = Mathf.Abs(transform.position.y - hit.point.y);
            isGrounded = groundAngle < slopeLimit && distToGround <= groundCheckDistance;
            groundNormal = hit.normal;

            Vector3 slopeDirection = Vector3.Cross(Vector3.Cross(Vector3.up, groundNormal), groundNormal).normalized;
            Vector3 moveDir = rb.linearVelocity.normalized;

            // Project movement onto slope direction
            slopeDirectionDot = Vector3.Dot(moveDir, slopeDirection);
        }
        else
        {
            groundAngle = 0f;
            distToGround = 999f;
            slopeDirectionDot = 0f;
            groundNormal = Vector3.up;
            isGrounded = false;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + forceCrouchOffset, forceCrouchRadius);

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, groundNormal);
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, Vector3.ProjectOnPlane(moveInput, groundNormal).normalized);

        if(currMoveState == MoveStateEnum.ChinmeyClimbing)
        {
            Gizmos.color = Color.yellow;
            Vector3 pos = transform.position + crouchCollider.center + Vector3.up * chimneyExitHeight + transform.forward * (crouchCollider.radius + chimneyExitCheckDistF);
            Gizmos.DrawWireSphere(pos, 0.1f);
            Gizmos.DrawLine(pos, pos + Vector3.down * chimneyExitCheckDistD);
        }

        if (currMoveState == MoveStateEnum.Walking)
        {
            Gizmos.color = Color.yellow;
            Vector3 pos = transform.position + crouchCollider.center + Vector3.up * clamberCheckHeight + transform.forward * (crouchCollider.radius + clamberCheckDistF);
            Gizmos.DrawWireSphere(pos, 0.1f);
            Gizmos.DrawLine(pos, pos + Vector3.down * clamberCheckDistD);
        }
    }
}