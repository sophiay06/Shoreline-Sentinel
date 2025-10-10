using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class UIBinder : MonoBehaviour
{
    [Header("Targets")]
    public DuneController dune;
    public VegetationController veg;
    public SandbarController bar;

    [Header("Water hookup")]
    [Tooltip("Renderer that draws the water mesh. Leave empty and enable Auto Discover if unsure.")]
    public Renderer waterRenderer;
    [Tooltip("StylizedWater2_Demo material asset (global change). Leave empty if using a Renderer.")]
    public Material waterMaterialAsset;

    [Header("Auto discover (single mesh only)")]
    public bool autoDiscoverWater = true;
    public string waterRendererNameHint = "Water";

    [Header("Wave property")]
    [Tooltip("Shader property name for wave height (_WaveHeight for StylizedWater2).")]
    public string waveHeightProperty = "_WaveHeight";
    public float waveMin = 0f;
    public float waveMax = 2f;

    [Header("Sandbar position → protection mapping")]
    [Tooltip("If true: bar.maxZOffset means 'closest to beach'. If false: bar.maxZOffset means 'farthest'.")]
    public bool zOffsetMaxIsCloser = true;

    [Header("UI Sliders (normalized 0..1)")]
    public Slider sWaveHeight;
    public Slider sDuneHeight;
    public Slider sDuneWidth;
    public Slider sVegDensity;
    public Slider sBarHeight;
    public Slider sBarZ; // 0=far? 1=close? (we'll map with bar.minZOffset/maxZOffset)

    [Header("Multi-water (preferred when multiple water meshes)")]
    public MultiWaterWaveBinder multiBinder;

    // ---- internals ----
    MaterialPropertyBlock _mpb;
    int _waveID = -1;
    string _resolvedProp = null;
    float _normalizedWave = 0.5f; // single source of truth for wave in [0,1]
    bool _waveDirty = false;

    void Awake()
    {
        if (string.IsNullOrEmpty(waveHeightProperty)) waveHeightProperty = "_WaveHeight";
        _waveID = Shader.PropertyToID(waveHeightProperty);
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
    }

    void Start()
    {
        if (autoDiscoverWater && !waterRenderer)
        {
            var all = FindObjectsOfType<Renderer>(includeInactive: true);
            waterRenderer = all.FirstOrDefault(r =>
                r && r.sharedMaterial && r.sharedMaterial.shader && r.name.Contains(waterRendererNameHint));
            if (waterRenderer)
                Debug.Log($"[UIBinder] Auto-discovered water renderer: {waterRenderer.name}");
            else
                Debug.LogWarning("[UIBinder] Auto-discover failed. Assign Water Renderer or use MultiWaterWaveBinder.");
        }

        ResolveWaveProperty();
        HookUI();

        // initialize controller states from sliders (if present)
        ApplyDuneFromUI();
        ApplyVegetationFromUI();
        ApplySandbarFromUI();

        // initialize wave from UI
        if (sWaveHeight) _normalizedWave = Mathf.Clamp01(sWaveHeight.value);
        _waveDirty = true;
        ApplyWaveNow(); // push once
    }

    void OnDestroy()
    {
        UnhookUI();
    }

    // ------------------- UI wiring -------------------

    void HookUI()
    {
        if (sWaveHeight) sWaveHeight.onValueChanged.AddListener(OnWaveSliderChanged);
        if (sDuneHeight) sDuneHeight.onValueChanged.AddListener(_ => { ApplyDuneFromUI(); RecalculateWaveHeight(); });
        if (sDuneWidth) sDuneWidth.onValueChanged.AddListener(_ => { ApplyDuneFromUI(); RecalculateWaveHeight(); });
        if (sVegDensity) sVegDensity.onValueChanged.AddListener(_ => { ApplyVegetationFromUI(); RecalculateWaveHeight(); });
        if (sBarHeight) sBarHeight.onValueChanged.AddListener(_ => { ApplySandbarFromUI(); RecalculateWaveHeight(); });
        if (sBarZ) sBarZ.onValueChanged.AddListener(_ => { ApplySandbarFromUI(); RecalculateWaveHeight(); });
    }

    void UnhookUI()
    {
        if (sWaveHeight) sWaveHeight.onValueChanged.RemoveListener(OnWaveSliderChanged);
        if (sDuneHeight) sDuneHeight.onValueChanged.RemoveAllListeners();
        if (sDuneWidth) sDuneWidth.onValueChanged.RemoveAllListeners();
        if (sVegDensity) sVegDensity.onValueChanged.RemoveAllListeners();
        if (sBarHeight) sBarHeight.onValueChanged.RemoveAllListeners();
        if (sBarZ) sBarZ.onValueChanged.RemoveAllListeners();
    }

    // ------------------- Controller forwarding -------------------

    void ApplyDuneFromUI()
    {
        if (!dune) return;
        if (sDuneHeight)
            dune.heightScale = Mathf.Lerp(dune.minHeightScale, dune.maxHeightScale, Mathf.Clamp01(sDuneHeight.value));
        if (sDuneWidth)
            dune.widthScale = Mathf.Lerp(dune.minWidthScale, dune.maxWidthScale, Mathf.Clamp01(sDuneWidth.value));
    }

    void ApplyVegetationFromUI()
    {
        if (!veg) return;
        if (sVegDensity)
            veg.density = Mathf.Clamp01(sVegDensity.value);
    }

    void ApplySandbarFromUI()
    {
        if (!bar) return;
        if (sBarHeight)
            bar.heightScale = Mathf.Lerp(bar.minHeightScale, bar.maxHeightScale, Mathf.Clamp01(sBarHeight.value));
        if (sBarZ)
            bar.zOffset = Mathf.Lerp(bar.minZOffset, bar.maxZOffset, Mathf.Clamp01(sBarZ.value));
    }

    // ------------------- Wave logic -------------------

    public void OnWaveSliderChanged(float v01)
    {
        SetWaveNormalized(v01);
    }

    void SetWaveNormalized(float t01)
    {
        float c = Mathf.Clamp01(t01);
        if (!Mathf.Approximately(_normalizedWave, c))
        {
            _normalizedWave = c;
            _waveDirty = true;

            // keep slider visually in sync WITHOUT re-firing event
            if (sWaveHeight) sWaveHeight.SetValueWithoutNotify(c);
        }
    }

    void LateUpdate()
    {
        if (_waveDirty)
        {
            ApplyWaveNow();
            _waveDirty = false;
        }
    }

    void ApplyWaveNow()
    {
        float height = Mathf.Lerp(waveMin, waveMax, _normalizedWave);

        // Preferred path: multi-mesh applier
        if (multiBinder)
        {
            multiBinder.Apply(height);
            return;
        }

        // Fallback: single water renderer via MPB
        if (waterRenderer && !string.IsNullOrEmpty(_resolvedProp))
        {
            waterRenderer.GetPropertyBlock(_mpb);
            _mpb.Clear();
            _mpb.SetFloat(_resolvedProp, height);
            waterRenderer.SetPropertyBlock(_mpb);
            return;
        }

        // Last resort: write to shared material asset (global)
        if (waterMaterialAsset && !string.IsNullOrEmpty(_resolvedProp))
        {
            waterMaterialAsset.SetFloat(_resolvedProp, height);
        }
    }

    // ------------------- Protection model -------------------

    public void RecalculateWaveHeight()
    {
        float protection = 0f;

        // Example weights — tune to taste
        if (sDuneHeight) protection += 0.30f * sDuneHeight.value;
        if (sDuneWidth) protection += 0.15f * sDuneWidth.value;
        if (sVegDensity) protection += 0.30f * sVegDensity.value;
        if (sBarHeight) protection += 0.40f * sBarHeight.value;
        //if (sBarZ) protection += 0.40f * (1f - sBarZ.value);
        // --- Sandbar position contribution (use actual controller state, not raw slider) ---
        if (bar != null)
        {
            // Normalize the current zOffset into [0,1] within [minZOffset, maxZOffset]
            // t=0 at minZOffset, t=1 at maxZOffset
            float t = Mathf.InverseLerp(bar.minZOffset, bar.maxZOffset, bar.zOffset);

            // Interpret which side is "closer to beach"
            // If max means closer, closeness = t; otherwise flip it.
            float closeness = zOffsetMaxIsCloser ? t : (1f - t);

            // More closeness → more protection (waves smaller)
            protection += 0.20f * Mathf.Clamp01(closeness);
        }


        // More protection → smaller waves
        float normalizedWave = Mathf.Clamp01(1f - protection);

        // Update unified wave value (applied in LateUpdate)
        SetWaveNormalized(normalizedWave);
    }

    // ------------------- Utility -------------------

    void ResolveWaveProperty()
    {
        _resolvedProp = waveHeightProperty;
        if (string.IsNullOrEmpty(_resolvedProp))
        {
            Debug.LogWarning("[UIBinder] Wave property name empty; defaulting to _WaveHeight");
            _resolvedProp = "_WaveHeight";
        }
    }
}
