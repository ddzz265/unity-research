using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AcrophobiaVR.Navigation
{
    /// <summary>
    /// Full-screen black overlay used to mask tier-to-tier teleports. Expects a
    /// world-space or screen-space-camera Canvas with a full-screen Image assigned here.
    /// Kept as a simple opacity fade — no motion, no camera shake, per the plan's note
    /// that comfort confounds should be minimized so they don't contaminate the fear signal.
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        [Tooltip("Full-screen black Image. Should start fully transparent (alpha 0).")]
        public Image fadeImage;
        // Local integration: the fixed session clock supplies pause-aware fade progress.
        public void ApplyOpacity(float alpha) => SetAlpha(Mathf.Clamp01(alpha));

        public IEnumerator FadeOut(float duration) => Fade(0f, 1f, duration);
        public IEnumerator FadeIn(float duration) => Fade(1f, 0f, duration);

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (fadeImage == null) yield break;

            SetAlpha(from);
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                SetAlpha(Mathf.Lerp(from, to, duration <= 0f ? 1f : t / duration));
                yield return null;
            }
            SetAlpha(to);
        }

        private void SetAlpha(float a)
        {
            var c = fadeImage.color;
            c.a = a;
            fadeImage.color = c;
        }
    }
}
