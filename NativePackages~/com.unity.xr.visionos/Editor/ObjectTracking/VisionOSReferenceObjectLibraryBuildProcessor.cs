#if UNITY_VISIONOS
using System;
using System.Linq;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.ARSubsystems;
using UnityEngine;
using UnityEngine.XR.VisionOS;
using UnityEngine.XR.ARSubsystems;

namespace UnityEditor.XR.VisionOS
{
    static class VisionOSReferenceObjectLibraryBuildProcessor
    {
        [Flags]
        enum Warnings
        {
            None = 0,
            MissingEntry = 1 << 0,
            EmptyLibrary = 1 << 1,
            MissingName = 1 << 2,
            All =
                MissingEntry |
                EmptyLibrary |
                MissingName
        }

        static void ValidateReferenceObjects(Warnings warnings)
        {
            foreach (var library in VisionOSBuildHelper.GetReferenceLibraries<XRReferenceObjectLibrary, XRReferenceObject>(true, true))
            {
                var resourceCount = 0;

                foreach (var referenceObject in library)
                {
                    if (string.IsNullOrEmpty(referenceObject.name) && (warnings & Warnings.MissingName) != 0)
                    {
                        Debug.LogWarning($"Reference object {library.IndexOf(referenceObject)} named '{referenceObject.name}' in library {AssetDatabase.GetAssetPath(library)} does not have a name. The reference object will still work, but you will not be able to refer to it by name.");
                    }

                    var visionOSEntry = referenceObject.FindEntry<VisionOSReferenceObjectEntry>();
                    if (visionOSEntry == null)
                    {
                        if ((warnings & Warnings.MissingEntry) != 0)
                        {
                            Debug.LogWarning($"The VisionOS variant for reference object {library.IndexOf(referenceObject)} named '{referenceObject.name}' in library {AssetDatabase.GetAssetPath(library)} is missing. This reference object will omitted from the library.");
                        }
                    }
                    else
                    {
                        var assetPath = AssetDatabase.GetAssetPath(visionOSEntry);
                        if (string.IsNullOrEmpty(assetPath))
                            throw new BuildFailedException($"The VisionOS variant for reference object {library.IndexOf(referenceObject)} named '{referenceObject.name}' in reference object library {AssetDatabase.GetAssetPath(library)} does not refer to a valid asset file.");

                        resourceCount++;
                    }
                }

                if (resourceCount == 0 && (warnings & Warnings.EmptyLibrary) != 0)
                {
                    Debug.LogWarning($"Reference object library at {AssetDatabase.GetAssetPath(library)} does not contain any VisionOS reference objects. The library will be empty.");
                }
            }
        }

        class Preprocessor : IPreprocessBuildWithReport, ARBuildProcessor.IPreprocessBuild
        {
            public int callbackOrder => 1;

            static bool VisionOSEnabled(BuildReport report)
            {
                return report.summary.platform == BuildTarget.VisionOS;
            }

            static void UpdateAssets(bool visionOSEnabled)
            {
                VisionOSReferenceObjectEntry.SetObjectBytesEnabled(visionOSEnabled);

                if (visionOSEnabled)
                {
                    ValidateReferenceObjects(Warnings.All);
                }
            }

            void ARBuildProcessor.IPreprocessBuild.OnPreprocessBuild(PreprocessBuildEventArgs buildEventArgs)
            {
                UpdateAssets(buildEventArgs.activeLoadersForBuildTarget.OfType<VisionOSLoader>().Any());
            }

            void IPreprocessBuildWithReport.OnPreprocessBuild(BuildReport report)
            {
                UpdateAssets(VisionOSEnabled(report));
            }
        }
    }
}
#endif
