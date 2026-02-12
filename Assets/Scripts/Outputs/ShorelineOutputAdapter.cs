using System.Collections.Generic;
using UnityEngine;
using MappingTool;

public class ShorelineOutputAdapter : MonoBehaviour
{
    [Header("Targets")]
    public VegetationController vegetation;
    public DuneController dune;
    public SandbarController sandbar;

    [Header("Canvas UI")]
    public UIBinder uiBinder;

    [Header("Vegetation height shaping")]
    [Tooltip("Kkeep height in a smaller band before converting to treeHeightScale")]
    [Range(0f, 1f)] public float vegHeightMin01 = 0.3f;
    [Range(0f, 1f)] public float vegHeightMax01 = 1.0f;
    

    public void ApplyVegetationDensity(float v01)
    {
        v01 = Mathf.Clamp01(v01);

        if (vegetation != null)
            vegetation.density = v01;

        // Sync UI (no feedback loop)
        if (uiBinder != null && uiBinder.sVegDensity != null)
            uiBinder.sVegDensity.SetValueWithoutNotify(v01);

        if (uiBinder != null)
            uiBinder.RecalculateWaveHeight();
    }

    public void ApplyVegetationHeight(float v01)
    {
        v01 = Mathf.Clamp01(v01);

        float tScaled01 = Mathf.Lerp(
            Mathf.Min(vegHeightMin01, vegHeightMax01),
            Mathf.Max(vegHeightMin01, vegHeightMax01),
            v01
        );

        if (vegetation != null)
        {
            float minH = vegetation.minTreeHeightScale;
            float maxH = vegetation.maxTreeHeightScale;
            vegetation.treeHeightScale = Mathf.Lerp(minH, maxH, tScaled01);
        }

        if (uiBinder != null && uiBinder.sVegHeight != null)
            uiBinder.sVegHeight.SetValueWithoutNotify(tScaled01);

        if (uiBinder != null)
            uiBinder.RecalculateWaveHeight();
    }

    public void ApplyDuneHeight(float v01)
    {
        v01 = Mathf.Clamp01(v01);

        if (dune != null)
            dune.heightScale = Mathf.Lerp(dune.minHeightScale, dune.maxHeightScale, v01);

        if (uiBinder != null && uiBinder.sDuneHeight != null)
            uiBinder.sDuneHeight.SetValueWithoutNotify(v01);

        if (uiBinder != null)
            uiBinder.RecalculateWaveHeight();
    }

    public void ApplyDuneWidth(float v01)
    {
        v01 = Mathf.Clamp01(v01);

        if (dune != null)
            dune.widthScale = Mathf.Lerp(dune.minWidthScale, dune.maxWidthScale, v01);

        if (uiBinder != null && uiBinder.sDuneWidth != null)
            uiBinder.sDuneWidth.SetValueWithoutNotify(v01);

        if (uiBinder != null)
            uiBinder.RecalculateWaveHeight();
    }

    public void ApplySandbarElevation(float v01)
    {
        v01 = Mathf.Clamp01(v01);
        Debug.Log($"[Adapter] SandbarElevation v01={v01:F2} target={(sandbar? sandbar.name : (uiBinder?.bar? uiBinder.bar.name : "NULL"))}");

        var barTarget = sandbar;
        if (barTarget == null && uiBinder != null) barTarget = uiBinder.bar;

        if (barTarget != null)
            barTarget.heightScale = Mathf.Lerp(barTarget.minHeightScale, barTarget.maxHeightScale, v01);

        if (uiBinder != null && uiBinder.sBarHeight != null)
            uiBinder.sBarHeight.SetValueWithoutNotify(v01);

        if (uiBinder != null)
            uiBinder.RecalculateWaveHeight();
    }

    public void ApplySandbarOffshoreDistance(float v01)
    {
        v01 = Mathf.Clamp01(v01);

        var bar = uiBinder != null ? uiBinder.bar : null;
        if (bar == null) return;

        float offset = Mathf.Lerp(bar.minZOffset, bar.maxZOffset, v01);
        bar.zOffset = offset;

        if (uiBinder.sBarZ)
            uiBinder.sBarZ.SetValueWithoutNotify(v01);

        uiBinder.RecalculateWaveHeight();
    }


    public void Apply(Dictionary<OutputParam, float> outputs01)
    {
        Debug.Log($"[ShorelineOutputAdapter] Apply called. keys={outputs01?.Count ?? 0}");
        if (outputs01 == null) return;

        if (outputs01.TryGetValue(OutputParam.VegetationDensity, out var vegDensity01))
            ApplyVegetationDensity(vegDensity01);

        if (outputs01.TryGetValue(OutputParam.VegetationHeight, out var vegHeight01))
            ApplyVegetationHeight(vegHeight01);

        if (outputs01.TryGetValue(OutputParam.DuneHeight, out var duneHeight01))
            ApplyDuneHeight(duneHeight01);

        if (outputs01.TryGetValue(OutputParam.DuneWidth, out var duneWidth01))
            ApplyDuneWidth(duneWidth01);
        
        if (outputs01.TryGetValue(OutputParam.SandbarElevation, out var sbElev01))
            ApplySandbarElevation(sbElev01);

        if (outputs01.TryGetValue(OutputParam.SandbarOffshoreDistance, out var sbDist01))
            ApplySandbarOffshoreDistance(sbDist01);

    }
}
