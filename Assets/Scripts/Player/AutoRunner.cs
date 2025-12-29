using UnityEngine;

namespace ChogZombies.Player
{
    public class AutoRunner : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] float speed = 5f;
        [SerializeField] Vector3 direction = Vector3.forward;
        [SerializeField] bool useWorldSpace = true;
        [SerializeField] bool clampAtMaxWorldZ = true;

        public bool Enabled { get; set; } = true;
        float _maxWorldZ = float.PositiveInfinity;

        public void Stop()
        {
            Enabled = false;
        }

        public void SetMaxWorldZ(float value)
        {
            _maxWorldZ = value;
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

            if (clampAtMaxWorldZ && pos.z >= _maxWorldZ)
            {
                pos.z = Mathf.Min(pos.z, _maxWorldZ);
                transform.position = pos;
                Enabled = false;
                return;
            }

            transform.position = pos;
        }
    }
}
