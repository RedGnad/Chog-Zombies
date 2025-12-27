using UnityEngine;
using ChogZombies.Player;
using ChogZombies.Enemies;
using ChogZombies.Game;

namespace ChogZombies.UI
{
    public class HUDController_TMP : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] PlayerCombatController player;
        [SerializeField] TMPro.TextMeshProUGUI soldiersText;
        [SerializeField] TMPro.TextMeshProUGUI bossHpText;
        [SerializeField] TMPro.TextMeshProUGUI levelText;
        [SerializeField] TMPro.TextMeshProUGUI goldText;
        [SerializeField] TMPro.TextMeshProUGUI stateText;
        [SerializeField] TMPro.TextMeshProUGUI hintText;

        BossBehaviour _boss;
        RunGameController _run;

        void Start()
        {
            if (player == null)
            {
                player = FindObjectOfType<PlayerCombatController>();
            }

            _run = FindObjectOfType<RunGameController>();
        }

        void Update()
        {
            UpdatePlayerSection();
            UpdateBossSection();
            UpdateRunSection();
        }

        void UpdatePlayerSection()
        {
            if (soldiersText == null)
                return;

            if (player == null)
            {
                soldiersText.text = "Power: -";
                return;
            }

            soldiersText.text = $"Power: {player.SoldierCount}";
        }

        void UpdateBossSection()
        {
            if (bossHpText == null)
                return;

            if (_boss == null)
            {
                _boss = FindObjectOfType<BossBehaviour>();
            }

            if (_boss == null)
            {
                bossHpText.text = "Boss: -";
            }
            else
            {
                bossHpText.text = $"Boss HP: {_boss.CurrentHp}/{_boss.MaxHp}";
            }
        }

        void UpdateRunSection()
        {
            if (_run == null)
            {
                _run = FindObjectOfType<RunGameController>();
            }

            if (levelText != null)
            {
                int level = RunGameController.CurrentLevelIndex;
                levelText.text = level > 0 ? $"Level: {level}" : "Level: -";
            }

            if (goldText != null)
            {
                goldText.text = $"Gold: {RunGameController.CurrentGold}";
            }

            if (stateText != null)
            {
                if (_run == null)
                {
                    stateText.text = "";
                }
                else
                {
                    stateText.text = _run.State.ToString();
                }
            }

            if (hintText != null)
            {
                if (_run == null)
                {
                    hintText.text = "";
                    return;
                }

                switch (_run.State)
                {
                    case RunGameController.RunState.Playing:
                        hintText.text = "";
                        break;
                    case RunGameController.RunState.Won:
                        hintText.text = "Victory! Press N for next level.";
                        break;
                    case RunGameController.RunState.Lost:
                        hintText.text = "Defeat. Press R to retry.";
                        break;
                }
            }
        }
    }
}
