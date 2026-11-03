using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.XR.CoreUtils.Editor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityObject = UnityEngine.Object;

#if UNITY_HAS_URP
using UnityEngine.Rendering.Universal;
#endif

namespace UnityEditor.XR.VisionOS
{
    // Just the rule definitions and supporting fields and types
    static partial class VisionOSProjectValidation
    {
        /// <summary>
        /// Test container to include a name for each rule and Setup method so the test can validate that `CheckPredicate` returns false when the rule should be
        /// triggered. Also provides a TearDown method to undo whatever was done in Setup. TearDown should reset any project state that was modified in SetUp,
        /// and SetUp should set things up so that the rule is enabled (IsRuleEnabled returns true) and the check fails (CheckPredicate returns false).
        /// </summary>
        internal struct RuleTestContainer
        {
            /// <summary>
            /// The name of this rule, which will be included in the name of the test
            /// </summary>
            public string Name;

            /// <summary>
            /// The validation rule
            /// </summary>
            public BuildValidationRule Rule;

            /// <summary>
            /// SetUp method used by tests to ensure the rule is enabled and temporarily fails the check. The test will then ensure that `IsRuleEnabled` returns
            /// true and `CheckPredicate` returns false. Then it will run `FixIt`, ensure there are no unexpected logs, check that `CheckPredicate` returns true,
            /// and run TearDown to reset project/editor state.
            /// Return a message if any errors were encountered during setup (this is to avoid a dependency on nunit in this assembly), null otherwise.
            /// </summary>
            public Func<string> SetUp;

            /// <summary>
            /// TearDown method used by tests to reset project/editor state to what it was before SetUp and FixIt were run.
            /// Return a message if any errors were encountered during teardown (this is to avoid a dependency on nunit in this assembly), null otherwise.
            /// </summary>
            public Func<string> TearDown;

            /// <summary>
            /// Return a message if this test should be skipped for some reason. For example, some URP validations can only be tested if a URP asset is set,
            /// null otherwise.
            /// </summary>
            public Func<string> SkipTest;

            /// <summary>
            /// When FixItAutomatic is false, we expect the user to take some action. Define this method to fill in what the user might do. If there really is
            /// no way to fix the issue in code, return true for SkipTest above.
            /// Return a message if any errors were encountered during this method (this is to avoid a dependency on nunit in this assembly), null otherwise.
            /// </summary>
            public Func<string> TestFixIt;
        }

        const string k_CategoryFormat = "VisionOS - {0}";
        const string k_ARSessionMessageMetal = "An ARSession component is required to be active in the scene.";
        const string k_ARSessionMessageRealityKit = "An ARSession component is required to be active in the scene to provide access to ARKit features.";

#if UNITY_HAS_URP
        const string k_RendererDataListPropertyName = "m_RendererDataList";
        const string k_CopyDepthModePropertyName = "m_CopyDepthMode";
#endif

        const BuildTargetGroup k_VisionOSBuildTarget = BuildTargetGroup.VisionOS;

        // This field needs to be made internal for testing
        // ReSharper disable once MemberCanBePrivate.Global
        internal static IEnumerable<RuleTestContainer> GetValidationRules()
        {
            yield return new()
            {
                Name = "Color Space",
                Rule = new()
                {
                    Message = "The Color Space inside Player Settings must be set to Linear.",
                    Category = string.Format(k_CategoryFormat, "Color Space"),
                    Error = true,
                    CheckPredicate = () => PlayerSettings.colorSpace == ColorSpace.Linear,
                    FixIt = () => PlayerSettings.colorSpace = ColorSpace.Linear
                },
                SetUp = () =>
                {
                    s_PreviousColorSpace = PlayerSettings.colorSpace;
                    PlayerSettings.colorSpace = ColorSpace.Gamma;
                    return null;
                },
                TearDown = () =>
                {
                    PlayerSettings.colorSpace = s_PreviousColorSpace;
                    return null;
                }
            };

            var arSessionRuleContainer = new RuleTestContainer()
            {
                Name = "ARSession",
                Rule = new()
                {
                    Message = k_ARSessionMessageMetal,
                    Category = string.Format(k_CategoryFormat, "ARSession"),
                    Error = true,
                    FixIt = CreateARSession,
                    IsRuleEnabled = () => VisionOSEditorUtils.IsLoaderEnabled() && !CheckAppMode(VisionOSSettings.AppMode.Windowed)
                },
                SetUp = () =>
                {
                    var message = CheckForLeakedARSession();
                    if (message != null)
                        return message;

                    message = GetEditorSettingsIfExists(out var settings);
                    if (message != null)
                        return message;

                    s_LoaderWasEnabled = VisionOSEditorUtils.IsLoaderEnabled();
                    message = SetVisionOSLoaderEnabledForTests(true);
                    if (message != null)
                        return message;

                    s_PreviousAppMode = settings.appMode;
                    settings.appMode = VisionOSSettings.AppMode.Metal;

                    return null;
                },
                TearDown = () =>
                {
                    DestroyARSessionIfNewSessionExists();
                    var settingsMessage = GetEditorSettingsIfExists(out var settings);
                    if (settingsMessage == null)
                        settings.appMode = s_PreviousAppMode;

                    var loaderMessage = SetVisionOSLoaderEnabledForTests(s_LoaderWasEnabled);
                    return HandleMultipleMessagesInTearDown(settingsMessage, loaderMessage);
                }
            };

            arSessionRuleContainer.Rule.CheckPredicate = () =>
            {
                var isMetal = CheckAppMode(VisionOSSettings.AppMode.Metal);
                arSessionRuleContainer.Rule.Error = isMetal;
                arSessionRuleContainer.Rule.Message = isMetal ? k_ARSessionMessageMetal : k_ARSessionMessageRealityKit;
                return UnityObject.FindAnyObjectByType<ARSession>(FindObjectsInactive.Include) != null;
            };

            yield return arSessionRuleContainer;

            yield return new()
            {
                Name = "ARSession and XR Plug-in",
                Rule = new()
                {
                    Message = "The ARSession component requires the Apple visionOS plug-in to be enabled in the XR Plug-in Management.",
                    FixItMessage = "Enable the Apple visionOS plug-in",
                    Category = string.Format(k_CategoryFormat, "ARSession and XR Plug-in"),
                    CheckPredicate = VisionOSEditorUtils.IsLoaderEnabled,
                    FixIt = () => SetVisionOSLoaderEnabled(true),
                    IsRuleEnabled = () => UnityObject.FindAnyObjectByType<ARSession>(FindObjectsInactive.Include) != null
                },
                SetUp = () =>
                {
                    var message = CheckForLeakedARSession();
                    if (message != null)
                        return message;

                    s_LoaderWasEnabled = VisionOSEditorUtils.IsLoaderEnabled();
                    message = SetVisionOSLoaderEnabledForTests(false);
                    if (message != null)
                        return message;

                    // Create an AR Session to trigger the check
                    CreateARSession();

                    return null;
                },
                TearDown = () =>
                {
                    DestroyARSessionIfNewSessionExists();
                    return SetVisionOSLoaderEnabledForTests(s_LoaderWasEnabled);
                }
            };

            yield return new()
            {
                Name = "Metal app mode and XR Plug-in",
                Rule = new()
                {
                    Message = "Metal rendering requires the Apple visionOS plug-in to be enabled in the XR Plug-in Management.",
                    FixItMessage = "Enable the Apple visionOS plug-in",
                    Category = string.Format(k_CategoryFormat, "Metal app mode and XR Plug-in"),
                    Error = true,
                    CheckPredicate = VisionOSEditorUtils.IsLoaderEnabled,
                    FixIt = () => SetVisionOSLoaderEnabled(true),
                    IsRuleEnabled = () =>
                    {
                        var settings = VisionOSSettings.currentSettings;
                        if (settings == null)
                            return false;

                        return settings.appMode == VisionOSSettings.AppMode.Metal && !VisionOSEditorUtils.IsLoaderEnabled();
                    }
                },
                SetUp = () =>
                {
                    var message = GetEditorSettingsIfExists(out var settings);
                    if (message != null)
                        return message;

                    s_LoaderWasEnabled = VisionOSEditorUtils.IsLoaderEnabled();
                    message = SetVisionOSLoaderEnabledForTests(false);
                    if (message != null)
                        return message;

                    s_PreviousAppMode = settings.appMode;
                    settings.appMode = VisionOSSettings.AppMode.Metal;

                    return null;
                },
                TearDown = () =>
                {
                    var settingsMessage = GetEditorSettingsIfExists(out var settings);
                    if (settings != null)
                        settings.appMode = s_PreviousAppMode;

                    var loaderMessage = SetVisionOSLoaderEnabledForTests(s_LoaderWasEnabled);
                    return HandleMultipleMessagesInTearDown(settingsMessage, loaderMessage);
                }
            };

            yield return new()
            {
                Name = "World Sensing Usage Description",
                Rule = new()
                {
                    Message =
                        "World Sensing Usage Description (in Apple visionOS settings) is required for world sensing features (images, planes or meshes). " +
                        "If your app uses world sensing, you need to add a World Sensing Usage Description in the Apple visionOS settings. If your app does not " +
                        "use world sensing features, you can safely ignore this warning.",
                    FixItMessage = "Update the World Sensing Usage Description",
                    Category = string.Format(k_CategoryFormat, "World Sensing Usage Description"),
                    Error = false,
                    CheckPredicate = () => !GetEditorSettings(out var editorSettings) || !string.IsNullOrEmpty(editorSettings.worldSensingUsageDescription),
                    FixItAutomatic = false,
                    FixIt = () => SettingsService.OpenProjectSettings("Project/XR Plug-in Management/Apple visionOS"),
                    IsRuleEnabled = () => VisionOSEditorUtils.IsLoaderEnabled() && !CheckAppMode(VisionOSSettings.AppMode.Windowed)
                },
                SetUp = () =>
                {
                    var message = GetEditorSettingsIfExists(out var settings);
                    if (message != null)
                        return message;

                    s_LoaderWasEnabled = VisionOSEditorUtils.IsLoaderEnabled();
                    message = SetVisionOSLoaderEnabledForTests(true);
                    if (message != null)
                        return message;

                    s_PreviousAppMode = settings.appMode;
                    s_PreviousUsageDescription = settings.worldSensingUsageDescription;
                    settings.appMode = VisionOSSettings.AppMode.Metal;
                    settings.worldSensingUsageDescription = null;

                    return null;
                },
                TearDown = () =>
                {
                    var settingsMessage = GetEditorSettingsIfExists(out var settings);
                    if (settings != null)
                    {
                        settings.appMode = s_PreviousAppMode;
                        settings.worldSensingUsageDescription = s_PreviousUsageDescription;
                    }

                    var loaderMessage = SetVisionOSLoaderEnabledForTests(s_LoaderWasEnabled);
                    return HandleMultipleMessagesInTearDown(settingsMessage, loaderMessage);
                },
                TestFixIt = () =>
                {
                    var message = GetEditorSettingsIfExists(out var settings);
                    if (message != null)
                        return message;

                    settings.worldSensingUsageDescription = k_TestUsageDescription;
                    return null;
                }
            };

            yield return new()
            {
                Name = "Splash Screen",
                Rule = new()
                {
                    Message = "Splash screen is not yet supported for visionOS. If the splash screen is enabled, you may have errors when building or when " +
                        "running your application in the simulator or in the device.",
                    FixItMessage = "Disable the splash screen",
                    Category = string.Format(k_CategoryFormat, "Splash Screen"),
                    Error = true,
                    CheckPredicate = () => !PlayerSettings.SplashScreen.show,
                    FixIt = () => PlayerSettings.SplashScreen.show = false
                },
                SetUp = () =>
                {
                    s_SplashScreenWasEnabled = PlayerSettings.SplashScreen.show;
                    PlayerSettings.SplashScreen.show = true;
                    return null;
                },
                TearDown = () =>
                {
                    PlayerSettings.SplashScreen.show = s_SplashScreenWasEnabled;
                    return null;
                }
            };

#if UNITY_HAS_URP
            yield return new()
            {
                Name = "Camera depth texture",
                Rule = new()
                {
                    Message = "Each camera must generate a depth texture.",
                    Category = string.Format(k_CategoryFormat, "Camera depth texture"),
                    Error = true,
                    CheckPredicate = IsCamerasDepthTextureDisabled,
                    FixIt = SetCamerasDepthTextureToEnabled,
                    IsRuleEnabled = () => VisionOSEditorUtils.IsLoaderEnabled() && AppModeSupportsMetal()
                },
                SetUp = () =>
                {
                    var message = GetEditorSettingsIfExists(out var settings);
                    if (message != null)
                        return message;

                    s_LoaderWasEnabled = VisionOSEditorUtils.IsLoaderEnabled();
                    message = SetVisionOSLoaderEnabledForTests(true);
                    if (message != null)
                        return message;

                    s_PreviousAppMode = settings.appMode;
                    settings.appMode = VisionOSSettings.AppMode.Metal;

                    return CreateTestCameraWithNoDepthTexture();
                },
                TearDown = () =>
                {
                    var settingsMessage = GetEditorSettingsIfExists(out var settings);
                    if (settings != null)
                        settings.appMode = s_PreviousAppMode;

                    var destroyMessage = DestroyTestCamera();
                    var loaderMessage = SetVisionOSLoaderEnabledForTests(s_LoaderWasEnabled);
                    return HandleMultipleMessagesInTearDown(settingsMessage, destroyMessage, loaderMessage);
                },
                SkipTest = () =>
                    UniversalRenderPipeline.asset == null ? "Skipping Camera depth texture validation test because there is no URP asset set." : null
            };

            yield return new()
            {
                Name = "DepthTextureMode",
                Rule = new()
                {
                    Message = "After Opaques is the only supported Depth Texture Mode for visionOS Metal applications.",
                    Category = string.Format(k_CategoryFormat, "DepthTextureMode"),
                    Error = true,
                    CheckPredicate = IsDepthTextureModeNotAfterOpaques,
                    FixIt = () => SetCopyDepthMode(CopyDepthMode.AfterOpaques),
                    IsRuleEnabled = () => VisionOSEditorUtils.IsLoaderEnabled() && AppModeSupportsMetal()
                },
                SetUp = () =>
                {
                    var message = GetEditorSettingsIfExists(out var settings);
                    if (message != null)
                        return message;

                    s_LoaderWasEnabled = VisionOSEditorUtils.IsLoaderEnabled();
                    message = SetVisionOSLoaderEnabledForTests(true);
                    if (message != null)
                        return message;

                    s_PreviousAppMode = settings.appMode;
                    settings.appMode = VisionOSSettings.AppMode.Metal;

                    StoreCopyDepthModeSettings();
                    SetCopyDepthMode(CopyDepthMode.AfterTransparents);

                    return null;
                },
                TearDown = () =>
                {
                    var settingsMessage = GetEditorSettingsIfExists(out var settings);
                    if (settings != null)
                        settings.appMode = s_PreviousAppMode;

                    RestoreCopyDepthModes();
                    var loaderMessage = SetVisionOSLoaderEnabledForTests(s_LoaderWasEnabled);
                    return HandleMultipleMessagesInTearDown(settingsMessage, loaderMessage);
                },
                SkipTest = () => UniversalRenderPipeline.asset == null ? "Skipping DepthTextureMode validation test because there is no URP asset set." : null
            };
#endif

#if INCLUDE_UNITY_XR_HANDS
            yield return new()
            {
                Name = "Hand Tracking Usage Description Error",
                Rule = new()
                {
                    Message = "Hand Tracking Usage Description (in Apple visionOS settings) is required to automatically initialize hand tracking. You must " +
                        "set a usage description to prevent your app from crashing when trying to start an AR Session.",
                    FixItMessage = "Update the Hand Tracking Usage Description or disable Initialize Hand Tracking On Startup",
                    Category = string.Format(k_CategoryFormat, "Hand Tracking Usage Description"),
                    Error = true,
                    CheckPredicate = () => !GetEditorSettings(out var editorSettings) || !string.IsNullOrEmpty(editorSettings.handsTrackingUsageDescription),
                    FixItAutomatic = false,
                    FixIt = () => SettingsService.OpenProjectSettings("Project/XR Plug-in Management/Apple visionOS"),
                    IsRuleEnabled = () => VisionOSEditorUtils.IsLoaderEnabled() && !CheckAppMode(VisionOSSettings.AppMode.Windowed)

                        // Show an error if initializeHandTrackingOnStartup is true
                        && GetRuntimeSettings(out var runtimeSettings) && runtimeSettings.initializeHandTrackingOnStartup
                },
                SetUp = () =>
                {
                    var message = GetEditorSettingsIfExists(out var editorSettings);
                    if (message != null)
                        return message;

                    message = GetRuntimeSettingsIfExists(out var runtimeSettings);
                    if (message != null)
                        return message;

                    s_LoaderWasEnabled = VisionOSEditorUtils.IsLoaderEnabled();
                    message = SetVisionOSLoaderEnabledForTests(true);
                    if (message != null)
                        return message;

                    s_PreviousAppMode = editorSettings.appMode;
                    s_PreviousUsageDescription = editorSettings.handsTrackingUsageDescription;
                    s_InitializeHandTrackingWasEnabled = runtimeSettings.initializeHandTrackingOnStartup;
                    editorSettings.appMode = VisionOSSettings.AppMode.Metal;
                    editorSettings.handsTrackingUsageDescription = null;
                    runtimeSettings.initializeHandTrackingOnStartup = true;

                    return null;
                },
                TearDown = () =>
                {
                    var editorSettingsMessage = GetEditorSettingsIfExists(out var editorSettings);
                    if (editorSettings != null)
                    {
                        editorSettings.appMode = s_PreviousAppMode;
                        editorSettings.handsTrackingUsageDescription = s_PreviousUsageDescription;
                    }

                    var runtimeSettingsMessage = GetRuntimeSettingsIfExists(out var runtimeSettings);
                    if (runtimeSettings != null)
                        runtimeSettings.initializeHandTrackingOnStartup = s_InitializeHandTrackingWasEnabled;

                    var loaderMessage = SetVisionOSLoaderEnabledForTests(s_LoaderWasEnabled);
                    return HandleMultipleMessagesInTearDown(editorSettingsMessage, runtimeSettingsMessage, loaderMessage);
                },
                TestFixIt = () =>
                {
                    var message = GetEditorSettingsIfExists(out var editorSettings);
                    if (message != null)
                        return message;

                    editorSettings.handsTrackingUsageDescription = k_TestUsageDescription;
                    return null;
                }
            };

            yield return new()
            {
                Name = "Hand Tracking Usage Description Warning",
                Rule = new()
                {
                    Message = "Hand Tracking Usage Description (in Apple visionOS settings) is required for hand tracking features. If your app uses hand " +
                        "tracking, your app will crash when trying to start an AR Session. If your app does not use hand tracking features, you can safely " +
                        "ignore this warning.",
                    FixItMessage = "Update the Hand Tracking Usage Description",
                    Category = string.Format(k_CategoryFormat, "Hand Tracking Usage Description"),
                    CheckPredicate = () => !GetEditorSettings(out var editorSettings) || !string.IsNullOrEmpty(editorSettings.handsTrackingUsageDescription),
                    FixItAutomatic = false,
                    FixIt = () => SettingsService.OpenProjectSettings("Project/XR Plug-in Management/Apple visionOS"),
                    IsRuleEnabled = () => VisionOSEditorUtils.IsLoaderEnabled() && !CheckAppMode(VisionOSSettings.AppMode.Windowed)

                        // Show a warning if initializeHandTrackingOnStartup is false
                        && GetRuntimeSettings(out var runtimeSettings) && !runtimeSettings.initializeHandTrackingOnStartup
                },
                SetUp = () =>
                {
                    var message = GetEditorSettingsIfExists(out var editorSettings);
                    if (message != null)
                        return message;

                    message = GetRuntimeSettingsIfExists(out var runtimeSettings);
                    if (message != null)
                        return message;

                    s_LoaderWasEnabled = VisionOSEditorUtils.IsLoaderEnabled();
                    message = SetVisionOSLoaderEnabledForTests(true);
                    if (message != null)
                        return message;

                    s_PreviousAppMode = editorSettings.appMode;
                    s_PreviousUsageDescription = editorSettings.handsTrackingUsageDescription;
                    s_InitializeHandTrackingWasEnabled = runtimeSettings.initializeHandTrackingOnStartup;
                    editorSettings.appMode = VisionOSSettings.AppMode.Metal;
                    editorSettings.handsTrackingUsageDescription = null;
                    runtimeSettings.initializeHandTrackingOnStartup = false;

                    return null;
                },
                TearDown = () =>
                {
                    var editorSettingsMessage = GetEditorSettingsIfExists(out var editorSettings);
                    if (editorSettings != null)
                    {
                        editorSettings.appMode = s_PreviousAppMode;
                        editorSettings.handsTrackingUsageDescription = s_PreviousUsageDescription;
                    }

                    var runtimeSettingsMessage = GetRuntimeSettingsIfExists(out var runtimeSettings);
                    if (runtimeSettings != null)
                        runtimeSettings.initializeHandTrackingOnStartup = s_InitializeHandTrackingWasEnabled;

                    var loaderMessage = SetVisionOSLoaderEnabledForTests(s_LoaderWasEnabled);
                    return HandleMultipleMessagesInTearDown(runtimeSettingsMessage, editorSettingsMessage, loaderMessage);
                },
                TestFixIt = () =>
                {
                    var message = GetEditorSettingsIfExists(out var editorSettings);
                    if (message != null)
                        return message;

                    editorSettings.handsTrackingUsageDescription = k_TestUsageDescription;
                    return null;
                }
            };
#endif

            yield return new()
            {
                Name = "Minimum visionOS Version",
                Rule = new()
                {
                    Message = "Metal, RealityKit, and Hybrid apps require minimum visionOS version 2.0.",
                    FixItMessage = "Set the Target Minimum visionOS Version to 2.0 in Player Settings.",
                    Category = string.Format(k_CategoryFormat, "Minimum visionOS Version"),
                    Error = true,
                    CheckPredicate = () =>
                    {
                        if (!GetEditorSettings(out var settings))
                            return true;

                        // Only check for Metal, RealityKit, or Hybrid modes
                        if (settings.appMode == VisionOSSettings.AppMode.Windowed)
                            return true;

                        var targetVersion = PlayerSettings.VisionOS.targetOSVersionString;
                        if (string.IsNullOrEmpty(targetVersion))
                            return true; // Default/not set, will be set by post-processor

                        // Check if version is >= 2.0
                        return VisionOSBuildProcessor.CompareVersionStrings(targetVersion, "2.0") >= 0;
                    },
                    FixIt = () =>
                    {
                        try
                        {
                            PlayerSettings.VisionOS.targetOSVersionString = "2.0";
                            Debug.Log("Set Target Minimum visionOS Version to 2.0");
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"Failed to set minimum visionOS version: {e.Message}");
                        }
                    },
                    IsRuleEnabled = () =>
                    {
                        if (!GetEditorSettings(out var settings))
                            return false;

                        // Only enable for Metal, RealityKit, or Hybrid modes
                        return settings.appMode != VisionOSSettings.AppMode.Windowed && VisionOSEditorUtils.IsLoaderEnabled();
                    }
                },
                SetUp = () =>
                {
                    var message = GetEditorSettingsIfExists(out var settings);
                    if (message != null)
                        return message;

                    s_LoaderWasEnabled = VisionOSEditorUtils.IsLoaderEnabled();
                    message = SetVisionOSLoaderEnabledForTests(true);
                    if (message != null)
                        return message;

                    s_PreviousAppMode = settings.appMode;
                    settings.appMode = VisionOSSettings.AppMode.Metal;

                    s_PreviousTargetOSVersion = PlayerSettings.VisionOS.targetOSVersionString;
                    PlayerSettings.VisionOS.targetOSVersionString = "1.0";

                    return null;
                },
                TearDown = () =>
                {
                    var message = GetEditorSettingsIfExists(out var settings);
                    if (settings != null)
                        settings.appMode = s_PreviousAppMode;

                    PlayerSettings.VisionOS.targetOSVersionString = s_PreviousTargetOSVersion;

                    var loaderMessage = SetVisionOSLoaderEnabledForTests(s_LoaderWasEnabled);
                    return HandleMultipleMessagesInTearDown(message, loaderMessage);
                }
            };

            yield return new()
            {
                Name = "MSAA Sample Count",
                Rule = new()
                {
                    Message = "MSAA set to 8x is not supported on visionOS.",
                    FixItMessage = "Set MSAA sample count to 4x or disable 8x.",
                    Category = string.Format(k_CategoryFormat, "MSAA Sample Count"),
                    Error = true,
                    CheckPredicate = () => GetMSAASampleCount() != 8,
                    FixIt = () => SetMSAASampleCount(4),
                    IsRuleEnabled = () => VisionOSEditorUtils.IsLoaderEnabled()
                },
                SetUp = () =>
                {
                    var message = GetEditorSettingsIfExists(out var settings);
                    if (message != null)
                        return message;

                    s_LoaderWasEnabled = VisionOSEditorUtils.IsLoaderEnabled();
                    message = SetVisionOSLoaderEnabledForTests(true);
                    if (message != null)
                        return message;

                    s_PreviousAppMode = settings.appMode;
                    s_PreviousMSAASampleCount = GetMSAASampleCount();
                    settings.appMode = VisionOSSettings.AppMode.Metal;
                    SetMSAASampleCount(8);

                    return null;
                },
                TearDown = () =>
                {
                    var settingsMessage = GetEditorSettingsIfExists(out var settings);
                    if (settings != null)
                        settings.appMode = s_PreviousAppMode;

                    SetMSAASampleCount(s_PreviousMSAASampleCount);

                    var loaderMessage = SetVisionOSLoaderEnabledForTests(s_LoaderWasEnabled);
                    return HandleMultipleMessagesInTearDown(settingsMessage, loaderMessage);
                }
            };
        }

        static string HandleMultipleMessagesInTearDown(params string[] messages)
        {
            string result = null;
            var length = messages.Length;
            for (var i = 0; i < length; i++)
            {
                var message = messages[i];
                if (message != null)
                {
                    // Return first message if first step failed, log second message in case both steps failed
                    if (result == null)
                        result = message;
                    else
                        Debug.LogError(message);
                }
            }

            return result;
        }

        static int GetMSAASampleCount()
        {
#if UNITY_HAS_URP
            var asset = UniversalRenderPipeline.asset;
            if (asset != null)
                return asset.msaaSampleCount;
#endif

            return QualitySettings.antiAliasing;
        }

        static void SetMSAASampleCount(int sampleCount)
        {
#if UNITY_HAS_URP
            var asset = UniversalRenderPipeline.asset;
            if (asset != null)
            {
                var serializedObject = new SerializedObject(asset);
                var msaaProperty = serializedObject.FindProperty("m_MSAA");
                if (msaaProperty == null)
                    return;

                msaaProperty.intValue = sampleCount;
                serializedObject.ApplyModifiedProperties();
            }
#endif

            QualitySettings.antiAliasing = sampleCount;
        }
    }
}