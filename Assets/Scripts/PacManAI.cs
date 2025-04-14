using System.Collections.Generic;
using System.Threading;
using PacMan.PacMan;
using Scripts.Map;
using UnityEngine;

namespace PacMan
{
    public class PacManAI : MonoBehaviour
    {
        private IPacManAgent _agentAgentManager;
        private ObstacleMap _obstacleMap;
        private MapManager _mapManager;

        public void Initialize(MapManager mapManager, float finishPlanningTimestamp) // Ticked when all agents spawned by the network and seen properly by the client. The game is already running. Not the same as Start or Awake in this assignment.
        {
            _agentAgentManager = GetComponent<IPacManAgent>();
            _mapManager = mapManager;
            _obstacleMap = ObstacleMap.Initialize(_mapManager, new List<GameObject>(), Vector3.one);

            Debug.Log(finishPlanningTimestamp);
            Debug.Log(Time.realtimeSinceStartup);
         
            while (Time.realtimeSinceStartup < finishPlanningTimestamp) // NB!!!!!! You must use real time for this comparison! Time.time and Time.fixedTime do not progress unless you release the frame!
            {   // This is not a very elegant solution, since we block the main thread completely.
                Thread.Sleep(100); // Do planning
                //Remember, finishPlanningTimestamp is for ALL agents, not just one. If you need to plan with multiple agents, you need to ensure ALL agents finish planning by this time.
                //You might want to terminate a little bit earlier to avoid planning once the match has started - there are always some delays over the network and the sync wont be perfect.
            }
        }

        public PacManAction Tick() //The Tick from the network controller
        {
            _agentAgentManager.GetTimeRemaining();
            _agentAgentManager.GetScore();
            bool isGhost = _agentAgentManager.IsGhost();
            bool isScared = _agentAgentManager.IsScared();
            float scaredDuration = _agentAgentManager.GetScaredRemainingDuration();

            float carriedFoodCount = _agentAgentManager.GetCarriedFoodCount();

            List<GameObject> foodPositions = _agentAgentManager.GetFoodObjects();
            List<GameObject> capsulePositions = _agentAgentManager.GetCapsuleObjects();

            var isLocalPointTraversable = _obstacleMap?.GetLocalPointTraversibility(transform.localPosition);

            var friendlyAgentManager = _agentAgentManager.GetFriendlyAgents()[0];
            friendlyAgentManager.IsGhost();

            var visibleEnemyAgents = _agentAgentManager.GetVisibleEnemyAgents();
            PacManObservations fetchEnemyObservations = _agentAgentManager.GetEnemyObservations();
            if (fetchEnemyObservations.Observations.Length > 0)
            {
                // Debug.Log(fetchEnemyObservations.ObservationFixedTime);
            }

            // Since the RigidBody is updated server side and the client only syncs position, rigidbody.Velocity does not report a velocity
            _agentAgentManager.GetVelocity(); // Use the manager method to get the true velocity from the server
            // friendlyAgentManager.GetVelocity(); // Given the damping, max velocity magnitude is around 2.34

            // // replace the human input below with some AI stuff
            var x = 0;
            var z = 0;

            if (TeamAssignmentUtil.CheckTeam(gameObject) == Team.Blue && Input.GetKey("w") || TeamAssignmentUtil.CheckTeam(gameObject) == Team.Red && Input.GetKey("up"))
            {
                z = 1;
            }

            if (TeamAssignmentUtil.CheckTeam(gameObject) == Team.Blue && Input.GetKey("a") || TeamAssignmentUtil.CheckTeam(gameObject) == Team.Red && Input.GetKey("left"))
            {
                x = -1;
            }

            if (TeamAssignmentUtil.CheckTeam(gameObject) == Team.Blue && Input.GetKey("s") || TeamAssignmentUtil.CheckTeam(gameObject) == Team.Red && Input.GetKey("down"))
            {
                z = -1;
            }

            if (TeamAssignmentUtil.CheckTeam(gameObject) == Team.Blue && Input.GetKey("d") || TeamAssignmentUtil.CheckTeam(gameObject) == Team.Red && Input.GetKey("right"))
            {
                x = 1;
            }

            var droneAction = new PacManAction
            {
                AccelerationDirection = new Vector2(x, z), // Controller converts to [0; 1] normalized acceleration vector,
                AccelerationMagnitude = 1f // 1 means max acceleration in chosen direction // 0.3 guarantees not observed
            };

            return droneAction;
        }
    }
}