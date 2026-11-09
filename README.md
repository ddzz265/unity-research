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
3. Updated third audio, full file: approximately 10 min (600.004 sec).
4. Updated fourth audio, full file: approximately 15 min 5.253 sec.
5. Updated fifth audio: source **00:10–09:50**, exactly **9 min 40 sec**.
6. Updated sixth audio, full file: approximately 14 min 47.165 sec.

The mindfulness route follows the selected clip's actual sample duration and DSP playback progress. Only group E uses settling feedback (9 UI units, 0.45 sec), without a central selected dot; A/B/C/D/F retain ordinary selection marks without sinking. The questionnaire is centred on the current gaze when shown, with head roll removed and a one-time pre-render pose correction. Camera motion blur in the mindfulness forest is disabled to remove its rotation-induced smearing. Other timing triggers and answer logging are preserved.

See [2026-11-09 scene, interaction, performance and audio updates](docs/2026-11-09-update.md) for details and experimental-variable changes. These changes have passed conditional C# compilation and data/audio checks; Android shader compilation, APK building and Quest Pro visual/performance validation remain pending.