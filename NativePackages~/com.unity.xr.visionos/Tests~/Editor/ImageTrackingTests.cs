#if UNITY_VISIONOS
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor.XR.ARSubsystems;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace UnityEditor.XR.VisionOSTests
{
    class ImageTrackingTests
    {
        const string k_ImageLibraryGuid = "bd90f1713c87d4e888cb04e83f470fce";
        const string k_DataStorePropertyName = "m_DataStore";
        const string k_StoragePropertyName = "m_Storage";
        const string k_KeyPropertyName = "key";
        const string k_DataStoreExpectedKey = "com.unity.xr.visionos";

        [Test]
        public void ReferenceLibraryBuildPreprocess()
        {
            ARBuildProcessor.PreprocessBuild(BuildTarget.VisionOS);
            var imageLibrary = GetReferenceImageLibrary();
            var serializedObject = new SerializedObject(imageLibrary);
            var dataStoreProperty = serializedObject.FindProperty(k_DataStorePropertyName);
            var storageProperty = dataStoreProperty.FindPropertyRelative(k_StoragePropertyName);
            Assert.Greater(storageProperty.arraySize, 0);
            var firstElement = storageProperty.GetArrayElementAtIndex(0);
            var keyProperty = firstElement.FindPropertyRelative(k_KeyPropertyName);
            Assert.AreEqual(keyProperty.stringValue, k_DataStoreExpectedKey);
        }

        // Clear all data stores before and after tests
        [OneTimeSetUp]
        [OneTimeTearDown]
        public void ClearImageLibraryStorage()
        {
            var allLibraries = AssetDatabase
                .FindAssets($"t:{nameof(XRReferenceImageLibrary)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>);

            foreach (var library in allLibraries)
            {
                library.ClearDataStore();
            }

            AssetDatabase.SaveAssets();
        }

        static XRReferenceImageLibrary GetReferenceImageLibrary()
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(k_ImageLibraryGuid);
            Assert.IsTrue(AssetDatabase.AssetPathExists(assetPath));
            var asset = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(assetPath);
            Assert.IsNotNull(asset);
            return asset;
        }
    }
}
#endif
