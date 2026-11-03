using UnityEngine;

namespace BreathingOrbDemo
{
    // Automatic pacing only. This does not detect the participant's breathing.
    [DisallowMultipleComponent]
    public sealed class BreathingOrb : MonoBehaviour
    {
        [Header("Timing (seconds)")]
        [Min(0.2f)] public float inhaleSeconds = 4f;
        [Min(0.2f)] public float exhaleSeconds = 6f;
        [Header("Diameter (Unity units)")]
        [Min(0.01f)] public float minimumDiameter = 0.85f;
        [Min(0.01f)] public float maximumDiameter = 1.2f;
        [Header("Display")]
        public bool showControls = true;
        public bool paused;
        public Renderer orbRenderer;
        [Range(0.1f, 2f)] public float glow = 0.8f;

        public float Breath01 { get; private set; }
        public bool IsInhaling { get; private set; }
        public float PhaseProgress { get; private set; }
        private double elapsed;
        private MaterialPropertyBlock properties;
        private static readonly int BreathId = Shader.PropertyToID("_Breath01");
        private static readonly int GlowId = Shader.PropertyToID("_Glow");

        private void OnEnable()
        {
            properties = new MaterialPropertyBlock();
            if (orbRenderer == null) orbRenderer = GetComponent<Renderer>();
            Apply();
        }

        private void Update()
        {
            if (!paused) elapsed += Time.deltaTime;
            Apply();
        }

        private void Apply()
        {
            double inhale = Mathf.Max(0.2f, inhaleSeconds);
            double exhale = Mathf.Max(0.2f, exhaleSeconds);
            double time = elapsed % (inhale + exhale);
            IsInhaling = time < inhale;
            PhaseProgress = (float)(IsInhaling ? time / inhale : (time - inhale) / exhale);
            // Half cosine: zero velocity at both ends, no abrupt size changes.
            float eased = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * PhaseProgress);
            Breath01 = IsInhaling ? eased : 1f - eased;
            float low = Mathf.Max(0.01f, minimumDiameter);
            float high = Mathf.Max(low, maximumDiameter);
            transform.localScale = Vector3.one * Mathf.Lerp(low, high, Breath01);
            if (orbRenderer == null) return;
            orbRenderer.GetPropertyBlock(properties);
            properties.SetFloat(BreathId, Breath01);
            properties.SetFloat(GlowId, glow);
            orbRenderer.SetPropertyBlock(properties);
        }

        private void OnGUI()
        {
            if (!showControls) return;
            Matrix4x4 oldMatrix = GUI.matrix;
            float uiScale = Mathf.Clamp(Screen.height / 900f, 0.55f, 1.7f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1));
            float width = Screen.width / uiScale;
            float height = Screen.height / uiScale;
            GUIStyle title = new GUIStyle(GUI.skin.label) { fontSize = 29, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = new Color(0.74f, 0.95f, 1f);
            GUIStyle subtitle = new GUIStyle(title) { fontSize = 14 };
            GUI.Label(new Rect(0, 34, width, 44), "B R E A T H I N G   O R B", title);
            GUI.Label(new Rect(0, 82, width, 26), "3D sphere  /  automatic animation demo", subtitle);
            string phase = paused ? "PAUSED" : IsInhaling ? "BREATHE IN" : "BREATHE OUT";
            GUI.Label(new Rect(0, height - 158, width, 44), phase, title);
            GUI.Label(new Rect(0, height - 112, width, 24), "Follow comfortably; this is not sensor feedback.", subtitle);
            Rect bar = new Rect(width / 2 - 120, height - 66, 240, 3);
            Color oldColor = GUI.color;
            GUI.color = new Color(0.12f, 0.27f, 0.33f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = new Color(0.45f, 0.87f, 0.95f);
            bar.width *= PhaseProgress;
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = oldColor;
            GUILayout.BeginArea(new Rect(24, 130, 225, 250), GUI.skin.box);
            GUILayout.Label("ANIMATION SETTINGS");
            GUILayout.Space(12);
            GUILayout.Label($"Inhale: {inhaleSeconds:F1} seconds");
            inhaleSeconds = GUILayout.HorizontalSlider(inhaleSeconds, 1f, 10f);
            GUILayout.Label($"Exhale: {exhaleSeconds:F1} seconds");
            exhaleSeconds = GUILayout.HorizontalSlider(exhaleSeconds, 1f, 12f);
            GUILayout.Label($"Glow: {glow:F2}");
            glow = GUILayout.HorizontalSlider(glow, 0.1f, 2f);
            GUILayout.Space(8);
            if (GUILayout.Button(paused ? "Resume" : "Pause")) paused = !paused;
            if (GUILayout.Button("Restart cycle")) { elapsed = 0; Apply(); }
            GUILayout.EndArea();
            GUI.matrix = oldMatrix;
        }
    }
}
