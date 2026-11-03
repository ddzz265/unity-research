using System.Collections;
using System.IO;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.VisionOS;

namespace UnityEditor.XR.VisionOSTests
{
    class ObjectTrackingTests
    {
        // Path to the .referenceobject file within the package.
        const string k_TestFilePath = "Packages/com.unity.xr.visionos/Tests/TestResources/UnityCube.referenceobject";

        [UnityTest]
        public IEnumerator CreateVisionOSReferenceObjectEntry()
        {
            Assert.IsTrue(File.Exists(k_TestFilePath));

            // Load the binary contents of the .referenceobject file.
            var fileData = File.ReadAllBytes(k_TestFilePath);

            // Ensure the file was loaded successfully.
            Assert.IsNotNull(fileData);
            Assert.IsTrue(fileData.Length > 0);

            // Ensure a valid test file path was found
            Assert.False(string.IsNullOrEmpty(k_TestFilePath), $"File not found: {k_TestFilePath}");

            // Create a NativeArray and NativeSlice from the loaded data.
            var nativeArray = new NativeArray<byte>(fileData, Allocator.Temp);
            var dataSlice = new NativeSlice<byte>(nativeArray);

            // Use the data slice to create the VisionOSReferenceObjectEntry.
            var entry = VisionOSReferenceObjectEntry.Create(dataSlice);

            // Clean up the NativeArray after use.
            nativeArray.Dispose();

            // Validate the created entry.
            Assert.IsNotNull(entry);

            yield return null;
        }
    }
}
