using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ScreenEffectsManager : MonoBehaviour
{
    public Volume volume;

    private Vignette vignette;
    private ColorAdjustments colorAdjustments;
    private ChromaticAberration chromaticAberration;
    private DepthOfField depthOfField;

    // Optional extras
    private FilmGrain filmGrain;
    private MotionBlur motionBlur;

    private static ScreenEffectsManager instance;

    void Awake()
    {
        if (!volume.profile.TryGet(out vignette)) Debug.LogWarning("No Vignette found!");
        if (!volume.profile.TryGet(out colorAdjustments)) Debug.LogWarning("No Color Adjustments found!");
        if (!volume.profile.TryGet(out chromaticAberration)) Debug.LogWarning("No Chromatic Aberration found!");
        if (!volume.profile.TryGet(out depthOfField)) Debug.LogWarning("No Depth of Field found!");
        volume.profile.TryGet(out filmGrain);
        volume.profile.TryGet(out motionBlur);
        instance = this;
    }

    public static void SetVignette(float intensity)
    {
        if (instance.vignette != null)
        {
            instance.vignette.intensity.value = Mathf.Clamp01(intensity); // 0 to 1
        }
    }

    public static void SetDepthOfField(float intensity)
    {
        if (instance.depthOfField != null)
        {
            instance.depthOfField.focusDistance.value = Mathf.Lerp(2.5f, 0.1f, intensity) ; // 0 to 1
            instance.depthOfField.focusDistance.overrideState = intensity > 0;
        }
    }

    public static void SetSaturation(float amount)
    {
        if (instance.colorAdjustments != null)
        {
            instance.colorAdjustments.saturation.value = Mathf.Lerp(0, -100f, Mathf.Clamp01(amount)); // full to grayscale
        }
    }

    public static void SetAberration(float intensity)
    {
        if (instance.chromaticAberration != null)
        {
            instance.chromaticAberration.intensity.value = Mathf.Clamp01(intensity); // adds distortion
        }
    }

    public static void SetFilmGrain(float intensity)
    {
        if (instance.filmGrain != null)
        {
            instance.filmGrain.intensity.value = Mathf.Clamp01(intensity);
        }
    }

    public static void SetMotionBlur(float amount)
    {
        if (instance.motionBlur != null)
        {
            instance.motionBlur.intensity.value = Mathf.Clamp01(amount);
        }
    }
}