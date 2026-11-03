#if UNITY_VISIONOS
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEditor.XR.ARSubsystems;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.VisionOS;

namespace UnityEditor.XR.VisionOS
{
    /// <summary>
    /// Looks at all XRReferenceImageLibraries in the project and generates an AR Resource Group for each library,
    /// then inserts them into a new Xcode asset catalog called "ARReferenceImages".
    /// </summary>
    static class VisionOSReferenceImageLibraryBuildProcessor
    {
        static IEnumerable<ValueTuple<ARResourceGroup, XRReferenceImageLibrary>> ResourceGroups(List<XRReferenceImageLibrary> libraries)
        {
            // Create a resource group for each reference image library
            foreach (var library in libraries)
            {
                var resourceGroup = new ARResourceGroup(library.name + "_" + library.guid.ToUUIDString());

                // Create a resource group for each library
                foreach (var referenceImage in library)
                {
                    try
                    {
                        resourceGroup.AddResource(new ARReferenceImage(referenceImage));
                    }
                    catch (ARReferenceImage.InvalidWidthException)
                    {
                        throw new BuildFailedException(string.Format("ARKit requires dimensions for all images. Reference image at index {0} named '{1}' in library '{2}' requires a non-zero width.",
                            library.indexOf(referenceImage), referenceImage.name, AssetDatabase.GetAssetPath(library)));
                    }
                    catch (ARReferenceImage.MissingTextureException)
                    {
                        throw new BuildFailedException(string.Format("Reference image at index {0} named '{1}' in library '{2}' is missing a texture.",
                            library.indexOf(referenceImage), referenceImage.name, AssetDatabase.GetAssetPath(library)));
                    }
                    catch (ARReferenceImage.BadTexturePathException)
                    {
                        throw new BuildFailedException(string.Format("Could not resolve texture path for reference image at index {0} named '{1}' in library '{2}'.",
                            library.indexOf(referenceImage), referenceImage.name, AssetDatabase.GetAssetPath(library)));
                    }
                    catch (ARReferenceImage.LoadTextureException e)
                    {
                        throw new BuildFailedException(string.Format("Could not load texture at path {0} for reference image at index {1} named '{2}' in library '{3}'.",
                            e.path, library.indexOf(referenceImage), referenceImage.name, AssetDatabase.GetAssetPath(library)));
                    }
                    catch (ARReferenceImage.TextureNotExportableException)
                    {
                        throw new BuildFailedException(string.Format(
                            "Reference image at index {0} named '{1}' in library '{2}' could not be exported. " +
                            "ARKit can directly use a texture's source asset if it is a JPG or PNG. " +
                            "For all other formats, the texture must be exported to PNG, which requires the texture to be readable and uncompressed. " +
                            "Change the Texture Import Settings or use a JPG or PNG.",
                            library.indexOf(referenceImage), referenceImage.name, AssetDatabase.GetAssetPath(library)));
                    }
                    catch
                    {
                        Debug.LogErrorFormat("Failed to generate AR reference image at index {0} named '{1}' in library '{2}'.",
                            library.indexOf(referenceImage), referenceImage.name, AssetDatabase.GetAssetPath(library));

                        throw;
                    }
                }

                yield return (resourceGroup, library);
            }
        }

        // Fail the build if any of the reference images are invalid
        class Preprocessor : IPreprocessBuildWithReport, ARBuildProcessor.IPreprocessBuild
        {
            public int callbackOrder => 0;

            static void BuildAssets(List<XRReferenceImageLibrary> libraries, bool useACtool)
            {
                try
                {
                    var index = 0;
                    foreach (var (resourceGroup, library) in ResourceGroups(libraries))
                    {
                        index++;
                        EditorUtility.DisplayProgressBar(
                            $"Compiling {nameof(XRReferenceImageLibrary)} ({index} of {libraries.Count})",
                            $"{AssetDatabase.GetAssetPath(library)} ({library.count} image{(library.count == 1 ? "" : "s")})",
                            (float)index / libraries.Count);

                        // Do not change this name. It must match the native call to referenceImagesInGroupNamed.
                        resourceGroup.name = "ARReferenceImages";

                        // Convert the resource group to a 'car' (compiled asset catalog) file
                        library.SetDataForKey(VisionOSPackageInfo.identifier, resourceGroup.ToCar(useACtool));
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }

            void ARBuildProcessor.IPreprocessBuild.OnPreprocessBuild(PreprocessBuildEventArgs eventArgs)
            {
                // Build image libraries using ACTool because this is triggered by user code (likely for asset bundles); no chance to compile them in Xcode later
                BuildAssets(VisionOSBuildHelper.GetReferenceLibraries<XRReferenceImageLibrary, XRReferenceImage>(printWarnings: true), true);
            }

            void IPreprocessBuildWithReport.OnPreprocessBuild(BuildReport report)
            {
                if (!VisionOSEditorUtils.IsLoaderEnabled())
                    return;

                if (report.summary.platform != BuildTarget.VisionOS)
                    return;

                var useACTool = false;
                var visionOSSettings = VisionOSSettings.currentSettings;
                if (visionOSSettings != null)
                    useACTool = visionOSSettings.useACTool;

                BuildAssets(VisionOSBuildHelper.GetReferenceLibraries<XRReferenceImageLibrary, XRReferenceImage>(true, true), useACTool);
            }
        }

        class Postprocessor : IPostprocessBuildWithReport
        {
            public int callbackOrder => 0;

            public void OnPostprocessBuild(BuildReport report)
            {
                if (!VisionOSEditorUtils.IsLoaderEnabled())
                    return;

                if (report.summary.platform != BuildTarget.VisionOS)
                    return;

                var buildOutputPath = report.summary.outputPath;

                // Read in the PBXProject
                var project = new PBXProject();

                var pbxProjectPath = Path.Combine(buildOutputPath, "Unity-VisionOS.xcodeproj/project.pbxproj");

                project.ReadFromString(File.ReadAllText(pbxProjectPath));

                // Create a new asset catalog
                var assetCatalog = new XcodeAssetCatalog("ARReferenceImages");

                // Generate resource groups and add each one to the asset catalog
                var libraries = VisionOSBuildHelper.GetReferenceLibraries<XRReferenceImageLibrary, XRReferenceImage>(true);
                foreach (var (resourceGroup, library) in ResourceGroups(libraries))
                {
                    // Only add libraries where we don't already have the data
                    if (!library.dataStore.ContainsKey(VisionOSPackageInfo.identifier))
                    {
                        assetCatalog.AddResourceGroup(resourceGroup);
                    }
                }

                // Don't create empty asset catalogs
                if (assetCatalog.count == 0)
                    return;

                // Create the asset catalog on disk
                assetCatalog.WriteAndAddToPBXProject(project, buildOutputPath);

                // Write out the updated Xcode project file
                File.WriteAllText(pbxProjectPath, project.WriteToString());
            }
        }
    }
}
#endif
