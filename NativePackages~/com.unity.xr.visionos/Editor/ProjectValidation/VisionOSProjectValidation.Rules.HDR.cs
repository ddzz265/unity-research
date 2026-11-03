using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_HAS_URP
using UnityEngine.Rendering.Universal;
#endif

namespace UnityEditor.XR.VisionOS
{
    // Just the rule definitions and supporting fields and types
    static partial class VisionOSProjectValidation
    {
        // TODO: LXR-4185 Expose color gamuts player setting API
        static readonly MethodInfo k_GetColorGamuts = typeof(PlayerSettings).GetMethod("GetColorGamuts", BindingFlags.Static | BindingFlags.NonPublic);
        static readonly MethodInfo k_SetColorGamuts = typeof(PlayerSettings).GetMethod("SetColorGamuts", BindingFlags.Static | BindingFlags.NonPublic);
        static readonly RuleTestContainer k_ColorGamut = new()
        {
            Name = "DisplayP3 Color Gamut",
            Rule = new()
            {
                Message = "DisplayP3 color gamut is required for HDR rendering and must be in the first slot.",
                FixItMessage = "Enable DisplayP3 color gamut",
                Category = string.Format(k_CategoryFormat, "DisplayP3 Color Gamut"),
                CheckPredicate = () =>
                {
                    k_ColorGamut.Rule.Error = IsHDREnabled();
                    if (k_GetColorGamuts == null)
                        return true;

                    var gamuts = k_GetColorGamuts.Invoke(null, null) as ColorGamut[];
                    if (gamuts == null)
                        return true;

                    // You can't remove sRGB from the list, but just in case somehow we end up with an empty array, check for that.
                    if (gamuts.Length < 1)
                        return false;

                    return gamuts[0] == ColorGamut.DisplayP3;
                },
                FixIt = () =>
                {
                    if (k_SetColorGamuts == null)
                        return;

                    try
                    {
                        k_SetColorGamuts.Invoke(null, new object[] { new[] { ColorGamut.DisplayP3, ColorGamut.sRGB } });
                    }
                    catch (Exception)
                    {
                        // Ignore potential null reference exception that can occur if the user clicks "Fix" before having ever loaded player settings
                    }
                },
                IsRuleEnabled = () => VisionOSEditorUtils.IsLoaderEnabled() && AppModeSupportsMetal()
            },
            SetUp = () =>
            {
                var message = GetEditorSettingsIfExists(out var settings);
                if (message != null)
                    return message;

                if (k_GetColorGamuts == null)
                    return "Validation test failed: GetColorGamuts method could not be found via reflection.";

                // Check for SetColorGamuts since FixMe will silently fail if it is null
                if (k_SetColorGamuts == null)
                    return "Validation test failed: SetColorGamuts method could not be found via reflection.";

                s_PreviousColorGamuts = k_GetColorGamuts.Invoke(null, null);
                if (s_PreviousColorGamuts == null)
                    return "Validation test failed: GetColorGamuts returned null.";

                // Set color gamuts to ensure DisplayP3 is not included
                try
                {
                    k_SetColorGamuts.Invoke(null, new object[] { new[] { ColorGamut.sRGB } });
                }
                catch (Exception)
                {
                    // Ignore potential null reference exception that can occur if the user clicks "Fix" before having ever loaded player settings
                }

                s_LoaderWasEnabled = VisionOSEditorUtils.IsLoaderEnabled();
                message = SetVisionOSLoaderEnabledForTests(true);
                if (message != null)
                    return message;

                s_PreviousAppMode = settings.appMode;
                s_HdrWasEnabled = IsHDREnabled();
                settings.appMode = VisionOSSettings.AppMode.Metal;
                SetHDREnabled(true);

                return null;
            },
            TearDown = () =>
            {
                var settingsMessage = GetEditorSettingsIfExists(out var settings);
                if (settingsMessage != null)
                    settings.appMode = s_PreviousAppMode;

                string reflectionMessage = null;
                if (k_SetColorGamuts == null)
                {
                    reflectionMessage = "Validation test failed: SetColorGamuts method could not be found via reflection.";
                }
                else
                {
                    try
                    {
                        k_SetColorGamuts.Invoke(null, new[] { s_PreviousColorGamuts });
                    }
                    catch (Exception)
                    {
                        // Ignore potential null reference exception that can occur if the tests are run before having ever loaded player settings
                    }
                }

                SetHDREnabled(s_HdrWasEnabled);

                var loaderMessage = SetVisionOSLoaderEnabledForTests(s_LoaderWasEnabled);
                return HandleMultipleMessagesInTearDown(settingsMessage, reflectionMessage, loaderMessage);
            }
        };

        static readonly RuleTestContainer k_AllowHdr = new()
        {
            Name = "Allow HDR Display Output",
            Rule = new()
            {
                // TODO: LXR-4047 This is only necessary for URP because PLATFORM_REQUIRES_HDR doesn't change the behavior of shader stripping.
                // URP strips the HDR Overlay shader if PlayerSettings.allowHDRDisplaySupport is false, even though we have things
                // configured to assume this setting is always enabled in the player
                Message = "Allow HDR Display Output is required for HDR rendering on visionOS with the Universal Render Pipeline.",
                FixItMessage = "Enable Allow HDR Display Output",
                Category = string.Format(k_CategoryFormat, "Allow HDR Display Output"),
                CheckPredicate = () =>
                {
                    k_AllowHdr.Rule.Error = IsHDREnabled();
                    return PlayerSettings.allowHDRDisplaySupport;
                },
                FixIt = () => PlayerSettings.allowHDRDisplaySupport = true,
                IsRuleEnabled = () => VisionOSEditorUtils.IsLoaderEnabled() && AppModeSupportsMetal() && HasUrpAsset()
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
                s_HdrWasEnabled = IsHDREnabled();
                s_AllowHDRDisplaySupportWasEnabled = PlayerSettings.allowHDRDisplaySupport;
                settings.appMode = VisionOSSettings.AppMode.Metal;
                SetHDREnabled(true);
                PlayerSettings.allowHDRDisplaySupport = false;

                return null;
            },
            TearDown = () =>
            {
                var settingsMessage = GetEditorSettingsIfExists(out var settings);
                if (settings != null)
                    settings.appMode = s_PreviousAppMode;

                SetHDREnabled(s_HdrWasEnabled);
                PlayerSettings.allowHDRDisplaySupport = s_AllowHDRDisplaySupportWasEnabled;

                var loaderMessage = SetVisionOSLoaderEnabledForTests(s_LoaderWasEnabled);
                return HandleMultipleMessagesInTearDown(settingsMessage, loaderMessage);
            },
            SkipTest = () => !HasUrpAsset() ? "Skipping Allow HDR Display Output validation test because there is no URP asset set." : null
        };

        static readonly RuleTestContainer k_HdrToneMapping = new()
        {
            Name = "HDR Post Processing Tone Mapping",
            Rule = new()
            {
                Message = "HDR rendering will not look right when post processing is enabled but tone mapping is disabled.",
                FixItMessage = "Enable tone mapping in global volume profile",
                Category = string.Format(k_CategoryFormat, "HDR Post Processing Tone Mapping"),
                CheckPredicate = () =>
                {
                    k_HdrToneMapping.Rule.Error = IsHDREnabled();
                    return IsToneMappingEnabled();
                },
                FixIt = () => SetToneMappingEnabled(true),
                IsRuleEnabled = () => VisionOSEditorUtils.IsLoaderEnabled() && AppModeSupportsMetal() && HasUrpAsset()
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
                s_HdrWasEnabled = IsHDREnabled();
                s_ToneMappingWasEnabled = IsToneMappingEnabled();
                settings.appMode = VisionOSSettings.AppMode.Metal;
                SetHDREnabled(true);
                SetToneMappingEnabled(false);
                return null;
            },
            TearDown = () =>
            {
                var settingsMessage = GetEditorSettingsIfExists(out var settings);
                if (settings != null)
                    settings.appMode = s_PreviousAppMode;

                SetHDREnabled(s_HdrWasEnabled);
                SetToneMappingEnabled(s_ToneMappingWasEnabled);

                var loaderMessage = SetVisionOSLoaderEnabledForTests(s_LoaderWasEnabled);
                return HandleMultipleMessagesInTearDown(settingsMessage, loaderMessage);
            },
            SkipTest = () => !HasUrpAsset() ? "Skipping HDR Post Processing Tone Mapping validation test because there is no URP asset set." : null
        };

        static readonly RuleTestContainer k_HighQualityBloom = new()
        {
            Name = "Bloom High Quality Filtering",
            Rule = new()
            {
                Message = "Bloom High Quality Filtering must be disabled.",
                FixItMessage = "Disable Bloom High Quality Filtering in the Graphics Settings.",
                Category = string.Format(k_CategoryFormat, "Bloom High Quality Filtering"),
                CheckPredicate = () =>
                {
                    k_HighQualityBloom.Rule.Error = IsHDREnabled();
                    return IsBloomHighQualityFilteringDisabled();
                },
                FixIt = () => SetBloomHighQualityFilteringEnabled(false),
                IsRuleEnabled = () => VisionOSEditorUtils.IsLoaderEnabled() && AppModeSupportsMetal() && HasUrpAsset()
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
                s_HdrWasEnabled = IsHDREnabled();
                s_BloomHighQualityFilteringWasEnabled = IsBloomHighQualityFilteringDisabled();
                settings.appMode = VisionOSSettings.AppMode.Metal;
                SetHDREnabled(true);
                SetBloomHighQualityFilteringEnabled(true);
                return null;
            },
            TearDown = () =>
            {
                var settingsMessage = GetEditorSettingsIfExists(out var settings);
                if (settings != null)
                    settings.appMode = s_PreviousAppMode;

                SetHDREnabled(s_HdrWasEnabled);
                SetToneMappingEnabled(s_BloomHighQualityFilteringWasEnabled);

                var loaderMessage = SetVisionOSLoaderEnabledForTests(s_LoaderWasEnabled);
                return HandleMultipleMessagesInTearDown(settingsMessage, loaderMessage);
            },
            SkipTest = () => !HasUrpAsset() ? "Skipping HDR Post Processing Tone Mapping validation test because there is no URP asset set." : null
        };

        static readonly RuleTestContainer k_F16Format = new()
        {
            Name = "FP16 Format",
            Rule = new()
            {
                // FP16 is the only supported HDR format for builtin that includes alpha
                Message = "FP16 format is required for passthrough",
                FixItMessage = "Enable FP16 format",
                Category = string.Format(k_CategoryFormat, "FP16 Format"),
                CheckPredicate = () =>
                {
                    k_F16Format.Rule.Error = IsHDREnabled();
                    return GetTierSettings().hdrMode == CameraHDRMode.FP16;
                },
                FixIt = () =>
                {
                    var tierSettings = GetTierSettings();
                    tierSettings.hdrMode = CameraHDRMode.FP16;
                    SetTierSettings(tierSettings);
                },
                IsRuleEnabled = () => VisionOSEditorUtils.IsLoaderEnabled() && AppModeSupportsMetal() && !HasUrpAsset() && AppSupportsMixedImmersion()
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
                s_PreviousImmersionStyle = settings.metalImmersionStyle;
                s_HdrWasEnabled = IsHDREnabled();
                s_PreviousTierSettings = GetTierSettings();
                var newTierSettings = s_PreviousTierSettings;
                newTierSettings.hdrMode = CameraHDRMode.R11G11B10;
                newTierSettings.hdr = true;
                SetTierSettings(newTierSettings);
                settings.appMode = VisionOSSettings.AppMode.Metal;
                settings.metalImmersionStyle = VisionOSSettings.ImmersionStyle.Automatic;

                return null;
            },
            TearDown = () =>
            {
                var settingsMessage = GetEditorSettingsIfExists(out var settings);
                if (settings != null)
                {
                    settings.appMode = s_PreviousAppMode;
                    settings.metalImmersionStyle = s_PreviousImmersionStyle;
                }

                SetTierSettings(s_PreviousTierSettings);

                var loaderMessage = SetVisionOSLoaderEnabledForTests(s_LoaderWasEnabled);
                return HandleMultipleMessagesInTearDown(settingsMessage, loaderMessage);
            },
            SkipTest = () => HasUrpAsset() ? "Skipping FP16 Format validation test because URP is in use." : null
        };

        static readonly RuleTestContainer k_PreserveAlphaOutput = new()
        {
            Name = "Alpha Output",
            Rule = new()
            {
                Message = "Alpha output is required for passthrough when using HDR Universal Render Pipeline.",
                FixItMessage = "Enable Renders Over Native UI in project settings.",
                Category = string.Format(k_CategoryFormat, "Alpha Output"),
                CheckPredicate = () =>
                {
                    k_PreserveAlphaOutput.Rule.Error = IsHDREnabled();
                    return IsAlphaOutputEnabled();
                },
                FixIt = () => SetAlphaOutputEnabled(true),
                IsRuleEnabled = () => VisionOSEditorUtils.IsLoaderEnabled() && AppModeSupportsMetal() && AppSupportsMixedImmersion() && HasUrpAsset()
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
                s_HdrWasEnabled = IsHDREnabled();
                s_AlphaOutputWasEnabled = IsAlphaOutputEnabled();

                settings.appMode = VisionOSSettings.AppMode.Metal;
                SetHDREnabled(true);
                SetAlphaOutputEnabled(false);

                return null;
            },
            TearDown = () =>
            {
                var settingsMessage = GetEditorSettingsIfExists(out var settings);
                if (settings != null)
                    settings.appMode = s_PreviousAppMode;

                SetHDREnabled(s_HdrWasEnabled);
                SetAlphaOutputEnabled(s_AlphaOutputWasEnabled);
                var loaderMessage = SetVisionOSLoaderEnabledForTests(s_LoaderWasEnabled);
                return HandleMultipleMessagesInTearDown(settingsMessage, loaderMessage);
            },
            SkipTest = () => !HasUrpAsset() ? "Skipping Alpha Output validation test because there is no URP asset set." : null
        };

#if UNITY_HAS_URP
        static readonly RuleTestContainer k_AlphaProcessing = new()
        {
            Name = "Alpha Processing",
            Rule = new()
            {
                Message = "Alpha processing is required for passthrough when using post processing on Universal Render Pipeline.",
                FixItMessage = "Enable and alpha processing URP asset. You can ignore the warning in the URP asset inspector about camera back-buffer format",
                Category = string.Format(k_CategoryFormat, "Alpha Processing"),
                CheckPredicate = () =>
                {
                    k_AlphaProcessing.Rule.Error = IsUrpPostProcessingEnabled();
                    return IsAlphaProcessingEnabled();
                },
                FixIt = () => SetAlphaProcessingEnabled(true),
                IsRuleEnabled = () => VisionOSEditorUtils.IsLoaderEnabled() && AppModeSupportsMetal() && AppSupportsMixedImmersion()
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

#if UNITY_HAS_URP
                var asset = UniversalRenderPipeline.asset;
                if (asset != null)
                    s_UrpPostProcessAlphaOutputWasEnabled = asset.allowPostProcessAlphaOutput;
#endif

                settings.appMode = VisionOSSettings.AppMode.Metal;
                SetAlphaProcessingEnabled(false);

                return null;
            },
            TearDown = () =>
            {
                var settingsMessage = GetEditorSettingsIfExists(out var settings);
                if (settings != null)
                    settings.appMode = s_PreviousAppMode;

                SetAlphaProcessingEnabled(s_UrpPostProcessAlphaOutputWasEnabled);
                var loaderMessage = SetVisionOSLoaderEnabledForTests(s_LoaderWasEnabled);
                return HandleMultipleMessagesInTearDown(settingsMessage, loaderMessage);
            },
            SkipTest = () => !HasUrpAsset() ? "Skipping Alpha Output validation test because there is no URP asset set." : null
        };

        static readonly RuleTestContainer k_UpscalingFilterRule = new()
        {
            Name = "Upscaling Filter",
            Rule = new()
            {
                Message = "The application will not render if the Upscaling Filter is not set to Automatic.",
                FixItMessage = "Set the Upscaling Filter to Automatic in your active Render Pipeline Asset",
                Category = string.Format(k_CategoryFormat, "Upscaling Filter"),
                CheckPredicate = () =>
                {
                    k_UpscalingFilterRule.Rule.Error = IsHDREnabled();
                    return IsRenderPipelineUpscalingFilterAutomatic();
                },
                FixIt = () => SetRenderPipelineUpscalingFilter(UpscalingFilterSelection.Auto),
                IsRuleEnabled = () => VisionOSEditorUtils.IsLoaderEnabled() && AppModeSupportsMetal() && HasUrpAsset()
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

                s_HdrWasEnabled = IsHDREnabled();
                s_PreviousAppMode = settings.appMode;

                SetHDREnabled(true);
                settings.appMode = VisionOSSettings.AppMode.Metal;

                StoreRenderPipelineUpscalingFilterSettings();
                SetRenderPipelineUpscalingFilter(UpscalingFilterSelection.FSR);
                return null;
            },
            TearDown = () =>
            {
                var settingsMessage = GetEditorSettingsIfExists(out var settings);
                if (settings != null)
                    settings.appMode = s_PreviousAppMode;

                SetHDREnabled(s_HdrWasEnabled);

                RestoreRenderPipelineUpscalingFilterSettings();

                var loaderMessage = SetVisionOSLoaderEnabledForTests(s_LoaderWasEnabled);
                return HandleMultipleMessagesInTearDown(settingsMessage, loaderMessage);
            },
            SkipTest = () => !HasUrpAsset() ? "Skipping Quality Upscaling Filter validation test because there is no URP asset set." : null
        };
#endif

        // This field needs to be made internal for testing
        // ReSharper disable once MemberCanBePrivate.Global
        internal static IEnumerable<RuleTestContainer> GetHDRValidationRules()
        {
            yield return k_ColorGamut;
            yield return k_AllowHdr;
            yield return k_HdrToneMapping;
            yield return k_HighQualityBloom;
            yield return k_F16Format;
            yield return k_PreserveAlphaOutput;

#if UNITY_HAS_URP
            yield return k_UpscalingFilterRule;
            yield return k_AlphaProcessing;
#endif
        }


        static bool IsHDREnabled()
        {
#if UNITY_HAS_URP
            var asset = UniversalRenderPipeline.asset;
            if (asset != null)
                return asset.supportsHDR;
#endif

            return GetTierSettings().hdr;
        }

        static void SetHDREnabled(bool enabled)
        {
#if UNITY_HAS_URP
            var asset = UniversalRenderPipeline.asset;
            if (asset != null)
            {
                Undo.RecordObject(asset, "Disable URP HDR");
                asset.supportsHDR = enabled;
                return;
            }
#endif

            var tierSettings = GetTierSettings();
            tierSettings.hdr = enabled;
            SetTierSettings(tierSettings);
        }
    }
}
