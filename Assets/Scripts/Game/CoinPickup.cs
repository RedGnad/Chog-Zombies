using UnityEngine;
using ChogZombies.Player;
using ChogZombies.Game;

namespace ChogZombies.Game
{
    [RequireComponent(typeof(Collider))]
    public class CoinPickup : MonoBehaviour
    {
        [SerializeField] int goldAmount = 1;
        [SerializeField] float rotationSpeed = 90f;

        void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        void Update()
        {
            if (rotationSpeed != 0f)
            {
                transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.World);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<PlayerCombatController>();
            if (player == null)
                return;

            var run = FindObjectOfType<RunGameController>();
            if (run == null)
                return;

            if (goldAmount > 0)
            {
                run.AddGold(goldAmount);
            }

            Destroy(gameObject);
        }
    }
}
