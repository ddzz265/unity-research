using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace UnityEngine.XR.VisionOS
{
    using SessionProvider = VisionOSSessionSubsystem.VisionOSSessionProvider;

    class VisionOSMeshProvider : IVisionOSProvider
    {
        public AR_Authorization_Type RequiredAuthorizationType => NativeApi.SceneReconstruction.ar_scene_reconstruction_provider_get_required_authorization_type();
        public bool IsSupported => NativeApi.SceneReconstruction.ar_scene_reconstruction_provider_is_supported();
        public bool ShouldBeActive => GetMeshSubsystemStatus() == SubsystemStatus.Started;
        public IntPtr CurrentProvider { get; private set; } = IntPtr.Zero;

        IntPtr m_ARSession = IntPtr.Zero;

        public bool TryStartNativeSession(Feature features)
        {
            if (!IsSupported)
            {
                Debug.LogWarning("Scene reconstruction provider is not supported");
                return false;
            }

            // Early-out if provider is already running
            if (m_ARSession != IntPtr.Zero)
                return true;

            var mode = AR_Scene_Reconstruction_Mode.Default;
            if ((features & Feature.MeshClassification) != 0)
                mode = AR_Scene_Reconstruction_Mode.Classification;

            CurrentProvider = CreateSceneReconstructionProvider(mode);
            if (CurrentProvider == IntPtr.Zero)
            {
                Debug.LogWarning("Failed to create scene reconstruction provider.");
                return false;
            }

            Debug.Log("Starting mesh provider.");
            SessionProvider.StartProviderSession(CurrentProvider);
            return true;
        }

        public bool TryStopNativeSession()
        {
            // Early-out if provider has not been started
            if (m_ARSession == IntPtr.Zero)
                return false;

            Debug.Log("Stopping mesh provider.");
            NativeApi.Session.ar_session_stop(m_ARSession);

            m_ARSession = IntPtr.Zero;
            CurrentProvider = IntPtr.Zero;
            return true;
        }

        public void SetNativeProviderState(AR_Data_Provider_State newState)
        {
            // Do nothing; This provider does not poll for data
        }

        [DllImport(NativeApi.Constants.LibraryName, EntryPoint = "UnityVisionOS_CreateSceneReconstructionProvider")]
        static extern IntPtr CreateSceneReconstructionProvider(AR_Scene_Reconstruction_Mode mode);

        [DllImport(NativeApi.Constants.LibraryName, EntryPoint = "UnityVisionOS_GetMeshSubsystemStatus")]
        static extern SubsystemStatus GetMeshSubsystemStatus();
    }
}
