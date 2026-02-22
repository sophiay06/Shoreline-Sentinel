namespace MappingTool
{
    public enum MappingCondition
    {
        DiminishedSelf,
        NaiveLLM,
        Expert,
        Custom
    }

    public enum OutputParam
    {
        VegetationDensity,
        VegetationHeight,
        DuneHeight,
        DuneWidth,
        SandbarElevation,
        SandbarOffshoreDistance,
    }

    public enum InputSignal
    {
        LeftHand_FingerDistance01,
        LeftHand_WristHeight01,
        RightHand_WristHeight01,
        RightHand_FingerDistance01,
        Torso_ShoulderHeight01,
        Torso_Lean01,

        [DebugOnly] LeftHand_WristAboveWaistCm,
        [DebugOnly] RightHand_WristAboveHipCm,
        [DebugOnly] Torso_ShoulderYcm,
        [DebugOnly] Torso_LeanDeg,

        Torso_Yaw01,
        Head_Pitch01,
        LeftArm_ShoulderFlex01,
        RightArm_ShoulderAbd01,

        [DebugOnly] Torso_YawDeg,
        [DebugOnly] Head_PitchDeg,
        [DebugOnly] LeftArm_ShoulderFlexDeg,
        [DebugOnly] RightArm_ShoulderAbdDeg,

        LeftHand_FingerSpread01,
        Forearms_VerticalDistance01,
        Forearms_HorizontalDistance01,
        RightHand_HandHeight01,
        Locomotion_DistanceFromStart01,

        [DebugOnly] LeftHand_FingerSpreadCm,
        [DebugOnly] Forearms_VerticalDistanceCm,
        [DebugOnly] Forearms_HorizontalDistanceCm,
        [DebugOnly] RightHand_HandHeightYcm,
        [DebugOnly] Locomotion_DistanceFromStartCm,

        Slime_RightKneeFlexion01,
        Slime_FeetDistance01,

        [DebugOnly] Slime_RightKneeFlexionDeg,
        [DebugOnly] Slime_FeetDistanceM
    }


}
