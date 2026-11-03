using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Scripting;
using UnityEngine.XR.ARSubsystems;

namespace UnityEngine.XR.VisionOS
{
    using SessionProvider = VisionOSSessionSubsystem.VisionOSSessionProvider;

    /// <summary>
    /// This subsystem provides implementing functionality for the <c>XREnvironmentProbeSubsystem</c> class.
    /// Its lifecycle is managed by the XR SDK system and VisionOSLoader. When environment probes are requested, a VisionOSEnvironmentProbeProvider is created
    /// and started, and the provider is stopped and destroyed, and updated by XR SDK as needed. This subsystem calls directly into native ARKit APIs via
    /// DLLInterop, except for UnityVisionOS_impl_ar_anchor_get_origin_from_anchor_transform_to_float_array and ExtractTextureDescriptorFromEnvironmentProbe,
    /// which are implemented in VisionOSNativeBridge and VisionOSDisplayProvider respectively.
    /// </summary>
    [Preserve]
    class VisionOSEnvironmentProbeSubsystem : XREnvironmentProbeSubsystem
    {
        internal const string environmentProbeSubsystemId = "VisionOS-EnvironmentProbe";

        class VisionOSEnvironmentProbeProvider : Provider, IVisionOSProvider
        {
            enum EnumeratorCallbackType
            {
                Added,
                Updated,
                Removed
            }

            const int k_InitialSize = 1;
            const Feature k_EnvironmentProbesFeature = Feature.EnvironmentProbes;

            static VisionOSEnvironmentProbeProvider s_Instance;
            static readonly NativeApi.EnvironmentLightEstimation.AR_Environment_Light_Estimation_Update_Handler_Function k_EnvironmentLightEstimationUpdateHandlerFunction =
                EnvironmentLightEstimationUpdateHandler;

            static readonly NativeApi.EnvironmentLightEstimation.AR_Environment_Probe_Anchors_Enumerator_Function k_EnvironmentLightEstimationEnumeratorFunction =
                EnvironmentLightEstimationEnumeratorFunction;

            // TODO: Create a native struct/pointer to contain this data which is retained between calls to native ARKit APIs.
            // These static variables are used to keep track of state while using ar_environment_probe_anchors_enumerate_anchors, which is a native method
            // that enumerates a list of environment probes with a callback. We increment s_EnumeratorCount in the callback and when it is >= s_EnumeratorCountMax
            // we know to return false and finish iterating.
            // We can pass that struct along using the context argument instead of relying on static variables to maintain this state between calls.
            static int s_EnumeratorCount;
            static int s_EnumeratorCountMax;
            static EnumeratorCallbackType s_EnumeratorCallbackType;

            readonly Dictionary<TrackableId, XREnvironmentProbe> m_TempAddedProbes = new(k_InitialSize);
            readonly Dictionary<TrackableId, XREnvironmentProbe> m_TempUpdatedProbes = new(k_InitialSize);
            readonly HashSet<TrackableId> m_TempRemovedProbes = new(k_InitialSize);
            NativeArray<XREnvironmentProbe> m_AddedProbes = new(k_InitialSize, Allocator.Persistent);
            NativeArray<XREnvironmentProbe> m_UpdatedProbes = new(k_InitialSize, Allocator.Persistent);
            NativeArray<TrackableId> m_RemovedProbes = new(k_InitialSize, Allocator.Persistent);

            IntPtr m_EnvironmentLightEstimationConfiguration;

            public AR_Authorization_Type RequiredAuthorizationType => NativeApi.EnvironmentLightEstimation.ar_environment_light_estimation_provider_get_required_authorization_type();
            public bool IsSupported => NativeApi.EnvironmentLightEstimation.ar_environment_light_estimation_provider_is_supported();
            public bool ShouldBeActive => running;
            public IntPtr CurrentProvider { get; private set; } = IntPtr.Zero;
            IntPtr m_ARSession = IntPtr.Zero;

            /// <summary>
            /// Enable or disable automatic placement of environment probes by the provider.
            /// </summary>
            /// <param name='value'><c>true</c> if the provider should automatically place environment probes in the scene.
            /// Otherwise, <c>false</c></param>.
            /// <remarks>ARKit on visionOS does not allow Environment Probes to be placed manually.  Regardless of value this will always be automatic.</remarks>
            public override bool automaticPlacementRequested
            {
                get => true;
                set
                {
                    if (!value)
                        throw new NotSupportedException("ARKit on visionOS only supports the automatic placement of environment probes.");
                }
            }

            /// <summary>
            /// Get whether automatic placement is enabled. This property is always true.
            /// </summary>
            public override bool automaticPlacementEnabled => true;

            /// <summary>
            /// Get or set whether HDR environment texture generation is requested.
            /// </summary>
            /// Whether the HDR environment texture generation state is requested.
            /// <remarks>ARKit on visionOS will only ever return environmental textures that are not HDR.  This can only be set to <c>false</c>.</remarks>
            public override bool environmentTextureHDRRequested
            {
                get => false;
                set
                {
                    if (value)
                    {
                        throw new NotSupportedException("ARKit on visionOS only supports non-HDR for environment textures.");
                    }
                }
            }

            /// <summary>
            /// Get whether HDR environment textures are enabled. This always returns false.
            /// </summary>
            public override bool environmentTextureHDREnabled => false;

            public VisionOSEnvironmentProbeProvider()
            {
                s_Instance = this;
            }

            public bool TryStartNativeSession(Feature features)
            {
                if (!IsSupported)
                {
                    Debug.LogWarning("Environment probe provider is not supported");
                    return false;
                }

                // Early-out if provider is already running
                if (m_ARSession != IntPtr.Zero)
                    return true;

                m_EnvironmentLightEstimationConfiguration = NativeApi.EnvironmentLightEstimation.ar_environment_light_estimation_configuration_create();

                CurrentProvider = NativeApi.EnvironmentLightEstimation.ar_environment_light_estimation_provider_create(m_EnvironmentLightEstimationConfiguration);
                if (CurrentProvider == IntPtr.Zero)
                {
                    Debug.LogWarning("Failed to create environment probe provider.");
                    return false;
                }

                // TODO: Send instance handle along with context ptr
                NativeApi.EnvironmentLightEstimation.ar_environment_light_estimation_provider_set_update_handler_f(CurrentProvider, IntPtr.Zero, IntPtr.Zero,
                    k_EnvironmentLightEstimationUpdateHandlerFunction);

                Debug.Log("Starting environment probe provider.");
                m_ARSession = SessionProvider.StartProviderSession(CurrentProvider);
                return true;
            }

            public bool TryStopNativeSession()
            {
                // Early-out if provider has not been started
                if (m_ARSession == IntPtr.Zero)
                    return false;

                Debug.Log("Stopping environment probe provider.");
                NativeApi.Session.ar_session_stop(m_ARSession);

                // Clear any data in temp collections so it can't be consumed by Unity; it is invalid now
                ClearTempCollections();
                m_ARSession = IntPtr.Zero;
                CurrentProvider = IntPtr.Zero;
                return true;
            }

            public void SetNativeProviderState(AR_Data_Provider_State newState)
            {
                // Do nothing; This provider does not poll for data
            }

            // ReSharper disable InconsistentNaming
            [MonoPInvokeCallback(typeof(NativeApi.EnvironmentLightEstimation.AR_Environment_Light_Estimation_Update_Handler_Function))]
            static void EnvironmentLightEstimationUpdateHandler(IntPtr context, IntPtr added_anchors, IntPtr updated_anchors, IntPtr removed_anchors)
            {
                // MonoPInvokeCallback methods will leak exceptions and cause crashes; always use a try/catch in these methods
                try
                {
                    var count = NativeApi.EnvironmentLightEstimation.ar_environment_probe_anchors_get_count(added_anchors);
                    if (count > 0)
                    {
                        s_EnumeratorCount = 0;
                        s_EnumeratorCountMax = count;
                        s_EnumeratorCallbackType = EnumeratorCallbackType.Added;
                        NativeApi.EnvironmentLightEstimation.ar_environment_probe_anchors_enumerate_anchors_f(added_anchors, IntPtr.Zero, k_EnvironmentLightEstimationEnumeratorFunction);
                    }

                    count = NativeApi.EnvironmentLightEstimation.ar_environment_probe_anchors_get_count(updated_anchors);
                    if (count > 0)
                    {
                        s_EnumeratorCount = 0;
                        s_EnumeratorCountMax = count;
                        s_EnumeratorCallbackType = EnumeratorCallbackType.Updated;
                        NativeApi.EnvironmentLightEstimation.ar_environment_probe_anchors_enumerate_anchors_f(updated_anchors, IntPtr.Zero, k_EnvironmentLightEstimationEnumeratorFunction);
                    }

                    count = NativeApi.EnvironmentLightEstimation.ar_environment_probe_anchors_get_count(removed_anchors);
                    if (count > 0)
                    {
                        s_EnumeratorCount = 0;
                        s_EnumeratorCountMax = count;
                        s_EnumeratorCallbackType = EnumeratorCallbackType.Removed;
                        NativeApi.EnvironmentLightEstimation.ar_environment_probe_anchors_enumerate_anchors_f(removed_anchors, IntPtr.Zero, k_EnvironmentLightEstimationEnumeratorFunction);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            [MonoPInvokeCallback(typeof(NativeApi.EnvironmentLightEstimation.AR_Environment_Probe_Anchors_Enumerator_Function))]
            static bool EnvironmentLightEstimationEnumeratorFunction(IntPtr context, IntPtr environmentProbeAnchor)
            {
                // MonoPInvokeCallback methods will leak exceptions and cause crashes; always use a try/catch in these methods
                try
                {
                    s_Instance.ProcessProbeUpdates(environmentProbeAnchor, s_EnumeratorCallbackType);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }

                return ++s_EnumeratorCount < s_EnumeratorCountMax;
            }

            void ProcessProbeUpdates(IntPtr environmentProbeAnchor, EnumeratorCallbackType type)
            {
                var trackableId = NativeApi.Utilities.GetTrackableId(environmentProbeAnchor);
                switch (type)
                {
                    case EnumeratorCallbackType.Added:
                        // NB: Using a dictionary does not preserve update order
                        m_TempAddedProbes[trackableId] = GetEnvironmentProbe(environmentProbeAnchor, trackableId);
                        break;
                    case EnumeratorCallbackType.Updated:
                        // NB: Using a dictionary does not preserve update order
                        // If the probe is already in added probes, we haven't acquired it yet, so replace it instead of adding to updated probes
                        var probe = GetEnvironmentProbe(environmentProbeAnchor, trackableId);
                        if (m_TempAddedProbes.ContainsKey(trackableId))
                            m_TempAddedProbes[trackableId] = probe;
                        else
                            m_TempUpdatedProbes[trackableId] = probe;

                        break;
                    case EnumeratorCallbackType.Removed:
                        var removed = m_TempAddedProbes.Remove(trackableId);
                        removed |= m_TempUpdatedProbes.Remove(trackableId);

                        // Only add to removed planes if we have already acquired this plane (i.e. it does not exist in the other temp dictionaries)
                        if (!removed)
                            m_TempRemovedProbes.Add(trackableId);

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            // ReSharper enable InconsistentNaming

            static XREnvironmentProbe GetEnvironmentProbe(IntPtr probeAnchor, TrackableId trackableId)
            {
                var isTracked = NativeApi.Anchor.ar_trackable_anchor_is_tracked(probeAnchor);

                // TODO: For some reason this method was just returning the same pointer you gave it, so it needed to be wrapped in ObjC
                var transformFloatArray = NativeApi.Anchor.UnityVisionOS_impl_ar_anchor_get_origin_from_anchor_transform_to_float_array(probeAnchor);

                var worldMatrix = Marshal.PtrToStructure<FloatArrayToMatrix4x4>(transformFloatArray);
                var pose = new Pose(worldMatrix.GetPosition(), worldMatrix.GetRotation());

                // TODO: Request probe size API from Apple, or establish a reasonable default size
                var trackingState = isTracked ? TrackingState.Tracking : TrackingState.None;
                var textureDescriptor = NativeApi.EnvironmentLightEstimation.ExtractTextureDescriptorFromEnvironmentProbe(probeAnchor);
                var size = Vector3.one * 10f;

                return new XREnvironmentProbe(trackableId, Vector3.one, pose, size, textureDescriptor, trackingState, probeAnchor);
            }

            public override void Start()
            {
                VisionOSProviderRegistration.RegisterProvider(k_EnvironmentProbesFeature, this);
            }

            public override void Stop()
            {
                // Do not call TryStopNativeSession in Subsystem Stop callback. This will be handled by SessionSubsystem
                VisionOSProviderRegistration.UnregisterProvider(k_EnvironmentProbesFeature, this);
                ClearTempCollections();
            }

            public override void Destroy()
            {
                // Try to stop the native session in case TryStop hasn't been called yet.
                if (!TryStopNativeSession())
                {
                    // Clear things out in case TryStopNativeSession didn't do its job previously
                    m_ARSession = IntPtr.Zero;
                    CurrentProvider = IntPtr.Zero;
                    ClearTempCollections();
                }

                m_AddedProbes.Dispose();
                m_UpdatedProbes.Dispose();
                m_RemovedProbes.Dispose();
            }

            void ClearTempCollections()
            {
                m_TempAddedProbes.Clear();
                m_TempUpdatedProbes.Clear();
                m_TempRemovedProbes.Clear();
            }

            public override unsafe TrackableChanges<XREnvironmentProbe> GetChanges(XREnvironmentProbe defaultEnvironmentProbe, Allocator allocator)
            {
                try
                {
                    NativeApi.Utilities.DictionaryToNativeArray(m_TempAddedProbes, ref m_AddedProbes);
                    NativeApi.Utilities.DictionaryToNativeArray(m_TempUpdatedProbes, ref m_UpdatedProbes);
                    NativeApi.Utilities.HashSetToNativeArray(m_TempRemovedProbes, ref m_RemovedProbes);

                    var changes = new TrackableChanges<XREnvironmentProbe>(
                        m_AddedProbes.GetUnsafePtr(), m_TempAddedProbes.Count,
                        m_UpdatedProbes.GetUnsafePtr(), m_TempUpdatedProbes.Count,
                        m_RemovedProbes.GetUnsafePtr(), m_TempRemovedProbes.Count,
                        defaultEnvironmentProbe, sizeof(XREnvironmentProbe), allocator);

                    return changes;
                }
                finally
                {
                    ClearTempCollections();
                }
            }
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#endif
        static void RegisterDescriptor()
        {
            var cinfo = new XREnvironmentProbeSubsystemDescriptor.Cinfo
            {
                id = environmentProbeSubsystemId,
                providerType = typeof(VisionOSEnvironmentProbeProvider),
                subsystemTypeOverride = typeof(VisionOSEnvironmentProbeSubsystem),

                supportsManualPlacement = false,
                supportsRemovalOfManual = false,
                supportsAutomaticPlacement = true,
                supportsRemovalOfAutomatic = false,
                supportsEnvironmentTexture = true,
                supportsEnvironmentTextureHDR = false // TODO: Investigate using get_clipping_point_lux to provide HDR data

            };

            XREnvironmentProbeSubsystemDescriptor.Register(cinfo);
        }
    }
}
