using System;
using System.Collections.Generic;
using AOT;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Scripting;
using UnityEngine.XR.ARSubsystems;

namespace UnityEngine.XR.VisionOS
{
    using SessionProvider = VisionOSSessionSubsystem.VisionOSSessionProvider;

    /// <summary>
    /// VisionOS implementation of the <c>XRObjectTrackingSubsystem</c>.
    /// </summary>
    [Preserve]
    public sealed class VisionOSObjectTrackingSubsystem : XRObjectTrackingSubsystem
    {
        internal const string k_ObjectTrackingSubsystemId = "VisionOS-ObjectTracking";
        const Feature k_ObjectTrackingFeature = Feature.ObjectTracking;

        internal void AddReferenceObject(XRReferenceObjectLibrary localLibrary, VisionOSReferenceObject referenceObject)
        {
            if (localLibrary == null)
                throw new ArgumentNullException(nameof(localLibrary));

            if (library == localLibrary)
            {
                VisionOSObjectTrackingProvider.AddReferenceObject(referenceObject);
            }
            else
            {
                Debug.LogWarning("You are attempting to add a VisionOSReferenceObject to a reference library that is not active on the current object tracking provider, this will be ignored.");
            }
        }

        class VisionOSObjectTrackingProvider : Provider, IVisionOSProvider
        {
            enum EnumeratorCallbackType
            {
                Added,
                Updated,
                Removed
            }

            const int k_InitialSize = 16;
            static VisionOSObjectTrackingProvider s_Instance;

            static readonly NativeApi.ObjectTracking.AR_Object_Tracking_Update_Handler_Function k_ObjectTrackingUpdateHandler = ObjectTrackingUpdateHandler;
            static readonly NativeApi.ObjectTracking.AR_Object_Anchors_Enumerator_Function k_ObjectAnchorsEnumerator = ObjectAnchorsEnumerator;

            IntPtr m_ObjectTrackingConfiguration;

            readonly Dictionary<TrackableId, XRTrackedObject> m_TempAddedObjects = new(k_InitialSize);
            readonly Dictionary<TrackableId, XRTrackedObject> m_TempUpdatedObjects = new(k_InitialSize);
            readonly HashSet<TrackableId> m_TempRemovedObjects = new(k_InitialSize);

            NativeArray<XRTrackedObject> m_AddedObjects = new(k_InitialSize, Allocator.Persistent);
            NativeArray<XRTrackedObject> m_UpdatedObjects = new(k_InitialSize, Allocator.Persistent);
            NativeArray<TrackableId> m_RemovedObjects = new(k_InitialSize, Allocator.Persistent);

            static int s_EnumeratorCount;
            static int s_EnumeratorCountMax;
            static EnumeratorCallbackType s_EnumeratorCallbackType;

            public AR_Authorization_Type RequiredAuthorizationType => NativeApi.ObjectTracking.ar_object_tracking_provider_get_required_authorization_type();
            public bool IsSupported
            {
                get
                {
                    if (VisionOS.IsSimulator())
                    {
                        return false;
                    }

                    return NativeApi.ObjectTracking.ar_object_tracking_provider_is_supported();
                }
            }
            public bool ShouldBeActive => running;
            public IntPtr CurrentProvider { get; private set; } = IntPtr.Zero;

            IntPtr m_ARSession = IntPtr.Zero;

            IntPtr m_NativeReferenceObjectCollection = IntPtr.Zero;

            Dictionary<IntPtr,Guid> m_ReferenceObjectToGUID = new();
            List<IntPtr> m_ReferenceObjectCollection = new();

            public bool TryStartNativeSession(Feature features)
            {
                if (!IsSupported)
                {
                    Debug.LogWarning("Object tracking provider is not supported");
                    return false;
                }

                // Check the library has been loaded and is non-empty
                if (m_ReferenceObjectCollection.Count == 0)
                {
                    Debug.LogWarning("Attempting to start visionOS object tracking provider with an empty library.");
                    return false;
                }

                // Early-out if provider is already running
                if (m_ARSession != IntPtr.Zero)
                    return true;

                // Configuration may have already been created by imageLibrary setter, otherwise no library is selected
                m_ObjectTrackingConfiguration = NativeApi.ObjectTracking.ar_object_tracking_configuration_create();
                if (m_ObjectTrackingConfiguration == IntPtr.Zero)
                {
                    Debug.LogWarning("Image tracking configuration is null.");
                    return false;
                }

                NativeApi.ObjectTracking.ar_object_tracking_configuration_add_reference_objects(m_ObjectTrackingConfiguration, m_NativeReferenceObjectCollection);

                CurrentProvider = NativeApi.ObjectTracking.ar_object_tracking_provider_create(m_ObjectTrackingConfiguration);
                if (CurrentProvider == IntPtr.Zero)
                {
                    Debug.LogWarning("Failed to create object tracking provider.");
                    return false;
                }

                NativeApi.ObjectTracking.ar_object_tracking_provider_set_update_handler_f(CurrentProvider, IntPtr.Zero,  IntPtr.Zero, k_ObjectTrackingUpdateHandler);

                Debug.Log("Starting object tracking provider.");
                m_ARSession = SessionProvider.StartProviderSession(CurrentProvider);
                return true;
            }

            public bool TryStopNativeSession()
            {
                // Early-out if provider has not been started
                if (m_ARSession == IntPtr.Zero)
                    return false;

                NativeApi.Session.ar_session_stop(m_ARSession);

                ClearTempCollections();
                ResetNativePointers();
                return true;
            }

            public void SetNativeProviderState(AR_Data_Provider_State newState)
            {
                // Do nothing; This provider does not poll for data
            }

            public override XRReferenceObjectLibrary library
            {
                set
                {
                    if (!IsSupported)
                    {
                        Debug.LogWarning("Object tracking is not supported");
                        return;
                    }

                    if (value == null)
                    {
                        Stop();
                    }
                    else
                    {
                        m_ReferenceObjectToGUID.Clear();
                        m_ReferenceObjectCollection.Clear();

                        m_NativeReferenceObjectCollection = NativeApi.ObjectTracking.ar_reference_objects_create();
                        foreach (var obj in value)
                        {
                            var entry = obj.FindEntry<VisionOSReferenceObjectEntry>();
                            if (entry != null)
                            {
                                var referenceObject = entry.GetVisionOSReferenceObject(obj);
                                if (referenceObject.IsLoaded)
                                {
                                    m_ReferenceObjectToGUID[referenceObject.AsIntPtr()] = obj.guid;
                                    m_ReferenceObjectCollection.Add(referenceObject.AsIntPtr());
                                    NativeApi.ObjectTracking.ar_reference_objects_add_object(m_NativeReferenceObjectCollection, referenceObject.AsIntPtr());
                                }
                            }
                        }
                    }
                }
            }

            public VisionOSObjectTrackingProvider()
            {
                s_Instance = this;
            }

            /// <summary>
            /// Invoked when <c>Start</c> is called on the subsystem. This method is only called if the subsystem was not previously running.
            /// </summary>
            public override void Start()
            {
                VisionOSProviderRegistration.RegisterProvider(k_ObjectTrackingFeature, this);
            }

            /// <summary>
            /// Invoked when <c>Stop</c> is called on the subsystem. This method is only called if the subsystem was previously running.
            /// </summary>
            public override void Stop()
            {
                VisionOSProviderRegistration.UnregisterProvider(k_ObjectTrackingFeature, this);
            }

            public override void Destroy()
            {
                // Try to stop the native session in case TryStop hasn't been called yet.
                if (!TryStopNativeSession())
                {
                    ResetNativePointers(); // Clear things out in case TryStopNativeSession didn't do its job previously
                    ClearTempCollections();
                }

                m_AddedObjects.Dispose();
                m_UpdatedObjects.Dispose();
                m_RemovedObjects.Dispose();
            }

            void ResetNativePointers()
            {
                m_ARSession = IntPtr.Zero;
                CurrentProvider = IntPtr.Zero;
            }

            void ClearTempCollections()
            {
                m_TempAddedObjects.Clear();
                m_TempUpdatedObjects.Clear();
                m_TempRemovedObjects.Clear();
            }

            // ReSharper disable InconsistentNaming
            [MonoPInvokeCallback(typeof(NativeApi.ObjectTracking.AR_Object_Tracking_Update_Handler_Function))]
            static void ObjectTrackingUpdateHandler(IntPtr context, IntPtr added_anchors, IntPtr updated_anchors, IntPtr removed_anchors)
            {
                // MonoPInvokeCallback methods will leak exceptions and cause crashes; always use a try/catch in these methods
                try
                {
                    var count = NativeApi.ObjectTracking.ar_object_anchors_get_count(added_anchors);
                    if (count > 0)
                    {
                        s_EnumeratorCount = 0;
                        s_EnumeratorCountMax = count;
                        s_EnumeratorCallbackType = EnumeratorCallbackType.Added;
                        NativeApi.ObjectTracking.ar_object_anchors_enumerate_anchors_f(added_anchors, context, k_ObjectAnchorsEnumerator);
                    }

                    count = NativeApi.ObjectTracking.ar_object_anchors_get_count(updated_anchors);
                    if (count > 0)
                    {
                        s_EnumeratorCount = 0;
                        s_EnumeratorCountMax = count;
                        s_EnumeratorCallbackType = EnumeratorCallbackType.Updated;
                        NativeApi.ObjectTracking.ar_object_anchors_enumerate_anchors_f(updated_anchors, context, k_ObjectAnchorsEnumerator);
                    }

                    count = NativeApi.ObjectTracking.ar_object_anchors_get_count(removed_anchors);
                    if (count > 0)
                    {
                        s_EnumeratorCount = 0;
                        s_EnumeratorCountMax = count;
                        s_EnumeratorCallbackType = EnumeratorCallbackType.Removed;
                        NativeApi.ObjectTracking.ar_object_anchors_enumerate_anchors_f(removed_anchors, context, k_ObjectAnchorsEnumerator);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            [MonoPInvokeCallback(typeof(NativeApi.ObjectTracking.AR_Object_Anchors_Enumerator_Function))]
            static bool ObjectAnchorsEnumerator(IntPtr context, IntPtr objectAnchor)
            {
                try
                {
                    s_Instance.ProcessObjectAnchorUpdates(context, objectAnchor, s_EnumeratorCallbackType);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }

                return ++s_EnumeratorCount < s_EnumeratorCountMax;
            }

            void ProcessObjectAnchorUpdates(IntPtr context, IntPtr objectAnchor, EnumeratorCallbackType type)
            {
                var trackableId = NativeApi.Utilities.GetTrackableId(objectAnchor);
                switch (type)
                {
                    case EnumeratorCallbackType.Added:
                        // NB: Using a dictionary does not preserve update order
                        m_TempAddedObjects[trackableId] = GetTrackedObject(context, objectAnchor, trackableId);
                        break;
                    case EnumeratorCallbackType.Updated:
                        // NB: Using a dictionary does not preserve update order
                        // If the probe is already in added probes, we haven't acquired it yet, so replace it instead of adding to updated probes
                        var trackedObject = GetTrackedObject(context, objectAnchor, trackableId);
                        if (m_TempAddedObjects.ContainsKey(trackableId))
                            m_TempAddedObjects[trackableId] = trackedObject;
                        else
                            m_TempUpdatedObjects[trackableId] = trackedObject;

                        break;
                    case EnumeratorCallbackType.Removed:
                        var removed = m_TempAddedObjects.Remove(trackableId);
                        removed |= m_TempAddedObjects.Remove(trackableId);

                        // Only add to removed objects if we have already acquired this object (i.e. it does not exist in the other temp dictionaries)
                        if (!removed)
                            m_TempRemovedObjects.Add(trackableId);

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            XRTrackedObject GetTrackedObject(IntPtr context, IntPtr objectAnchor, TrackableId trackableId)
            {
                var referenceObjectOnAnchor = NativeApi.ObjectTracking.ar_object_anchor_get_reference_object(objectAnchor);

                // The reference_object attached to the anchor will not match any of those generated when we load the library.
                // We need to use ar_reference_object_get_identifier to compare their internal uuid_t's for a match.
                var trackableGuid = Guid.Empty;
                foreach (var activeObject in m_ReferenceObjectCollection)
                {
                    if (NativeApi.ObjectTracking.UnityVisionOSCompareReferenceObjectUUIDs(activeObject, referenceObjectOnAnchor))
                    {
                        if (!m_ReferenceObjectToGUID.TryGetValue(activeObject, out var value))
                        {
                            Debug.LogError("Couldn't find reference object for tracked object.");
                            return default(XRTrackedObject);
                        }

                        trackableGuid = value;
                        break;
                    }
                }
                if (trackableGuid == Guid.Empty)
                {
                    Debug.LogError("Reference object on tracked anchor is not in the active library.");
                    return default(XRTrackedObject);
                }

                var pose = NativeApi.Utilities.GetWorldPose(objectAnchor);
                var isTracked = NativeApi.Anchor.ar_trackable_anchor_is_tracked(objectAnchor);

                return new XRTrackedObject(trackableId, pose, isTracked ? TrackingState.Tracking : TrackingState.Limited, context, trackableGuid);
            }

            public override unsafe TrackableChanges<XRTrackedObject> GetChanges(
                XRTrackedObject defaultTrackedObject,
                Allocator allocator)
            {
                try
                {
                    NativeApi.Utilities.DictionaryToNativeArray(m_TempAddedObjects, ref m_AddedObjects);
                    NativeApi.Utilities.DictionaryToNativeArray(m_TempUpdatedObjects, ref m_UpdatedObjects);
                    NativeApi.Utilities.HashSetToNativeArray(m_TempRemovedObjects, ref m_RemovedObjects);

                    var changes = new TrackableChanges<XRTrackedObject>(
                        m_AddedObjects.GetUnsafePtr(), m_TempAddedObjects.Count,
                        m_UpdatedObjects.GetUnsafePtr(), m_TempUpdatedObjects.Count,
                        m_RemovedObjects.GetUnsafePtr(), m_TempRemovedObjects.Count,
                        defaultTrackedObject, sizeof(XRTrackedObject), allocator);

                    return changes;
                }
                finally
                {
                    ClearTempCollections();
                }
            }

            void AddReferenceObjectNative(VisionOSReferenceObject referenceObject)
            {
                if (m_NativeReferenceObjectCollection == IntPtr.Zero)
                {
                    Debug.LogError("m_referenceObjectCollection hasn't been initialized yet.");
                }
                else
                {
                    NativeApi.ObjectTracking.ar_reference_objects_add_object(m_NativeReferenceObjectCollection,
                        referenceObject.AsIntPtr());
                }
            }

            internal static void AddReferenceObject(VisionOSReferenceObject referenceObject)
            {
                if (s_Instance == null)
                {
                    Debug.LogError("We don't have a VisionOSObjectTrackingProvider instance yet. ");
                    return;
                }

                s_Instance.AddReferenceObjectNative(referenceObject);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor()
        {
            XRObjectTrackingSubsystemDescriptor.Register(new XRObjectTrackingSubsystemDescriptor.Cinfo
            {
                id = k_ObjectTrackingSubsystemId,
                providerType = typeof(VisionOSObjectTrackingProvider),
                subsystemTypeOverride = typeof(VisionOSObjectTrackingSubsystem),
            });
        }
    }
}
