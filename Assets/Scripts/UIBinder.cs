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

    [Header("Auto discover")]
    public bool autoDiscoverWater = true;
    public string waterRendererNameHint = "Water";

    [Header("Initial values")]
    [Range(0f,1f)] public float initialVegDensity = 0.50f;

    [Header("Tree Type UI")]
    public Dropdown ddTreeType;
    public Toggle tMixedTypes; 
    
    [Header("Wave property")]
    [Tooltip("Shader property name for wave height (_WaveHeight for StylizedWater2)")]
    public string waveHeightProperty = "_WaveHeight";
    public float waveMin = 0f;
    public float waveMax = 2f;

    [Header("Sandbar position → protection mapping")]
    [Tooltip("If true: bar.maxZOffset means 'closest to beach'. If false: bar.maxZOffset means 'farthest'.")]
    public bool zOffsetMaxIsCloser = true;

    [Header("UI Sliders")]
    public Slider sWaveHeight;
    public Slider sDuneHeight;
    public Slider sDuneWidth;
    public Slider sVegDensity;
    public Slider sVegHeight;
    public Slider sBarHeight;
    public Slider sBarZ;

    [Header("Multi-water")]
    public MultiWaterWaveBinder multiBinder;

    //internals
    MaterialPropertyBlock _mpb;
    int _waveID = -1;
    string _resolvedProp = null;
    float _normalizedWave = 0.5f;
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

        // initialize controller states
        ApplyDuneFromUI();
        ApplyVegetationFromUI();
        SetupTreeTypeUI();
        ApplySandbarFromUI();

        SetVegDensity01(initialVegDensity, recalcWave: true);

        // initialize wave from UI
        if (sWaveHeight) _normalizedWave = Mathf.Clamp01(sWaveHeight.value);
        _waveDirty = true;
        ApplyWaveNow(); 
    }

    void OnDestroy()
    {
        UnhookUI();
    }

    void HookUI()
    {
        if (sWaveHeight) sWaveHeight.onValueChanged.AddListener(OnWaveSliderChanged);
        if (sDuneHeight) sDuneHeight.onValueChanged.AddListener(_ => { ApplyDuneFromUI(); RecalculateWaveHeight(); });
        if (sDuneWidth) sDuneWidth.onValueChanged.AddListener(_ => { ApplyDuneFromUI(); RecalculateWaveHeight(); });
        if (sVegDensity) sVegDensity.onValueChanged.AddListener(v => SetVegDensity01(v, recalcWave: true));
        if (sVegHeight) sVegHeight.onValueChanged.AddListener(_ => { ApplyVegetationFromUI(); RecalculateWaveHeight(); });
        if (sBarHeight) sBarHeight.onValueChanged.AddListener(_ => { ApplySandbarFromUI(); RecalculateWaveHeight(); });
        if (sBarZ) sBarZ.onValueChanged.AddListener(_ => { ApplySandbarFromUI(); RecalculateWaveHeight(); });
    }

    void UnhookUI()
    {
        if (sWaveHeight) sWaveHeight.onValueChanged.RemoveListener(OnWaveSliderChanged);
        if (sDuneHeight) sDuneHeight.onValueChanged.RemoveAllListeners();
        if (sDuneWidth) sDuneWidth.onValueChanged.RemoveAllListeners();
        if (sVegDensity) sVegDensity.onValueChanged.RemoveAllListeners();
        if (sVegHeight) sVegHeight.onValueChanged.RemoveAllListeners();
        if (sBarHeight) sBarHeight.onValueChanged.RemoveAllListeners();
        if (sBarZ) sBarZ.onValueChanged.RemoveAllListeners();
        if (ddTreeType) ddTreeType.onValueChanged.RemoveAllListeners();
        if (tMixedTypes) tMixedTypes.onValueChanged.RemoveAllListeners();
    }

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
        if (sVegHeight)
            veg.treeHeightScale = Mathf.Lerp(veg.minTreeHeightScale, veg.maxTreeHeightScale, Mathf.Clamp01(sVegHeight.value));
    }

    void ApplySandbarFromUI()
    {
        if (!bar) return;
        if (sBarHeight)
            bar.heightScale = Mathf.Lerp(bar.minHeightScale, bar.maxHeightScale, Mathf.Clamp01(sBarHeight.value));
        if (sBarZ)
            bar.zOffset = Mathf.Lerp(bar.minZOffset, bar.maxZOffset, Mathf.Clamp01(sBarZ.value));
    }

    void SetupTreeTypeUI() {
        if (!veg) return;

        if (ddTreeType)  {
            ddTreeType.ClearOptions();
            var options = new System.Collections.Generic.List<Dropdown.OptionData>();

            bool usedVariants = veg.variants != null && veg.variants.Count > 0;
            if (usedVariants)
            {
                foreach (var v in veg.variants)
                    options.Add(new Dropdown.OptionData(v ? v.displayName : "Tree"));
                ddTreeType.AddOptions(options);
                ddTreeType.SetValueWithoutNotify(Mathf.Clamp(veg.selectedVariantIndex, 0, veg.variants.Count - 1));
            }
            else if (veg.prefabs != null && veg.prefabs.Length > 0)
            {
                foreach (var p in veg.prefabs)
                    options.Add(new Dropdown.OptionData(p ? p.name : "Tree"));
                ddTreeType.AddOptions(options);
                ddTreeType.SetValueWithoutNotify(0);
            }

            ddTreeType.onValueChanged.AddListener(i =>
            {
                veg.SetTreeTypeIndex(i);
                RecalculateWaveHeight();
            });
        }

        if (tMixedTypes) {
            tMixedTypes.isOn = veg.mixedTypes;
            tMixedTypes.onValueChanged.AddListener(on =>
            {
                veg.SetMixedTypes(on);
                // Keep dropdown enabled only when not mixed
                if (ddTreeType) ddTreeType.interactable = !on;
                RecalculateWaveHeight();
            });

            if (ddTreeType) ddTreeType.interactable = !tMixedTypes.isOn;
        }
}

    //Wave
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

        if (multiBinder)
        {
            multiBinder.Apply(height);
            return;
        }

        if (waterRenderer && !string.IsNullOrEmpty(_resolvedProp))
        {
            waterRenderer.GetPropertyBlock(_mpb);
            _mpb.Clear();
            _mpb.SetFloat(_resolvedProp, height);
            waterRenderer.SetPropertyBlock(_mpb);
            return;
        }

        if (waterMaterialAsset && !string.IsNullOrEmpty(_resolvedProp))
        {
            waterMaterialAsset.SetFloat(_resolvedProp, height);
        }
    }

    public void RecalculateWaveHeight()
    {
        float protection = 0f;

        if (sDuneHeight) protection += 0.30f * sDuneHeight.value;
        if (sDuneWidth) protection += 0.30f * sDuneWidth.value;
        if (sVegDensity) protection += 0.30f * sVegDensity.value;
        if (sVegHeight) protection += 0.30f * sVegHeight.value;
        if (sBarHeight) protection += 0.40f * sBarHeight.value;
        if (bar != null)
        {
            float t = Mathf.InverseLerp(bar.minZOffset, bar.maxZOffset, bar.zOffset);
            float closeness = zOffsetMaxIsCloser ? t : (1f - t);
            protection += 0.20f * Mathf.Clamp01(closeness);
        }

        float normalizedWave = Mathf.Clamp01(1f - protection);

        SetWaveNormalized(normalizedWave);
    }


    void ResolveWaveProperty()
    {
        _resolvedProp = waveHeightProperty;
        if (string.IsNullOrEmpty(_resolvedProp))
        {
            Debug.LogWarning("[UIBinder] Wave property name empty; defaulting to _WaveHeight");
            _resolvedProp = "_WaveHeight";
        }
    }

    public void SetVegDensity01(float v01, bool recalcWave = true)
    {
        float t = Mathf.Clamp01(v01);
        if (veg) veg.density = t; 

        if (sVegDensity)     
            sVegDensity.SetValueWithoutNotify(t);

        if (recalcWave) RecalculateWaveHeight();

        Debug.Log($"[Veg] density set -> {t:F2}");

    }

}
