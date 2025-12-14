using UnityEngine;

namespace ChogZombies.Player
{
    public class AutoRunner : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] float speed = 5f;
        [SerializeField] Vector3 direction = Vector3.forward;
        [SerializeField] bool useWorldSpace = true;

        public bool Enabled { get; set; } = true;

        public void Stop()
        {
            Enabled = false;
        }

        void Update()
        {
            if (!Enabled)
                return;

            // On verrouille la position Y pour éviter toute dérive verticale
            Vector3 pos = transform.position;
            float fixedY = pos.y;

            Vector3 dir = direction;
            if (!useWorldSpace)
            {
                dir = transform.TransformDirection(direction);
            }

            pos += dir.normalized * speed * Time.deltaTime;
            pos.y = fixedY;
            transform.position = pos;
        }
    }
}
