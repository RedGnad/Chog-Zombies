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

                // Consommer également les autres portes du même segment pour éviter les doubles choix.
                ConsumeSiblingGatesInSameSegment();

                player.ApplyGate(_gate);

                // Feedback simple: on désactive la porte après utilisation
                gameObject.SetActive(false);
            }
        }

        void ConsumeSiblingGatesInSameSegment()
        {
            // Les deux portes d'un segment partagent la même position Z (au bruit numérique près).
            float z = transform.position.z;
            const float sameSegmentThreshold = 0.5f;

            var gates = FindObjectsOfType<GateBehaviour>();
            for (int i = 0; i < gates.Length; i++)
            {
                var other = gates[i];
                if (other == null || other == this)
                    continue;

                if (other._consumed)
                    continue;

                if (Mathf.Abs(other.transform.position.z - z) <= sameSegmentThreshold)
                {
                    other._consumed = true;
                    other.gameObject.SetActive(false);
                }
            }
        }
    }
}
