---
uid: vosxr-changelog
---
# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [3.2.2] - 2026-07-30

## [3.2.1] - 2026-07-22

Version incremented to synchronize with other PolySpatial and visionOS package versions.

## [3.2.0] - 2026-07-15

### Added
- Added a project validation rule that flags and prevents MSAA sample count set to 8x, as the configuration is not supported on visionOS.
- Added a project validation rule requiring a minimum Target visionOS Version of 2.0 for Metal, RealityKit, and Hybrid apps.

### Fixed
- Fixed the visionOS build to respect the Target Minimum visionOS Version set in Player Settings.

## [3.1.0] - 2026-02-20

### Added
- Added MSAA and Post Processing AA controls to Metal samples to make testing AA modes easier.
- Added NativeApi.Utilities.ConvertExistingDataToNativeArrayWithSafetyHandle to create safety handles when converting native pointers to NativeArrays.

## [3.0.5] - 2026-01-08

### Fixed
- Fixed a NPE in VisionOSImageTrackingProvider.GetTrackedImage in cases where image tracking would get enabled/disabled multiple times.

## [3.0.3-pre.4] - 2025-11-19

### Fixed
- Fixed a crash in the visionOS player when disabling hand tracking.

## [3.0.3-pre.1] - 2025-10-13

## [3.0.2] - 2025-10-06

## [3.0.1] - 2025-10-03

## [3.0.0] - 2025-09-30

## [2.4.3] - 2025-09-29

## [2.4.2] - 2025-09-23

### Changed
- Broadened the scope of HDR-related Project Validation rules to show as warnings even if HDR is not enabled in the URP settings asset. Because this setting can be toggled at runtime, it was possible to miss configuration issues if HDR is not enabled in the Editor but scripts (like the `HDR Toggle` button in the sample) enable it at runtime. These validations become errors when HDR is enabled.

### Fixed
- Disable `HDR Toggle` button in URP sample scene if HDR is not set up properly in Player Settings. This prevents a crash that can occur when trying to enable HDR in builds with incorrect settings.

## [2.3.1] - 2025-05-12

### Added
- Added a HighQualityRecordingMode toggle to help users record in high quality. Having the toggle enabled will disable rendering in the right eye and also disable foveated rendering.

## [2.2.4] - 2025-03-05

### Fixed
- Fixed a null reference exception that can occur in `VisionOSPlayModeInput.Awake` when entering play mode in a project with PolySpatial packages installed, but the AppMode set to Metal.
- Fixed log spam in Metal and Hybrid builds about querying hand anchors and presenting drawables. These warnings may show up as a one-off from time to time, but no longer spam continuously the way they did in some situations.
- Fix compile errors on tvOS.

## [2.1.2] - 2024-11-26

### Added
- Consolidated the documentation for the Apple visionOS XR Plug-in settings into a single page located at `visionOS Platform Overview > visionOS Settings`.
- Added Object Tracking support.
- Added `Use AC Tool` setting under `Project Settings > XR Plug-in Management > Apple visionOS` to make compiling reference image libraries with actool optional. This step can be done later in the Xcode project, and can fail sometimes, interrupting Build and Run.

### Changed
- Quit button in sample scene is marked non-interactable in play mode to make it clear that it doesn't have any effect (`Application.Quit` doesn't do anything in play mode).
- Skip processing image marker libraries that are not included in the build.

### Deprecated

### Removed

### Fixed
- Fixed an issue where requesting a volume size that was too large or too small when connected to PlayToDevice could result in a loop of WindowStateChanged event invocations.
- Fixed an issue in `MetalSampleURP/Shaders/UnlitTransparentColor.shader` where it was writing alpha values to the framebuffer, causing pass-through to bleed through AR planes in the URP sample scene.

### Security

## [2.0.4] - 2024-09-25

### Added
- Added `metalImmersiveOverlays` and `realityKitImmersiveOverlays` settings to `VisionOSSettings`, which allow hiding the visionOS hand gesture menus in immersive spaces.

### Changed
- Update minimum Editor version to 6000.0.22f1.

### Deprecated

### Removed

### Fixed
- Fixed Windows long path error when processing ARReferenceImage's build postprocessor.
- Called EditorUtility.ClearProgressBar(); when finished processing visionOS reference image library assets to clear progress bar.
- Fixed hand tracking lag by returning CACurrentMediaTime instead of 0 from `NativeApi.HandTracking.GetLatestHandTrackingTiming` when no LayerRenderer is active (for example when using PolySpatial).
- Fixed potential NullReferenceExceptions in build processors.
- Fixed a missing script on the AR Controls prefab in the built-in sample.
- Fixed an issue where the initial immersion style in Info.plist did not match the immersion style chosen in settings.

### Security

## [2.0.0-pre.11] - 2024-08-12

### Added
- Exposed `SkipPresentToMainScreen` setting in `VisionOSSettings`. This will be enabled by default, along with a Project Validation rule to encourage its use, on Unity 6000.0.11f1, which includes fixes for this mode. This version of Unity fixes frame pacing issues for visionOS apps using Metal rendering, along with a GPU resource leak related to this setting.
- Added `VisionOS.SetMinimumFrameRepeatCount` API to enable content that can't hit 90hz to ask for more time to render each frame.
- Added Initial Target Frame Rate runtime setting which sets `Application.targetFrameRate` and `VisionOS.SetMinimumFrameRepeatCount` at start-up based on the selected option.
- Added more details to Metal App Mode documentation page, including how to set up pass-through in Metal-based apps.
- Added support for HDR rendering for Metal and Hybrid app modes. This includes a variety of fixes, project validation rules, and updates to the package samples. HDR rendering requires a minimum Unity version of 6000.0.18f1.

### Changed
- Use new `cp_frame_binocular_frustum_matrix` API to query a proper culling matrix from the system. 2.0.0-pre.9 used a hard-coded 150-degree FOV to ensure nothing was culled prematurely, but may have regressed performance due to more objects being considered "in-view" than was necessary.

### Deprecated

### Removed

### Fixed
- Wrap all MonoPInvokeCallback methods in try/catch to avoid potential crashes in player builds.
- Fixed potential exceptions in OnAuthorizationChanged that can happen if the original call was made off the main thread.
- Fixed an issue where the app can crash if the AR Session is restarted when rendering with Metal. This may also fix other Metal rendering crashes accompanied by error logs that start with `BUG IN CLIENT:`.
- Fixed an issue where visionOS player builds can crash when trying to collect crash logs.

### Security

## [2.0.0-pre.9] - 2024-07-24

### Added
- Added `VisionOS.AuthorizationChanged` and `VisionOS.QueryAuthorizationStatus` APIs to enable user code to query and respond to changes in AR authorizations like Hand Tracking and World Sensing.

### Changed
- The visionOS ARKit plugin now uses a separate AR Session for each data provider. This means that individual systems like Hand Tacking can be started and stopped without interrupting others, like Head Tracking.

### Deprecated

### Removed

### Fixed
- Fixed a crash that can happen when unloading a scene after tracking an image with ARKit image tracking.
- Fixed issues in the InputSystem UI sample scene.

### Security

## [2.0.0-pre.3] - 2024-04-22

### Added
- VisionOSTrackingState for hand joints to provide extra data about whether or not the joint is in view.

### Changed
- Updated VR samples to support two-handed interactions.
- Enabled shadows in URP VR sample.
- Tracking origin mode now correctly reports to XROrigin, which will vertically reposition content. The CameraOffset transform which is usually a child of XROrigin will now have it's local Y position set to 0 for all requested tracking origin modes. Some projects may need to be updated to account for this change. **Note: XR Interaction Toolkit samples and GameObject menu presets will behave differently than they did in prior versions.**
- Re-organize Swift app trampoline for VR mode to be more modular and share code with MR mode. This should make it easier to extend and re-configure the Xcode project to implement custom SwiftUI solutions.
- Add a floor object to VR sample scene and tweak XR Origin and interactable transforms to be in a more sensible location.
- Always report ARKit hand joint tracking state as `XRHandJointTrackingState.Pose`, as long as part of the hand is tracked. This exposes estimated joint poses that were previously unavailable when using the result of `ar_skeleton_joint_is_tracked` to set the joint tracking state. Estimated poses are provided even when the joint is not in view, and `ar_skeleton_joint_is_tracked` returns `false`.

### Deprecated

### Removed

### Fixed
- Fix compilation issues when targeting tvOS.
- Fix compilation issues when making a non-visionOS build while visionOS is the active build target.
- Fixed random compilation issue when targeting visionOS, removed LaunchScreen-iPhone.storyboard from xcode project output.
- Fix VR frame timing issues.

### Security

## [1.1.4] - 2024-02-26

### Added

### Changed

### Deprecated

### Removed

### Fixed

### Security

## [1.1.3] - 2024-02-22

### Added

### Changed

### Deprecated

### Removed

### Fixed

### Security

## [1.1.2] - 2024-02-21

### Added

### Changed

### Deprecated

### Removed

### Fixed

### Security

## [1.1.1] - 2024-02-15

### Added
- Selected validation profiles are now set automatically depending on the App mode dropdown. Users targeting multiple modes can still manually override the selected validation profiles.
- Added extension method `GetVisionOSHandJoint` to `XRHand` for platform-specific data.
- Added project validation rule and settings error telling users they need the `Apple visionOS` XR Loader enabled for VR builds to work.

### Changed
- Cleaned up trampoline code.
- `VisionOSPlayModeInput` reports position/rotation data in the camera's local space to more accurately reflect input on the device.
- Updated the Project Validation rules category to include a small description.

### Deprecated

### Removed
- Removed unused `VisionOSAppController` that was added to Xcode projects for VR builds.

### Fixed
- Fixed an issue where VR apps would crash when the user opened the OS menu or Control Center (requires Unity 2022.3.20f1).
- Fixed an issue where VR apps would present frames in the background, resulting in logs that say `Insufficient Permission (to submit GPU work from background)` (requires Unity 2022.3.20f1).
- Updated samples to properly handle scenes with an XR Origin that is moved, rotated, or scaled (i.e. no longer located at 0, 0, 0).
- Fixed launch crash on device when you do an incremental build over a folder that previously held a build with Target SDK set to Simulator.
- Fixed culling issue on device where objects were culled aggressively on the periphery.
- Fixed an issue where Xcode projects would fail to build if they were moved from their original location. VisionOSSettings.swift now uses a project-relative path instead of an absolute path.

### Security

## [1.0.3] - 2024-01-20

### Added
- Added a Project Validation rule to check for UniversalRenderData with `Depth Texture Mode` set to anything other than `After Opaques`, which will cause rendering glitches when no opaque objects are visible.
- Added a workaround to build the post processor for `ARM64 branch out of range` error which can occur when building in Xcode.
- Added `interactionRayRotation` control which exposes a gaze ray which can be used for draggable UI elements. It begins with a rotation pointing in the direction of the gaze ray, and follows a position which is offset by the change in `devicePosition`. In practice, users can gaze at a slider, pinch their fingers and move their hand right and left to drag it side-to-side.
- Added a UI canvas to `Main` sample scene, configured to use the `XRUIInputModule` from `com.unity.xr.interaction.toolkit`.
- Added an `InputSystem UI` scene configured to use the `InputSystemUIInputModule` from `com.unity.inputsystem`.
- Added an affordance to the Apple visionOS settings UI to install PolySpatial packages if the user switches AppMode to Mixed Reality or clicks the Install Packages button visible when AppMode is set to Mixed Reality.
- Add Windowed AppMode.

### Changed
- Updated Xcode version used to build native libraries to 15.2 (15C500b)
- Renamed `devicePosition` and `deviceRotation` input controls to `inputDevicePosition` and `inputDeviceRotation`.

### Deprecated

### Removed

### Fixed
- Use the correct deployment target version `1.0` when invoking `actool` to compile image marker libraries.
- Fixed an issue in samples where the world anchor that is placed by user input used an empty GameObject instead of a visible prefab.
- Fixed the `HandVisualizer` script in package samples to properly disable joint visual GameObjects when the joint is not tracked.

### Security

## [0.7.1] - 2023-12-13

### Added
- Added a step to the build pre-processor which disables splash screen on visionOS player builds.
- Enabled foveated rendering for VR builds on Unity 2022.3.16f1 and above.
- Added extension method `TryGetVisionOSRotation` to `XRHandJoint` when using the `UnityEngine.XR.VisionOS` namespace. If you depended on the rotations reported before this version, use this `TryGetVisionOSRotaiton` instead of the rotation reported from `XRHandJoint.TryGetPose`.

### Changed
- Changed the platforms behavior to report rotations of hand joints through `XRHandSubsystem` that align more closely with OpenXR's rotations. If you depended on the previous reporting of rotations, use the rotation reported by `TryGetVisionOSRotation`, a new extension method to `XRHandJoint`.
- All packages now require 2022.3.15f1 and later (rather than 2022.3.11fa and later) to pick up fixes for various memory leaks made in 15f1.

### Deprecated

### Removed
- Support for Unity versions earlier than 2022.3.11f1.
- Removed gray "Loading..." window in VR builds. VR apps now launch directly into the immersive space.

### Fixed
- Fixed a linker error in Xcode when building the visionOS player with App Mode set to VR, but the visionOS loader is not enabled.
- Fixed a memory leak in `VisionOSHandProvider`.
- Fixed a memory leak caused by using particle systems in VR mode.
- Implemented lifecycle management. Unity now suspends and resumes properly when the home menu is brought forward.
- Fixed an issue where closing the gray "Loading..." window would mute audio.
- Fixed an issue where spatial audio would use the gray "Loading..." window as its source location.
- XRHMD Input device now properly reports HMD input. This enables existing VR projects and templates to properly track head movement in visionOS VR builds.

### Security

## [0.6.3] - 2023-11-28

### Added

### Changed
- Changed license check modal option from "See Pricing" to "Learn about a 30-day trial".

### Deprecated

### Removed

### Fixed

### Security

## [0.6.2] - 2023-11-13

### Added

### Changed

### Deprecated

### Removed

### Fixed

### Security

## [0.6.1] - 2023-11-09

### Added

### Changed

### Deprecated

### Removed

### Fixed

### Security

## [0.6.0] - 2023-11-08

### Added
- Added additional input controls on `VisionOSSpatialPointerDevice` which are needed to drive an XR Ray Interactor.
- Added VR samples for both Built-in and Universal Render Pipelines.

### Changed

### Deprecated

### Removed

### Fixed
- Fixed compile errors when the project has `com.unity.render-pipelines.core` but not `com.unity.render-pipelines.universal`.
- Fixed issue with over releasing material references for canvas items.

### Security

## [0.5.0] - 2023-10-26

### Added
- `VisionOSSpatialPointerDevice` for pinch/gaze input support in VR mode.

### Changed

### Deprecated

### Removed

### Fixed

### Security

## [0.4.3] - 2023-10-13

## [0.4.2] - 2023-10-12

### Fixed
- Fixed an issue where VR builds would only render to the left eye in device builds when using the built-in pipeline.

## [0.4.1] - 2023-10-06

### Added
- PolySpatial now supports Xcode 15.1 beta 1 and visionOS 1.0 beta 4
- Project Validation rules for Linear Color Space, ARSession + ARInputManager components, and cameras generating depth textures inside of the VisionOS XR package

### Removed

- Removed `VisionOSSettings.renderMode`, `VisionOSSettings.deviceTarget`, and related `visionos_config.h` file that was generated during builds. The XR plugin will automatically switch between single-pass and multi-pass rendering depending on whether the app was built for the visionOS simulator or a device.

### Fixed

- Fixed an issue where VR builds would only render to the left eye in device builds when using the built-in pipeline.

## [0.3.3] - 2023-09-28

### Changed

- Revert changes that were mistakenly included in 0.3.2

## [0.3.2] - 2023-09-27

### Changed

- Use renamed `ar_skeleton_get_anchor_from_joint_transform_for_joint` API. This fixes an issue where builds are rejected on TestFlight for using deprecated `ar_skeleton_get_skeleton_root_transform_for_joint` API.

## [0.3.1] - 2023-09-13

### Fixed

- Fixed linker errors in Xcode when building without visionOS loader enabled.

## [0.3.0] - 2023-09-12

### Added

- VisionOSSessionSubsystem now returns a structure including the native session pointer from the `nativePtr` property.

### Changed

- Xcode beta 8 and visionOS beta 3 compatibility.
- Static libraries were rebuilt with Xcode Version 15.0 beta 8 (15A5229m).

### Fixed

- Fixed an issue where plane detection would be disabled if meshing was not enabled.

## [0.2.0] - 2023-08-21

### Changed

- Xcode beta 5 and visionOS beta 2 compatibility
- Static libraries were rebuilt with Xcode Version 15.0 beta 2 (15A5161b).

### Fixed

- Fixed issues with AR mesh alignment.
- Fixed issues with AR anchor position.
- Fixed issues with AR authorization and session startup.
- Fixed an issue where Plane alignment values would not match the values expected by AR Foundation.

## [0.1.3] - 2023-07-19

### This is the first release of *Unity Package Apple visionOS XR Plugin*.

*Provides XR support for visionOS*
