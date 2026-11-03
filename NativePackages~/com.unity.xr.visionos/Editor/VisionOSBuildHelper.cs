#if UNITY_VISIONOS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace UnityEditor.XR.VisionOS
{
    static class VisionOSBuildHelper
    {
        static readonly Type k_ResourcesAPIInternalType = Type.GetType("UnityEngine.ResourcesAPIInternal, UnityEngine.CoreModule");
        static readonly MethodInfo k_GetAllPathsMethod = k_ResourcesAPIInternalType?.GetMethod("GetAllPaths");
        static readonly object[] k_GetAllPathsMethodArguments = { string.Empty };

        public static List<TLibrary> GetReferenceLibraries<TLibrary, TElement>(bool onlyIncludedInBuild = false, bool printWarnings = false) where TLibrary : UnityObject, IEnumerable<TElement>
        {
            var assets = AssetDatabase.FindAssets($"t:{typeof(TLibrary).Name}");
            var libraries = new List<TLibrary>();
            var dependencies = new HashSet<string>();
            if (onlyIncludedInBuild && k_GetAllPathsMethod == null)
            {
                Debug.LogError("VisionOSReferenceImageLibraryBuildProcessor failed to find internal method ResourcesAPIInternal.GetAllPaths. Falling " +
                    "back to legacy behavior. All XRReferenceImageLibrary assets will be included in this build.");

                onlyIncludedInBuild = false;
            }

            if (onlyIncludedInBuild)
            {
                foreach (var scene in EditorBuildSettings.scenes)
                {
                    if (!scene.enabled)
                        continue;

                    dependencies.UnionWith(AssetDatabase.GetDependencies(scene.path));
                }

                foreach (var asset in PlayerSettings.GetPreloadedAssets())
                {
                    if (asset == null)
                        continue;

                    var path = AssetDatabase.GetAssetPath(asset);
                    dependencies.UnionWith(AssetDatabase.GetDependencies(path));
                }

                var resourcePaths = (string[])k_GetAllPathsMethod.Invoke(null, k_GetAllPathsMethodArguments);
                foreach (var path in resourcePaths)
                {
                    dependencies.UnionWith(AssetDatabase.GetDependencies(path));
                }
            }

            foreach (var guid in assets)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (onlyIncludedInBuild && !dependencies.Contains(path))
                    continue;

                var library = AssetDatabase.LoadAssetAtPath<TLibrary>(path);
                if (!library.Any())
                {
                    if (printWarnings)
                        Debug.LogWarning($"VisionOSReferenceImageLibraryBuildProcessor skipping empty reference image library at path {path}");

                    continue;
                }

                libraries.Add(library);
            }

            return libraries;
        }
    }
}
#endif
