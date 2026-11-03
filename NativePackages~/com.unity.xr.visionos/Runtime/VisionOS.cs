using System;
using UnityEngine;

#if UNITY_VISIONOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace UnityEngine.XR.VisionOS
{
    /// <summary>
    /// Runtime scripting API for VisionOS.
    /// </summary>
    public static class VisionOS
    {
#if UNITY_VISIONOS && !UNITY_EDITOR
        const string k_LibraryName = "__Internal";

        /// <summary>
        /// Set the range of values used for depth sorting.
        /// These values should match Camera.nearClipPlane and Camera.farClipPlane
        /// </summary>
        /// <param name="near">The value for the near clipping plane.</param>
        /// <param name="far">The value for the far clipping plane.</param>
        [DllImport(k_LibraryName, EntryPoint = "SetDepthRange")]
        public static extern void SetDepthRange(float near, float far);

        /// <summary>
        /// Determine whether the immersive space for the app is ready.
        /// </summary>
        /// <returns><see langword="true"/> if the immersive space is ready. Otherwise, <see langword="false"/>.</returns>
        [DllImport(k_LibraryName, EntryPoint = "UnityVisionOS_IsImmersiveSpaceReady")]
        public static extern bool IsImmersiveSpaceReady();

        /// <summary>
        /// Get the native pointer to the `LayerRenderer` currently being used for rendering, or IntPtr.Zero (null) if none exists. A LayerRenderer
        /// is provided by the system for communicating with the XR compositor.
        /// </summary>
        /// <remarks>
        /// Note that the layer renderer is not immediately available on app start, or immediately after enabling Metal rendering.
        /// You may need to check if this is null for a few frames before a valid pointer is available.
        /// This API always returns IntPtr.Zero (null) in the Editor.
        /// </remarks>
        /// <returns>The `LayerRenderer` currently being used for rendering, or IntPtr.Zero (null) if none exists.</returns>
        [DllImport(k_LibraryName, EntryPoint = "UnityVisionOS_GetLayerRenderer")]
        public static extern IntPtr GetLayerRenderer();
#else
        /// <summary>
        /// Set the range of values used for depth sorting.
        /// These values should match Camera.nearClipPlane and Camera.farClipPlane
        /// </summary>
        /// <param name="near">The value for the near clipping plane.</param>
        /// <param name="far">The value for the far clipping plane.</param>
        public static void SetDepthRange(float near, float far) { }

        /// <summary>
        /// Determine whether the immersive space for the app is ready.
        /// Return true in the Editor for testing purposes.
        /// </summary>
        /// <returns><see langword="true"/> if the immersive space is ready (or in the Editor when targeting visionOS).
        /// Otherwise, <see langword="false"/>.</returns>
        public static bool IsImmersiveSpaceReady()
        {
#if UNITY_VISIONOS
            return true;
#else
            return false;
#endif
        }

        /// <summary>
        /// Get the native pointer to the `LayerRenderer` currently being used for rendering, or IntPtr.Zero (null) if none exists. A LayerRenderer
        /// is provided by the system for communicating with the XR compositor.
        /// </summary>
        /// <remarks>
        /// Note that the layer renderer is not immediately available on app start, or immediately after enabling Metal rendering.
        /// You may need to check if this is null for a few frames before a valid pointer is available.
        /// This API always returns IntPtr.Zero (null) in the Editor.
        /// </remarks>
        /// <returns>The `LayerRenderer` currently being used for rendering, or IntPtr.Zero (null) if none exists.</returns>
        public static IntPtr GetLayerRenderer()
        {
            return IntPtr.Zero;
        }
#endif

        /// <summary>
        /// Determine whether the app is running in the visionOS simulator.
        /// Treat the Editor targeting visionOS as running in simulator.
        /// </summary>
        /// <returns><see langword="true"/> if the app is running in the visionOS Simulator.
        /// Otherwise, <see langword="false"/>.</returns>
        public static bool IsSimulator()
        {
#if UNITY_VISIONOS
#if UNITY_EDITOR
            return true;
#else
            return Environment.GetEnvironmentVariable("SIMULATOR_ROOT") != null;
#endif
#else
            return false;
#endif
        }

        /// <summary>
        /// Called when an ARKit authorization like hand tracking or world tracking changes status. Some examples of when this occurs:
        /// - On app start when initially querying the authorization status, if it was already requested
        /// - After querying authorization status, if it was not requested and the user allowed or declined
        /// - When the user changes authorizations in Settings and returns to the app
        /// </summary>
        public static event Action<VisionOSAuthorizationEventArgs> AuthorizationChanged;

        /// <summary>
        /// Query the current authorization status for a given authorization type.
        /// </summary>
        /// <param name="type">The authorization type to query.</param>
        /// <returns>The status of the queried authorization type.</returns>
        public static VisionOSAuthorizationStatus QueryAuthorizationStatus(VisionOSAuthorizationType type)
        {
            return VisionOSSessionSubsystem.VisionOSSessionProvider.QueryAuthorizationStatus(type);
        }

        internal static void OnAuthorizationChanged(VisionOSAuthorizationType type, VisionOSAuthorizationStatus status)
        {
            AuthorizationChanged?.Invoke(new VisionOSAuthorizationEventArgs { type = type, status = status });
        }

        /// <summary>
        /// Set the minimum number of additional times the system displays each frame. See Apple's documentation for cp_layer_renderer_set_minimum_frame_repeat_count
        /// for more details.
        /// </summary>
        /// <remarks>
        /// If your takes longer than 11ms to render a frame (assuming a target of 90hz), the compositor will repeat the last available frame. If you know you
        /// will not be able to consistently render at 90hz, you should use this API, along with <see cref="Application.targetFrameRate"/> to consistently
        /// present frames at a lower rate, rather than a fluctuating rate which can cause issues with compositing and re-projection.
        /// Each repeated frame effectively increases the amount you will divide by 90 to decide on the appropriate target frame rate. For example, a value of 1
        /// will show each frame two times, for a target frame rate of 45hz. A value of 2 will show each frame three times, for a target frame rate of 33hz. You
        /// can use <see cref="VisionOSRuntimeSettings.GetTargetFrameRateForRepeatCount"/> to get the correct target frame rate value for a given repeat count.
        /// Adjusting this setting will inform the system that it should allow for more time between frames when calculating the optimal time to sample input
        /// and start rendering the next frame. If you do not use <see cref="Application.targetFrameRate"/>, the XR plugin will still block Unity for enough
        /// time so that it does not out-pace the target frame rate, but your content may behave differently due to the fact that we are waiting at a different
        /// point in the player loop. In general, this API should be avoided until you see problems with animations or moving objects, and you should experiment
        /// with different settings until you see them resolved. The ideal outcome would be to optimize the scene so that it can consistently render at 90hz,
        /// but this is not always possible.
        /// You may also want to reduce the target frame rate (and increase frame repeat count) to save battery life, just like you would on other mobile platforms.
        /// This API requires a LayerRenderer, which is only available when using Metal Rendering with CompositorServices (Apple's XR compositor). It does not
        /// apply to RealityKit rendering.
        /// </remarks>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when <paramref name="repeatCount"/> is less than 0.</exception>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="layerRenderer"/> is null.</exception>
        /// <param name="layerRenderer">The layer renderer that is currently in use <see cref="GetLayerRenderer"/>.</param>
        /// <param name="repeatCount">The desired repeat count.</param>
        public static void SetMinimumFrameRepeatCount(IntPtr layerRenderer, int repeatCount)
        {
            if (repeatCount < 0)
                throw new ArgumentOutOfRangeException(nameof(repeatCount));

            if (layerRenderer == IntPtr.Zero)
                throw new ArgumentNullException(nameof(layerRenderer));

            NativeApi.LayerRenderer.cp_layer_renderer_set_minimum_frame_repeat_count(layerRenderer, repeatCount);
        }
    }
}
