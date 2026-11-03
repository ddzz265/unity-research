using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MusicForestAudioImport
{
    public static void Batch() {Import();EditorApplication.Exit(0);}
    [MenuItem("Serene Night/Import Music MP3 from D Codexput MusicInput")]
    public static void Import()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode before importing music.");
        string folder="D:/Codexput/MusicInput";
        var files=Directory.Exists(folder)?Directory.GetFiles(folder,"*.mp3"):Array.Empty<string>();
        if(files.Length!=1)throw new InvalidOperationException("Place exactly one MP3 downloaded from the specified Pixabay page in D:/Codexput/MusicInput.");
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        const string asset="Assets/SereneNightExperiment/Audio/FreeSoul_485738.mp3";
        File.Copy(files[0],asset,true);AssetDatabase.ImportAsset(asset);
        var importer=(AudioImporter)AssetImporter.GetAtPath(asset);var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.Streaming;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Serene_Night_Music.unity");
        var session=UnityEngine.Object.FindAnyObjectByType<MusicForestSession>();session.music.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(asset);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log($"Music imported: {files[0]} ({session.music.clip.length:F3} seconds). Session ends when this track finishes playing once at original speed.");
    }
}
