using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    [SerializeField] private float parallaxStrength = 0.05f;
    [SerializeField] private Vector2 maxOffset = new Vector2(150f, 150f);

    private Transform[] layers;
    private Vector3[] startPositions;

    void Start()
    {
        int count = transform.childCount;
        layers = new Transform[count];
        startPositions = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            layers[i] = transform.GetChild(i);
            startPositions[i] = layers[i].localPosition;
        }
    }

    void Update()
    {
        Vector2 mousePos = Input.mousePosition;
        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
        Vector2 offset = (mousePos - screenCenter) / screenCenter; // range [-1, 1]

        for (int i = 0; i < layers.Length; i++)
        {
            float layerDepth = (float)i / layers.Length;
            Vector3 targetOffset = new Vector3(
                offset.x * maxOffset.x * layerDepth * parallaxStrength,
                offset.y * maxOffset.y * layerDepth * parallaxStrength,
                0f);

            layers[i].localPosition = startPositions[i] + targetOffset;
        }
    }
}