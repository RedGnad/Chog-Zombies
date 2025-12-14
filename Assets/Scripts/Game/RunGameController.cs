using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using ChogZombies.LevelGen;
using ChogZombies.Player;
using ChogZombies.Enemies;

namespace ChogZombies.Game
{
    public class RunGameController : MonoBehaviour
    {
        public enum RunState
        {
            Playing,
            Won,
            Lost
        }

        [Header("References")]
        [SerializeField] LevelRuntimeVisualizer levelVisualizer;
        [SerializeField] PlayerCombatController player;

        [Header("Run Parameters")]
        [SerializeField] int startingLevelIndex = 1;
        [SerializeField] int seed = 12345;

        static int s_currentLevelIndex = 0;

        RunState _state = RunState.Playing;
        int _levelIndexUsed;
        BossBehaviour _boss;

        public static int CurrentLevelIndex => s_currentLevelIndex;
        public RunState State => _state;

        void Awake()
        {
            if (s_currentLevelIndex <= 0)
            {
                s_currentLevelIndex = Mathf.Max(1, startingLevelIndex);
            }
        }

        void Start()
        {
            _levelIndexUsed = s_currentLevelIndex;

            if (player == null)
            {
                player = FindObjectOfType<Player.PlayerCombatController>();
                if (player == null)
                {
                    Debug.LogWarning("RunGameController: aucun PlayerCombatController trouvé dans la scène.");
                }
            }

            if (levelVisualizer != null)
            {
                levelVisualizer.BuildWithParams(_levelIndexUsed, seed);
            }

            _boss = FindObjectOfType<BossBehaviour>();
        }

        void Update()
        {
            if (_state == RunState.Playing)
            {
                UpdateRunState();
            }

            HandleInput();
        }

        void UpdateRunState()
        {
            if (player != null && !player.IsAlive)
            {
                _state = RunState.Lost;
                Debug.Log("Run state: LOST");
                return;
            }

            if (_boss == null)
            {
                _boss = FindObjectOfType<BossBehaviour>();
            }

            if (_boss == null)
            {
                _state = RunState.Won;
                Debug.Log("Run state: WON");
            }
        }

        void HandleInput()
        {
            if (Keyboard.current == null)
                return;

            if (_state == RunState.Lost && Keyboard.current.rKey.wasPressedThisFrame)
            {
                ReloadSameLevel();
            }
            else if (_state == RunState.Won && Keyboard.current.nKey.wasPressedThisFrame)
            {
                NextLevel();
            }
        }

        void ReloadSameLevel()
        {
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex);
        }

        void NextLevel()
        {
            s_currentLevelIndex = _levelIndexUsed + 1;
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex);
        }
    }
}
