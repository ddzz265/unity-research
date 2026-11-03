using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace UnityEngine.XR.VisionOS
{
    using SessionProvider = VisionOSSessionSubsystem.VisionOSSessionProvider;

    class VisionOSWorldTrackingProvider : IVisionOSProvider
    {
        internal static VisionOSWorldTrackingProvider Instance { get; private set; }
        internal event Action<IntPtr> OnCreated;

        public AR_Authorization_Type RequiredAuthorizationType => NativeApi.WorldTracking.ar_world_tracking_provider_get_required_authorization_type();
        public bool IsSupported => NativeApi.WorldTracking.ar_world_tracking_provider_is_supported();

        public bool ShouldBeActive => GetTrackingSubsystemStatus() == SubsystemStatus.Started;

        public IntPtr CurrentProvider { get; private set; } = IntPtr.Zero;

        IntPtr m_ARSession = IntPtr.Zero;

        public VisionOSWorldTrackingProvider()
        {
            Instance = this;
        }

        public bool TryStartNativeSession(Feature features)
        {
            if (!IsSupported)
            {
                Debug.LogWarning("World tracking provider is not supported.");
                return false;
            }

            // Early-out if provider is already running
            if (m_ARSession != IntPtr.Zero)
                return true;

            CurrentProvider = CreateWorldTrackingProvider();
            OnCreated?.Invoke(CurrentProvider);
            if (CurrentProvider == IntPtr.Zero)
            {
                Debug.LogWarning("Failed to create world tracking provider.");
                return false;
            }

            Debug.Log("Starting world tracking provider.");
            m_ARSession = SessionProvider.StartProviderSession(CurrentProvider);
            return true;
        }

        public bool TryStopNativeSession()
        {
            // Early-out if provider has not been started
            if (m_ARSession == IntPtr.Zero)
                return false;

            Debug.Log("Stopping world tracking provider.");
            NativeApi.Session.ar_session_stop(m_ARSession);

            m_ARSession = IntPtr.Zero;
            CurrentProvider = IntPtr.Zero;
            return true;
        }

        public void SetNativeProviderState(AR_Data_Provider_State newState)
        {
            // Do nothing: Native plugin handles provider state on its own.
        }

        [DllImport(NativeApi.Constants.LibraryName, EntryPoint = "UnityVisionOS_CreateWorldTrackingProvider")]
        static extern IntPtr CreateWorldTrackingProvider();

        [DllImport(NativeApi.Constants.LibraryName, EntryPoint = "UnityVisionOS_GetTrackingSubsystemStatus")]
        static extern SubsystemStatus GetTrackingSubsystemStatus();
    }
}
