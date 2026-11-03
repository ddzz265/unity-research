using System;
using System.IO;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.VisionOS;

#if UNITY_VISIONOS && UNITY_HAS_POLYSPATIAL
using Unity.PolySpatial;
#endif

namespace UnityEditor.XR.VisionOS
{
    /// <summary>
    /// Holds settings that are used to configure the Apple visionOS XR Plug-in.
    /// </summary>
    [Serializable]
    [XRConfigurationData("Apple visionOS", Constants.k_SettingsKey)]
    public class VisionOSSettings : ScriptableObject, IPackageSettings
    {
        const string k_HandTrackingUsageTooltip = "Provide a brief description of what hand tracking will be used for." +
            "This will be shown to users in a dialog asking them to allow authorization when hand tracking is requested." +
            "This description will be added to the Info.plist file of the generated visionOS Player Xcode project. If your " +
            "application does not use hand tracking, you can safely leave this field blank.";

        const string k_WorldSensingUsageTooltip = "Provide a brief description of what world sensing (planes, meshes, images)" +
            "will be used for. This will be shown to users in a dialog asking them to allow authorization when world sensing" +
            " is requested. This description will be added to the Info.plist file of the generated visionOS Player Xcode project. If your " +
            "application does not use world sensing, you can safely leave this field blank.";

        // TODO: Update this tooltip when we support volumes and immersive spaces simultaneously. In this case, tis setting will affect volume content
        // as long as the immersive space is open. In other words, when limb visibility is disabled, and the immersive space is open, the user's hands
        // will be occluded by content in a volume even though they can see pass-through.
        // TODO: Expose UpperLimbVisibility for Metal and RealityKit separately (should probably be in volume camera config, along with immersion style)
        const string k_UpperLimbVisibilityTooltip = "Controls how your app displays passthrough video of the user's hands and forearms. In the " +
            "Virtual Reality App Mode, hands are only shown when you enable this setting and always render on top of virtual content. In the " +
            "Mixed Reality App Mode, this setting controls how the user's hands and arms are blended with virtual objects. When enabled, hands " +
            "are blended with virtual objects based on depth. When disabled, hands are always displayed behind virtual objects. This setting is " +
            "ignored when the Volume Camera is in Bounded mode, or when the App Mode is set to Windowed.";

        const string k_HighQualityRecordingModeTooltip = "This mode will disable the right eye view and also disable foveated rendering. This is intended to be used when recording applications in 4k (like you can with Realiy Composer Pro)";

        const string k_FoveatedRenderingTooltip = "Controls if foveated rendering is enabled or disabled. This setting only applies to Virtual Reality apps. " +
            "Foveated rendering requires the Universal Render Pipeline.";

        const string k_MetalImmersionStyleTooltip = "The ImmersionStyle to be used for the Metal ImmersiveSpace.";
        const string k_RealityKitImmersionStyleTooltip = "The ImmersionStyle to be used for the RealityKit ImmersiveSpace.";

        const string k_MetalImmersiveOverlaysTooltip = "The visibility of persistent system overlays (such as the visionOS hand gesture menus) " +
            "in Metal immersive spaces.";
        const string k_RealityKitImmersiveOverlaysTooltip = "The visibility of persistent system overlays (such as the visionOS hand gesture menus) " +
            "in RealityKit immersive spaces.";

        const string k_IL2CPPLargeExeWorkaroundTooltip = "Patches the `Unity-VisionOS` project to work around linker errors that can occur in some " +
            "large projects. Check this box if you encounter the \"ARM64 branch out of range\" error when building the project in Xcode.";

        const string k_UseACToolTooltip = "Use the actool utility to compile reference image libraries during the Unity build process. This can save some time " +
                "in the Xcode build, but this actool can fail sometimes, interrupting Build and Run. This setting does not affect the behavior of " +
                "ARBuildProcessor.PreprocessBuild, which will always invoke actool.";

        const string k_RuntimeSettingsFileName = "VisionOS Runtime Settings.asset";

        /// <summary>
        /// Which mode the app will use.
        /// </summary>
        public enum AppMode
        {
            /// <summary>
            /// Metal Rendering with Compositor Services
            /// </summary>
            [InspectorName("Metal Rendering with Compositor Services")]
            Metal,

            /// <summary>
            /// RealityKit with PolySpatial
            /// </summary>
            [InspectorName("RealityKit with PolySpatial")]
            RealityKit,

            /// <summary>
            /// Windowed - 2D Window
            /// </summary>
            [InspectorName("Windowed - 2D Window")]
            Windowed,

            /// <summary>
            /// Hybrid - Switch between Metal and RealityKit
            /// </summary>
            [InspectorName("Hybrid - Switch between Metal and RealityKit")]
            Hybrid,
        }

        /// <summary>
        /// The ImmersionStyle for a given ImmersiveSpace. These enums correspond to their equivalently named Apple APIs.
        /// Refer to Apple Developer documentation for more information.
        /// </summary>
        public enum ImmersionStyle
        {
            /// <summary>
            /// The default immersion style. It currently defaults to Mixed.
            /// </summary>
            Automatic,

            /// <summary>
            /// Displays unbounded content that completely replaces passthrough.
            /// </summary>
            Full,

            /// <summary>
            /// Displays unbounded content mixed with passthrough.
            /// </summary>
            Mixed,

            /// <summary>
            /// Displays unbounded content that partially replaces passthrough.
            /// </summary>
            Progressive
        }

        /// <summary>
        /// Visibility values for elements such as upper limbs and persistent system overlays.  Corresponds to the equivalent enum
        /// in <a href="https://developer.apple.com/documentation/swiftui/visibility">SwiftUI</a>.
        /// </summary>
        public enum Visibility
        {
            /// <summary>
            /// The element may be visible or hidden depending on the policies of the component accepting the visibility configuration.
            /// </summary>
            Automatic,

            /// <summary>
            /// The element may be visible.
            /// </summary>
            Visible,

            /// <summary>
            /// The element may be hidden.
            /// </summary>
            Hidden
        }

        [SerializeField, Tooltip("Initial mode of the app.")]
        AppMode m_AppMode = AppMode.Metal;

        [SerializeField, Tooltip(k_HandTrackingUsageTooltip)]
        string m_HandsTrackingUsageDescription;

        [SerializeField, Tooltip(k_WorldSensingUsageTooltip)]
        string m_WorldSensingUsageDescription;

        [SerializeField, HideInInspector]
        VisionOSRuntimeSettings m_RuntimeSettings;

        /// <summary>
        /// Retrieves an existing VisionOSRuntimeSettings asset or creates a new one.
        /// </summary>
        /// <returns>A VisionOSRuntimeSettings.</returns>
        public VisionOSRuntimeSettings GetOrCreateRuntimeSettings()
        {
            if (m_RuntimeSettings != null)
                return m_RuntimeSettings;

            var assetGuids = AssetDatabase.FindAssets($"t:{nameof(VisionOSRuntimeSettings)}");
            foreach (var assetGuid in assetGuids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                var asset = AssetDatabase.LoadAssetAtPath<VisionOSRuntimeSettings>(assetPath);
                if (asset != null)
                {
                    m_RuntimeSettings = asset;
                    break;
                }
            }

            if (m_RuntimeSettings != null)
                return m_RuntimeSettings;

            var path = GetAssetPathForSubFolders(new[] { "XR", "Settings" });
            if (string.IsNullOrEmpty(path))
                return null;

            m_RuntimeSettings = CreateInstance<VisionOSRuntimeSettings>();
            var targetPath = Path.Combine(path, k_RuntimeSettingsFileName);
            AssetDatabase.CreateAsset(m_RuntimeSettings, targetPath);
            AssetDatabase.SaveAssets();
            return m_RuntimeSettings;
        }

        [SerializeField, Tooltip(k_UpperLimbVisibilityTooltip)]
        Visibility m_UpperLimbVisibility;

        [SerializeField, Tooltip(k_HighQualityRecordingModeTooltip)]
        bool m_HighQualityRecordingMode;

        [SerializeField, Tooltip(k_FoveatedRenderingTooltip)]
        bool m_FoveatedRendering = true;

        [SerializeField, Tooltip(k_MetalImmersionStyleTooltip)]
        ImmersionStyle m_MetalImmersionStyle;

        [SerializeField, Tooltip(k_RealityKitImmersionStyleTooltip)]
        ImmersionStyle m_RealityKitImmersionStyle;

        [SerializeField, Tooltip(k_MetalImmersiveOverlaysTooltip)]
        Visibility m_MetalImmersiveOverlays;

        [SerializeField, Tooltip(k_RealityKitImmersiveOverlaysTooltip)]
        Visibility m_RealityKitImmersiveOverlays;

        [SerializeField, Tooltip(k_IL2CPPLargeExeWorkaroundTooltip)]
        bool m_IL2CPPLargeExeWorkaround;

        [SerializeField, Tooltip(k_UseACToolTooltip)]
        bool m_UseACTool;

        /// <summary>
        /// App mode.
        /// </summary>
        public AppMode appMode
        {
            get => m_AppMode;
            set => m_AppMode = value;
        }

        /// <summary>
        /// Hands tracking usage description (added to Info.plist).
        /// </summary>
        public string handsTrackingUsageDescription
        {
            get => m_HandsTrackingUsageDescription;
            set => m_HandsTrackingUsageDescription = value;
        }

        /// <summary>
        /// World sensing usage description (added to Info.plist).
        /// </summary>
        public string worldSensingUsageDescription
        {
            get => m_WorldSensingUsageDescription;
            set => m_WorldSensingUsageDescription = value;
        }

        /// <summary>
        /// Upper limb visibility setting (currently only set at the beginning of an app)
        /// </summary>
        public Visibility upperLimbVisibility
        {
            get => m_UpperLimbVisibility;
            set => m_UpperLimbVisibility = value;
        }

        /// <summary>
        /// This setting is for when you want to create a high quality recording of your app.
        /// Enabling this setting will disable rendering in the right eye and disable foveated rendering.
        /// This is expected to be used when creating a high quality recording with Reality Composer Pro.
        /// </summary>
        public bool highQualityRecordingMode
        {
            get => m_HighQualityRecordingMode;
            set => m_HighQualityRecordingMode = value;
        }

        /// <summary>
        /// Controls if foveated rendering is enabled or disabled. This setting only applies to Virtual Reality apps. Foveated rendering requires
        /// the Universal Render Pipeline.
        /// </summary>
        public bool foveatedRendering
        {
            get => m_FoveatedRendering;
            set => m_FoveatedRendering = value;
        }

        /// <summary>
        /// The ImmersionStyle to be used for the Metal ImmersiveSpace.
        /// </summary>
        public ImmersionStyle metalImmersionStyle
        {
            get => m_MetalImmersionStyle;
            set => m_MetalImmersionStyle = value;
        }

        /// <summary>
        /// The ImmersionStyle to be used for the RealityKit ImmersiveSpace.
        /// </summary>
        public ImmersionStyle realityKitImmersionStyle
        {
            get => m_RealityKitImmersionStyle;
            set => m_RealityKitImmersionStyle = value;
        }

        /// <summary>
        /// The visibility of persistent system overlays (such as the visionOS hand gesture menus) in Metal immersive spaces.
        /// </summary>
        public Visibility metalImmersiveOverlays
        {
            get => m_MetalImmersiveOverlays;
            set => m_MetalImmersiveOverlays = value;
        }

        /// <summary>
        /// The visibility of persistent system overlays (such as the visionOS hand gesture menus) in RealityKit immersive spaces.
        /// </summary>
        public Visibility realityKitImmersiveOverlays
        {
            get => m_RealityKitImmersiveOverlays;
            set => m_RealityKitImmersiveOverlays = value;
        }

        /// <summary>
        /// Setting that determines if the IL2CPP_LARGE_EXECUTABLE_ARM_WORKAROUND flag is used when building an Xcode project.
        /// </summary>
        public bool il2CPPLargeExeWorkaround
        {
            get => m_IL2CPPLargeExeWorkaround;
            set => m_IL2CPPLargeExeWorkaround = value;
        }

        /// <summary>
        /// Setting this to true will enable the use of the actool utility during player builds to compile reference image libraries.
        /// </summary>
        public bool useACTool
        {
            get => m_UseACTool;
            set => m_UseACTool = value;
        }

        /// <summary>
        /// Gets the currently selected settings, or creates default settings if no <see cref="VisionOSSettings"/> have been set in Player Settings.
        /// </summary>
        /// <returns>The visionOS settings to use for the current Player build.</returns>
        public static VisionOSSettings GetOrCreateSettings()
        {
            var settings = currentSettings;
            if (settings != null)
                return settings;

            settings = CreateInstance<VisionOSSettings>();
            settings.m_RuntimeSettings = settings.GetOrCreateRuntimeSettings();
            return settings;
        }

        /// <summary>
        /// Get or set the <see cref="VisionOSSettings"/> to use for the Player build.
        /// </summary>
        public static VisionOSSettings currentSettings
        {
            get => EditorBuildSettings.TryGetConfigObject(Constants.k_SettingsKey, out VisionOSSettings settings) ? settings : null;

            set
            {
                if (value == null)
                {
                    EditorBuildSettings.RemoveConfigObject(Constants.k_SettingsKey);
                }
                else
                {
                    EditorBuildSettings.AddConfigObject(Constants.k_SettingsKey, value, true);
                }
            }
        }

        internal static bool TrySelect()
        {
            var settings = currentSettings;
            if (settings == null)
                return false;

            Selection.activeObject = settings;
            return true;
        }

        internal static SerializedObject GetSerializedSettings() => new(GetOrCreateSettings());

        static string GetAssetPathForSubFolders(string[] subFolders)
        {
            if (subFolders.Length <= 0)
                return null;

            var parentFolder = "Assets";
            foreach (var pathComponent in subFolders)
            {
                var fullPath = Path.Combine(parentFolder, pathComponent);
                var shouldCreate = true;
                foreach (var folder in AssetDatabase.GetSubFolders(parentFolder))
                {
                    if (string.Compare(FileUtil.GetPhysicalPath(folder), FileUtil.GetPhysicalPath(fullPath), true) == 0)
                    {
                        shouldCreate = false;
                        break;
                    }
                }

                if (shouldCreate)
                    AssetDatabase.CreateFolder(parentFolder, pathComponent);

                parentFolder = fullPath;
            }

            return parentFolder;
        }

        /// <summary>
        /// Returns the Swift string representation of the provided visibility value.
        /// </summary>
        /// <param name="visibility">The visibility value to convert.</param>
        /// <returns>The Swift string representation of the visibility.</returns>
        /// <exception cref="ArgumentOutOfRangeException">If an unknown value is provided.</exception>
        public static string VisibilityToString(Visibility visibility)
        {
            switch (visibility)
            {
                case Visibility.Automatic:
                    return ".automatic";
                case Visibility.Visible:
                    return ".visible";
                case Visibility.Hidden:
                    return ".hidden";
                default:
                    throw new ArgumentOutOfRangeException(nameof(visibility), visibility, null);
            }
        }

        /// <summary>
        /// Returns the Swift string representation of the provided immersion style value.
        /// </summary>
        /// <param name="immersionStyle">The immersion style value to convert.</param>
        /// <returns>The Swift string representation of the immersion style.</returns>
        /// <exception cref="ArgumentOutOfRangeException">If an unknown value is provided.</exception>
        public static string ImmersionStyleToString(ImmersionStyle immersionStyle)
        {
            switch (immersionStyle)
            {
                case ImmersionStyle.Automatic:
                    return ".automatic";
                case ImmersionStyle.Full:
                    return ".full";
                case ImmersionStyle.Mixed:
                    return ".mixed";
                case ImmersionStyle.Progressive:
                    return ".progressive";
                default:
                    throw new ArgumentOutOfRangeException(nameof(immersionStyle), immersionStyle, null);
            }
        }

#if UNITY_VISIONOS && UNITY_HAS_POLYSPATIAL
        [InitializeOnLoadMethod]
        static void Initialize()
        {
            PolySpatialSettings.Instance.RegisterPlayToDeviceCondition(PlayToDeviceAppModeCondition);
        }

        static bool PlayToDeviceAppModeCondition()
        {
            // If the app mode is not RealityKit, we can't enable PlayToDevice.
            var settings = GetOrCreateSettings();
            if (settings == null || settings.appMode != AppMode.RealityKit)
            {
                Debug.LogWarning("Play to Device only supports the RealityKit AppMode. Please change the selected AppMode in Project Settings > " +
                                 "XR Plug-in Management > Apple visionOS, or disable the Play To Device feature.");
                return false;
            }

            return true;
        }
#endif
    }
}
