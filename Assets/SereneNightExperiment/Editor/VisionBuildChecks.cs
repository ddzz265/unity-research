using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
#if UNITY_EDITOR_OSX
using UnityEditor.iOS.Xcode;
#endif

public class VisionBuildChecks : IPreprocessBuildWithReport
{
    public int callbackOrder=>-1000;
    public void OnPreprocessBuild(BuildReport report)
    {
        if(report.summary.platform!=BuildTarget.VisionOS)return;
        if(!File.Exists("mac-native-ready.txt"))throw new BuildFailedException("Run bash prepare-mac.command on your Mac mini before building for visionOS.");
    }
#if UNITY_EDITOR_OSX
    [PostProcessBuild(9999)]
    public static void AddPrivacy(BuildTarget target,string path)
    {
        if(target!=BuildTarget.VisionOS)return;
        var file=Path.Combine(path,"Info.plist");var plist=new PlistDocument();plist.ReadFromFile(file);
        plist.root.SetString("NSAccessoryTrackingUsageDescription","Tracks your PS VR2 Sense controllers so you can point at and answer questionnaire options.");
        plist.root.SetBoolean("UIFileSharingEnabled",true);plist.root.SetBoolean("LSSupportsOpeningDocumentsInPlace",true);
        plist.WriteToFile(file);
    }
#endif
}
