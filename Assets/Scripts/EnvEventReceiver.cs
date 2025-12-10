using UnityEngine;

public class EnvEventReceiver : MonoBehaviour
{
    public UIBinder ui;

    // Vegetation density (already has a nice API)
    public void SetVegetationDensity(float v01)
    {
        if (!ui) return;
        ui.SetVegDensity01(v01, recalcWave: true);
    }

    // Vegetation height
    public void SetVegetationHeight(float v01)
    {
        if (!ui || !ui.veg) return;

        float t = Mathf.Clamp01(v01);

        // apply to controller
        ui.veg.treeHeightScale =
            Mathf.Lerp(ui.veg.minTreeHeightScale, ui.veg.maxTreeHeightScale, t);

        if (ui.sVegHeight)
            ui.sVegHeight.SetValueWithoutNotify(t);

        ui.RecalculateWaveHeight();
    }

    // Dune height
    public void SetDuneHeight(float v01)
    {
        if (!ui || !ui.dune) return;

        float t = Mathf.Clamp01(v01);

        ui.dune.heightScale =
            Mathf.Lerp(ui.dune.minHeightScale, ui.dune.maxHeightScale, t);

        if (ui.sDuneHeight)
            ui.sDuneHeight.SetValueWithoutNotify(t);

        ui.RecalculateWaveHeight();
    }

    // Dune width
    public void SetDuneWidth(float v01)
    {
        if (!ui || !ui.dune) return;

        float t = Mathf.Clamp01(v01);

        ui.dune.widthScale =
            Mathf.Lerp(ui.dune.minWidthScale, ui.dune.maxWidthScale, t);

        if (ui.sDuneWidth)
            ui.sDuneWidth.SetValueWithoutNotify(t);

        ui.RecalculateWaveHeight();
    }

    // Sandbar height
    public void SetSandbarHeight(float v01)
    {
        if (!ui || !ui.bar) return;

        float t = Mathf.Clamp01(v01);

        ui.bar.heightScale =
            Mathf.Lerp(ui.bar.minHeightScale, ui.bar.maxHeightScale, t);

        if (ui.sBarHeight)
            ui.sBarHeight.SetValueWithoutNotify(t);

        ui.RecalculateWaveHeight();
    }

    // Sandbar distance from shore
    public void SetSandbarDistance(float v01)
    {
        if (!ui || !ui.bar) return;

        float t = Mathf.Clamp01(v01);

        ui.bar.zOffset =
            Mathf.Lerp(ui.bar.minZOffset, ui.bar.maxZOffset, t);

        if (ui.sBarZ)
            ui.sBarZ.SetValueWithoutNotify(t);

        ui.RecalculateWaveHeight();
    }

    // Wave intensity
    public void SetWaveIntensity(float v01)
    {
        if (!ui) return;
        ui.OnWaveSliderChanged(v01);
    }
}
