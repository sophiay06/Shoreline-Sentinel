using UnityEngine;

[ExecuteAlways]
public class WaveHeightController : MonoBehaviour
{
    [Header("Range (scene UI uses normalized 0..1)")]
    public float minHeight = 0f;
    public float maxHeight = 2f;

    [Header("Current value (for debugging)")]
    public float currentHeight = 0.8f;

    // Internals
    private Renderer waterRenderer;
    private MaterialPropertyBlock _mpb;
    private static readonly int WaveHeightID = Shader.PropertyToID("_WaveHeight");

    void Awake()
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        // Always look for the scene object called "Water"
        if (waterRenderer == null)
        {
            GameObject waterObj = GameObject.Find("Water");
            if (waterObj != null)
            {
                waterRenderer = waterObj.GetComponent<Renderer>();
            }
        }
    }

    void OnValidate()
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        currentHeight = Mathf.Clamp(currentHeight, minHeight, maxHeight);
    }

    /// <summary>
    /// Set via UI with a normalized slider value [0..1].
    /// </summary>
    public void SetNormalized(float t)
    {
        float h = Mathf.Lerp(minHeight, maxHeight, Mathf.Clamp01(t));
        SetHeight(h);
    }

    /// <summary>
    /// Set an absolute height value (in shader units).
    /// </summary>
    public void SetHeight(float height)
    {
        height = Mathf.Clamp(height, minHeight, maxHeight);
        currentHeight = height;
        ApplyHeight(height);
    }

    /// <summary>
    /// Read current normalized value (useful to initialize a slider from the scene).
    /// </summary>
    public float GetNormalized()
    {
        float h = currentHeight;
        return Mathf.InverseLerp(minHeight, maxHeight, h);
    }

    private void ApplyHeight(float height)
    {
        if (waterRenderer == null) return;

        waterRenderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat(WaveHeightID, height);
        waterRenderer.SetPropertyBlock(_mpb);
    }
}
