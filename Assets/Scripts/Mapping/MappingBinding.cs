// Assets/Scripts/Mapping/MappingBinding.cs
using System;
using UnityEngine;

namespace MappingTool
{
    [Serializable]
    public class MappingBinding
    {
        [Header("What controls what")]
        public InputSignal inputSignal;
        public OutputParam outputParam;

        [Header("Input range (units depend on signal)")]
        public float inputMin = 0f;
        public float inputMax = 1f;

        [Header("Output range (usually 0..1)")]
        public float outputMin = 0f;
        public float outputMax = 1f;

        [Header("Shaping")]
        public bool invert = false;
        public CurvePreset curvePreset = CurvePreset.Linear;

        [Tooltip("0..1 (higher = smoother/slower). Suggested: 0.75..0.98")]
        [Range(0f, 0.999f)]
        public float smoothing = 0.85f;

        [Tooltip("Deadzone around neutral input. Units match input signal (cm/meters/degrees etc.).")]
        public float deadzone = 0f;

        [Header("Debug (runtime)")]
        [NonSerialized] public float debugRaw;
        [NonSerialized] public float debugMapped;
    }
}
