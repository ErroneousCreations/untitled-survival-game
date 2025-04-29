using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using Unity.Services.Vivox;
using Unity.Services.Vivox.AudioTaps;
using UnityEngine.Audio;
using Unity.Collections;
using System.Collections;

public class Player : NetworkBehaviour
{
    [Header("Mouselook Settings")]
    [SerializeField] private float Smoothing = 2f;
    [SerializeField] private float maxMouseAngularSpeed = 720f, shockJitterIntensityMax; // degrees per second
    [SerializeField] private Transform head;
    [SerializeField] private Vector2 headAngleRange;
    private Vector2 currentMouseLook;
    private Vector2 appliedMouseDelta;
    [Header("Components")]
    public PlayerMovement pm;
    public PlayerHealthController ph;
    public PlayerInventory pi;
    public Animator anim;
    public Renderer[] PlayermodelMeshes;
    [SerializeField] private Renderer PlayerBody;
    [SerializeField] private int skinMaterialID;
    [SerializeField] private GameObject speakingIndicator;
    [Header("Physics Settings")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private float standupForce = 100, standupDamping = 5, falloverForce = 1, fallRecoverSpeed = 10;
    public float incapacitatedgroundDrag = 5f;
    public float incapacitatedangularDrag = 5f;
    public float incapacitatedrotationStrength = 10f;
    [Header("Fall Damage")]
    [SerializeField] private float minimumFallVelocity = 5f;
    [SerializeField] private float lethalFallVelocity = 20f, knockdownThreshold = 10f, damageMultiplier = 1.5f;
    [Header("Eyes")]
    [SerializeField] private MeshRenderer Eyes;
    [SerializeField] private List<Texture2D> EyeTextures;
    [Header("Misc")]
    public AudioMixer Mixer;
    [Header("Hunger effects")]
    public AudioSource hungryAudiosource;

    [Tooltip("Mild Hunger (0.35 - 0.6)")]
    public AudioClip[] stomachRumbles;

    [Tooltip("Severe Hunger (< 0.35)")]
    public AudioClip[] extremeHungerSounds;

    private Coroutine hungerAudioRoutine;

    private Material eyesmat;

    private NetworkVariable<float> SyncedEyeTexture = new(0, writePerm: NetworkVariableWritePermission.Owner);
    private NetworkVariable<float> currLookAngle = new(0, writePerm: NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> isTalking = new(false, writePerm: NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> isSprinting = new(false, writePerm: NetworkVariableWritePermission.Owner);
    private NetworkVariable<FixedString128Bytes> username = new("", writePerm: NetworkVariableWritePermission.Owner);
    private float blinkCurr = 0;
    private float currfalloverTime = 0;
    private float currStandForce = 0;
    private bool inChannel = false;
    private Material skinMat;
    private float beatTimer;

    private VivoxParticipant participant;

    //shock camera jitter
    private float jitterCd;

    public static bool GetCanStand => LocalPlayer.ph.GetCanStand && LocalPlayer.currfalloverTime <= 0; // Can stand if not falling over and not in a fallover state

    public static Player LocalPlayer { get; private set; }

    public static Vector3 GetLocalPlayerCentre => LocalPlayer.transform.TransformPoint(LocalPlayer.pm.crouchCollider.center);

    public static bool GetLocalPlayerInvBusy => LocalPlayer.pi.GetBusy;

    public Rigidbody GetRigidbody => pm.GetRigidbody;

    public string GetUsername => username.Value.ToString();
    
    private int GetFace
    {
        get
        {
            int returned = 0;
            if (pm.GetRigidbody.linearVelocity.sqrMagnitude > 7 * 7) { returned = 1; }
            if(ph.GetBleedSpeed > ph.bleedRatePerWound * 1.1f) { returned = 2; }
            if(ph.consciousness.Value <= 0.6f) { returned = 3; }
            if (ph.GetRecentDamage || blinkCurr <= 0.1f) { returned = 4; }
            if (!ph.isConscious.Value || currfalloverTime>0) { returned = 4; }
            if(!ph.heartBeating.Value) { returned = 5; }
            return returned;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log(collision.relativeVelocity.magnitude);

        // Filter out minor scrapes
        if (collision.relativeVelocity.magnitude < minimumFallVelocity) return;

        // Optional: Only consider ground collisions
        if (collision.gameObject.layer != LayerMask.NameToLayer("Terrain")) return;

        float fallVelocity = collision.relativeVelocity.magnitude;

        float damage = (fallVelocity - minimumFallVelocity) * damageMultiplier;

        var contactpos = collision.GetContact(0).point;
        var contactnorm = -collision.GetContact(0).normal;

        // Cap damage at lethal velocity
        if (fallVelocity >= lethalFallVelocity)
        {
            damage = 9999f; // Instakill, or do custom death
        }

        // Apply knockdown?
        if (fallVelocity >= knockdownThreshold)
        {
            KnockOver(fallVelocity / knockdownThreshold * 3); // Knockdown time is proportional to fall velocity
        }

        ph.ApplyDamage(damage, DamageType.Blunt, contactpos, contactnorm); // Or however your system takes damage
    }

    public void KnockOver(float time)
    {
        currfalloverTime = time;
        currStandForce = 0;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        foreach (var rend in PlayermodelMeshes)
        {
            rend.shadowCastingMode = IsOwner ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On; //only show shadows for our own body
        }
        if (IsOwner)
        {
            hungerAudioRoutine = StartCoroutine(HungerSoundLoop());
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Camera.main.transform.parent = pm.CameraParent;
            Camera.main.transform.localPosition = Vector3.zero;
            pm.ViewmodelParent.parent = Camera.main.transform;
            LocalPlayer = this;
            SyncedEyeTexture.Value = 0;
            blinkCurr = Random.Range(2.1f, 2.6f);
            VivoxManager.JoinMainChannel(() =>
            {
                inChannel = true;
                foreach (var kvp in VivoxService.Instance.ActiveChannels[VivoxManager.DEFAULTCHANNEL])
                {
                    FindOwnParticipant(kvp);
                }
            });
            username.Value = PlayerPrefs.GetString("USERNAME", "NoName");
        }
        skinMat = PlayerBody.materials[skinMaterialID];
        eyesmat = Eyes.material;
        eyesmat.mainTexture = EyeTextures[0];
    }

    IEnumerator HungerSoundLoop()
    {
        yield return new WaitForSeconds(1f); // wait for a second to avoid playing sound immediately
        while (true)
        {
            if (!ph.isAlive.Value) { yield break; }
            float waitTime = Random.Range(7f, 25f);

            if (ph.GetHunger < 0.35f && extremeHungerSounds.Length > 0)
            {
                waitTime = Random.Range(2f, 10f);
                waitTime += PlayRandomClip(extremeHungerSounds, 1);
            }
            else if (ph.GetHunger < 0.6f && stomachRumbles.Length > 0)
            {
                waitTime += PlayRandomClip(stomachRumbles, 0);
            }

            yield return new WaitForSeconds(waitTime);
        }
    }

    float PlayRandomClip(AudioClip[] clips, int array)
    {
        if (hungryAudiosource == null || clips.Length == 0) return 0;
        var index = Random.Range(0, clips.Length);
        var clip = clips[index];
        hungryAudiosource.Stop();
        var pitch = Random.Range(0.8f, 1.2f);
        hungryAudiosource.pitch = pitch;
        hungryAudiosource.clip = clip; // slight pitch/volume variation
        hungryAudiosource.Play();
        PlayClipRPC(array, index, pitch); // send RPC to play the clip on other clients
        return clip.length;
    }

    [Rpc(SendTo.NotOwner)]
    void PlayClipRPC(int array, int clip, float pitch)
    {
        hungryAudiosource.Stop();
        hungryAudiosource.pitch = pitch;
        hungryAudiosource.clip = array == 0 ? stomachRumbles[clip] : extremeHungerSounds[clip]; // slight pitch/volume variation
        hungryAudiosource.Play();
    }

    void FindOwnParticipant(VivoxParticipant part)
    {
        if (part.DisplayName != Extensions.UniqueIdentifier) return;

        participant = part;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (IsOwner) {
            pm.ViewmodelParent.parent = pm.CameraParent; // detach viewmodel parent
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Camera.main.transform.parent = null;
            Camera.main.transform.localPosition = Vector3.zero;
            if (VivoxManager.InMainChannel && !VivoxManager.LeavingChannel)
            {
                VivoxManager.LeavingChannel = true; // set leaving channel to true to prevent the leave channel callback from being called twice
                VivoxManager.LeaveMainChannel(()=> { VivoxManager.LeavingChannel = false; });
            }
            participant = null;
        }
    }

    private void Update()
    {
        skinMat.SetFloat("_Paleness", 1-ph.currentBlood.Value);
        head.localRotation = Quaternion.AngleAxis(Mathf.Clamp(currLookAngle.Value, headAngleRange.x, headAngleRange.y) + (pm.GetCrouching ? 49 : 0), Vector3.forward);
        eyesmat.mainTexture = EyeTextures[(int)SyncedEyeTexture.Value];
        anim.SetBool("Crouching", pm.GetCrouching && StandingUp);
        anim.SetBool("KO", !ph.breathing.Value);
        anim.SetBool("Sprinting", isSprinting.Value);
        speakingIndicator.SetActive(isTalking.Value && !IsOwner);
        speakingIndicator.transform.forward = (Camera.main.transform.position - speakingIndicator.transform.position).normalized;

        if (!IsOwner) { return; }

        // Muffle sound with low-pass filter
        float cutoff = ph.isConscious.Value ? Mathf.Lerp(11000f, 400f, Mathf.Clamp01(1f - ph.consciousness.Value)) : 22000f;
        Mixer.SetFloat("LowPass", cutoff);

        // Lower pitch slightly
        float pitch = Mathf.Lerp(1f, 0.8f, Mathf.Clamp01(1f - ph.consciousness.Value));
        Mixer.SetFloat("PitchShift", pitch);

        //heartbeat
        if (ph.isConscious.Value) { ScreenEffectsManager.SetVignette(1 - ph.consciousness.Value); }
        else if (ph.heartBeating.Value)
        {
            beatTimer += Time.deltaTime;

            // Heartbeat as a repeated sharp pulse
            float t = Mathf.Repeat(beatTimer, 1) / 1f;
            float heartbeatvalue = Mathf.Pow(Mathf.Clamp01(1f - t), 1.5f);
            ScreenEffectsManager.SetVignette(0.9f + (heartbeatvalue*0.1f));
        }
        else { ScreenEffectsManager.SetVignette(1); }
        ScreenEffectsManager.SetSaturation(1-ph.currentBlood.Value); // 0 to -100
        ScreenEffectsManager.SetAberration(ph.shock.Value * 0.8f);
        ScreenEffectsManager.SetMotionBlur(ph.GetBlurriness);
        UIManager.SetBodyDamage(ph.bodyHealth.Value);
        UIManager.SetHeadDamage(ph.headHealth.Value);
        UIManager.SetFeetDamage(ph.legHealth.Value);

        if (inChannel && !ph.isConscious.Value && !VivoxManager.GetisMuted) { VivoxManager.ToggleInputMute(); }
        if(inChannel && ph.isConscious.Value && Input.GetKeyDown(KeyCode.V)) { VivoxManager.ToggleInputMute(); }
        if (inChannel && ph.isAlive.Value) { VivoxManager.SetPosition(gameObject); }
        UIManager.SetMuteIcon(!VivoxManager.GetisMuted && inChannel);
        isSprinting.Value = pm.GetIsSprinting;
        isTalking.Value = ph.isConscious.Value && participant != null && participant.SpeechDetected;
        blinkCurr -= Time.deltaTime;
        if (blinkCurr <= 0) { blinkCurr = Random.Range(2.1f, 2.6f); }
        SyncedEyeTexture.Value = GetFace;
        if(currfalloverTime > 0) { currfalloverTime -= Time.deltaTime; } // fallover time countdown
        if (currfalloverTime > 0 || !ph.GetCanStand) { currStandForce = 0; } // fallover time countdown
        else { currStandForce = Mathf.Lerp(currStandForce, standupForce, fallRecoverSpeed*Time.deltaTime); }
        if (currfalloverTime > 0 || !ph.GetCanStand) { return; }
        MouseLook();
    }

    private void MouseLook()
    {
        float sens = PlayerPrefs.GetFloat("SENS", 2);
        float consciousnessDrift = ph.MouseTrippyness;

        float driftSmoothing = Mathf.Lerp(1f, 60f, consciousnessDrift); // lower value = more delay
        Vector2 rawInput = new(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        Vector2 scaledInput = sens * Vector2.Scale(rawInput, Vector2.one);
        if(ph.shock.Value > 0.2f) { 
            jitterCd -= Time.deltaTime;
            if(jitterCd <= 0)
            {
                scaledInput += new Vector2(Random.Range(-shockJitterIntensityMax, shockJitterIntensityMax) * ph.shock.Value, Random.Range(-shockJitterIntensityMax, shockJitterIntensityMax) * ph.shock.Value);
                jitterCd = Random.Range(0.025f, 1f);
            }
        }
        appliedMouseDelta = Vector2.Lerp(appliedMouseDelta, scaledInput, (1 / Smoothing) * (1/driftSmoothing));

        if (!UIManager.GetPauseMenuOpen) { currentMouseLook += appliedMouseDelta; } 
        //currentMouseLook += appliedMouseDelta;
        currentMouseLook.y = Mathf.Clamp(currentMouseLook.y, -85, 90);

        // Apply rotations
        if (GetCanStand) {
            currLookAngle.Value = currentMouseLook.y;
            Camera.main.transform.localRotation = Quaternion.AngleAxis(-currentMouseLook.y, Vector3.right);
        }
    }

    private bool StandingUp => Vector3.Angle(transform.up, Vector3.up) < 10;

    private void FixedUpdate()
    {
        if(!IsOwner) { return; }
        if (GetCanStand)
        {
            ApplyMouseTorque();
            ApplyUprightTorque();
        }
        else if (StandingUp) { rb.AddForceAtPosition(transform.forward * falloverForce, transform.TransformPoint(new Vector3(0, pm.crouchCollider.height, 0)), ForceMode.Force); } //pushes over then stops applying force
        else
        {
            ApplyFriction();
        }
    }

    void ApplyFriction()
    {
        // Simulate friction to reduce sliding
        Vector3 lateralVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
        Vector3 frictionForce = -lateralVelocity * incapacitatedgroundDrag;
        rb.AddForce(frictionForce, ForceMode.Acceleration);

        rb.angularVelocity *= 1f / (1f + incapacitatedangularDrag * Time.fixedDeltaTime);
    }

    void ApplyUprightTorque()
    {
        Vector3 desiredUp = Vector3.up;
        Vector3 currentUp = transform.up;

        Vector3 torqueAxis = Vector3.Cross(currentUp, desiredUp);
        float angle = Vector3.Angle(currentUp, desiredUp) * Mathf.Deg2Rad;

        Vector3 correctiveTorque = (torqueAxis.normalized * angle * currStandForce * ph.GetStandDampeningMult) - (ph.GetStandDampeningMult * standupDamping * rb.angularVelocity);
        rb.AddTorque(correctiveTorque, ForceMode.Acceleration);
    }

    void ApplyMouseTorque()
    {
        float currentYaw = rb.rotation.eulerAngles.y;
        float deltaYaw = Mathf.DeltaAngle(currentYaw, currentMouseLook.x);

        // Convert delta yaw to angular velocity around up axis
        float angularSpeedRad = deltaYaw * Mathf.Deg2Rad / Time.fixedDeltaTime;
        float maxSpeedRad = maxMouseAngularSpeed * Mathf.Deg2Rad;

        float clampedAngularSpeed = Mathf.Clamp(angularSpeedRad, -maxSpeedRad, maxSpeedRad);
        Vector3 angularVelocity = Vector3.up * clampedAngularSpeed;

        // Preserve any current angular velocity on other axes (from physics)
        Vector3 preservedXZ = Vector3.ProjectOnPlane(rb.angularVelocity, Vector3.up);
        rb.angularVelocity = preservedXZ + angularVelocity;
    }
}
