using System;
using UnityEngine.XR.VisionOS;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

#if UNITY_HAS_POLYSPATIAL_XR
using System.Linq;
using UnityEditor.PolySpatial.Utilities;
using Unity.XR.CoreUtils.Capabilities.Editor;
#endif

#if UNITY_HAS_URP
using UnityEngine.Rendering.Universal;
#endif

namespace UnityEditor.XR.VisionOS
{
    [CustomEditor(typeof(VisionOSSettings))]
    class VisionOSSettingsEditor : Editor
    {
        const string k_WorldSensingUsageWarning = "World sensing usage description is required if world sensing features (images, planes, or meshes) will be " +
            "used. If this field is blank, the app will not be allowed to request world sensing authorization, and will crash when trying to start an AR " +
            "Session using these data providers. If your app does not use world sensing, you can safely leave this field blank.";

        const string k_TargetFrameRateTooltip = "Choose the initial target frame rate. Most apps should start with a value of 90hz and expect to render within " +
            "the 11ms available per frame. If you see dropped frames or profiling data that indicates that you cannot reliably render at 90hz, try dropping " +
            "this down to 45hz and see if your frame timings are more consistent. This value is used to set Application.targetFrameRate and " +
            "VisionOS.SetMinimumFrameRepeatCount during application startup.";

        const string k_TargetFrameRateLabelText = "Initial Target Frame Rate";
        const string k_TargetFrameRateFormat = "{0}hz";

#if INCLUDE_UNITY_XR_HANDS
        const string k_HandTrackingUsageWarning = "Hand tracking usage description is required if hand tracking features will be used. If this field is blank, " +
            "the app will not be allowed to request hand tracking authorization, and will crash when trying to start an AR Session using this data provider. " +
            "If your app does not use hand tracking, you can safely leave this field blank.";

        const string k_HandTrackingUsageError = "Hand tracking usage description is required if hand tracking features will be used. If this field is blank, " +
            "the app will not be allowed to request hand tracking authorization, and will crash when trying to start an AR Session using this data provider. " +
            "Because Initialize Hand Tracking On " +
            "Startup is enabled, the hand subsystem will be started automatically and requires authorization to run. Building without this usage description " +
            "will cause a crash when the AR Session is initialized. Please disable Initialize Hand Tracking On Startup or provide a usage description.";

        const bool k_HasHandsPackage = true;
#else
        const bool k_HasHandsPackage = false;
#endif

        const int k_TargetFrameRateOptionCount = 3;
        static readonly string[] k_TargetFrameRateOptions;

#if !UNITY_HAS_POLYSPATIAL_VISIONOS
        static readonly string[] k_PolySpatialPackages = { "com.unity.polyspatial.visionos", "com.unity.polyspatial.xr" };
#endif

#if UNITY_HAS_POLYSPATIAL_XR
        const string k_CapabilityProfilesMessageFormat = "Selected Validation Profiles: <b>{0}</b>.";
        const string k_NoProfilesSelectedOption = "none";
        const string k_EditButtonLabel = "Edit";
        const string k_EditButtonTooltip = "Open Project Validation";
        const string k_ProjectValidationSettingsPath = "Project/XR Plug-in Management/Project Validation";
        const string k_LinkLabel = "Documentation";
        const string k_ProjectValidationURL = "https://docs.unity3d.com/Packages/com.unity.polyspatial.visionos@latest/index.html?subfolder=/manual/PolySpatialXRProjectValidation.html";
#endif

        [SerializeField]
        AddAndRemoveRequest m_InstallRequest;

        SerializedProperty m_AppModeProperty;
        SerializedProperty m_SetTargetFrameRateOnStartupProperty;
        SerializedProperty m_InitialMinimumFrameRepeatCountProperty;
        SerializedProperty m_HandsTrackingUsageDescriptionProperty;
        SerializedProperty m_WorldSensingUsageDescriptionProperty;
        SerializedProperty m_InitializeHandTrackingOnStartupProperty;
        SerializedProperty m_UpperLimbVisibilityProperty;
        SerializedProperty m_HighQualityRecordingModeProperty;
        SerializedProperty m_FoveatedRenderingProperty;
        SerializedProperty m_MetalImmersionStyleProperty;
        SerializedProperty m_RealityKitImmersionStyleProperty;
        SerializedProperty m_MetalImmersiveOverlaysProperty;
        SerializedProperty m_RealityKitImmersiveOverlaysProperty;
        SerializedProperty m_IL2CPPLargeExeWorkaroundProperty;
        SerializedProperty m_UseACToolProperty;

        Lazy<GUIContent> m_TargetFrameRateLabel = new(() => new GUIContent(k_TargetFrameRateLabelText, k_TargetFrameRateTooltip));

        static VisionOSSettingsEditor()
        {
            k_TargetFrameRateOptions = new string[k_TargetFrameRateOptionCount];
            for (var i = 0; i < k_TargetFrameRateOptionCount; i++)
            {
                k_TargetFrameRateOptions[i] = string.Format(k_TargetFrameRateFormat, VisionOSRuntimeSettings.GetTargetFrameRateForRepeatCount(i));
            }
        }

        void OnEnable()
        {
            m_AppModeProperty = serializedObject.FindProperty("m_AppMode");
            m_HandsTrackingUsageDescriptionProperty = serializedObject.FindProperty("m_HandsTrackingUsageDescription");
            m_WorldSensingUsageDescriptionProperty = serializedObject.FindProperty("m_WorldSensingUsageDescription");
            m_UpperLimbVisibilityProperty = serializedObject.FindProperty("m_UpperLimbVisibility");
            m_HighQualityRecordingModeProperty = serializedObject.FindProperty("m_HighQualityRecordingMode");
            m_FoveatedRenderingProperty = serializedObject.FindProperty("m_FoveatedRendering");
            m_MetalImmersionStyleProperty = serializedObject.FindProperty("m_MetalImmersionStyle");
            m_RealityKitImmersionStyleProperty = serializedObject.FindProperty("m_RealityKitImmersionStyle");
            m_MetalImmersiveOverlaysProperty = serializedObject.FindProperty("m_MetalImmersiveOverlays");
            m_RealityKitImmersiveOverlaysProperty = serializedObject.FindProperty("m_RealityKitImmersiveOverlays");
            m_IL2CPPLargeExeWorkaroundProperty = serializedObject.FindProperty("m_IL2CPPLargeExeWorkaround");
            m_UseACToolProperty = serializedObject.FindProperty("m_UseACTool");

            // Initialize RuntimeSettings on a delay to prevent asset creation errors that can happen on first import
            EditorApplication.delayCall += InitializeRuntimeSettings;

#if UNITY_HAS_POLYSPATIAL_XR
            // If there was a serialized install request we just switched to RealityKit or Hybrid mode
            if (m_InstallRequest != null)
            {
                if (m_InstallRequest.Status == StatusCode.Success)
                    VisionOSEditorUtils.UpdateSelectedCapabilityProfiles((VisionOSSettings.AppMode)m_AppModeProperty.intValue);

                m_InstallRequest = null;
            }
#endif
        }

        void InitializeRuntimeSettings()
        {
            var runtimeSettings = new SerializedObject(VisionOSRuntimeSettings.GetOrCreate());
            m_InitializeHandTrackingOnStartupProperty = runtimeSettings.FindProperty("m_InitializeHandTrackingOnStartup");
            m_SetTargetFrameRateOnStartupProperty = runtimeSettings.FindProperty("m_SetTargetFrameRateOnStartup");
            m_InitialMinimumFrameRepeatCountProperty = runtimeSettings.FindProperty("m_InitialMinimumFrameRepeatCount");
        }

        public override void OnInspectorGUI()
        {
            var isLoaderEnabled = VisionOSEditorUtils.IsLoaderEnabled();

            serializedObject.Update();
            EditorGUIUtility.labelWidth = 200;

#if !UNITY_HAS_POLYSPATIAL_VISIONOS || UNITY_HAS_POLYSPATIAL_XR
            var previousAppMode = (VisionOSSettings.AppMode)m_AppModeProperty.intValue;
            if (m_InstallRequest != null && m_InstallRequest.IsCompleted)
                m_InstallRequest = null;
#endif

            EditorGUILayout.PropertyField(m_AppModeProperty);
            var appMode = (VisionOSSettings.AppMode)m_AppModeProperty.intValue;
            var hasMetalSupport = appMode is VisionOSSettings.AppMode.Metal or VisionOSSettings.AppMode.Hybrid;
            var hasRealityKitSupport = appMode is VisionOSSettings.AppMode.RealityKit or VisionOSSettings.AppMode.Hybrid;

#if !UNITY_HAS_POLYSPATIAL_VISIONOS
            // ChangeCheckScope can fire when first viewing the inspector, so just compare previous to current state
            if (hasRealityKitSupport && previousAppMode != appMode)
            {
                EditorApplication.delayCall += () =>
                {
                    if (EditorUtility.DisplayDialog("Install PolySpatial",
                            "RealityKit apps require PolySpatial packages. Click Yes to install PolySpatial. Clicking No will revert this setting to its previous value.",
                            "Yes", "No"))
                    {
                        m_InstallRequest = Client.AddAndRemove(k_PolySpatialPackages);
                    }
                    else
                    {
                        m_AppModeProperty.intValue = (int)previousAppMode;
                        m_AppModeProperty.serializedObject.ApplyModifiedProperties();
                    }
                };
            }
#endif

#if UNITY_HAS_POLYSPATIAL_XR
            using (new GUILayout.VerticalScope(GUILayout.Height(24)))
            {
                var capabilityProfilesMessage = CapabilityProfileSelection.Selected.Count > 0 ?
                    string.Format(k_CapabilityProfilesMessageFormat, string.Join(", ", CapabilityProfileSelection.Selected.Where(p => p != null).Select(p => p.name))) :
                    string.Format(k_CapabilityProfilesMessageFormat, k_NoProfilesSelectedOption);

                if (PolySpatialEditorGUIUtils.DrawFixMeBox(capabilityProfilesMessage, MessageType.None, k_EditButtonLabel, k_EditButtonTooltip, k_LinkLabel, k_ProjectValidationURL))
                {
                    SettingsService.OpenProjectSettings(k_ProjectValidationSettingsPath);
                    GUIUtility.ExitGUI();
                }
            }

            // ChangeCheckScope can fire when first viewing the inspector, so just compare previous to current state
            if (previousAppMode != appMode)
                VisionOSEditorUtils.UpdateSelectedCapabilityProfiles(appMode);
#endif

#if UNITY_HAS_URP
            var hasUrpAsset = UniversalRenderPipeline.asset != null;
            var foveationSupported = hasMetalSupport && hasUrpAsset;
            if(m_HighQualityRecordingModeProperty.boolValue)
                foveationSupported = false;
#else
            const bool foveationSupported = false;
#endif

            // Usage descriptions are only needed when loader is enabled (it will be disabled under Windowed mode regardless)
            using (new EditorGUI.DisabledScope(!isLoaderEnabled || appMode == VisionOSSettings.AppMode.Windowed))
            {
                var setTargetFrameRateOnStartup = false;
                if (m_SetTargetFrameRateOnStartupProperty == null)
                {
                    // Fall back to a label in case we see the UI between OnEnable and initializing this property
                    GUILayout.Label("Initializing Runtime Settings...");
                }
                else
                {
                    using (var changed = new EditorGUI.ChangeCheckScope())
                    {
                        m_SetTargetFrameRateOnStartupProperty.serializedObject.Update();
                        EditorGUILayout.PropertyField(m_SetTargetFrameRateOnStartupProperty);
                        if (changed.changed)
                        {
                            m_SetTargetFrameRateOnStartupProperty.serializedObject.ApplyModifiedProperties();
                        }

                        setTargetFrameRateOnStartup = m_SetTargetFrameRateOnStartupProperty.boolValue;
                    }
                }

                using (new EditorGUI.DisabledScope(!setTargetFrameRateOnStartup))
                {
                    if (m_InitialMinimumFrameRepeatCountProperty == null)
                    {
                        // Fall back to a label in case we see the UI between OnEnable and initializing this property
                        GUILayout.Label("Initializing Runtime Settings...");
                    }
                    else
                    {
                        using (var changed = new EditorGUI.ChangeCheckScope())
                        {
                            m_InitialMinimumFrameRepeatCountProperty.serializedObject.Update();
                            var repeatCount = m_InitialMinimumFrameRepeatCountProperty.intValue;
                            repeatCount = EditorGUILayout.Popup(m_TargetFrameRateLabel.Value, repeatCount, k_TargetFrameRateOptions);
                            m_InitialMinimumFrameRepeatCountProperty.intValue = repeatCount;
                            if (changed.changed)
                            {
                                m_InitialMinimumFrameRepeatCountProperty.serializedObject.ApplyModifiedProperties();
                            }
                        }
                    }
                }

                using (new EditorGUI.DisabledScope(!k_HasHandsPackage))
                {
                    if (m_InitializeHandTrackingOnStartupProperty == null)
                    {
                        // Fall back to a label in case we see the UI between OnEnable and initializing this property
                        GUILayout.Label("Initializing Runtime Settings...");
                    }
                    else
                    {
                        using (var changed = new EditorGUI.ChangeCheckScope())
                        {
                            m_InitializeHandTrackingOnStartupProperty.serializedObject.Update();
                            EditorGUILayout.PropertyField(m_InitializeHandTrackingOnStartupProperty);
                            if (changed.changed)
                            {
                                m_InitializeHandTrackingOnStartupProperty.serializedObject.ApplyModifiedProperties();
                            }
                        }
                    }

                    EditorGUILayout.PropertyField(m_HandsTrackingUsageDescriptionProperty);

#if INCLUDE_UNITY_XR_HANDS
                    if (isLoaderEnabled && string.IsNullOrEmpty(m_HandsTrackingUsageDescriptionProperty.stringValue)
                                        && m_InitializeHandTrackingOnStartupProperty != null)
                    {
                        if (m_InitializeHandTrackingOnStartupProperty.boolValue)
                            EditorGUILayout.HelpBox(k_HandTrackingUsageError, MessageType.Error);
                        else
                            EditorGUILayout.HelpBox(k_HandTrackingUsageWarning, MessageType.Warning);
                    }
#endif
                }

                EditorGUILayout.PropertyField(m_WorldSensingUsageDescriptionProperty);
                if (isLoaderEnabled && string.IsNullOrEmpty(m_WorldSensingUsageDescriptionProperty.stringValue))
                    EditorGUILayout.HelpBox(k_WorldSensingUsageWarning, MessageType.Warning);

                EditorGUILayout.PropertyField(m_MetalImmersionStyleProperty);
                EditorGUILayout.PropertyField(m_RealityKitImmersionStyleProperty);
                EditorGUILayout.PropertyField(m_MetalImmersiveOverlaysProperty);
                EditorGUILayout.PropertyField(m_RealityKitImmersiveOverlaysProperty);
                EditorGUILayout.PropertyField(m_UpperLimbVisibilityProperty);
                EditorGUILayout.PropertyField(m_HighQualityRecordingModeProperty);
                using (new EditorGUI.DisabledScope(!foveationSupported))
                {
                    EditorGUILayout.PropertyField(m_FoveatedRenderingProperty);
                }
            }

            EditorGUILayout.PropertyField(m_IL2CPPLargeExeWorkaroundProperty);
            EditorGUILayout.PropertyField(m_UseACToolProperty);

            if (hasMetalSupport)
            {
                switch (PlayerSettings.VisionOS.sdkVersion)
                {
                    case VisionOSSdkVersion.Device:
                        EditorGUILayout.HelpBox("When building for visionOS Device SDK, Single-Pass Instanced rendering will be used.", MessageType.Info);
                        break;
                    case VisionOSSdkVersion.Simulator:
                        EditorGUILayout.HelpBox("When building for visionOS Simulator SDK, Multi-Pass rendering will be used.", MessageType.Info);

#if UNITY_HAS_URP
                        if (m_FoveatedRenderingProperty.boolValue && hasUrpAsset)
                            EditorGUILayout.HelpBox("Foveated rendering will be disabled for this build because it is not supported in the visionOS simulator.", MessageType.Info);
#endif

                        break;
                }

                if (!isLoaderEnabled)
                    EditorGUILayout.HelpBox("Metal and Hybrid apps require the Apple visionOS plug-in to be enabled in the XR Plug-in Management.", MessageType.Error);
            }

            if (hasRealityKitSupport)
            {
#if UNITY_HAS_POLYSPATIAL_VISIONOS
                EditorGUILayout.HelpBox("The initial window configuration at app launch is determined by the default volume settings, found in Project " +
                    "Settings > PolySpatial Settings.", MessageType.Info);
#else
                if (m_InstallRequest != null && !m_InstallRequest.IsCompleted)
                {
                    EditorGUILayout.HelpBox("Installing PolySpatial packages...", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("RealityKit on visionOS requires PolySpatial and the PolySpatial visionOS packages.", MessageType.Error);
                    if (GUILayout.Button("Install Packages"))
                        m_InstallRequest = Client.AddAndRemove(k_PolySpatialPackages);
                }
#endif
            }

            if (appMode == VisionOSSettings.AppMode.Windowed)
            {
                if (isLoaderEnabled)
                {
                    EditorGUILayout.HelpBox("The Apple visionOS XR loader is not supported when building a visionOS Windowed application. It will be " +
                        "disabled prior to builds and then re-enabled afterward. You may need to manually re-enable the loader in XR Plugin Management " +
                        "settings if the build fails.", MessageType.Warning);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
