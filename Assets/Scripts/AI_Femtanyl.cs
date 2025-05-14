using UnityEngine;

public class AI_Femtanyl : MonoBehaviour
{
    public AILocomotor locomotor;

    private void Update()
    {
        if (Player.LocalPlayer)
        {
            locomotor.SetDestination(Player.LocalPlayer.transform.position);
        }
    }
}
