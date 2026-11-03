using System;
using System.Runtime.InteropServices;

namespace UnityEngine.XR.VisionOS
{
    /// <summary>
    /// Native API wrappers for layer renderer.
    /// signatures should match layer_renderer.h
    /// </summary>
    static partial class NativeApi
    {
        internal static class LayerRenderer
        {
            [DllImport(Constants.LibraryName, EntryPoint = "cp_layer_renderer_set_minimum_frame_repeat_count")]
            public static extern void cp_layer_renderer_set_minimum_frame_repeat_count(IntPtr layer_renderer, int frame_repeat_count);
        }
    }
}
