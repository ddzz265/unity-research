using System;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.XR.VisionOS;

namespace UnityEditor.XR.VisionOS
{
    /// <summary>
    /// Importer for `.referenceobject` files. See
    /// <a href="https://developer.apple.com/documentation/arkit/scanning_and_detecting_3d_objects">Scanning and Detecting 3D Objects</a>
    /// for instructions on how to generate these files.
    /// </summary>
    /// <seealso cref="VisionOSReferenceObjectEntry"/>
    [ScriptedImporter(0, "referenceobject")]
    internal class ARObjectImporter : ScriptedImporter
    {
        /// <summary>
        /// Invoked automatically when an `.referenceobject` file is imported.
        /// </summary>
        /// <param name="ctx">The context associated with the asset import.</param>
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var entry = ScriptableObject.CreateInstance<VisionOSReferenceObjectEntry>();

            entry.SetSourceAsset(ctx.assetPath);

            ctx.AddObjectToAsset("referenceobject", entry, null);
            ctx.SetMainObject(entry);
        }
    }
}
