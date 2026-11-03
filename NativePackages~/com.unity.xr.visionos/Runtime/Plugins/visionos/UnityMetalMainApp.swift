import CompositorServices
import SwiftUI
import UnityFramework

@main
struct UnityMetalMain: App {
    @UIApplicationDelegateAdaptor
    var swiftUIdelegate: UnitySwiftUIAppDelegate

    var body: some Scene {
        unityVisionOSCompositorSpace
    }
}
