using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace BreathingOrbDemo
{
    // Prototype amplitude feedback, not a validated breathing phase detector.
    [DisallowMultipleComponent]
    public sealed class RealtimeBreathOrb : MonoBehaviour
    {
        public int listenPort = 5005;
        [Min(5)] public float calibrationSeconds = 30f;
        public bool invertSignal;
        [Min(0.01f)] public float minimumDiameter = 0.85f;
        [Min(0.01f)] public float maximumDiameter = 1.2f;
        [Min(0.01f)] public float smoothingSeconds = 0.15f;
        [Min(0.1f)] public float disconnectSeconds = 1.5f;
        [Range(0.1f, 2f)] public float glow = 0.8f;
        public bool showStatus = true;

        private readonly object gate = new object();
        private UdpClient udp;
        private Thread worker;
        private double receivedAt = -1;
        private float receivedValue;
        private long revision, consumedRevision;
        private string networkError;
        private readonly List<float> calibration = new List<float>();
        private double calibrationStart = -1;
        private bool calibrated, stale = true;
        private float lower, upper, breath01 = 0.5f, raw;
        private Renderer targetRenderer;
        private MaterialPropertyBlock block;
        private string status = "Waiting for UDP data";
        private static readonly int BreathId = Shader.PropertyToID("_Breath01");
        private static readonly int GlowId = Shader.PropertyToID("_Glow");
        private static double Now => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;

        private void OnEnable()
        {
            // Prevent the original timer and sensor feedback from both changing scale.
            BreathingOrb timer = GetComponent<BreathingOrb>();
            if (timer != null && timer.enabled) timer.enabled = false;
            targetRenderer = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
            lock (gate) { receivedAt = -1; revision = 0; networkError = null; }
            consumedRevision = 0;
            stale = true;
            ResetCalibration();
            try
            {
                udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, listenPort));
                UdpClient socket = udp;
                worker = new Thread(() => Receive(socket)) { IsBackground = true, Name = "Breath UDP" };
                worker.Start();
            }
            catch (Exception error)
            {
                udp?.Close(); udp = null;
                lock (gate) networkError = error.Message;
                UnityEngine.Debug.LogError("Breath UDP could not start: " + error.Message, this);
            }
        }

        private void Receive(UdpClient socket)
        {
            IPEndPoint sender = new IPEndPoint(IPAddress.Loopback, 0);
            while (true)
            {
                try
                {
                    byte[] bytes = socket.Receive(ref sender);
                    if (bytes.Length > 128 || !IPAddress.IsLoopback(sender.Address)) continue;
                    if (!float.TryParse(Encoding.ASCII.GetString(bytes).Trim(),
                        NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                        || float.IsNaN(value) || float.IsInfinity(value)) continue;
                    lock (gate) { receivedValue = value; receivedAt = Now; revision++; }
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException error)
                {
                    // Closing the socket during OnDisable is expected.
                    lock (gate) networkError = error.Message;
                    break;
                }
            }
        }

        private void ResetCalibration()
        {
            calibration.Clear(); calibrated = false; calibrationStart = -1;
        }

        private void Update()
        {
            double arrival; long currentRevision; string error;
            lock (gate) { raw = receivedValue; arrival = receivedAt; currentRevision = revision; error = networkError; }
            double now = Now;
            bool fresh = arrival >= 0 && now - arrival < disconnectSeconds;
            float target = 0.5f;
            if (!string.IsNullOrEmpty(error)) status = "UDP error: " + error;
            else if (!fresh)
            {
                status = arrival < 0 ? "Waiting for UDP data on 127.0.0.1:" + listenPort : "Signal lost - neutral size";
                stale = true;
            }
            else
            {
                if (stale) { ResetCalibration(); stale = false; }
                if (!calibrated)
                {
                    if (calibrationStart < 0) calibrationStart = now;
                    if (currentRevision != consumedRevision && calibration.Count < 10000) calibration.Add(raw);
                    double remaining = calibrationSeconds - (now - calibrationStart);
                    status = "Calibrating: breathe naturally (" + Math.Max(0, remaining).ToString("F0") + " s)";
                    if (remaining <= 0)
                    {
                        calibration.Sort();
                        if (calibration.Count >= 30)
                        {
                            lower = calibration[(int)((calibration.Count - 1) * 0.05f)];
                            upper = calibration[(int)((calibration.Count - 1) * 0.95f)];
                            calibrated = upper - lower > Math.Max(0.000001f, Math.Abs(lower) * 0.000001f);
                        }
                        if (!calibrated)
                        {
                            ResetCalibration();
                            status = "Calibration needs changing signal; retrying";
                        }
                    }
                }
                if (calibrated)
                {
                    target = Mathf.Clamp01((raw - lower) / (upper - lower));
                    if (invertSignal) target = 1 - target;
                    status = "Live amplitude feedback";
                }
            }
            consumedRevision = currentRevision;
            float alpha = 1 - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.01f, smoothingSeconds));
            breath01 = Mathf.Lerp(breath01, target, alpha);
            float low = Mathf.Max(0.01f, minimumDiameter);
            transform.localScale = Vector3.one * Mathf.Lerp(low, Mathf.Max(low, maximumDiameter), breath01);
            if (targetRenderer != null)
            {
                targetRenderer.GetPropertyBlock(block);
                block.SetFloat(BreathId, breath01); block.SetFloat(GlowId, glow);
                targetRenderer.SetPropertyBlock(block);
            }
        }

        private void OnGUI()
        {
            if (!showStatus) return;
            GUILayout.BeginArea(new Rect(20, 20, Mathf.Min(480, Screen.width - 40), 185), GUI.skin.box);
            GUILayout.Label("BREATH BELT - UDP FEEDBACK");
            GUILayout.Label(status);
            GUILayout.Label("Raw: " + raw.ToString("G7") + "   Amplitude: " + breath01.ToString("F2"));
            if (calibrated) GUILayout.Label("Calibration: " + lower.ToString("G6") + " to " + upper.ToString("G6"));
            invertSignal = GUILayout.Toggle(invertSignal, "Invert signal (if inhalation shrinks the orb)");
            if (GUILayout.Button("Recalibrate")) ResetCalibration();
            GUILayout.EndArea();
        }

        private void OnDisable()
        {
            udp?.Close(); udp = null;
            if (worker != null && worker.IsAlive) worker.Join(500);
            worker = null;
        }
    }
}
