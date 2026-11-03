#if UNITY_EDITOR
using UnityEditor;
using UnityObject = UnityEngine.Object;
#endif // UNITY_EDITOR

namespace UnityEngine.XR.VisionOS
{
#if UNITY_EDITOR
    public interface IPackageSettings
    {
        VisionOSRuntimeSettings GetOrCreateRuntimeSettings();
    }
#endif // UNITY_EDITOR

    /// <summary>
    /// Build time settings for visionOS. These are serialized and available at runtime.
    /// </summary>
    public class VisionOSRuntimeSettings : ScriptableObject
    {
        // TODO: LXR-3993 Find a way to query the hardware frame rate, instead of assuming 90hz
        const int k_MaximumFrameRate = 90;

        const string k_InitializeHandTrackingOnStartupTooltip = "Controls if hand tracking should be initialized when the application begins. An XR Hands " +
            "provider or package (ex: com.unity.xr.hands) is required for hand tracking.";

        const string k_SetTargetFrameRateAtStartTooltip = "Set the target frame rate and frame repeat count when the application begins.";

        const string k_MinimumFrameRepeatCountTooltip = "The minimum number of frames to repeat a rendered frame. Increasing this value from its default of 0" +
            "will increase the amount of time you have to render each frame. For example, a value of 1 will repeat each frame one time, resulting in double " +
            "the amount of time per frame. Assuming a maximum frame rate of 90hz, repeating each frame once will reduce Unity's target frame rate to 45hz. A " +
            "value of 2 will give you three times the normal amount of time per frame, with a target of 33 hz, and so on. This setting will be used at " +
            "start-up to decide the target frame rate and initial value provided to the system. You can change this at runtime by setting " +
            "Application.targetFrameRate and calling VisionOS.SetMinimumFrameRepeatCount. This will be presented as Target Frame Rate in the settings UI.";

        const string k_MinimumFrameRepeatCountValidationError = "Minimum frame repeat count must be greater than or equal to 0.";

        [SerializeField, Tooltip(k_InitializeHandTrackingOnStartupTooltip)]
        bool m_InitializeHandTrackingOnStartup = true;

        [SerializeField, Tooltip(k_SetTargetFrameRateAtStartTooltip)]
        bool m_SetTargetFrameRateOnStartup = true;

        [SerializeField, Tooltip(k_MinimumFrameRepeatCountTooltip)]
        int m_InitialMinimumFrameRepeatCount;

        /// <summary>
        /// Controls if hand tracking should be initialized when the application begins. An XR Hands provider or package (ex: com.unity.xr.hands)
        /// is required for hand tracking.
        /// </summary>
        public bool initializeHandTrackingOnStartup
        {
            get => m_InitializeHandTrackingOnStartup;
            set
            {
                m_InitializeHandTrackingOnStartup = value;
#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
#endif
            }
        }

        /// <summary>
        /// Controls if the target frame rate and frame repeat count should be set when the application begins.
        /// </summary>
        public bool setTargetFrameRateOnStartup
        {
            get => m_SetTargetFrameRateOnStartup;
            set
            {
                m_SetTargetFrameRateOnStartup = value;
#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
#endif
            }
        }

        /// <summary>
        /// The minimum number of frames to repeat a rendered frame. Increasing this value from its default of 0 will increase the amount of time you have to
        /// render each frame. For example, a value of 1 will repeat each frame one time, resulting in double the amount of time per frame. For a maximum frame
        /// rate of 90hz, repeating each frame once will reduce Unity's target frame rate to 45hz. A value of 2 will give you three times the normal amount of
        /// time per frame, with a target of 33 hz, and so on. This setting will be used at start-up to decide the target frame rate and initial value provided
        /// to the system. You can change this at runtime by setting Application.targetFrameRate and calling VisionOS.SetMinimumFrameRepeatCount. This will be
        /// presented as Target Frame Rate in the settings UI.
        /// </summary>
        public int initialMinimumFrameRepeatCount
        {
            get
            {
                // It is possible to load an invalid value from serialization, so always check it before returning
                if (m_InitialMinimumFrameRepeatCount < 0)
                {
                    Debug.LogWarning(k_MinimumFrameRepeatCountValidationError);
                    return 0;
                }

                return m_InitialMinimumFrameRepeatCount;
            }
            set
            {
                if (m_InitialMinimumFrameRepeatCount < 0)
                {
                    Debug.LogError(k_MinimumFrameRepeatCountValidationError);
                    return;
                }

                m_InitialMinimumFrameRepeatCount = value;

#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
#endif
            }
        }

        /// <summary>
        /// For a given value of repeat count, return the value that should be set on <see cref="Application.targetFrameRate"/>. This assumes a maximum frame
        /// rate of 90hz.
        /// </summary>
        /// <param name="repeatCount">The repeat count which will be given to <see cref="VisionOS.SetMinimumFrameRepeatCount"/></param>
        /// <returns>The target frame rate that should be set on <see cref="Application.targetFrameRate"/></returns>
        public static int GetTargetFrameRateForRepeatCount(int repeatCount)
        {
            if (repeatCount < 0)
            {
                Debug.LogWarning(k_MinimumFrameRepeatCountValidationError);
                return k_MaximumFrameRate;
            }

            return k_MaximumFrameRate / (repeatCount + 1);
        }

#if !UNITY_EDITOR
        static VisionOSRuntimeSettings s_Settings;
        void Awake() => s_Settings = this;
#endif // !UNITY_EDITOR

        internal static VisionOSRuntimeSettings GetOrCreate()
        {
            // When running in the Unity Editor, we have to load user's customization of configuration data directly from
            // EditorBuildSettings. At runtime, we need to grab it from the static instance field instead.
#if UNITY_EDITOR
            return GetPackageSettings().GetOrCreateRuntimeSettings();
#else // !UNITY_EDITOR
            if (s_Settings == null)
                s_Settings = CreateInstance<VisionOSRuntimeSettings>();

            return s_Settings;
#endif // !UNITY_EDITOR
        }

#if UNITY_EDITOR
        static IPackageSettings GetPackageSettings()
        {
            if (EditorBuildSettings.TryGetConfigObject<UnityObject>(Constants.k_SettingsKey, out var settings) && settings is IPackageSettings packageSettings)
                return packageSettings;

            return null;
        }
#endif // UNITY_EDITOR
    }
}
