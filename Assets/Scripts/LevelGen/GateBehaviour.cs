using UnityEngine;
using ChogZombies.LevelGen;
using ChogZombies.Player;

namespace ChogZombies.LevelGen
{
    [RequireComponent(typeof(Collider))]
    public class GateBehaviour : MonoBehaviour
    {
        GateData _gate;
        bool _consumed;

        public void Initialize(GateData gate)
        {
            _gate = gate;
        }

        void OnTriggerEnter(Collider other)
        {
            if (_consumed)
                return;

            var player = other.GetComponentInParent<PlayerCombatController>();
            if (player != null)
            {
                _consumed = true;
                player.ApplyGate(_gate);

                // Feedback simple: on désactive la porte après utilisation
                gameObject.SetActive(false);
            }
        }
    }
}
