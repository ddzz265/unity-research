using System;
using UnityEngine;

namespace UnityEngine.XR.VisionOS
{
    /// <summary>
    /// Responsible for setting the target frame rate and repeat count when the application starts. Do not use this type directly. Instead, use visionOS XR
    /// Settings (Project Settings > XR Plug-in Management > visionOS) to enable or disable this, and set the actual target.
    /// </summary>
    [AddComponentMenu("")]
    public class VisionOSTargetFrameRateSetter : MonoBehaviour
    {
        IntPtr m_CurrentLayerRenderer = IntPtr.Zero;

#if !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnBeforeSceneLoad()
        {
            var settings = VisionOSRuntimeSettings.GetOrCreate();
            if (!settings.setTargetFrameRateOnStartup)
                return;

            var gameObject = new GameObject(nameof(VisionOSTargetFrameRateSetter));
            gameObject.AddComponent<VisionOSTargetFrameRateSetter>();
            DontDestroyOnLoad(gameObject);
        }
#endif

        void Start()
        {
            var settings = VisionOSRuntimeSettings.GetOrCreate();
            if (!settings.setTargetFrameRateOnStartup)
                return;

            var repeatCount = settings.initialMinimumFrameRepeatCount;
            var targetFrameRate = VisionOSRuntimeSettings.GetTargetFrameRateForRepeatCount(repeatCount);
            Debug.Log("Setting target frame rate");
            Application.targetFrameRate = targetFrameRate;
        }

        void Update()
        {
            // In case we somehow get another update before we're destroyed but after setting frame repeat count, early-out
            if (m_CurrentLayerRenderer != IntPtr.Zero)
                return;

            // We need to wait until we have a LayerRenderer to update the target frame repeat count
            m_CurrentLayerRenderer = VisionOS.GetLayerRenderer();
            if (m_CurrentLayerRenderer == IntPtr.Zero)
                return;

            var settings = VisionOSRuntimeSettings.GetOrCreate();
            VisionOS.SetMinimumFrameRepeatCount(m_CurrentLayerRenderer, settings.initialMinimumFrameRepeatCount);

            // We're done here. No more need to run this update method
            Destroy(gameObject);
        }
    }
}
