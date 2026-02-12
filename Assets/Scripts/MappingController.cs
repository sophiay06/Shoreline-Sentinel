using System.Collections.Generic;
using UnityEngine;
using Signals;

namespace MappingTool
{
    public class MappingController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Assign a component that implements ISignalProvider (e.g., SignalProvider).")]
        public MonoBehaviour signalProviderComponent;

        [SerializeField] private ShorelineOutputAdapter outputAdapter;

        [Header("Study condition")]
        public MappingCondition activeCondition = MappingCondition.DiminishedSelf;

        [Tooltip("Assign preset assets for DS, NaiveLLM, Expert.")]
        public List<MappingPreset> presets = new List<MappingPreset>();

        [Header("Editable bindings (the designer tool)")]
        public List<MappingBinding> bindings = new List<MappingBinding>();

        [Header("Runtime")]
        public bool runMapping = true;
        public bool autoLoadPreset = true;
        public bool reloadPresetOnConditionChange = true;

        private ISignalProvider _signals;
        private readonly Dictionary<OutputParam, float> _prevOutput = new();
        private readonly Dictionary<OutputParam, float> _outputThisFrame = new();
        private MappingCondition _lastCondition;

        void Awake()
        {
            _signals = signalProviderComponent as ISignalProvider;
            if (_signals == null && signalProviderComponent != null)
                Debug.LogError("[MappingController] signalProviderComponent does not implement ISignalProvider.", this);

            if (!outputAdapter)
                outputAdapter = GetComponent<ShorelineOutputAdapter>();

            if (!outputAdapter)
                Debug.LogError("[MappingController] ShorelineOutputAdapter not found.", this);

            _lastCondition = activeCondition;
        }

        void Start()
        {
            if (autoLoadPreset) LoadActivePreset();
        }

        private void OnValidate()
        {
            if (!outputAdapter) outputAdapter = GetComponent<ShorelineOutputAdapter>();

            if (!Application.isPlaying && autoLoadPreset)
            {
                LoadActivePreset();
                _lastCondition = activeCondition;
            }
        }

        void Update()
        {
            if (reloadPresetOnConditionChange && activeCondition != _lastCondition)
            {
                _lastCondition = activeCondition;
                if (autoLoadPreset) LoadActivePreset();
                _prevOutput.Clear();
                _outputThisFrame.Clear();
                Debug.Log($"[MappingController] Condition changed -> {activeCondition}. Preset reloaded.");
            }

            if (!runMapping || outputAdapter == null || _signals == null) return;

            _outputThisFrame.Clear();

            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (b == null) continue;

                if (!_signals.TryGetSignal(b.inputSignal, out float raw)) continue;

                b.debugRaw = raw;

                // Deadzone on input
                float center = (b.inputMin + b.inputMax) * 0.5f;
                raw = MappingMath.ApplyDeadzone(raw, center, b.deadzone);

                // Remap to 0-1 based on Min/Max
                float t = MappingMath.Remap01(raw, b.inputMin, b.inputMax);
                if (b.invert) t = 1f - t;

                // Curves
                t = MappingMath.ApplyPreset(t, b.curvePreset);

                // Map to Output Range
                float mapped = MappingMath.Lerp(b.outputMin, b.outputMax, t);

                // Smoothing
                float prev = _prevOutput.TryGetValue(b.outputParam, out float p) ? p : mapped;
                float smoothed = MappingMath.LowPass(prev, mapped, b.smoothing);

                // Output Rate Limit
                float maxDelta = 1.0f * Time.deltaTime;
                smoothed = Mathf.Clamp(smoothed, prev - maxDelta, prev + maxDelta);

                b.debugMapped = smoothed;
                _outputThisFrame[b.outputParam] = smoothed;
                _prevOutput[b.outputParam] = smoothed;
            }

            // Apply with Fallbacks (0.5 or current state)
            ApplyOutput(OutputParam.VegetationDensity, 0.5f);
            ApplyOutput(OutputParam.VegetationHeight, 0.5f);
            ApplyOutput(OutputParam.DuneHeight, 0.5f);
            ApplyOutput(OutputParam.DuneWidth, 0.5f);
            ApplyOutput(OutputParam.SandbarElevation, 0.5f);
            ApplyOutput(OutputParam.SandbarOffshoreDistance, 0.5f);
        }

        private void ApplyOutput(OutputParam param, float fallback)
        {
            if (_outputThisFrame.TryGetValue(param, out float val))
            {
                switch (param)
                {
                    case OutputParam.VegetationDensity: outputAdapter.ApplyVegetationDensity(val); break;
                    case OutputParam.VegetationHeight:  outputAdapter.ApplyVegetationHeight(val); break;
                    case OutputParam.DuneHeight:        outputAdapter.ApplyDuneHeight(val); break;
                    case OutputParam.DuneWidth:         outputAdapter.ApplyDuneWidth(val); break;
                    case OutputParam.SandbarElevation: outputAdapter.ApplySandbarElevation(val); break;
                    case OutputParam.SandbarOffshoreDistance: outputAdapter.ApplySandbarOffshoreDistance(val); break;
                }
            }
        }

        public void LoadActivePreset()
        {
            var preset = presets.Find(p => p && p.condition == activeCondition);
            if (!preset) return;

            bindings = new List<MappingBinding>();
            foreach (var b in preset.bindings)
            {
                if (b == null) continue;
                bindings.Add(new MappingBinding
                {
                    inputSignal = b.inputSignal,
                    outputParam = b.outputParam,
                    inputMin = b.inputMin,
                    inputMax = b.inputMax,
                    outputMin = b.outputMin,
                    outputMax = b.outputMax,
                    invert = b.invert,
                    curvePreset = b.curvePreset,
                    smoothing = b.smoothing,
                    deadzone = b.deadzone
                });
            }
        }

        public void SetRecommendedDefaultsAll()
        {

            if (activeCondition == MappingCondition.DiminishedSelf)
            {
                bindings = new List<MappingBinding>
                {
                    new MappingBinding
                    {
                        outputParam = OutputParam.VegetationDensity,
                        inputSignal = InputSignal.LeftHand_FingerDistance01,
                        inputMin = 0f, inputMax = 1f,
                        outputMin = 0f, outputMax = 1f,
                        curvePreset = CurvePreset.EaseOut,
                        smoothing = 0.90f
                    },
                    new MappingBinding
                    {
                        outputParam = OutputParam.VegetationHeight,
                        inputSignal = InputSignal.LeftHand_WristHeight01,
                        inputMin = 0f, inputMax = 1f,
                        outputMin = 0f, outputMax = 1f,
                        curvePreset = CurvePreset.Linear,
                        smoothing = 0.0f,
                        deadzone = 0f 
                    },
                    new MappingBinding
                    {
                        outputParam = OutputParam.DuneHeight,
                        inputSignal = InputSignal.RightHand_WristHeight01,
                        inputMin = 0f, inputMax = 1f,
                        outputMin = 0f, outputMax = 1f,
                        curvePreset = CurvePreset.EaseInOut,
                        smoothing = 0.90f
                    },
                    new MappingBinding
                    {
                        outputParam = OutputParam.DuneWidth,
                        inputSignal = InputSignal.RightHand_FingerDistance01,
                        inputMin = 0f, inputMax = 1f,
                        outputMin = 0f, outputMax = 1f,
                        curvePreset = CurvePreset.EaseOut,
                        smoothing = 0.90f
                    },
                    new MappingBinding
                    {
                        outputParam = OutputParam.VegetationDensity,
                        inputSignal = InputSignal.Torso_Yaw01,
                        inputMin = 0f, inputMax = 1f,
                        outputMin = 0f, outputMax = 1f,
                        curvePreset = CurvePreset.Linear,
                        smoothing = 0.0f,
                        deadzone = 0f
                    },
                    new MappingBinding
                    {
                        outputParam = OutputParam.VegetationHeight,
                        inputSignal = InputSignal.Head_Pitch01,
                        inputMin = 0f, inputMax = 1f,
                        outputMin = 0f, outputMax = 1f,
                        curvePreset = CurvePreset.Linear,
                        smoothing = 0.0f,
                        deadzone = 0f
                    },
                    new MappingBinding
                    {
                        outputParam = OutputParam.DuneHeight,
                        inputSignal = InputSignal.LeftArm_ShoulderFlex01,
                        inputMin = 0f, inputMax = 1f,
                        outputMin = 0f, outputMax = 1f,
                        curvePreset = CurvePreset.Linear,
                        smoothing = 0.0f,
                        deadzone = 0f
                    },
                    new MappingBinding
                    {
                        outputParam = OutputParam.DuneWidth,
                        inputSignal = InputSignal.RightArm_ShoulderAbd01,
                        inputMin = 0f, inputMax = 1f,
                        outputMin = 0f, outputMax = 1f,
                        curvePreset = CurvePreset.Linear,
                        smoothing = 0.0f,
                        deadzone = 0f
                    },
                    new MappingBinding
                    {
                        outputParam = OutputParam.VegetationDensity,
                        inputSignal = InputSignal.LeftHand_FingerSpread01,
                        inputMin = 0f, inputMax = 1f,
                        outputMin = 0f, outputMax = 1f,
                        curvePreset = CurvePreset.EaseOut,
                        smoothing = 0.0f,
                        deadzone = 0f
                    },
                    new MappingBinding
                    {
                        outputParam = OutputParam.DuneHeight,
                        inputSignal = InputSignal.Forearms_VerticalDistance01,
                        inputMin = 0f, inputMax = 1f,
                        outputMin = 0f, outputMax = 1f,
                        curvePreset = CurvePreset.EaseInOut,
                        smoothing = 0.90f
                    },
                    new MappingBinding
                    {
                        outputParam = OutputParam.DuneWidth,
                        inputSignal = InputSignal.Forearms_HorizontalDistance01,
                        inputMin = 0f, inputMax = 1f,
                        outputMin = 0f, outputMax = 1f,
                        curvePreset = CurvePreset.EaseOut,
                        smoothing = 0.0f,
                        deadzone = 0f
                    },
                    new MappingBinding
                    {
                        outputParam = OutputParam.SandbarElevation,
                        inputSignal = InputSignal.RightHand_HandHeight01,
                        inputMin = 0f, inputMax = 1f,
                        outputMin = 0f, outputMax = 1f,
                        curvePreset = CurvePreset.EaseOut,
                        smoothing = 0.0f,
                        deadzone = 0f
                    },
                    new MappingBinding
                    {
                        outputParam = OutputParam.SandbarOffshoreDistance,
                        inputSignal = InputSignal.Locomotion_DistanceFromStart01,
                        inputMin = 0f, inputMax = 1f,
                        outputMin = 0f, outputMax = 1f,
                        curvePreset = CurvePreset.EaseOut,
                        smoothing = 0.0f,
                        deadzone = 0f
                    },
                };
            }
            Debug.Log($"[MappingController] Defaults set for {activeCondition}");
        }
    }
}