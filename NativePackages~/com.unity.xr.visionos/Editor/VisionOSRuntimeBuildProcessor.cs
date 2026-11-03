using System;
using System.IO;
using System.Linq;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.VisionOS;

namespace UnityEditor.XR.VisionOS
{
    class VisionOSRuntimeBuildProcessor : XRBuildHelper<VisionOSRuntimeSettings>
    {
        public override string BuildSettingsKey => Constants.k_SettingsKey;

        public override UnityEngine.Object SettingsForBuildTargetGroup(BuildTargetGroup buildTargetGroup)
        {
            if (buildTargetGroup != BuildTargetGroup.VisionOS)
                return null;

            EditorBuildSettings.TryGetConfigObject(Constants.k_SettingsKey, out VisionOSSettings settings);
            if (settings == null)
                return null;

            return settings.GetOrCreateRuntimeSettings();
        }
    }
}
