using System;
using System.Collections.Generic;
using PacMan.PacMan;
using Scripts.Map;
//using UnityEditor.TextCore.Text;
using UnityEngine;

namespace PacMan
{
    public class PacManAI : MonoBehaviour
    {
        private IPacManAgent _agentAgentManager;
        private ObstacleMap _obstacleMap;
        private MapManager _mapManager;

        //General Info
        int pacManIndex = 0;
        private bool isBlue;
        private int nrOfFriendlyAgents;
        private List<Vector3> allAgentPositions;
        private Vector2Int startCell;
        private Vector2Int oldCell;
        private bool respawned = false;
        
        //Defense
        private bool isDefense = true;
        private Vector2Int myDefensePosition;
        private List<Vector2Int> defensePositions;
        private GameObject visibleIntruder;
        private Vector3? noisyIntruderGuess;
        public Vector3? lastStolenFoodPos;
        private float suspiciousCooldown = 5f;
        public float lastFoodLossTime = -100f;
        private CentralGameTracker.DefenseState currentDefenseState = CentralGameTracker.DefenseState.Idle;

        //Pd tracker
        private float k_p = 2;
        private float k_d = 2;

        //Veronoi
        private VeronoiMap _veronoiMap;

        //Path
        private List<Vector2Int> path = new List<Vector2Int>();
        private List<Vector3> waypoints;
        private int currentWaypointIndex = -1;
        private bool reachedDestination = false;

        public void Initialize(MapManager mapManager) // Ticked when all agents spawned by the network and seen properly by the client. The game is already running. Not the same as Start or Awake in this assignment.
        {
            _agentAgentManager = GetComponent<IPacManAgent>();
            _mapManager = mapManager;
            _obstacleMap = ObstacleMap.Initialize(_mapManager, new List<GameObject>(), Vector3.one, new Vector3(0.95f, 1f, 0.95f));
            
            AllPairsShortestPaths.ComputeAllPairsShortestPaths(_obstacleMap);
            CentralGameTracker.Initialize(_agentAgentManager, _obstacleMap);

            isBlue = (TeamAssignmentUtil.CheckTeam(gameObject) == Team.Blue);    

            nrOfFriendlyAgents = _agentAgentManager.GetFriendlyAgents().Count;
            
            var startPositions = mapManager.startPositions;
            allAgentPositions = new List<Vector3>(2 * nrOfFriendlyAgents);
            for (int i = 0; i < 2 * nrOfFriendlyAgents; i++)
            {
                allAgentPositions.Add(startPositions[i]);
            }

            Vector3Int startCell3D = _obstacleMap.WorldToCell(transform.position);
            startCell = new Vector2Int(startCell3D.x, startCell3D.z);
            oldCell = startCell;

            //_veronoiMap = new VeronoiMap();
            //_veronoiMap.GenerateMap(_obstacleMap, allAgentPositions);             
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

            // Replan if you got eaten and you respawn
            Vector3Int currentCell3D = _obstacleMap.WorldToCell(transform.position);
            Vector2Int currentCell = new Vector2Int(currentCell3D.x, currentCell3D.z);
            if (currentCell == startCell && Vector2Int.Distance(currentCell, oldCell) > 1)
            {
                respawned = true;
                Debug.Log("respawned");
                GenerateWaypoints(path[^1]);
            }
            else respawned = false;
            oldCell = currentCell;

            AssignDefense(); // TO DO : Move to pre-planning

            if (isDefense /*&& isBlue*/)
            {
                if (pacManIndex == 0) // First agent updates assignment once per tick
                {
                    CentralGameTracker.UpdateDefenseAssignments(_agentAgentManager.GetFriendlyAgents(), isBlue);
                }

                var assignment = CentralGameTracker.GetDefenseAssignment(pacManIndex);

                switch (assignment.State)
                {
                    case CentralGameTracker.DefenseState.Idle:
                        Vector3Int myCell3D = _obstacleMap.WorldToCell(transform.position);
                        Vector2Int myCell = new Vector2Int(myCell3D.x, myCell3D.z);
                        if (myCell != myDefensePosition) GoToDefensePosition();
                        else ContinuePatrol();
                        break;

                    case CentralGameTracker.DefenseState.Patrol:
                        ContinuePatrol();
                        break;

                    case CentralGameTracker.DefenseState.Chase:
                        if (assignment.TargetIntruder != null)
                        {
                            Vector3Int position3D = _obstacleMap.WorldToCell(assignment.TargetIntruder.transform.position);
                            GenerateWaypoints(new Vector2Int(position3D.x, position3D.z));
                        }
                        break;

                    case CentralGameTracker.DefenseState.Investigate:
                        if (assignment.TargetPosition.HasValue)
                        {
                            Vector3Int cell = _obstacleMap.WorldToCell(assignment.TargetPosition.Value);
                            GenerateWaypoints(new Vector2Int(cell.x, cell.z));
                        }
                        break;

                    case CentralGameTracker.DefenseState.Return:
                        GoToDefensePosition();
                        break;
                }
            }

            /*else if (isDefense && !isBlue)
            {
                if (waypoints == null) GoToDefensePosition();
                else ContinuePatrol();
            }*/

            //Attacking
            else
            {
                if (currentWaypointIndex == -1)
                {
                    GoGetFood();
                }

                if (reachedDestination)
                {
                    reachedDestination = false;

                    // If you've finished your path and are in the opponent's home, go home
                    if ((transform.position.x >= 0 && isBlue) || (transform.position.x < 0 && !isBlue))
                    {
                        ReturnHome();
                    }
                    else
                    {
                        GoGetFood();
                    }
                }
            }

            var tol = 0.5f;
            if (waypoints.Count >0 && (gameObject.transform.position - waypoints[currentWaypointIndex]).magnitude < tol)
            {
                if (currentWaypointIndex < waypoints.Count - 1)
                {
                    currentWaypointIndex += 1;
                    
                }
                else
                {
                    reachedDestination = true;
                }
            }

            CentralGameTracker.checkFood();

            // Since the RigidBody is updated server side and the client only syncs position, rigidbody.Velocity does not report a velocity
            _agentAgentManager.GetVelocity(); // Use the manager method to get the true velocity from the server
            // friendlyAgentManager.GetVelocity(); // Given the damping, max velocity magnitude is around 2.34
                    
            return PDControll();
        }

        private PacManAction PDControll(float magnitude = 1f)
        {
            Vector3 currentPos = gameObject.transform.position;

            Vector3 targetPos = transform.position;
            if (waypoints.Count >0)
            {
                targetPos = waypoints[currentWaypointIndex];
            }
            
            Vector3 posError = targetPos - currentPos;
                      
            Vector3 currentVelocity = _agentAgentManager.GetVelocity();
            Vector3 targetVelocity = posError / Time.fixedDeltaTime;

            Vector3 velocityError = targetVelocity - currentVelocity;

            Vector3 desiredAcc = k_p * posError + k_d * velocityError;
            
            var droneAction = new PacManAction
            {
                AccelerationDirection = new Vector2(desiredAcc.x, desiredAcc.z), // Controller converts to [0; 1] normalized acceleration vector,
                AccelerationMagnitude = magnitude // 1 means max acceleration in chosen direction // 0.3 guarantees not observed
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
            DrawDefense();
            //DrawPacMan();
            //DrawFood();
            //Gizmos.color = Color.green;
            DrawCurrentWaypoint();  
            DrawWaypoints();
            
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

        private void DrawCurrentWaypoint()
        {
            Gizmos.color = Color.red;
            if (isBlue)
            {
                Gizmos.color = Color.blue;
            }
            if (waypoints.Count >0) Gizmos.DrawSphere(waypoints[currentWaypointIndex], 0.3f);
        }
        
        private void DrawWaypoints()
        {
            Gizmos.color = Color.red;
            if (isBlue)
            {
                Gizmos.color = Color.blue;
            }
            
            foreach (var pos in waypoints)
            {              
                Gizmos.DrawSphere(pos, 0.1f);
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
            defensePositions = Defense.GetDefensePositions(agentCount - attackers, isBlue, _obstacleMap);

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

        private void GenerateWaypoints(Vector2Int destination)
        {
            Vector3Int myCell3D = _obstacleMap.WorldToCell(transform.position);
            Vector2Int myCell = new Vector2Int(myCell3D.x, myCell3D.z);

            if (path.Count > 1 && path[0] == myCell && path[path.Count - 1] == destination) return;

            path = AllPairsShortestPaths.ComputeShortestPath(myCell, destination);

            waypoints = new List<Vector3>(path.Count);
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 waypoint = _obstacleMap.CellToWorld(new Vector3Int((int)path[i].x, 0, (int)path[i].y)) + _obstacleMap.trueScale / 2;
                waypoint.y = 0f;

                waypoints.Add(waypoint);
            }
            currentWaypointIndex = 0;
            reachedDestination = false;
        }

        private void GenerateWaypointsCluster(Vector2Int closestFood, List<Vector3Int> cluster)
        {
            GenerateWaypoints(closestFood);

            var worldCluster = new List<Vector3>();
            foreach (var cell in cluster)
            {
                worldCluster.Add(_obstacleMap.CellToWorld(cell));
            }
            
            worldCluster.Sort((a, b) => Vector3.Distance(a, waypoints[^1]).CompareTo(Vector3.Distance(b, waypoints[^1])));

            foreach (var point in worldCluster)
            {
                if (point != waypoints[^1])
                {
                    waypoints.Add(point + _obstacleMap.trueScale / 2);
                }
            }
        }

        private void ReturnHome()
        {
            Vector3Int myCell3D = _obstacleMap.WorldToCell(transform.position);
            Vector2Int myCell = new Vector2Int(myCell3D.x, myCell3D.z);
            Vector2Int closestHome = AllPairsShortestPaths.GetClosestHomeCell(myCell, isBlue);
            GenerateWaypoints(closestHome);
        }
        
        private void GoGetFood()
        { 
            var (closestFood, closestFoodCluster) = CentralGameTracker.FindClosestFoodCluster(gameObject.transform.position, isBlue);
            Vector2Int target = new Vector2Int(closestFood.x, closestFood.z);
            GenerateWaypointsCluster(target, closestFoodCluster);
        }

        private void UpdateDefenseState()
        {
            visibleIntruder = null;
            noisyIntruderGuess = null;

            // 1. Check visible enemies // TO DO : Modify for only one going there 
            foreach (var enemy in _agentAgentManager.GetVisibleEnemyAgents())
            {
                if (CentralGameTracker.IsOnMySide(enemy.gameObject.transform.position, isBlue))
                {
                    visibleIntruder = enemy.gameObject;
                    currentDefenseState = CentralGameTracker.DefenseState.Chase;
                    return;
                }
            }

            // 2. Noisy enemies (fast-moving ghosts) // TO DO : Modify for only one going there
            foreach (var obs in _agentAgentManager.GetEnemyObservations().Observations)
            {
                if (obs.Position.sqrMagnitude > 0.01f && CentralGameTracker.IsOnMySide(obs.Position, isBlue))
                {
                    noisyIntruderGuess = obs.Position;
                    currentDefenseState = CentralGameTracker.DefenseState.Investigate;
                    return;
                }
            }

            // 3. Food disappeared?
            var lostFood = CentralGameTracker.CheckForFoodLoss(isBlue);
            if (lostFood != null)
            {
                if (CentralGameTracker.IsOnMySide(_obstacleMap.CellToWorld(lostFood.Value), isBlue))
                {
                    lastStolenFoodPos = _obstacleMap.CellToWorld(lostFood.Value);
                    lastFoodLossTime = Time.time;
                    currentDefenseState = CentralGameTracker.DefenseState.Investigate;
                    return;
                }
            }

            // 4. If previously investigating and timeout expired
            if (currentDefenseState == CentralGameTracker.DefenseState.Investigate && Time.time - lastFoodLossTime > suspiciousCooldown)
            {
                currentDefenseState = CentralGameTracker.DefenseState.Return;
            }

            // 5. If back at my defense position
            Vector3Int myPosition3D = _obstacleMap.WorldToCell(transform.position);
            Vector2Int myPosition = new Vector2Int(myPosition3D.x, myPosition3D.z);
            if (currentDefenseState == CentralGameTracker.DefenseState.Return &&  myPosition == myDefensePosition)
            {
                currentDefenseState = CentralGameTracker.DefenseState.Idle;
            }
        }

        private void GoToDefensePosition()
        {
            GenerateWaypoints(myDefensePosition);
        }

        private void PatrolAroundDefensePosition()
        {
            //if ((currentDefenseState != DefenseState.Patrol) || (currentDefenseState == DefenseState.Patrol && ((gameObject.transform.position - waypoints[^1]).magnitude < 0.2f)))
            {
                // Patrol 2 units above/below defense point
                Vector2Int offset = new Vector2Int(0, 2);
                Vector2Int patrolTarget = (UnityEngine.Random.value > 0.5f) ? myDefensePosition + offset : myDefensePosition - offset;

                GenerateWaypoints(patrolTarget);
                currentDefenseState = CentralGameTracker.DefenseState.Patrol;
            }
        }

        private void ContinuePatrol()
        {
            if (reachedDestination)
            {
                currentDefenseState = CentralGameTracker.DefenseState.Idle;
            }
        }

        private void ChaseIntruder() // TO DO : Modify to go between the separation and the intruder
        {
            if (visibleIntruder == null)
            {
                currentDefenseState = CentralGameTracker.DefenseState.Return;
                return;
            }

            Vector3Int cell = _obstacleMap.WorldToCell(visibleIntruder.transform.position);

            GenerateWaypoints(new Vector2Int(cell.x, cell.z));
        }

        private void InvestigateSuspiciousArea()
        {
            Vector3 target = noisyIntruderGuess ?? lastStolenFoodPos ?? transform.position;
            Vector3Int cell = _obstacleMap.WorldToCell(target);
            GenerateWaypoints(new Vector2Int(cell.x, cell.z));
        }

    }
}