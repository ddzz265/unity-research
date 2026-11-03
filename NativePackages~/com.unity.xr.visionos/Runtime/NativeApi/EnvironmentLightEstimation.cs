using System;
using System.Runtime.InteropServices;
using UnityEngine.XR.ARSubsystems;

namespace UnityEngine.XR.VisionOS
{
    /// <summary>
    /// Native API wrappers for environment light estimation.
    /// Signatures and types should match environment_light_estimation.h.
    /// </summary>
    static partial class NativeApi
    {
        internal static class EnvironmentLightEstimation
        {
            static readonly IntPtr k_TextureDescriptorPointer;

            static EnvironmentLightEstimation()
            {
                k_TextureDescriptorPointer = Marshal.AllocHGlobal(Marshal.SizeOf<XRTextureDescriptor>());
            }

            /// <summary>
            /// Function to be called when there are updates to environment probe anchors.
            /// </summary>
            /// <param name="context">The application-defined context.</param>
            /// <param name="added_anchors">Collection of anchors that are added.</param>
            /// <param name="updated_anchors">Collection of anchors that are updated.</param>
            /// <param name="removed_anchors">Collection of anchors that are removed.</param>
            public delegate void AR_Environment_Light_Estimation_Update_Handler_Function(IntPtr context, IntPtr added_anchors, IntPtr updated_anchors, IntPtr removed_anchors);

            /// <summary>
            /// Function for enumerating a collection of environment probe anchors.
            /// </summary>
            /// <param name="context">The application-defined context.</param>
            /// <param name="environment_probe_anchor">The environment probe anchor.</param>
            /// <returns>Return `true` to continue enumerating, or `false` to stop enumerating.</returns>
            public delegate bool AR_Environment_Probe_Anchors_Enumerator_Function(IntPtr context, IntPtr environment_probe_anchor);

            /// <summary>
            /// Create an environment light estimation configuration.
            /// </summary>
            /// <remarks>
            /// This type supports ARC. In non-ARC files, use `ar_retain()` and `ar_release()` to retain and release the object.
            /// </remarks>
            /// <returns>An instance of `ar_environment_light_estimation_configuration_t`.</returns>
            [DllImport(Constants.LibraryName, EntryPoint = "ar_environment_light_estimation_configuration_create")]
            public static extern IntPtr ar_environment_light_estimation_configuration_create();

            /// <summary>
            /// Create an environment light estimation provider.
            /// </summary>
            /// <remarks>
            /// This type supports ARC. In non-ARC files, use `ar_retain()` and `ar_release()` to retain and release the object.
            /// </remarks>
            /// <param name="environment_light_estimation_configuration">An environment light estimation configuration.</param>
            /// <returns>An instance of `ar_environment_light_estimation_provider_t`.</returns>
            [DllImport(Constants.LibraryName, EntryPoint = "ar_environment_light_estimation_provider_create")]
            public static extern IntPtr ar_environment_light_estimation_provider_create(IntPtr environment_light_estimation_configuration);

            /// <summary>
            /// Set the function for receiving environment light estimation updates.
            /// Note: Setting this function will override the handler set using `ar_environment_light_estimation_provider_set_probe_update_handler`.
            /// </summary>
            /// <param name="environment_light_estimation_provider">Environment light estimation provider.</param>
            /// <param name="environment_light_estimation_anchor_updates_queue">Queue on which the function will be called. Passing NULL will default to the main queue.</param>
            /// <param name="context">The application-defined context parameter to pass to the function.</param>
            /// <param name="environment_light_estimation_update_handler_function">The function to be called when new data arrives.</param>
            [DllImport(Constants.LibraryName,
                EntryPoint = "ar_environment_light_estimation_provider_set_update_handler_f")]
            public static extern void ar_environment_light_estimation_provider_set_update_handler_f(
                IntPtr environment_light_estimation_provider,
                IntPtr environment_light_estimation_anchor_updates_queue,
                IntPtr context,
                AR_Environment_Light_Estimation_Update_Handler_Function environment_light_estimation_update_handler_function);

            /// <summary>
            /// Determines whether this device supports the environment light estimation provider.
            /// </summary>
            /// <returns><see langword="true"/> if the environment light estimation provider is supported on this device. Otherwise, <see langword="false"/>.</returns>
            [DllImport(NativeApi.Constants.LibraryName, EntryPoint = "ar_environment_light_estimation_provider_is_supported")]
            public static extern bool ar_environment_light_estimation_provider_is_supported();

            /// <summary>
            /// Get the authorization type required by the environment light estimation provider.
            /// </summary>
            /// <returns>Authorization type.</returns>
            [DllImport(NativeApi.Constants.LibraryName,
                EntryPoint = "ar_environment_light_estimation_provider_get_required_authorization_type")]
            public static extern AR_Authorization_Type
                ar_environment_light_estimation_provider_get_required_authorization_type();

            /// <summary>
            ///  Get the count of environment probe anchors in the collection.
            /// </summary>
            /// <param name="environment_probe_anchors">The collection of environment probe anchors.</param>
            /// <returns>The number of environment probe anchors in the collection.</returns>
            [DllImport(Constants.LibraryName, EntryPoint = "ar_environment_probe_anchors_get_count")]
            public static extern int ar_environment_probe_anchors_get_count(IntPtr environment_probe_anchors);

            /// <summary>
            /// Enumerate a collection of environment probe anchors.
            /// </summary>
            /// <param name="environment_probe_anchors">The collection of environment probe anchors.</param>
            /// <param name="context">The application-defined context parameter to pass to the function.</param>
            /// <param name="environment_probe_anchors_enumerator_function">The enumerator function.</param>
            [DllImport(Constants.LibraryName, EntryPoint = "ar_environment_probe_anchors_enumerate_anchors_f")]
            public static extern void ar_environment_probe_anchors_enumerate_anchors_f(
                IntPtr environment_probe_anchors,
                IntPtr context,
                AR_Environment_Probe_Anchors_Enumerator_Function environment_probe_anchors_enumerator_function);

            /// <summary>
            /// Extract a Unity texture descriptor for the given environment probe.
            /// </summary>
            /// <param name="environment_probe">An environment probe anchor</param>
            /// <param name="textureDescriptor">Output texture descriptor</param>
            [DllImport(Constants.LibraryName, EntryPoint = "UnityVisionOS_ExtractTextureDescriptorFromEnvironmentProbe")]
            static extern void UnityVisionOS_ExtractTextureDescriptorFromEnvironmentProbe(IntPtr environment_probe, IntPtr textureDescriptor);

            public static XRTextureDescriptor ExtractTextureDescriptorFromEnvironmentProbe(IntPtr environment_probe)
            {
                // TODO: Can we get away with re-using the same one or do we need to allocate more? I forget if the struct gets copied...
                UnityVisionOS_ExtractTextureDescriptorFromEnvironmentProbe(environment_probe, k_TextureDescriptorPointer);
                return Marshal.PtrToStructure<XRTextureDescriptor>(k_TextureDescriptorPointer);
            }
        }
    }
}
