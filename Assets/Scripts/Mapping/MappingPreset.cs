using System.Collections.Generic;
using UnityEngine;

namespace MappingTool
{
    [CreateAssetMenu(menuName = "Mapping/Mapping Preset", fileName = "MappingPreset")]
    public class MappingPreset : ScriptableObject
    {
        public MappingCondition condition = MappingCondition.DiminishedSelf;
        public List<MappingBinding> bindings = new List<MappingBinding>();
    }
}
