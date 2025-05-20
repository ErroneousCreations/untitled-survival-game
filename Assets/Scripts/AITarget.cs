using System.Collections.Generic;
using UnityEngine;

public class AITarget : MonoBehaviour
{
    public static List<AITarget> targets = new List<AITarget>();
    public string TargetType;

    private void OnEnable()
    {
        targets.Add(this);
    }

    private void OnDisable()
    {
        targets.Remove(this);
    }
}
