using UnityEngine;

[ExecuteAlways]
public class WaveHeightController : MonoBehaviour
{
    [Header("Range (scene UI uses normalized 0..1)")]
    public float minHeight = 0f;
    public float maxHeight = 2f;

    [Header("Current value (for debugging)")]
    public float currentHeight = 0.8f;

    //internals
    private Renderer waterRenderer;
    private MaterialPropertyBlock _mpb;
    private static readonly int WaveHeightID = Shader.PropertyToID("_WaveHeight");

    void Awake()
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();

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

    public void SetNormalized(float t)
    {
        float h = Mathf.Lerp(minHeight, maxHeight, Mathf.Clamp01(t));
        SetHeight(h);
    }

    public void SetHeight(float height)
    {
        height = Mathf.Clamp(height, minHeight, maxHeight);
        currentHeight = height;
        ApplyHeight(height);
    }

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
