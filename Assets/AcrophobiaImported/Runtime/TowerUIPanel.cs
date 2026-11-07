using AcrophobiaVR.Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AcrophobiaVR.UI
{
    /// <summary>
    /// Binds a world-space UI panel's buttons to a TierManager. Designed to be built by
    /// TierUIBuilder (Editor), but works with any Canvas hierarchy as long as the
    /// references below are assigned.
    /// </summary>
    public class TowerUIPanel : MonoBehaviour
    {
        [Header("References")]
        public TierManager tierManager;
        public Button nextTierButton;
        public Button forfeitButton;
        public TMP_Text tierLabel;

        [Header("Labels")]
        public string tierLabelFormat = "第 {0} 层 / 共 {1} 层";
        public string topTierLabelSuffix = "（顶层）";

        private void OnEnable()
        {
            if (nextTierButton != null)
                nextTierButton.onClick.AddListener(HandleNextTier);
            if (forfeitButton != null)
                forfeitButton.onClick.AddListener(HandleForfeit);
            if (tierManager != null)
                tierManager.onTierChanged.AddListener(HandleTierChanged);

            RefreshLabel();
        }

        private void OnDisable()
        {
            if (nextTierButton != null)
                nextTierButton.onClick.RemoveListener(HandleNextTier);
            if (forfeitButton != null)
                forfeitButton.onClick.RemoveListener(HandleForfeit);
            if (tierManager != null)
                tierManager.onTierChanged.RemoveListener(HandleTierChanged);
        }

        private void HandleNextTier() => tierManager?.GoToNextTier();
        private void HandleForfeit() => tierManager?.Forfeit();

        private void HandleTierChanged(int _)
        {
            RefreshLabel();
        }

        private void RefreshLabel()
        {
            if (tierManager == null) return;

            int current = tierManager.CurrentTierIndex;
            int total = tierManager.tierAnchors.Count;

            if (tierLabel != null)
            {
                string text = current < 0
                    ? "地面层"
                    : string.Format(tierLabelFormat, current + 1, total);

                if (!tierManager.HasNextTier)
                    text += topTierLabelSuffix;

                tierLabel.text = text;
            }

            if (nextTierButton != null)
                nextTierButton.interactable = tierManager.HasNextTier && !tierManager.IsTransitioning;

            if (forfeitButton != null)
                forfeitButton.interactable = !tierManager.IsTransitioning;
        }

        private void Update()
        {
            // Cheap and simple: keep button interactability in sync with transition state
            // without needing extra events for it. Panels like this render infrequently
            // enough that a per-frame check here is not a meaningful cost.
            if (tierManager == null) return;

            if (nextTierButton != null)
                nextTierButton.interactable = tierManager.HasNextTier && !tierManager.IsTransitioning;
            if (forfeitButton != null)
                forfeitButton.interactable = !tierManager.IsTransitioning;
        }
    }
}
