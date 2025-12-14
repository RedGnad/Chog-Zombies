using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace ChogZombies.Game
{
    public class SimpleRunRestart : MonoBehaviour
    {
        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                var scene = SceneManager.GetActiveScene();
                SceneManager.LoadScene(scene.buildIndex);
            }
        }
    }
}
