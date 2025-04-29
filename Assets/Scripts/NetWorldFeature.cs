using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

public class NetWorldFeature : NetworkBehaviour
{
    // first one is the index of what world feature it is (e.g. house or tree) second is the index of the generated feature in the world (if its house 1 or house 2 etc) and last one is the index of the type of feature (e.g. house type 1 or house type 2)
    private NetworkVariable<int> worldFeatureIndex = new(), generatedFeatureIndex = new(), featureTypeIndex = new();

    public void Init(int worldFeatureIndex, int generatedFeatureIndex, int featureTypeIndex)
    {
        this.worldFeatureIndex.Value = worldFeatureIndex;
        this.generatedFeatureIndex.Value = generatedFeatureIndex;
        this.featureTypeIndex.Value = featureTypeIndex;
    }

    public void RemoveFeature()
    {
        //call function on World to remove it todo
    }

    public void ChangeFeature(int newfeaturetype)
    {
        //call func on World to change it todo
    }

    /// <summary>
    /// The index of the world feature in the worldgenerator script (e.g. house or tree)
    /// </summary>
    public int GetFeatureIndex => worldFeatureIndex.Value;
    /// <summary>
    /// The index of the generated feature in the world (e.g. house 1 or house 2 etc)
    /// </summary>
    public int GetGeneratedFeatureIndex => generatedFeatureIndex.Value;
    /// <summary>
    /// The index of the type of feature (e.g. house type 1 or house type 2)
    /// </summary>
    public int GetFeatureType => featureTypeIndex.Value;
}
