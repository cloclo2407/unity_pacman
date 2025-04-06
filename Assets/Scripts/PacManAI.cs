using System.Collections.Generic;
using PacMan.PacMan;
using Scripts.Map;
using UnityEditor.TextCore.Text;
using UnityEngine;

namespace PacMan
{
    public class PacManAI : MonoBehaviour
    {
        private IPacManAgent _agentAgentManager;
        private ObstacleMap _obstacleMap;
        private MapManager _mapManager;

        private List<Vector3> allAgentPositions;
        
        private bool isBlue;
        private bool isDefense = true;
        private Vector2Int myDefensePosition;
        private List<Vector2Int> defensePositions;

        int pacManIndex = 0;


        private int nrOfFriendlyAgents;

        private bool printed = false;
        private VeronoiMap _veronoiMap;

        public void Initialize(MapManager mapManager) // Ticked when all agents spawned by the network and seen properly by the client. The game is already running. Not the same as Start or Awake in this assignment.
        {
            _agentAgentManager = GetComponent<IPacManAgent>();
            _mapManager = mapManager;
            _obstacleMap = ObstacleMap.Initialize(_mapManager, new List<GameObject>(), Vector3.one, new Vector3(0.95f, 1f, 0.95f));
            
            AllPairsShortestPaths.ComputeAllPairsShortestPaths(_obstacleMap);
            CentralGameTracker.Initialize(_agentAgentManager, _obstacleMap);

            isBlue = (TeamAssignmentUtil.CheckTeam(gameObject) == Team.Blue);

            // Example on how to draw the path between start and goal
            
            Vector2Int start = new Vector2Int(-4, 5);
            Vector2Int goal = new Vector2Int(3, 2);
            List<Vector2Int> path = AllPairsShortestPaths.ComputeShortestPath(start, goal);
            DrawPath(path);            

            nrOfFriendlyAgents = _agentAgentManager.GetFriendlyAgents().Count;
            
            var startPositions = mapManager.startPositions;
            allAgentPositions = new List<Vector3>(2 * nrOfFriendlyAgents);
            for (int i = 0; i < 2 * nrOfFriendlyAgents; i++)
            {
                allAgentPositions.Add(startPositions[i]);
            }

            _veronoiMap = new VeronoiMap();
            _veronoiMap.GenerateMap(_obstacleMap, allAgentPositions);
            
            
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

            //var friendlyAgentManager = _agentAgentManager.GetFriendlyAgents()[0];
            //friendlyAgentManager.IsGhost();

            
            
            
            var agentIndex = 0;
            
            foreach (var _friendlyAgentManager in _agentAgentManager.GetFriendlyAgents())
            {
                allAgentPositions[agentIndex] = _friendlyAgentManager.gameObject.transform.position;
                agentIndex++;
            }

            var visibleEnemyAgents = _agentAgentManager.GetVisibleEnemyAgents();
            PacManObservations fetchEnemyObservations = _agentAgentManager.GetEnemyObservations();
            
            if (fetchEnemyObservations.Observations.Length > 0)
            {
                foreach (var observation in fetchEnemyObservations.Observations)
                {
                    //Debug.Log($"agentindex {agentIndex}, the observed position is {observation.Position}, with sqrMagnitude {observation.Position.sqrMagnitude}");
                    if (observation.Position.sqrMagnitude > 0.01f) //If the observed position is not Vector3.zero (no sound -> keep old posiiton)
                    {
                        //Debug.Log("Passed magnitude > 0.01f!");
                        allAgentPositions[agentIndex] = observation.Position;
                    }
                     agentIndex++;
                }
            }
            
            /*
            if (!printed)
            {
                Debug.Log("_-------------------------------");
                Debug.Log($"Length is {allAgentPositions.Count}, allAgentPosisions = {allAgentPositions}");
                var i = 0;
                foreach (var agentPosition in allAgentPositions)
                {
                    
                    Debug.Log("Position = " + agentPosition + "for agent nr" + i);
                    i++;
                }
                Debug.Log("-------------------------------_");

                //printed = true;
            }
            */
            
            


            AssignDefense();
            if (isDefense)
            {
                // Example on how to draw the path between start and goal
                Vector3 start3D = _obstacleMap.WorldToCell(_agentAgentManager.GetStartPosition());
                Vector2Int start = new Vector2Int((int)start3D.x, (int)start3D.z);
                Vector2Int goal = myDefensePosition;

                List<Vector2Int> path = AllPairsShortestPaths.ComputeShortestPath(start, goal);
                DrawPath(path);
            }

            CentralGameTracker.checkFood();

            // Since the RigidBody is updated server side and the client only syncs position, rigidbody.Velocity does not report a velocity
            _agentAgentManager.GetVelocity(); // Use the manager method to get the true velocity from the server
            // friendlyAgentManager.GetVelocity(); // Given the damping, max velocity magnitude is around 2.34

            // // replace the human input below with some AI stuff
            var x = 0;
            var z = 0;

            x = 1;
            z = 1;
            
            /*

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
            */

            
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

        private void DrawVeronoiMap()
        {
            _veronoiMap = new VeronoiMap();
            _veronoiMap.GenerateMap(_obstacleMap, allAgentPositions);
            
            List<Color> playerColor = GetPlayerColors();
            
            foreach (var cellPosition in _veronoiMap.closestAgent.Keys)
            {
                var worldPos = _obstacleMap.CellToWorld(new Vector3Int(cellPosition.x, 0, cellPosition.y)) + _obstacleMap.trueScale / 2;
                
                worldPos.y = 0;
                worldPos += Vector3.up * 0.25f;

                var scale = Vector3.Scale(transform.localScale, _obstacleMap.trueScale);
                var gizmoSize = new Vector3(scale.x * 0.95f, 0.005f, scale.z * 0.95f);
                
                Gizmos.color = playerColor[_veronoiMap.closestAgent[cellPosition]];

                Gizmos.DrawCube(worldPos, gizmoSize);
                
            }
        }
        
        public List<Color> GetPlayerColors()
        {
            List<Color> playerColors = new List<Color>(2* nrOfFriendlyAgents);
            for (int i = 1; i <= 2*nrOfFriendlyAgents; i++)
            {
                // Normalize hue around the color wheel - Chat GPT
                float hue = (float) i / (2*nrOfFriendlyAgents); // Hue varies between 0 and 1
                float saturation = 1.0f; // Keep colors vivid
                float value = 1.0f; // Full brightness
                
                playerColors.Add(Color.HSVToRGB(hue, saturation, value));
            }
            return playerColors;
        }
        
        private void OnDrawGizmos()
        {
            //DrawVeronoiMap();
            //DrawDefense();
            //DrawPacMan();
            DrawFood();
            Gizmos.color = Color.green;
            Vector3 cell = _obstacleMap.CellToWorld(new Vector3Int(0, 0, 0)) + _obstacleMap.trueScale / 2;
            Gizmos.DrawWireSphere(cell, 0.5f);    
        }

        private void DrawDefense()
        {
            Gizmos.color = Color.red;
            if (isBlue)
            {
                Gizmos.color = Color.blue;
            }
            foreach (var pos in defensePositions)
            {
                Vector3 worldPos = _obstacleMap.CellToWorld(new Vector3Int((int)pos.x, 0, (int)pos.y)) + _obstacleMap.trueScale / 2;
                Gizmos.DrawWireSphere(worldPos, 0.5f);
            }
        }

        private void DrawPacMan()
        {
            
            Gizmos.color = Color.green;
            var friendlyAgents = _agentAgentManager.GetFriendlyAgents();
            foreach (var friend in friendlyAgents)
            {
                Vector3 pos = transform.position;
                Vector3 worldPos = _obstacleMap.CellToWorld(new Vector3Int((int)pos.x, 0, (int)pos.y)) + _obstacleMap.trueScale / 2;
                Gizmos.DrawWireSphere(pos, 0.5f);
            }
        }

        private void DrawFood()
        {
            Gizmos.color = Color.red;

            foreach (var food in CentralGameTracker.positiveClusters)
            {
                Vector3Int pos = food[0];
                Vector3 worldPos = _obstacleMap.CellToWorld(pos) + _obstacleMap.trueScale / 2;
                Gizmos.DrawSphere(worldPos, 0.5f);
            }

            Gizmos.color = Color.blue;

            foreach (var food in CentralGameTracker.negativeClusters)
            {
                Vector3Int pos = food[0];
                Vector3 worldPos = _obstacleMap.CellToWorld(pos) + _obstacleMap.trueScale / 2;
                Gizmos.DrawSphere(worldPos, 0.5f);
            }
        }

        private void AssignDefense()
        {
            var friendlyAgents = _agentAgentManager.GetFriendlyAgents();
            int agentCount = friendlyAgents.Count;

            if (agentCount == 0)
                return;

            int attackers; //nb of pacman who attack
            if (agentCount <= 3) attackers = 1;
            else attackers = 2;
            
            // Get defense positions
            defensePositions = Defense.GetDefensePositions(agentCount - attackers, isBlue);

            // Sort agents based on their respawn X position (closer to 0 attacks)
            friendlyAgents.Sort((a, b) => Mathf.Abs(a.GetStartPosition().x).CompareTo(Mathf.Abs(b.GetStartPosition().x)));

            for (int i = 0; i < agentCount; i++)
            {
                if (friendlyAgents[i] == _agentAgentManager)
                {
                    pacManIndex = i;
                    if (i < agentCount - attackers)
                    {
                        myDefensePosition = defensePositions[i];
                    }
                    else
                    {
                        isDefense = false;
                    }                    
                }               
            }
        }

    }

}