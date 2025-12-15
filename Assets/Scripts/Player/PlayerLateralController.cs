using UnityEngine;
using UnityEngine.InputSystem;

namespace ChogZombies.Player
{
    public class PlayerLateralController : MonoBehaviour
    {
        [Header("Lateral Movement")]
        [SerializeField] float lateralSpeed = 5f;
        [SerializeField] float maxOffsetX = 5f;

        public float MaxOffsetX => maxOffsetX;

        void Start()
        {
            var pos = transform.position;
            pos.x = 0f;
            transform.position = pos;
        }

        void Update()
        {
            Vector3 pos = transform.position;

            float input = 0f;

            // 1) clavier via nouveau Input System
            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                    input -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                    input += 1f;
            }

            if (Mathf.Abs(input) > 0.01f)
            {
                pos.x += input * lateralSpeed * Time.deltaTime;
            }
            // 2) souris uniquement pendant un clic (sinon on conserve la position)
            else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                var mousePos = Mouse.current.position.ReadValue();
                var cam = Camera.main;

                if (cam != null)
                {
                    var ray = cam.ScreenPointToRay(mousePos);
                    var plane = new Plane(Vector3.up, new Vector3(0f, pos.y, 0f));
                    if (plane.Raycast(ray, out float enter))
                    {
                        var hit = ray.GetPoint(enter);
                        float target = Mathf.Clamp(hit.x, -maxOffsetX, maxOffsetX);
                        pos.x = Mathf.Lerp(pos.x, target, Time.deltaTime * lateralSpeed);
                    }
                }
                else
                {
                    float t = Mathf.Clamp01(mousePos.x / Mathf.Max(1f, (float)Screen.width));
                    float target = (t - 0.5f) * 2f * maxOffsetX;
                    pos.x = Mathf.Lerp(pos.x, target, Time.deltaTime * lateralSpeed);
                }
            }

            pos.x = Mathf.Clamp(pos.x, -maxOffsetX, maxOffsetX);
            transform.position = pos;
        }
    }
}
