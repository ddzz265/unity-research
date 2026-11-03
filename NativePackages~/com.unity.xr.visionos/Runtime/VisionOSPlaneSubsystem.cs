using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine.Scripting;
using UnityEngine.XR.ARSubsystems;

namespace UnityEngine.XR.VisionOS
{
    using SessionProvider = VisionOSSessionSubsystem.VisionOSSessionProvider;

    /// <summary>
    /// The VisionOS implementation of the <c>XRPlaneSubsystem</c>. Do not create this directly. Use the <c>SubsystemManager</c> instead.
    /// </summary>
    [Preserve]
    public sealed class VisionOSPlaneSubsystem : XRPlaneSubsystem
    {
        internal const string planeSubsystemId = "VisionOS-Plane";

        class VisionOSPlaneProvider : Provider, IVisionOSProvider
        {
            const int k_InitialSize = 16;
            const Feature k_PlaneTrackingFeature = Feature.PlaneTracking;

            static VisionOSPlaneProvider s_Instance;

            static readonly unsafe NativeApi.PlaneDetection.AR_Plane_Detection_Update_Handler k_PlaneDetectionUpdateHandler = PlaneDetectionUpdateHandler;

            IntPtr m_PlaneDetectionConfiguration;
            PlaneDetectionMode m_CurrentPlaneDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;

            readonly Dictionary<TrackableId, BoundedPlane> m_TempAddedPlanes = new(k_InitialSize);
            readonly Dictionary<TrackableId, BoundedPlane> m_TempUpdatedPlanes = new(k_InitialSize);
            readonly HashSet<TrackableId> m_TempRemovedPlanes = new(k_InitialSize);

            NativeArray<BoundedPlane> m_AddedPlanes = new(k_InitialSize, Allocator.Persistent);
            NativeArray<BoundedPlane> m_UpdatedPlanes = new(k_InitialSize, Allocator.Persistent);
            NativeArray<TrackableId> m_RemovedPlanes = new(k_InitialSize, Allocator.Persistent);

            readonly Dictionary<TrackableId, NativeArray<Vector3>> m_Boundaries = new();

            public AR_Authorization_Type RequiredAuthorizationType => NativeApi.PlaneDetection.ar_plane_detection_provider_get_required_authorization_type();
            public bool IsSupported => NativeApi.PlaneDetection.ar_plane_detection_provider_is_supported();
            public bool ShouldBeActive => running;
            public IntPtr CurrentProvider { get; private set; } = IntPtr.Zero;

            IntPtr m_ARSession = IntPtr.Zero;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle m_BoundsArraySafetyHandle;
#endif

            /// <summary>
            /// Get the current plane detection mode in use.
            /// </summary>
            public override PlaneDetectionMode currentPlaneDetectionMode => m_CurrentPlaneDetectionMode;

            public VisionOSPlaneProvider()
            {
                s_Instance = this;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_BoundsArraySafetyHandle = AtomicSafetyHandle.Create();
                AtomicSafetyHandle.SetStaticSafetyId(ref m_BoundsArraySafetyHandle, AtomicSafetyHandle.NewStaticSafetyId<Vector3>());
#endif
            }

            public bool TryStartNativeSession(Feature features)
            {
                if (!IsSupported)
                {
                    Debug.LogWarning("Plane detection provider is not supported");
                    return false;
                }

                // Early-out if provider is already running
                if (m_ARSession != IntPtr.Zero)
                    return true;

                m_PlaneDetectionConfiguration = NativeApi.PlaneDetection.ar_plane_detection_configuration_create();

                // NB: This assumes that AR_Plane_Alignment and PlaneDetectionMode line up exactly, which is currently the case. If this changes, we must
                // explicitly define the conversion.
                NativeApi.PlaneDetection.ar_plane_detection_configuration_set_alignment(m_PlaneDetectionConfiguration, (AR_Plane_Alignment)m_CurrentPlaneDetectionMode);

                CurrentProvider = NativeApi.PlaneDetection.ar_plane_detection_provider_create(m_PlaneDetectionConfiguration);
                if (CurrentProvider == IntPtr.Zero)
                {
                    Debug.LogWarning("Failed to create plane detection provider.");
                    return false;
                }

                Debug.Log("Starting plane detection provider.");
                NativeApi.PlaneDetection.UnityVisionOS_impl_ar_plane_detection_provider_set_update_handler(CurrentProvider, k_PlaneDetectionUpdateHandler);
                m_ARSession = SessionProvider.StartProviderSession(CurrentProvider);
                return true;
            }

            public bool TryStopNativeSession()
            {
                // Early-out if provider has not been started
                if (m_ARSession == IntPtr.Zero)
                    return false;

                Debug.Log("Stopping plane detection provider.");
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

                m_AddedPlanes.Dispose();
                m_UpdatedPlanes.Dispose();
                m_RemovedPlanes.Dispose();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.Release(m_BoundsArraySafetyHandle);
#endif
            }

            public override void Start()
            {
                VisionOSProviderRegistration.RegisterProvider(k_PlaneTrackingFeature, this);
            }

            public override void Stop()
            {
                // Do not call TryStopNativeSession in Subsystem Stop callback. This will be handled by SessionSubsystem
                VisionOSProviderRegistration.UnregisterProvider(k_PlaneTrackingFeature, this);
                ClearTempCollections();
            }

            void ClearTempCollections()
            {
                m_TempAddedPlanes.Clear();
                m_TempUpdatedPlanes.Clear();
                m_TempRemovedPlanes.Clear();
            }

            // ReSharper disable InconsistentNaming
            // TODO: Use IntPtr instead of void*?
            [MonoPInvokeCallback(typeof(NativeApi.PlaneDetection.AR_Plane_Detection_Update_Handler))]
            static unsafe void PlaneDetectionUpdateHandler(void* added_anchors, int added_anchor_count,
                void* updated_anchors, int updated_anchor_count, void* removed_anchors, int removed_anchor_count)
            {
                // MonoPInvokeCallback methods will leak exceptions and cause crashes; always use a try/catch in these methods
                try
                {
                    s_Instance.ProcessPlaneUpdates(added_anchors, added_anchor_count, updated_anchors, updated_anchor_count, removed_anchors, removed_anchor_count);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            static PlaneClassifications AR_Plane_ClassificationToPlaneClassification(AR_Plane_Classification classification)
            {
                switch (classification)
                {
                    case AR_Plane_Classification.Wall:
                        return PlaneClassifications.WallFace;
                    case AR_Plane_Classification.Floor:
                        return PlaneClassifications.Floor;
                    case AR_Plane_Classification.Ceiling:
                        return PlaneClassifications.Ceiling;
                    case AR_Plane_Classification.Table:
                        return PlaneClassifications.Table;
                    case AR_Plane_Classification.Seat:
                        return PlaneClassifications.Seat;
                    case AR_Plane_Classification.Window:
                        return PlaneClassifications.WindowFrame;
                    case AR_Plane_Classification.Door:
                        return PlaneClassifications.DoorFrame;
                    case AR_Plane_Classification.Status_not_available:
                    case AR_Plane_Classification.Status_undetermined:
                    case AR_Plane_Classification.Status_unknown:
                    default:
                        return PlaneClassifications.None;
                }
            }

            BoundedPlane GetBoundedPlane(IntPtr planeAnchor)
            {
                var alignment = NativeApi.PlaneDetection.ar_plane_anchor_get_alignment(planeAnchor);
                var classification = NativeApi.PlaneDetection.ar_plane_anchor_get_plane_classification(planeAnchor);
                var isTracked = NativeApi.Anchor.ar_trackable_anchor_is_tracked(planeAnchor);

                // TODO: For some reason this method was just returning the same pointer you gave it, so it needed to be wrapped in ObjC
                var transformFloatArray = NativeApi.Anchor.UnityVisionOS_impl_ar_anchor_get_origin_from_anchor_transform_to_float_array(planeAnchor);
                var worldMatrix = Marshal.PtrToStructure<FloatArrayToMatrix4x4>(transformFloatArray);
                var pose = new Pose(worldMatrix.GetPosition(), worldMatrix.GetRotation());

                var trackableId = NativeApi.Utilities.GetTrackableId(planeAnchor);
                var trackingState = isTracked ? TrackingState.Tracking : TrackingState.None;
                var planeClassification = AR_Plane_ClassificationToPlaneClassification(classification);
                PlaneAlignment planeAlignment;
                switch (alignment)
                {
                    case AR_Plane_Alignment.None:
                        planeAlignment = PlaneAlignment.None;
                        break;
                    case AR_Plane_Alignment.Horizontal:
                        // TODO: refactor FloatArrayToMatrix4x4 to avoid repeated matrix conversion
                        // If y-component of second column is negative, plane is rotated upside-down
                        planeAlignment = worldMatrix.ToMatrix4x4().m11 > 0 ? PlaneAlignment.HorizontalUp : PlaneAlignment.HorizontalDown;
                        break;
                    case AR_Plane_Alignment.Vertical:
                        planeAlignment = PlaneAlignment.Vertical;
                        break;
                    case AR_Plane_Alignment.Slanted:
                        planeAlignment = PlaneAlignment.NotAxisAligned;
                        break;
                    default:
                        planeAlignment = PlaneAlignment.None;
                        Debug.LogError($"Unsupported plane alignment {alignment}");
                        break;
                }

                var planeGeometry = NativeApi.PlaneDetection.ar_plane_anchor_get_geometry(planeAnchor);
                var extents = NativeApi.PlaneDetection.ar_plane_geometry_get_plane_extent(planeGeometry);
                var width = NativeApi.PlaneDetection.ar_plane_extent_get_width(extents);
                var height = NativeApi.PlaneDetection.ar_plane_extent_get_height(extents);
                var size = new Vector2(width, height);

                var geometrySource = NativeApi.PlaneDetection.ar_plane_geometry_get_mesh_vertices(planeGeometry);
                var vertexFormat = NativeApi.SceneReconstruction.ar_geometry_source_get_format(geometrySource);
                if (vertexFormat == MTLVertexFormat.MTLVertexFormatFloat3)
                {
                    var vertexCount = NativeApi.SceneReconstruction.ar_geometry_source_get_count(geometrySource);
                    var vertexBuffer = NativeApi.SceneReconstruction.UnityVisionOS_impl_ar_geometry_source_get_buffer(geometrySource);
                    unsafe
                    {
                        // Because boundArray will be used in a job, we can't use AtomicSafetyHandle.GetTempUnsafePtrSliceHandle. Use our own handle instead.
                        var boundaryArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<Vector3>((void*)vertexBuffer, vertexCount, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref boundaryArray, m_BoundsArraySafetyHandle);
#endif

                        m_Boundaries[trackableId] = boundaryArray;
                    }
                }
                else
                {
                    Debug.LogError($"Got a vertex format other than Float3 trying to get AR plane geometry for {trackableId}");
                }

                return new BoundedPlane(trackableId, TrackableId.invalidId, pose, Vector2.zero, size, planeAlignment, trackingState, planeAnchor, planeClassification);
            }

            IEnumerable<BoundedPlane> ProcessPlaneArray(NativeArray<IntPtr> planeAnchors)
            {
                foreach (var planeAnchor in planeAnchors)
                {
                    // Trying to get plane info outside of update callback was failing, so create the bounded plane here,
                    // even though it might be removed or updated before we actually acquire it
                    var boundedPlane = GetBoundedPlane(planeAnchor);
                    var trackableId = boundedPlane.trackableId;
                    if (trackableId == TrackableId.invalidId)
                    {
                        Debug.LogError($"Trying to add AR plane anchor with invalid trackable id: {boundedPlane.trackableId}");
                        continue;
                    }

                    yield return boundedPlane;
                }
            }

            unsafe void ProcessPlaneUpdates(void* added_anchors, int added_anchor_count,
                void* updated_anchors, int updated_anchor_count, void* removed_anchors, int removed_anchor_count)
            {
                var planeAnchors = NativeApi.Utilities.ConvertExistingDataToNativeArrayWithSafetyHandle<IntPtr>(added_anchors, added_anchor_count, Allocator.None);
                foreach (var plane in ProcessPlaneArray(planeAnchors))
                {
                    // NB: Using a dictionary does not preserve update order
                    m_TempAddedPlanes[plane.trackableId] = plane;
                }

                planeAnchors = NativeApi.Utilities.ConvertExistingDataToNativeArrayWithSafetyHandle<IntPtr>(updated_anchors, updated_anchor_count, Allocator.None);
                foreach (var plane in ProcessPlaneArray(planeAnchors))
                {
                    // NB: Using a dictionary does not preserve update order
                    // If the plane is already in added planes, we haven't acquired it yet, so replace it instead of adding to updated planes
                    var trackableId = plane.trackableId;
                    if (m_TempAddedPlanes.ContainsKey(trackableId))
                        m_TempAddedPlanes[trackableId] = plane;
                    else
                        m_TempUpdatedPlanes[trackableId] = plane;
                }

                planeAnchors = NativeApi.Utilities.ConvertExistingDataToNativeArrayWithSafetyHandle<IntPtr>(removed_anchors, removed_anchor_count, Allocator.None);
                foreach (var plane in planeAnchors)
                {
                    var trackableId = NativeApi.Utilities.GetTrackableId(plane);
                    var removed = m_TempAddedPlanes.Remove(trackableId);
                    removed |= m_TempUpdatedPlanes.Remove(trackableId);

                    // Try removing from m_GeometryPointers anyway, but don't take that into account for tracking changes
                    if (m_Boundaries.TryGetValue(trackableId, out var boundaryArray))
                    {
                        boundaryArray.Dispose();
                        m_Boundaries.Remove(trackableId);
                    }

                    // Only add to removed planes if we have already acquired this plane (i.e. it does not exist in the other temp dictionaries)
                    if (!removed)
                        m_TempRemovedPlanes.Add(trackableId);
                }
            }

            // ReSharper restore InconsistentNaming

            public override void GetBoundary(
                TrackableId trackableId,
                Allocator allocator,
                ref NativeArray<Vector2> boundary)
            {
                if (!m_Boundaries.TryGetValue(trackableId, out var boundaryArray))
                {
                    Debug.LogError($"Trying to get AR plane boundary for {trackableId} but it does not exist in anchor dictionary");
                    return;
                }

                var vertexCount = boundaryArray.Length;
#if ARFOUNDATION_6_6_OR_NEWER
                Unity.XR.CoreUtils.NativeArrayUtils.EnsureExactSize(ref boundary, vertexCount, allocator);
#else
                CreateOrResizeNativeArrayIfNecessary(vertexCount, allocator, ref boundary);
#endif
                var transformPositionsHandle = new TransformBoundaryPositionsJob
                {
                    positionsIn = boundaryArray,
                    positionsOut = boundary
                }.Schedule(vertexCount, 1);

                new FlipBoundaryWindingJob
                {
                    positions = boundary
                }.Schedule(transformPositionsHandle).Complete();

                m_Boundaries.Remove(trackableId);
                boundaryArray.Dispose();
            }

            struct FlipBoundaryWindingJob : IJob
            {
                public NativeArray<Vector2> positions;

                public void Execute()
                {
                    var half = positions.Length / 2;
                    for (var i = 0; i < half; ++i)
                    {
                        var j = positions.Length - 1 - i;
                        (positions[i], positions[j]) = (positions[j], positions[i]);
                    }
                }
            }

            struct TransformBoundaryPositionsJob : IJobParallelFor
            {
                [ReadOnly]
                public NativeArray<Vector3> positionsIn;

                [WriteOnly]
                public NativeArray<Vector2> positionsOut;

                public void Execute(int index)
                {
                    positionsOut[index] = new Vector2(

                        // https://developer.apple.com/documentation/arkit/arplanegeometry/2941052-boundaryvertices?language=objc
                        // "The owning plane anchor's transform matrix defines the coordinate system for these points."
                        // It doesn't explicitly state the y component is zero, but that must be the case if the
                        // boundary points are in plane-space. Empirically, it has been true for horizontal and vertical planes.
                        // This IS explicitly true for the extents (see above) and would follow the same logic.
                        //
                        // Boundary vertices are in right-handed coordinates and clockwise winding order. To convert
                        // to left-handed, we flip the Z coordinate, but that also flips the winding, so we have to
                        // flip the winding back to clockwise by reversing the polygon index (j).
                        positionsIn[index].x,
                        -positionsIn[index].z);
                }
            }

            public override unsafe TrackableChanges<BoundedPlane> GetChanges(
                BoundedPlane defaultPlane,
                Allocator allocator)
            {
                try
                {
                    NativeApi.Utilities.DictionaryToNativeArray(m_TempAddedPlanes, ref m_AddedPlanes);
                    NativeApi.Utilities.DictionaryToNativeArray(m_TempUpdatedPlanes, ref m_UpdatedPlanes);
                    NativeApi.Utilities.HashSetToNativeArray(m_TempRemovedPlanes, ref m_RemovedPlanes);

                    var changes = new TrackableChanges<BoundedPlane>(
                        m_AddedPlanes.GetUnsafePtr(), m_TempAddedPlanes.Count,
                        m_UpdatedPlanes.GetUnsafePtr(), m_TempUpdatedPlanes.Count,
                        m_RemovedPlanes.GetUnsafePtr(), m_TempRemovedPlanes.Count,
                        defaultPlane, sizeof(BoundedPlane), allocator);

                    return changes;
                }
                finally
                {
                    ClearTempCollections();
                }
            }

            public override PlaneDetectionMode requestedPlaneDetectionMode
            {
                get => m_CurrentPlaneDetectionMode;
                set
                {
                    if (value == m_CurrentPlaneDetectionMode)
                        return;

                    m_CurrentPlaneDetectionMode = value;

                    // If configuration is null, we haven't started the subsystem yet. This is fine--we will use this plane detection mode when we start
                    if (m_PlaneDetectionConfiguration == IntPtr.Zero)
                        return;

                    // TODO: Do we need to restart the session when configurations change?
                    // NB: This assumes that AR_Plane_Alignment and PlaneDetectionMode line up exactly, which is currently the case. If this changes, we must
                    // explicitly define the conversion.
                    var nativeAlignment = (AR_Plane_Alignment)m_CurrentPlaneDetectionMode;
                    NativeApi.PlaneDetection.ar_plane_detection_configuration_set_alignment(m_PlaneDetectionConfiguration, nativeAlignment);
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
            var cinfo = new XRPlaneSubsystemDescriptor.Cinfo
            {
                id = planeSubsystemId,
                providerType = typeof(VisionOSPlaneProvider),
                subsystemTypeOverride = typeof(VisionOSPlaneSubsystem),
                supportsHorizontalPlaneDetection = true,

                // TODO: Does visionOS always support vertical planes?
                supportsVerticalPlaneDetection = true,
                supportsArbitraryPlaneDetection = false,

                // TODO: Does visionOS always support classification?
#if ARFOUNDATION_6_5_OR_NEWER
                supportsBoundaryVerticesDelegate = () => true,
                supportsClassificationDelegate = () => true
#else
                supportsBoundaryVertices = true,
                supportsClassification = true
#endif
            };

            XRPlaneSubsystemDescriptor.Register(cinfo);
        }
    }
}
