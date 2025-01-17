using System;
using UnityEngine;

namespace PacMan.Local
{
    public class ReplayPacManAgentManager : PacManAgentManager
    {
        public void SetRecordingData()
        {
            
        }
        public new void FixedUpdate()
        {
            UpdateAgentState();
            _movement.ApplyDesiredControl(action.AccelerationDirection, action.AccelerationMagnitude);

            ghostObject.SetActive(isGhost);
            pacManObject.SetActive(!isGhost);
        }

        public new void OnTriggerEnter(Collider other)
        {
            // Trigger disabled
        }

        public new void OnCollisionStay(Collision other)
        {
            // Collision reactions disabled
        }
    }
}