using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;
using UnityEngine;

namespace UnityEditor.XR.VisionOS
{
    static partial class VisionOSBuildProcessor
    {
        const string k_XcodeProjectFolder = "Unity-VisionOS.xcodeproj";
        const string k_XcodeProjectName = "project.pbxproj";

        static readonly string[] k_SwiftTrampolineFiles = {
            "SpatialPointerEvent.swift",
            "SwiftTrampoline/UnityLibrary.swift",
            "SwiftTrampoline/UnitySwiftUIAppDelegate.swift",
            "SwiftTrampoline/UnitySwiftUISceneDelegate.swift",
            "UnityCompositorServicesConfiguration.swift",
            "UnityCompositorSpace.swift",
            "UnityTypeUtils.swift",
            "UnityMetalMainApp.swift",
        };

        static bool s_SplashScreenWasEnabled;
        static bool s_LoaderWasEnabled;

        static string GetXcodeProjectPath(string outputPath)
        {
            return Path.Combine(outputPath, k_XcodeProjectFolder, k_XcodeProjectName);
        }

        internal static int CompareVersionStrings(string version1, string version2)
        {
            if (!version1.Contains('.'))
                version1 += ".0";

            if (!version2.Contains('.'))
                version2 += ".0";

            try
            {
                var v1 = System.Version.Parse(version1);
                var v2 = System.Version.Parse(version2);
                return v1.CompareTo(v2);
            }
            catch
            {
                // Fallback to string comparison if parsing fails
                return string.Compare(version1, version2, System.StringComparison.Ordinal);
            }
        }

        class LinkerProcessor : IUnityLinkerProcessor
        {
            public int callbackOrder => 0;

            public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
            {
                if (report.summary.platform != BuildTarget.VisionOS || !VisionOSEditorUtils.IsLoaderEnabled())
                    return null;

                return FileUtil.GetPhysicalPath(AssetDatabase.GUIDToAssetPath("bdb2b35a4686f4d8ca0540be9862764d"));
            }
        }
    }
}
