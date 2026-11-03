using System;
using Unity.Collections;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
#endif

namespace UnityEngine.XR.VisionOS
{
    /// <summary>
    /// Represents an VisionOS-specific reference object for participation in an
    /// <c>XRReferenceObjectLibrary</c>.
    /// </summary>
    /// <remarks>
    /// The actual data used at runtime is packaged into the Xcode project
    /// in an asset catalog called <c>ARReferenceObjects.xcassets</c>. It should
    /// exist on disk in your project as an <c>.referenceobject</c> file.
    /// See <a href="https://developer.apple.com/documentation/arkit/scanning_and_detecting_3d_objects">Scanning and Detecting 3D Objects</a>
    /// for instructions on how to generate these files.
    /// </remarks>
    /// <seealso cref="XRReferenceObject"/>
    /// <seealso cref="XRReferenceObjectLibrary"/>
    sealed class VisionOSReferenceObjectEntry : XRReferenceObjectEntry, ISerializationCallbackReceiver
    {
        public VisionOSReferenceObject GetVisionOSReferenceObject(XRReferenceObject referenceObject)
        {
            return m_VisionOSReferenceObject;
        }

        public static VisionOSReferenceObjectEntry Create(NativeSlice<byte> data)
        {
            var referenceObject = new VisionOSReferenceObject(data);

            var entry = CreateInstance<VisionOSReferenceObjectEntry>();
            entry.m_VisionOSReferenceObject = referenceObject;

            return entry;
        }

        /// <summary>
        /// Invoked when a new [XRReferenceObject](xref:UnityEngine.XR.ARSubsystems.XRReferenceObject) is added to an
        /// [XRReferenceObjectLibrary](xref:UnityEngine.XR.ARSubsystems.XRReferenceObjectLibrary).
        /// </summary>
        /// <param name="library">The library to which the reference object is being added.</param>
        /// <param name="xrReferenceObject">The reference object being added to the <paramref name="library"/>.</param>
        protected override void OnAddToLibrary(XRReferenceObjectLibrary library, XRReferenceObject xrReferenceObject)
        {
            base.OnAddToLibrary(library, xrReferenceObject);

            var instance = XRGeneralSettings.Instance;
            if (instance == null)
                return;

            var manager = instance.Manager;
            if (manager == null)
                return;

            var loader = manager.activeLoader;
            if (loader == null)
                return;

            if (loader.GetLoadedSubsystem<XRObjectTrackingSubsystem>() is VisionOSObjectTrackingSubsystem subsystem)
            {
                subsystem.AddReferenceObject(library, GetVisionOSReferenceObject(xrReferenceObject));
            }
        }

        void OnDestroy() => m_VisionOSReferenceObject.Dispose();

        /// <summary>
        /// Invoked just before serialization.
        /// </summary>
        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        /// <summary>
        /// Invoked just after deserialization.
        /// </summary>
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            m_VisionOSReferenceObject = new VisionOSReferenceObject(m_ReferenceObjectBytes);
        }


#if UNITY_EDITOR
        static bool s_ObjectBytesShouldBeSet;

        // Called by ARKitReferenceObjectLibraryBuildProcessor
        internal static void SetObjectBytesEnabled(bool value)
        {
            s_ObjectBytesShouldBeSet = value;

            // Iterate over all reference object libraries, reimporting if necessary
            var entries = AssetDatabase.FindAssets($"t:{nameof(XRReferenceObjectLibrary)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<XRReferenceObjectLibrary>)
                .SelectMany(library => library)
                .Select(referenceObject => referenceObject.FindEntry<VisionOSReferenceObjectEntry>())
                .Where(entry => entry != null);

            foreach (var entry in entries)
            {
                entry.ReimportIfNecessary();
            }
        }

        internal void SetSourceAsset(string path) => m_ReferenceObjectBytes = s_ObjectBytesShouldBeSet
            ? File.ReadAllBytes(path)
            : Array.Empty<byte>();

        void ReimportIfNecessary()
        {
            if (s_ObjectBytesShouldBeSet && m_ReferenceObjectBytes?.Length == 0 ||
                !s_ObjectBytesShouldBeSet && m_ReferenceObjectBytes?.Length > 0)
            {
                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(this));
            }
        }
#endif

#pragma warning disable CS0649
        [SerializeField]
        internal byte[] m_ReferenceObjectBytes;
#pragma warning restore CS0649

        VisionOSReferenceObject m_VisionOSReferenceObject;
    }
}
