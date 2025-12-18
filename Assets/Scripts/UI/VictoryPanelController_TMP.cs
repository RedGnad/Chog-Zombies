using UnityEngine;
using ChogZombies.Game;

namespace ChogZombies.UI
{
    public class VictoryPanelController_TMP : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] RunGameController runGame;

        [Header("UI")]
        [SerializeField] GameObject victoryPanelRoot;
        [SerializeField] GameObject bossLootVrfButtonRoot;
        [SerializeField] GameObject rerollRunSeedButtonRoot;

        bool _warnedMissingRunGame;
        bool _warnedMissingVictoryRoot;

        void Start()
        {
            if (runGame == null)
                runGame = FindObjectOfType<RunGameController>();

            if (victoryPanelRoot == null && !_warnedMissingVictoryRoot)
            {
                _warnedMissingVictoryRoot = true;
                Debug.LogWarning("[VictoryPanelController_TMP] victoryPanelRoot is not assigned. Victory panel cannot be shown.");
            }

            Refresh();
        }

        void Update()
        {
            Refresh();
        }

        void Refresh()
        {
            if (runGame == null)
                runGame = FindObjectOfType<RunGameController>();

            if (runGame == null && !_warnedMissingRunGame)
            {
                _warnedMissingRunGame = true;
                Debug.LogWarning("[VictoryPanelController_TMP] RunGameController not found in scene. Victory panel cannot be driven.");
            }

            bool shouldShowVictory = runGame != null && runGame.State == RunGameController.RunState.Won;

            if (victoryPanelRoot != null)
            {
                if (victoryPanelRoot.activeSelf != shouldShowVictory)
                    victoryPanelRoot.SetActive(shouldShowVictory);
            }

            if (bossLootVrfButtonRoot != null)
            {
                bool showBossLootButton = runGame != null && runGame.IsBossLootVrfAvailable;
                if (bossLootVrfButtonRoot.activeSelf != showBossLootButton)
                    bossLootVrfButtonRoot.SetActive(showBossLootButton);
            }

            if (rerollRunSeedButtonRoot != null)
            {
                bool showRerollRunSeedButton = runGame != null && runGame.IsRerollRunSeedAvailable;
                if (rerollRunSeedButtonRoot.activeSelf != showRerollRunSeedButton)
                    rerollRunSeedButtonRoot.SetActive(showRerollRunSeedButton);
            }
        }
    }
}
