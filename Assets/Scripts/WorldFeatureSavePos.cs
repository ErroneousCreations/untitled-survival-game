using UnityEngine;

//mostly for grass
public class WorldFeatureSavePos : WorldFeature
{
    public override void LoadFromSavedData(string data)
    {
        if (Destroyable)
        {
            var split = data.Split(',');
            if (split.Length < 5)
            {
                Debug.LogError($"Invalid data format: {data}");
                return;
            }
            CurrHealth = float.Parse(split[0]);
            transform.position = new Vector3(float.Parse(split[1]), float.Parse(split[2]), float.Parse(split[3]));
            transform.eulerAngles = new Vector3(0, float.Parse(split[4]), 0);
        }
        else
        {
            var split = data.Split(',');
            if (split.Length < 4)
            {
                Debug.LogError($"Invalid data format: {data}");
                return;
            }
            transform.position = new Vector3(float.Parse(split[0]), float.Parse(split[1]), float.Parse(split[2]));
            transform.eulerAngles = new Vector3(0, float.Parse(split[3]), 0);
        }
    }

    public override string GetSavedData
    {
        get
        {
            if (Destroyable)
            {
                return $"{System.Math.Round(CurrHealth, 1)},{System.Math.Round(transform.position.x, 2)},{System.Math.Round(transform.position.y, 2)},{System.Math.Round(transform.position.z, 2)},{System.Math.Round(transform.eulerAngles.y, 2)}";
            }
            else
            {
                return $"{System.Math.Round(transform.position.x, 2)},{System.Math.Round(transform.position.y, 2)},{System.Math.Round(transform.position.z, 2)},{System.Math.Round(transform.eulerAngles.y, 2)}";
            }
        }
    }
}
