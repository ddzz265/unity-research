using System;
using System.Runtime.InteropServices;

namespace UnityEngine.XR.VisionOS
{
    /// <summary>
    /// Native API wrappers for object tracking.
    /// Signatures should match object_tracking.h.
    /// </summary>
    static partial class NativeApi
    {
        internal static class ObjectTracking
        {
            /// <summary>
            /// Handler triggered when there are updates to object anchors.
            /// <param name="added_anchors">Collection of anchors that are added.</param>
            /// <param name="updated_anchors">Collection of anchors that are updated.</param>
            /// <param name="removed_anchors">Collection of anchors that are removed.</param>
            /// </summary>
            internal delegate void AR_Object_Tracking_Update_Handler_Function(IntPtr context, IntPtr added_anchors, IntPtr updated_anchors, IntPtr removed_anchors);

            /// <summary>
            /// Function for enumerating a collection of object anchors.
            /// <param name="context">The application-defined context.</param>
            /// <param name="object_anchor">The object anchor.</param>
            /// </summary>
            internal delegate bool AR_Object_Anchors_Enumerator_Function(IntPtr context, IntPtr object_anchor);

            internal delegate void AR_Reference_Object_URL_Load_Completion_Handler_Function(IntPtr context, IntPtr url, int success, IntPtr error,
                IntPtr reference_object);

            [DllImport(Constants.LibraryName, EntryPoint = "ar_object_anchors_get_count")]
            internal static extern int ar_object_anchors_get_count(IntPtr object_anchors);

            [DllImport(Constants.LibraryName, EntryPoint = "ar_object_anchors_enumerate_anchors_f")]
            internal static extern int ar_object_anchors_enumerate_anchors_f(
                IntPtr object_anchors,
                IntPtr context,
                AR_Object_Anchors_Enumerator_Function object_anchors_enumerator_function);

            /// <summary>
            /// Create object tracking configuration.
            /// </summary>
            /// <remarks>
            /// This type supports ARC. In non-ARC files, use `ar_retain()` and `ar_release()` to retain and release the object.
            /// </remarks>
            /// <returns>An instance of `ar_object_tracking_configuration_t`.</returns>
            [DllImport(Constants.LibraryName, EntryPoint = "ar_object_tracking_configuration_create")]
            internal static extern IntPtr ar_object_tracking_configuration_create();

            /// <summary>
            /// Create an object tracking provider.
            /// </summary>
            /// <remarks>
            /// This type supports ARC. In non-ARC files, use `ar_retain()` and `ar_release()` to retain and release the object.
            /// </remarks>
            /// <param name="object_tracking_configuration">Object Tracking configuration.</param>
            /// <returns>An instance of `ar_object_tracking_provider`.</returns>
            [DllImport(Constants.LibraryName, EntryPoint = "ar_object_tracking_provider_create")]
            internal static extern IntPtr ar_object_tracking_provider_create(IntPtr object_tracking_configuration);

            [DllImport(Constants.LibraryName, EntryPoint = "ar_object_tracking_provider_set_update_handler_f")]
            internal static extern void ar_object_tracking_provider_set_update_handler_f(
                IntPtr object_tracking_provider,
                IntPtr object_tracking_updates_queue,
                IntPtr context,
                AR_Object_Tracking_Update_Handler_Function object_tracking_update_handler_function);

            /// <summary>
            /// Determines whether this device supports the object tracking provider.
            /// </summary>
            /// <returns><see langword="true"/> if the object tracking provider is supported on this device. Otherwise, <see langword="false"/>.</returns>
            [DllImport(Constants.LibraryName, EntryPoint = "ar_object_tracking_provider_is_supported")]
            internal static extern bool ar_object_tracking_provider_is_supported();

            /// <summary>
            /// Get the authorization type required by the object tracking provider.
            /// </summary>
            /// <returns>Authorization type.</returns>
            [DllImport(Constants.LibraryName,
                EntryPoint = "ar_object_tracking_provider_get_required_authorization_type")]
            internal static extern AR_Authorization_Type ar_object_tracking_provider_get_required_authorization_type();

            [DllImport(Constants.LibraryName,
                EntryPoint = "ar_object_tracking_configuration_add_reference_objects")]
            internal static extern void ar_object_tracking_configuration_add_reference_objects(
                IntPtr object_tracking_configuration, IntPtr reference_objects);

            [DllImport(Constants.LibraryName, EntryPoint = "ar_reference_objects_create")]
            internal static extern IntPtr ar_reference_objects_create();

            [DllImport(Constants.LibraryName, EntryPoint = "ar_reference_objects_add_object")]
            internal static extern IntPtr ar_reference_objects_add_object(IntPtr reference_objects, IntPtr object_to_add);

            [DllImport(Constants.LibraryName, EntryPoint = "ar_object_anchor_get_reference_object")]
            internal static extern IntPtr ar_object_anchor_get_reference_object(IntPtr object_anchor);

            /// <summary>
            /// Compare two ar_reference_object_t's to see if their underlining uuid_t identifier's match.
            /// </summary>
            /// <param name="obj1">First ar_reference_object_t to compare against.</param>
            /// <param name="obj2">Second ar_reference_object_t to compare against.</param>
            /// <returns>true if the ar_reference_object_get_identifier's match between the two ar_reference_object_t's.</returns>
            [DllImport(Constants.LibraryName, EntryPoint = "UnityVisionOSCompareReferenceObjectUUIDs")]
            internal static extern bool UnityVisionOSCompareReferenceObjectUUIDs(IntPtr obj1, IntPtr obj2);

            [DllImport(Constants.LibraryName, EntryPoint = "UnityVisionOSPrintCFErrorDescription")]
            internal static extern void UnityVisionOSPrintCFErrorDescription(IntPtr error);

            [DllImport(Constants.LibraryName, EntryPoint = "ar_error_copy_cf_error")]
            internal static extern IntPtr ar_error_copy_cf_error(IntPtr error);

            [DllImport(Constants.LibraryName, EntryPoint = "UnityVisionOSReferenceObjectInitWithBytes")]
            internal static extern unsafe void InitWithBytes(void* bytes, int byteCount, IntPtr context,
                AR_Reference_Object_URL_Load_Completion_Handler_Function completion_handler_function);
        }
    }
}
