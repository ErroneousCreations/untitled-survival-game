using UnityEngine;

public class WorldFeature_Tree : WorldFeature
{
    private float DropItemsTime = 0;

    public override void LoadFromSavedData(string data)
    {
        var split = data.Split(',');
        CurrHealth = float.Parse(split[0]);
        DropItemsTime = float.Parse(split[1]);
        
    }

    public override string GetSavedData
    {
        get
        {
            return $"{System.Math.Round(CurrHealth, 1)},{System.Math.Round(transform.position.x, 2)},{System.Math.Round(transform.position.y, 2)},{System.Math.Round(transform.position.z, 2)},{System.Math.Round(transform.eulerAngles.y, 2)}";
        }
    }
}
