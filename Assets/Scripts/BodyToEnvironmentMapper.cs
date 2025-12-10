using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public enum EnvDimension {
    VegetationDensity,
    VegetationHeight,
    DuneHeight,
    DuneWidth,
    SandbarHeight,
    SandbarDistance,
    WaveIntensity
}

public enum BodySource {
    LeftHand,
    RightHand,
    LeftFoot,
    RightFoot,
    Head
}

public enum SourceFeature {
    FingerDistance,
    WristHeight,
    FootDorsiflexion,
    FootForward,
    HeadNodAmplitude
}

[Serializable]
public class FloatEvent : UnityEvent<float> { }

[Serializable]
public class MappingEntry
{
    public EnvDimension envDimension;
    public BodySource bodySource;
    public SourceFeature sourceFeature;

    [Header("Events")]
    public FloatEvent onValueChanged;
}

public class BodyToEnvironmentMapper : MonoBehaviour
{
    public List<MappingEntry> mappings = new List<MappingEntry>();

    private void Reset()
    {
        mappings = new List<MappingEntry>
        {
            new MappingEntry {
                envDimension   = EnvDimension.VegetationDensity,
                bodySource     = BodySource.LeftHand,
                sourceFeature  = SourceFeature.FingerDistance
            },
            new MappingEntry {
                envDimension   = EnvDimension.VegetationHeight,
                bodySource     = BodySource.LeftHand,
                sourceFeature  = SourceFeature.WristHeight
            },
            new MappingEntry {
                envDimension   = EnvDimension.DuneHeight,
                bodySource     = BodySource.RightHand,
                sourceFeature  = SourceFeature.WristHeight
            },
            new MappingEntry {
                envDimension   = EnvDimension.DuneWidth,
                bodySource     = BodySource.RightHand,
                sourceFeature  = SourceFeature.FingerDistance
            },
            new MappingEntry {
                envDimension   = EnvDimension.SandbarHeight,
                bodySource     = BodySource.LeftFoot,
                sourceFeature  = SourceFeature.FootDorsiflexion
            },
            new MappingEntry {
                envDimension   = EnvDimension.SandbarDistance,
                bodySource     = BodySource.LeftFoot,
                sourceFeature  = SourceFeature.FootForward
            },
            new MappingEntry {
                envDimension   = EnvDimension.WaveIntensity,
                bodySource     = BodySource.Head,
                sourceFeature  = SourceFeature.HeadNodAmplitude
            },
        };
    }

    public void PushInput(SourceFeature feature, float value)
    {
        foreach (var entry in mappings)
        {
            if (entry.sourceFeature == feature)
            {
                entry.onValueChanged?.Invoke(value);
            }
        }
    }
}
