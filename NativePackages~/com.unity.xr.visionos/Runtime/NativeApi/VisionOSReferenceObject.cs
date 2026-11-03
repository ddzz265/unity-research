using System;
using System.Runtime.InteropServices;
using AOT;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace UnityEngine.XR.VisionOS
{
    //Native representation of a tracked object (ar_reference_object_t)
    class VisionOSReferenceObject : IDisposable
    {
        // ar_reference_object_t under the hood.
        IntPtr m_Self;
        bool m_Loaded;

        public IntPtr AsIntPtr() => m_Self;
        public bool IsLoaded => m_Loaded;

        public void Dispose()
        {
            m_Self = IntPtr.Zero;
        }

        public VisionOSReferenceObject(NativeSlice<byte> bytes)
        {
            m_Self = IntPtr.Zero;
            m_Loaded = false;

            if (VisionOS.IsSimulator())
                return;

            unsafe
            {
                InitWithBytes(bytes.GetUnsafeReadOnlyPtr(), bytes.Length);
            }
        }

        public VisionOSReferenceObject(byte[] bytes)
        {
            m_Self = IntPtr.Zero;
            m_Loaded = false;

            if (VisionOS.IsSimulator())
                return;

            unsafe
            {
                fixed (void* ptr = bytes)
                {
                    InitWithBytes(ptr, bytes.Length);
                }
            }
        }

        [MonoPInvokeCallback(typeof(NativeApi.ObjectTracking.AR_Reference_Object_URL_Load_Completion_Handler_Function))]
        static void ReferenceObjectURLLoadCompletionHandler(IntPtr context, IntPtr url, int success, IntPtr error, IntPtr nativeReferenceObject)
        {
            try
            {
                var contextHandle = GCHandle.FromIntPtr(context);
                try
                {
                    var referenceObject = (VisionOSReferenceObject)contextHandle.Target;
                    if (success != 0)
                    {
                        referenceObject.m_Self = nativeReferenceObject;
                        referenceObject.m_Loaded = true;
                    }
                    else
                    {
                        referenceObject.m_Self = IntPtr.Zero;
                        Debug.LogError("Failed to load referenceobject file.");

                        var convertedError = NativeApi.ObjectTracking.ar_error_copy_cf_error(error);
                        NativeApi.ObjectTracking.UnityVisionOSPrintCFErrorDescription(convertedError);
                    }
                }
                finally
                {
                    contextHandle.Free();
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // ReSharper disable once UnusedMember.Local
        static readonly NativeApi.ObjectTracking.AR_Reference_Object_URL_Load_Completion_Handler_Function k_ReferenceObjectURLLoadCompletionHandler =
            ReferenceObjectURLLoadCompletionHandler;

#if UNITY_EDITOR
#if UNITY_VISIONOS_MAC_STUB
        unsafe void InitWithBytes(void* bytes, int byteCount)
        {
            var context = GCHandle.ToIntPtr(GCHandle.Alloc(this));
            NativeApi.ObjectTracking.InitWithBytes(bytes, byteCount, context, k_ReferenceObjectURLLoadCompletionHandler);
        }
#else
        // ReSharper disable UnusedParameter.Local
        static unsafe void InitWithBytes(void* bytes, int byteCount) { }
        // ReSharper restore UnusedParameter.Local
#endif
#else
        unsafe void InitWithBytes(void* bytes, int byteCount)
        {
            var context = GCHandle.ToIntPtr(GCHandle.Alloc(this));
            NativeApi.ObjectTracking.InitWithBytes(bytes, byteCount, context, k_ReferenceObjectURLLoadCompletionHandler);
        }
#endif
    }
}
