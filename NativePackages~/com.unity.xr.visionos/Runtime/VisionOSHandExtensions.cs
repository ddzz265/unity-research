#if INCLUDE_UNITY_XR_HANDS || PACKAGE_DOCS_GENERATION

using System;
using UnityEngine.XR.Hands;

namespace UnityEngine.XR.VisionOS
{
    /// <summary>
    /// Continues <c>XRHandJointID</c> with new, platform-specific
    /// values. Pass this to <c>xrHand.GetVisionOSJoint()</c>
    /// with an <c>XRHand</c> retrieved from an <c>XRHandSubsystem</c>.
    /// </summary>
    public enum VisionOSHandJointID
    {
        /// <summary>
        /// visionOS-specific joint for forearm-wrist.
        /// </summary>
        ForearmWrist = XRHandJointID.EndMarker,

        /// <summary>
        /// visionOS-specific joint for forearm-arm.
        /// </summary>
        ForearmArm,
    }

    /// <summary>
    /// Defines extension methods for platform-specific hand data.
    /// </summary>
    public static class VisionOSHandExtensions
    {
        internal class VisionOSHand
        {
            internal XRHandJoint GetJoint(VisionOSHandJointID jointID) => m_Joints[jointID.ToIndex() - XRHandJointID.EndMarker.ToIndex()];
            internal void SetJoint(XRHandJoint joint) => m_Joints[joint.id.ToIndex() - XRHandJointID.EndMarker.ToIndex()] = joint;

            XRHandJoint[] m_Joints = new XRHandJoint[NumVisionOSJoints];
        }

        /// <summary>
        /// The number of extra joints on visionOS
        /// </summary>
        public const int NumVisionOSJoints = 2;

        /// <summary>
        /// Call <c>.ToIndex()</c> on a <see cref="VisionOSHandJointID"/> to get its
        /// corresponding index into an array of joint data.
        /// </summary>
        /// <param name="jointId">ID of the joint to convert to an index.</param>
        /// <returns>
        /// The index matching the ID passed in.
        /// </returns>
        public static int ToIndex(this VisionOSHandJointID jointId) => (int)jointId - 1;

        /// <summary>
        /// Call this to get the corresponding <see cref="VisionOSHandJointID"/> from
        /// an index into an array of associated data.
        /// </summary>
        /// <param name="index">Index to convert to an ID.</param>
        /// <returns>
        /// The ID matching the index passed in.
        /// </returns>
        public static VisionOSHandJointID FromIndex(int index) => (VisionOSHandJointID)(index + 1);

        /// <summary>
        /// Gets the pose of the joint, if available, but without the
        /// Unity-defined change to the rotation to make the reported
        /// rotation cross-platform.
        /// </summary>
        /// <param name="joint">
        /// The joint this extension method extends. To call this extension
        /// method, write it like
        /// <c>myJoint.TryGetVisionOSRotation(out var rotation)</c>.
        /// </param>
        /// <param name="rotation">
        /// If this method returns <see langword="true"/>, this will be
        /// populated with the Apple-defined rotation for the given joint, but
        /// still converted to Unity space.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if successful and the rotation was available,
        /// <see langword="false"/> otherwise.
        /// </returns>
        public static bool TryGetVisionOSRotation(this XRHandJoint joint, out Quaternion rotation)
        {
            var rotations = joint.handedness == Handedness.Left ? k_LeftHandRotations : k_RightHandRotations;
            var nullableRotation = rotations[joint.id.ToIndex()];
            rotation = nullableRotation ?? Quaternion.identity;
            return nullableRotation.HasValue;
        }

        /// <summary>
        /// Gets the visinoOS-specific tracking state of the joint, if available.
        /// This tracking state will reflect whether or not the joint is visible.
        /// If the hand is tracked as a whole, every joint will have at least an
        /// estimated pose, regardless of whether it is visible. The trackingState
        /// exposed by XR Hands API will always report Pose tracking for every joint
        /// as long as the wrist is tracked, and this API can be used to differentiate
        /// joints that are visible from joints that are hidden from view.
        /// </summary>
        /// <param name="joint">
        /// The joint this extension method extends. To call this extension
        /// method, write it like
        /// <c>myJoint.TryGetVisionOSTrackingState(out var trackingState)</c>.
        /// </param>
        /// <param name="trackingState">
        /// If this method returns <see langword="true"/>, this will be
        /// populated with the Apple-defined tracking state for the given joint.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if successful and the rotation was available,
        /// <see langword="false"/> otherwise.
        /// </returns>
        public static bool TryGetVisionOSTrackingState(this XRHandJoint joint, out bool trackingState)
        {
            var trackingStates = joint.handedness == Handedness.Left ? k_LeftHandTrackingStates : k_RightHandTrackingStates;
            var nullableTrackingState = trackingStates[joint.id.ToIndex()];
            trackingState = nullableTrackingState ?? false;
            return nullableTrackingState.HasValue;
        }

        /// <summary>
        /// Retrieves a visionOS-specific hand joint by its ID.
        /// </summary>
        /// <param name="hand">
        /// The hand this extension method extends. To call this extension
        /// method, write it like
        /// <c>myHand.GetVisionOSJoint(VisionOSHandJointID.ForearmArm)</c>.
        /// </param>
        /// <param name="jointID">
        /// ID of the required joint.
        /// </param>
        /// <returns>
        /// The <c>XRHandJoint</c> corresponding the ID passed in.
        /// </returns>
        /// <exception>
        /// Will throw an exception if <paramref name="jointID"/> is not a named
        /// value of <see cref="VisionOSHandJointID"/>. Will also throw an exception
        /// if <paramref name="hand"/> was not retrieved from an <c>XRHandSubsystem</c>.
        /// </exception>
        public static XRHandJoint GetVisionOSJoint(this XRHand hand, VisionOSHandJointID jointID)
        {
            return hand.handedness switch
            {
                Handedness.Left => leftHand.GetJoint(jointID),
                Handedness.Right => rightHand.GetJoint(jointID),
                _ => throw new ArgumentException(
                    "Invalid hand - only use XRHands that are retrieved from either the leftHand or rightHand property of an XRHandSubsystem!")
            };
        }

        /// <summary>
        /// Set joint data on the specified hand
        /// </summary>
        /// <param name="handedness">The hand this joint belongs to</param>
        /// <param name="joint">The data for this joint. The ID of the joint must be in the range of
        /// <see cref="VisionOSHandJointID"/> values or this will throw.</param>
        public static void SetVisionOSJoint(Handedness handedness, XRHandJoint joint)
        {
            var visionOSHand = handedness == Handedness.Left ? leftHand : rightHand;
            visionOSHand.SetJoint(joint);
        }

        /// <summary>
        /// Set visionOS specific rotation per joint.
        /// </summary>
        /// <param name="handedness">The hand this joint belongs to</param>
        /// <param name="joint">The data for this joint. The ID of the joint must be in the range of
        /// <see cref="VisionOSHandJointID"/> values or this will throw.</param>
        /// <param name="rotation">The Apple-defined rotation for the given joint, but
        /// still converted to Unity space.</param>
        public static void SetVisionOSRotation(Handedness handedness, XRHandJoint joint, Quaternion? rotation)
        {
            var rotations = handedness == Handedness.Left ? k_LeftHandRotations : k_RightHandRotations;
            rotations[joint.id.ToIndex()] = rotation;
        }

        /// <summary>
        /// Set visionOS specific tracking state per joint.
        /// </summary>
        /// <param name="handedness">The hand this joint belongs to</param>
        /// <param name="joint">The data for this joint. The ID of the joint must be in the range of
        /// <see cref="VisionOSHandJointID"/> values or this will throw.</param>
        /// <param name="trackingState">The Apple-defined tracking state for the given joint.</param>
        public static void SetVisionOSTrackingState(Handedness handedness, XRHandJoint joint, bool? trackingState)
        {
            var trackingStates = handedness == Handedness.Left ? k_LeftHandTrackingStates : k_RightHandTrackingStates;
            trackingStates[joint.id.ToIndex()] = trackingState;
        }

        internal static VisionOSHand leftHand { get; } = new();
        internal static VisionOSHand rightHand { get; } = new();

        internal static Quaternion?[] GetVisionOSRotations(Handedness handedness) => handedness == Handedness.Left ? k_LeftHandRotations : k_RightHandRotations;
        internal static bool?[] GetVisionOSTrackingStates(Handedness handedness) => handedness == Handedness.Left ? k_LeftHandTrackingStates : k_RightHandTrackingStates;

        // extra +2 for both for the visionOS-specific joints (forearm-wrist and forearm-arm)
        static readonly Quaternion?[] k_LeftHandRotations = new Quaternion?[XRHandJointID.EndMarker.ToIndex() + NumVisionOSJoints];
        static readonly Quaternion?[] k_RightHandRotations = new Quaternion?[XRHandJointID.EndMarker.ToIndex() + NumVisionOSJoints];

        static readonly bool?[] k_LeftHandTrackingStates = new bool?[XRHandJointID.EndMarker.ToIndex() + NumVisionOSJoints];
        static readonly bool?[] k_RightHandTrackingStates = new bool?[XRHandJointID.EndMarker.ToIndex() + NumVisionOSJoints];
    }
}
#endif // INCLUDE_UNITY_XR_HANDS || PACKAGE_DOCS_GENERATION
