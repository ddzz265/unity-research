using System;

namespace UnityEngine.XR.VisionOS.InputDevices
{
    /// <summary>
    /// Represents the modifier keys for a visionOS spatial pointer event.
    /// </summary>
    [Flags]
    public enum VisionOSSpatialPointerModifierKeys : ushort
    {
        /// <summary>
        /// The Caps Lock modifier key.
        /// </summary>
        CapsLock = 1,
        /// <summary>
        /// The Control modifier key.
        /// </summary>
        Control = 2,
        /// <summary>
        /// The Alt modifier key.
        /// </summary>
        Alt = 4,
        /// <summary>
        /// The Command modifier key.
        /// </summary>
        Command = 8,
        /// <summary>
        /// The Option modifier key.
        /// </summary>
        Option = 16,
        /// <summary>
        /// The Shift modifier key.
        /// </summary>
        Shift = 32,
        /// <summary>
        /// The Numeric Pad modifier key.
        /// </summary>
        NumericPad = 64,
        /// <summary>
        /// The Function modifier key.
        /// </summary>
        FunctionKey = 128,
    }
}
