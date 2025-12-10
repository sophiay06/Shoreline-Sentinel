using UnityEngine;

public class MultiWaterWaveBinder : MonoBehaviour
{
    [Header("Assign ALL Water MeshRenderers you want to control")]
    public Renderer[] waterRenderers;

    [Header("Wave height range (shader units)")]
    public float minHeight = 0f;
    public float maxHeight = 2f;

    [Tooltip("Usually _WaveHeight for StylizedWater2")]
    public string waveHeightProperty = "_WaveHeight";

    MaterialPropertyBlock mpb;
    int waveID = -1;
    float lastApplied = float.NaN;

    void Awake()
    {
        if (mpb == null) mpb = new MaterialPropertyBlock();
        if (string.IsNullOrEmpty(waveHeightProperty)) waveHeightProperty = "_WaveHeight";
        waveID = Shader.PropertyToID(waveHeightProperty);
    }

    void OnValidate()
    {
        if (string.IsNullOrEmpty(waveHeightProperty)) waveHeightProperty = "_WaveHeight";
        waveID = Shader.PropertyToID(waveHeightProperty);
        if (minHeight > maxHeight) maxHeight = minHeight;
    }

    public void Apply(float height)
    {
        if (waterRenderers == null || waterRenderers.Length == 0) return;

        if (!float.IsNaN(lastApplied) && Mathf.Approximately(lastApplied, height))
            return;

        lastApplied = height;

        foreach (var r in waterRenderers)
        {
            if (!r) continue;
            r.GetPropertyBlock(mpb);
            mpb.Clear();
            mpb.SetFloat(waveID, height);
            r.SetPropertyBlock(mpb);
        }
    }

    public void ApplyNormalized(float t01)
    {
        Apply(Mathf.Lerp(minHeight, maxHeight, Mathf.Clamp01(t01)));
    }
}
