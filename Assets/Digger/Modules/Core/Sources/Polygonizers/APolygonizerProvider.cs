using UnityEngine;

namespace Digger.Modules.Core.Sources.Polygonizers
{
    public abstract class APolygonizerProvider : MonoBehaviour
    {
        public abstract void Init();
        public abstract IPolygonizer NewPolygonizer(DiggerSystem digger);
        public abstract Material[] GetMaterials();
    }
}