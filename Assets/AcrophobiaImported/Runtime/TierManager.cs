using AcrophobiaVR.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
namespace AcrophobiaVR.Navigation
{
    /// <summary>
    /// Drives tier-to-tier teleportation and the always-available forfeit control called
    /// for in the plan's ethics/safety section. Deliberately NOT the standard XR
    /// Interaction Toolkit teleport-by-pointing mechanic — this is scripted point-to-point
    /// movement triggered by UI buttons, since the player should never have to
    /// climb/navigate stairs between tiers.
    ///
    /// Attach to a persistent manager object in the scene. Wire tierAnchors in the
    /// Inspector (or use TierUIBuilder's auto-generation), or point xrOrigin at your rig root.
    /// </summary>
    public class TierManager : MonoBehaviour
    {
        [Header("Haptics")]
        public HapticImpulsePlayer leftHaptics;
        public HapticImpulsePlayer rightHaptics;

        [ContextMenu("Start Simulation")]
        private void ContextStartSimulation()
        {
            StartSimulation();
        }

        [ContextMenu("Go To Tier 1")]
        private void ContextTier1()
        {
            GoToTier(0);
        }

        [ContextMenu("Go To Tier 2")]
        private void ContextTier2()
        {
            GoToTier(1);
        }

        [ContextMenu("Go To Next Tier")]
        private void ContextNextTier()
        {
            GoToNextTier();
        }

        [ContextMenu("Forfeit")]
        private void ContextForfeit()
        {
            Forfeit();
        }

        [Header("UI")]
        public GameObject startCanvas;

        [Header("Rig")]
        [Tooltip("Root transform of the XR rig that should be moved (e.g. 'XR Origin' or your 'XR Interaction Simulator').")]
        public Transform xrOrigin;

        [Header("UI Panels")]
        [SerializeField]
        private List<TowerUIPanel> tierPanels = new();
        // Local integration: use the original fixed panel for each floor.
        public void ConfigurePanels(IList<TowerUIPanel> panels)
        {
            tierPanels.Clear();
            tierPanels.AddRange(panels);
        }

        [Tooltip("The tracked camera (head) under xrOrigin — used to preserve the player's real-world offset within the play area when teleporting.")]
        public Transform xrCamera;

        [Header("Destinations")]
        [Tooltip("One anchor per tier, in ascending order, matching TowerConfig.floorTiers. Position each near the bridge entrance for that tier.")]
        public List<Transform> tierAnchors = new List<Transform>();

        [Tooltip("Ground-level safe room anchor. The forfeit control always sends the player here regardless of current tier.")]
        public Transform safeRoomAnchor;

        [Header("Transition")]
        [Tooltip("Screen fader used to mask the teleport pop, reducing the disorientation/sickness confound the plan calls out.")]
        public ScreenFader screenFader;

        [Tooltip("Seconds to fade to black / back in, each direction.")]
        public float fadeDuration = 0.25f;

        [Header("Events")]
        [Tooltip("Fired after arriving at a new tier, with the new tier index (0-based).")]
        public UnityEvent<int> onTierChanged;

        [Tooltip("Fired after a forfeit completes (player back in the safe room).")]
        public UnityEvent onForfeit;

        public int CurrentTierIndex { get; private set; } = -1;
        public bool IsTransitioning { get; private set; }

        public bool HasNextTier => CurrentTierIndex + 1 < tierAnchors.Count;

        private void Reset()
        {
            if (xrOrigin != null && xrCamera == null)
                xrCamera = xrOrigin.GetComponentInChildren<Camera>()?.transform;
        }

        public void StartSimulation()
        {
            Debug.Log("StartSimulation called!");

            if (IsTransitioning)
                return;

            if (startCanvas != null)
                startCanvas.SetActive(false);

            GoToTier(0);
        }

        /// <summary>Call from the "Next Tier" UI button.</summary>
        public void GoToNextTier()
        {
            if (IsTransitioning) return;

            if (!HasNextTier)
            {
                Debug.Log("[TierManager] Already at the top tier — no further tier to advance to.");
                return;
            }
            
            Transform destination = tierAnchors[CurrentTierIndex + 1];

            if (destination == null)
            {
                Debug.LogError("Tier Anchor missing.");
                return;
            }

            StartCoroutine(TeleportRoutine(destination, CurrentTierIndex + 1));
        }

        /// <summary>Call from the "Forfeit" UI button. Always available, any tier, any time.</summary>
        public void Forfeit()
        {
            if (IsTransitioning) return;

            if (safeRoomAnchor == null)
            {
                Debug.LogError("[TierManager] No safeRoomAnchor assigned — cannot forfeit safely.");
                return;
            }

            StartCoroutine(ForfeitRoutine());
        }

        /// <summary>Jump directly to a specific tier index, e.g. for playtesting/QA shortcuts.</summary>
        public void GoToTier(int tierIndex)
        {
            if (IsTransitioning) return;
            if (tierIndex < 0 || tierIndex >= tierAnchors.Count)
            {
                Debug.LogError($"[TierManager] Tier index {tierIndex} out of range (0-{tierAnchors.Count - 1}).");
                return;
            }

            StartCoroutine(TeleportRoutine(tierAnchors[tierIndex], tierIndex));
        }

        private IEnumerator TeleportRoutine(Transform destination, int newTierIndex)
        {
            Debug.Log($"TeleportRoutine started. Tier = {newTierIndex}");

            IsTransitioning = true;

            if (screenFader != null)
                yield return screenFader.FadeOut(fadeDuration);

            MoveRigTo(destination);

            leftHaptics?.SendHapticImpulse(
                0.18f,
                0.08f);

            rightHaptics?.SendHapticImpulse(
                0.18f,
                0.08f);
            CurrentTierIndex = newTierIndex;

            Debug.Log($"tierPanels.Count = {tierPanels.Count}");

            for (int i = 0; i < tierPanels.Count; i++)
            {
                Debug.Log($"Element {i}: {(tierPanels[i] == null ? "NULL" : tierPanels[i].name)}");
            }

            foreach (var panel in tierPanels)
            {
                if (panel != null)
                    panel.gameObject.SetActive(false);
            }

            if (newTierIndex >= 0 && newTierIndex < tierPanels.Count)
            {
                tierPanels[newTierIndex].gameObject.SetActive(true);
                Debug.Log(
    $"{tierPanels[newTierIndex].name} activeSelf = {tierPanels[newTierIndex].gameObject.activeSelf}, " +
    $"activeInHierarchy = {tierPanels[newTierIndex].gameObject.activeInHierarchy}");
            }
            else
            {
                Debug.LogError($"No TierUIPanel assigned for tier {newTierIndex}");
            }

            if (screenFader != null)
                yield return screenFader.FadeIn(fadeDuration);

            IsTransitioning = false;
            onTierChanged?.Invoke(newTierIndex);
        }

        private IEnumerator ForfeitRoutine()
        {
            IsTransitioning = true;

            if (screenFader != null)
                yield return screenFader.FadeOut(fadeDuration);

            MoveRigTo(safeRoomAnchor);
            CurrentTierIndex = -1;

            if (screenFader != null)
                yield return screenFader.FadeIn(fadeDuration);

            IsTransitioning = false;
            onForfeit?.Invoke();
        }

        /// <summary>
        /// Repositions the rig so the player's feet land at the destination while
        /// preserving their real-world horizontal offset within the tracked play area —
        /// same principle XRI's teleportation provider uses, just triggered by UI instead
        /// of a pointer. Does not change rig rotation, so the player isn't snap-turned
        /// against their will on arrival.
        /// </summary>
        private void MoveRigTo(Transform destination)
        {
            if (xrOrigin == null || destination == null) return;

            Vector3 cameraOffset = Vector3.zero;
            if (xrCamera != null)
            {
                cameraOffset = xrCamera.position - xrOrigin.position;
                cameraOffset.y = 0f; // vertical placement comes entirely from the destination + real head height
            }

            xrOrigin.position = destination.position - cameraOffset;
            Vector3 euler = xrOrigin.eulerAngles;
            euler.y = destination.eulerAngles.y;
            xrOrigin.eulerAngles = euler;
        }
    }
}
