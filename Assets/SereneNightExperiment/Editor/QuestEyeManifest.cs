using System;
using System.IO;
using System.Xml;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;

// Add permission only to generated build output, preserving other manifest entries.
public sealed class QuestEyeManifest : OpenXRFeatureBuildHooks
{
    public override Type featureType=>typeof(EyeGazeInteraction);
    public override int callbackOrder=>100;
    protected override void OnPreprocessBuildExt(BuildReport report) {}
    protected override void OnPostprocessBuildExt(BuildReport report) {}
    protected override void OnPostGenerateGradleAndroidProjectExt(string path)
    {
        string file=Path.Combine(path,"src/main/AndroidManifest.xml");
        var doc=new XmlDocument();doc.Load(file);
        Add(doc,"uses-permission","com.oculus.permission.EYE_TRACKING",false);
        Add(doc,"uses-permission","android.permission.INTERNET",false);
        Add(doc,"uses-feature","oculus.software.eye_tracking",true);
        doc.Save(file);
    }
    static void Add(XmlDocument doc,string tag,string name,bool optional)
    {
        const string ns="http://schemas.android.com/apk/res/android";
        foreach(XmlElement item in doc.DocumentElement.GetElementsByTagName(tag))
            if(item.GetAttribute("name",ns)==name)return;
        var element=doc.CreateElement(tag);element.SetAttribute("name",ns,name);
        if(optional)element.SetAttribute("required",ns,"false");
        doc.DocumentElement.AppendChild(element);
    }
}
