using System;
using UnityEngine.XR.ARSubsystems;

namespace UnityEngine.XR.VisionOS
{
    /// <summary>
    /// Protocol for handling data providers in visionOS XR plugin
    /// </summary>
    interface IVisionOSProvider
    {
        /// <summary>
        /// The <see cref="AR_Authorization_Type"/> required by this data provider.
        /// </summary>
        AR_Authorization_Type RequiredAuthorizationType { get; }

        /// <summary>
        /// Whether this data provider is supported by the system.
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// Whether this data provider should be active
        /// </summary>
        bool ShouldBeActive { get; }

        /// <summary>
        /// The current <see cref="IntPtr"/> for the native data provider.
        /// </summary>
        IntPtr CurrentProvider { get; }

        /// <summary>
        /// Start a native data provider, along with its own AR Session, with the given <see cref="Feature"/> configuration.
        /// </summary>
        /// <param name="features"><see cref="Feature"/> flags requested by the app.</param>
        /// <returns><see langword="true"/> if the native provider was started successfully. Otherwise, <see langword="false"/>.</returns>
        bool TryStartNativeSession(Feature features);

        /// <summary>
        /// Try to stop the provider, and its native session, if they are running.
        /// </summary>
        /// <returns>Whether the session was stopped.</returns>
        bool TryStopNativeSession();

        /// <summary>
        /// Set the provider status based on the DataProviderStateChangeHandler callback.
        /// </summary>
        /// <param name="newState">The new state of the native provider.</param>
        void SetNativeProviderState(AR_Data_Provider_State newState);
    }
}
