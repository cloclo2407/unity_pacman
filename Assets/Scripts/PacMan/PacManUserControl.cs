using Imported.StandardAssets.CrossPlatformInput.Scripts;
using UnityEngine;

namespace PacMan
{
    public class PacManUserControl : MonoBehaviour
    {
        private PacManMovementController controller;

        private void Start()
        {
            controller = GetComponent<PacManMovementController>();
        }

        private void FixedUpdate()
        {
            float h = CrossPlatformInputManager.GetAxis("Horizontal");
            float v = CrossPlatformInputManager.GetAxis("Vertical");

            controller.ApplyDesiredControl(new Vector2(v, h), 1);
        }
    }
}