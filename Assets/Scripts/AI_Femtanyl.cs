using UnityEngine;
using Unity.Netcode;

public class AI_Femtanyl : NetworkBehaviour
{
    public enum FemtanylAIState { Idle, Scouting, ReturningWithNews, Searching, FightingTarget, GrabbingItem, Retreating }

    [Header("AI Settings")]
    public FemtanylAIState state;
    public AILocomotor locomotor;
    public float IdleWanderRange;
    public float ScoutRange;

    [Header("Visuals")]
    public Gradient SkinColourGradient, ClothesColourGradient;
    public Transform model;
    public Renderer BodyRenderer;
    public Renderer[] HandRends;

    //seed for rng appearance and behaviour and stuff
    private int ID;
    public void InitialiseCreature(int id, Vector3 nestpos, bool scout)
    {
        homelocation = nestpos;
        ID = id;
        this.scout = scout;
    }

    //characteristics
    private float speedmod = 1, eyeblinktime = 2.5f, throwwindup = 2f, reactiontime = 0.5f, wandercooldown, scoutcooldown;
    private Vector3 homelocation, wanderposition, newsPosition;
    private bool scout;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsOwner) { SyncIDRPC(ID); }
    }

    [Rpc(SendTo.Everyone)]
    private void SyncIDRPC(int id) { ID = id; UpdateRandomCharacteristics(); }

    private void UpdateRandomCharacteristics()
    {
        Random.InitState(ID);
        var skincol = SkinColourGradient.Evaluate(Random.Range(0f, 1f));
        BodyRenderer.materials[0].color = skincol;
        BodyRenderer.materials[1].color = ClothesColourGradient.Evaluate(Random.Range(0f, 1f));
        for (int i = 0; i < HandRends.Length; i++)
        {
            HandRends[i].material.color = skincol;
        }
        speedmod = Random.Range(0.96f, 1.05f);
        eyeblinktime = Random.Range(2f, 3f);
        throwwindup = Random.Range(1.9f, 2.2f);
        reactiontime = Random.Range(0.4f, 0.6f);
    }

    private void Update()
    {
        switch (state)
        {
            case FemtanylAIState.Idle:
                IdleUpdate();
                break;
            case FemtanylAIState.Scouting:
                ScoutUpdate();
                break;
            case FemtanylAIState.ReturningWithNews:
                break;
            case FemtanylAIState.Searching:
                break;
            case FemtanylAIState.FightingTarget:
                break;
            case FemtanylAIState.GrabbingItem:
                break;
            case FemtanylAIState.Retreating:
                break;
        }
    }

    void IdleUpdate()
    {
        wandercooldown -= Time.deltaTime;
        if (wandercooldown <= 0)
        {
            wanderposition = homelocation + Extensions.RandomCircle * IdleWanderRange;
            locomotor.SetDestination(wanderposition);
            wandercooldown = Random.Range(1f, 3f);
        }
        if ((locomotor.transform.position - wanderposition).sqrMagnitude < 0.25f)
        {
            locomotor.Stop();
            wandercooldown = Random.Range(1f, 3f);
        }

        if (scoutcooldown > 0) { scoutcooldown -= Time.deltaTime; }
        if(scout && scoutcooldown <= 0)
        {
            state = FemtanylAIState.Scouting;
            scoutcooldown = Random.Range(10f, 15f);
            newsPosition = homelocation;
            wanderposition = homelocation + Extensions.RandomCircle * ScoutRange;
            locomotor.SetDestination(wanderposition);
            return;
        }
    }

    private void ScoutUpdate()
    {
        if (scoutcooldown > 0 && (transform.position - wanderposition).sqrMagnitude < 1) { scoutcooldown -= Time.deltaTime; }
        if (scoutcooldown <= 0)
        {
            state = FemtanylAIState.ReturningWithNews;
            scoutcooldown = Random.Range(120f, 180f);
            locomotor.SetDestination(homelocation);
            return;
        }
    }
}
