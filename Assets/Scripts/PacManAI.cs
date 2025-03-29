using System.Collections.Generic;
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

        public void Initialize(MapManager mapManager) // Ticked when all agents spawned by the network and seen properly by the client. The game is already running. Not the same as Start or Awake in this assignment.
        {
            _agentAgentManager = GetComponent<IPacManAgent>();
            _mapManager = mapManager;
            _obstacleMap = ObstacleMap.Initialize(_mapManager, new List<GameObject>(), Vector3.one, new Vector3(0.95f, 1f, 0.95f));
            AllPairsShortestPaths.ComputeAllPairsShortestPaths(_obstacleMap);

            // Example on how to draw the path between start and goal
            Vector2Int start = new Vector2Int(-4, 5);
            Vector2Int goal = new Vector2Int(3, 2   );
            List<Vector2Int> path = AllPairsShortestPaths.ComputeShortestPath(start, goal);
            DrawPath(path);
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

        private void DrawPath(List<Vector2Int> path)
        {
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 first = _obstacleMap.CellToWorld(new Vector3Int((int)path[i].x, 0, (int)path[i].y)) + _obstacleMap.trueScale / 2;
                first.y = 0f;
                Vector3 second = _obstacleMap.CellToWorld(new Vector3Int((int)path[i + 1].x, -0, (int)path[i + 1].y)) + _obstacleMap.trueScale / 2;
                second.y = 0f;

                Debug.DrawLine(first, second, Color.red, 100000f);
            }
        }
    }

}