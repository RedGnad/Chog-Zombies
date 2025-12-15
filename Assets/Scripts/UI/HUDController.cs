using UnityEngine;
using UnityEngine.UI;
using ChogZombies.Player;
using ChogZombies.Enemies;
using ChogZombies.Game;

namespace ChogZombies.UI
{
    public class HUDController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] PlayerCombatController player;
        [SerializeField] Text soldiersText;
        [SerializeField] Text bossHpText;
        [SerializeField] Text levelText;
        [SerializeField] Text goldText;
        [SerializeField] Text stateText;
        [SerializeField] Text hintText;

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
                soldiersText.text = "Puissance: -";
                return;
            }

            soldiersText.text = $"Puissance: {player.SoldierCount}";
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
                levelText.text = level > 0 ? $"Niveau: {level}" : "Niveau: -";
            }

            if (goldText != null)
            {
                goldText.text = $"Or: {RunGameController.CurrentGold}";
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
                        hintText.text = "Victoire ! Appuie sur N pour niveau suivant.";
                        break;
                    case RunGameController.RunState.Lost:
                        hintText.text = "Défaite. Appuie sur R pour rejouer.";
                        break;
                }
            }
        }
    }
}
