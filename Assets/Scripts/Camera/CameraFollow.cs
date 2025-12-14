using UnityEngine;

namespace ChogZombies.CameraSystem
{
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new Vector3(0f, 5f, -10f);
        [SerializeField] float smoothTime = 0.15f;
        [SerializeField] bool lockX = false;
        [SerializeField] bool lockY = false;

        Vector3 _velocity;

        void LateUpdate()
        {
            if (target == null)
                return;

            Vector3 desired = target.position + offset;
            Vector3 current = transform.position;

            if (lockX)
                desired.x = current.x;
            if (lockY)
                desired.y = current.y;

            transform.position = Vector3.SmoothDamp(current, desired, ref _velocity, smoothTime);
        }

        public void SetTarget(Transform t)
        {
            target = t;
        }
    }
}
