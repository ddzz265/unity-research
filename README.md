# SereneNight_Test

Unity project for the Serene Night experiment.

## Open on another computer

1. Sign in to GitHub if you plan to push changes to this repository.
2. Clone this repository with GitHub Desktop (File > Clone Repository), or run `git clone https://github.com/ddzz265/unity-research.git`.
3. Install Unity Editor **6000.5.10f1** through Unity Hub, plus the modules required for your target platform.
4. In Unity Hub, choose Add project from disk and select the cloned repository folder.
5. Allow Unity to download registry dependencies, import assets and rebuild Library.
6. Open `Assets/Scenes/Serene_Night_Experiment.unity`.

Assets, Packages, ProjectSettings, NativePackages~ and ThirdParty~ are included. Library, personal settings, build output and local questionnaire results (Responses) are excluded.

For Mac/visionOS native plugin preparation, inspect the included `prepare-mac.command` and the third-party README files. That step requires the appropriate Xcode and visionOS SDK.

## Quest Pro standalone setup

The Android controller/head-tracking fixes and rendering changes are included in this revision. On the build computer:

1. Install Android Build Support (SDK/NDK and OpenJDK) for Unity **6000.5.10f1** and let Package Manager resolve the OpenXR dependencies.
2. Run **Serene Night > Configure Quest Pro (Android)**.
3. Switch Build Profiles to Android, run XR Project Validation, and select the intended experiment scene before building.

See [Quest Pro setup, changes and device validation](docs/QuestPro-setup.md) for details. Runtime C# compilation was checked for desktop and Android conditional paths; full Android import/build and Quest Pro performance measurements remain to be done on the build computer/device. D/E/F groups still require a real inhalation sensor; controller input does not simulate inhalation.

## Sync changes

Before starting work, Fetch/Pull in GitHub Desktop. After saving your changes in Unity, Commit and Push in GitHub Desktop. Avoid editing the same scene on both computers at the same time.

This repository is an upload snapshot from the original project. Continue development in a cloned repository or in this repository folder to make future Git commits track your work.

Ignore rules are based on https://github.com/github/gitignore/blob/main/Unity.gitignore.


## Six mindfulness experiences

Open `Assets/Scenes/Serene_Night_Experiment.unity`, select **Experiment Group Controller**, and set **第几次正念体验** to 1–6 before Play. The default is experience **6**. Select each experience manually; the clips do not play consecutively.

1. Original Mandarin mindfulness audio (preserved).
2. Breath awareness: source 00:10–09:35; 9 min 25 sec.
3. Full third audio; approximately 10 min 16.79 sec.
4. Full fourth audio; approximately 14 min 47.16 sec.
5. Fifth audio: source 00:09–18:09; 18 min.
6. Sixth audio: source 00:05–13:17; 13 min 12 sec.

The mindfulness route follows the selected clip's playback duration. A–F experiment groups and questionnaire rules remain unchanged.