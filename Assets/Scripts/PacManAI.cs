﻿using System.Collections.Generic;
using System.Threading;
﻿using System;
using PacMan.PacMan;
using System.Linq;
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

        private static bool globallyInitialized = false;
        private bool initialized = false; // Set to true when the agent is initialized and ready to start planning

        //General Info
        public int pacManIndex = 0;
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
        private CentralGameTrackerBlue.DefenseState currentDefenseState = CentralGameTrackerBlue.DefenseState.Idle;
        private bool defenseAssigned = false;

        //Attack
        public static int attackers = 1; // Number of attackers in the team

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

        public void Initialize(MapManager mapManager, float finishPlanningTimestamp) // Ticked when all agents spawned by the network and seen properly by the client. The game is already running. Not the same as Start or Awake in this assignment.
        {
            _agentAgentManager = GetComponent<IPacManAgent>();
            _mapManager = mapManager;
            _obstacleMap = ObstacleMap.Initialize(_mapManager, new List<GameObject>(), Vector3.one, new Vector3(0.95f, 1f, 0.95f));


            while (Time.realtimeSinceStartup < finishPlanningTimestamp) // NB!!!!!! You must use real time for this comparison! Time.time and Time.fixedTime do not progress unless you release the frame!
            {   // This is not a very elegant solution, since we block the main thread completely.
                if (!globallyInitialized)
                {
                    globallyInitialized = true;
                    var startPositions = mapManager.startPositions;
                    allAgentPositions = new List<Vector3>(2 * nrOfFriendlyAgents);
                    for (int i = 0; i < 2 * nrOfFriendlyAgents; i++)
                    {
                        allAgentPositions.Add(startPositions[i]);
                    }

                    AllPairsShortestPaths.ComputeAllPairsShortestPaths(_obstacleMap);
                }
                
                //Remember, finishPlanningTimestamp is for ALL agents, not just one. If you need to plan with multiple agents, you need to ensure ALL agents finish planning by this time.
                //You might want to terminate a little bit earlier to avoid planning once the match has started - there are always some delays over the network and the sync wont be perfect.
            }
        }


        public PacManAction Tick() //The Tick from the network controller
        {
            if (!initialized)
            {
                initialized = true;

                Vector3Int startCell3D = _obstacleMap.WorldToCell(transform.position);
                startCell = new Vector2Int(startCell3D.x, startCell3D.z);
                oldCell = startCell;

                isBlue = (TeamAssignmentUtil.CheckTeam(gameObject) == Team.Blue);
                if (isBlue) CentralGameTrackerBlue.Initialize(_agentAgentManager, _obstacleMap);
                else CentralGameTrackerRed.Initialize(_agentAgentManager, _obstacleMap);

                nrOfFriendlyAgents = _agentAgentManager.GetFriendlyAgents().Count;

                var startPositions = _mapManager.startPositions;
                allAgentPositions = new List<Vector3>(2 * nrOfFriendlyAgents);
                for (int i = 0; i < 2 * nrOfFriendlyAgents; i++)
                {
                    allAgentPositions.Add(startPositions[i]);
                }

            }
            if (!defenseAssigned)
            {
                AssignDefense();
                defenseAssigned = true;
            }
 
            // Replan if you got eaten and you respawn
            Vector3Int currentCell3D = _obstacleMap.WorldToCell(transform.position);
            Vector2Int currentCell = new Vector2Int(currentCell3D.x, currentCell3D.z);
            if (/*Vector2Int.Distance(currentCell,startCell) < 2 && */Vector2Int.Distance(currentCell, oldCell) > 2)
            {
                respawned = true;
                GenerateWaypoints(path[^1]);
            }
            else respawned = false;
            oldCell = currentCell;

            if (pacManIndex == 0) // First agent updates assignment once per tick
            {
                if (isBlue)
                {
                    CentralGameTrackerBlue.UpdateDefenseAssignments();
                    CentralGameTrackerBlue.checkFood();
                }
                else
                {
                    CentralGameTrackerRed.UpdateDefenseAssignments();
                    CentralGameTrackerRed.checkFood();
                }
            }

            if (isDefense)
            {
                CentralGameTrackerBlue.DefenseAssignment assignment;
                if (isBlue) assignment = CentralGameTrackerBlue.GetDefenseAssignment(pacManIndex);
                else assignment = CentralGameTrackerRed.GetDefenseAssignment(pacManIndex);

                switch (assignment.State)
                {
                    case CentralGameTrackerBlue.DefenseState.Idle:
                        Vector3Int myCell3D = _obstacleMap.WorldToCell(transform.position);
                        Vector2Int myCell = new Vector2Int(myCell3D.x, myCell3D.z);
                        if (myCell != myDefensePosition) GoToDefensePosition();
                        else
                        {
                            CentralGameTrackerBlue.SetDefenseAssignment(_agentAgentManager, CentralGameTrackerBlue.DefenseState.Patrol);
                            ContinuePatrol();
                        }
                        break;

                    case CentralGameTrackerBlue.DefenseState.Patrol:
                        ContinuePatrol();
                        break;

                    case CentralGameTrackerBlue.DefenseState.Chase:
                        /*if (assignment.TargetIntruder != null)
                        {
                            Vector3Int position3D = _obstacleMap.WorldToCell(assignment.TargetIntruder.transform.position);
                            GenerateWaypoints(new Vector2Int(position3D.x, position3D.z));
                        }*/
                        ChaseIntruder(assignment);
                        break;

                    case CentralGameTrackerBlue.DefenseState.Investigate:
                        if (assignment.TargetPosition.HasValue)
                        {
                            Vector3Int cell = _obstacleMap.WorldToCell(assignment.TargetPosition.Value);
                            GenerateWaypoints(new Vector2Int(cell.x, cell.z));
                        }
                        break;

                    case CentralGameTrackerBlue.DefenseState.Return:
                        GoToDefensePosition();
                        break;
                }
            }

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

            foreach (var food in CentralGameTrackerBlue.foodClusters)
            {
                Vector3Int pos = food[0];
                Vector3 worldPos = _obstacleMap.CellToWorld(pos) + _obstacleMap.trueScale / 2;
                Gizmos.DrawSphere(worldPos, 0.5f);
            }

            Gizmos.color = Color.blue;

            foreach (var food in CentralGameTrackerRed.foodClusters)
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

                    // First few are attackers now
                    if (i < attackers)
                    {
                        isDefense = false;
                    }
                    else
                    {
                        int defenseIndex = i - attackers;
                        myDefensePosition = defensePositions[defenseIndex];
                    }
                }
            }
        }

        private void GenerateWaypoints(Vector2Int destination)
        {
            Vector3Int myCell3D = _obstacleMap.WorldToCell(transform.position);
            Vector2Int myCell = new Vector2Int(myCell3D.x, myCell3D.z);
            if (respawned) currentWaypointIndex = 0;

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
            var safetyDistances = new Dictionary<Vector2Int, float>();

            Vector3Int myCell3D = _obstacleMap.WorldToCell(transform.position);
            Vector2Int myCell = new Vector2Int(myCell3D.x, myCell3D.z);

            foreach (var cell in _obstacleMap.traversabilityPerCell)
            {
                if ((isBlue && cell.Key.x == -1) || (!isBlue && cell.Key.x == 0))
                {
                    if (cell.Value != ObstacleMap.Traversability.Blocked)
                    {
                        var myDistance = AllPairsShortestPaths.distances[myCell][cell.Key];
                        for (int i = nrOfFriendlyAgents; i < 2 * nrOfFriendlyAgents; i++)
                        {

                            var agentPos = allAgentPositions[i];
                            if (agentPos != Vector3.zero)
                            {
                                var worldToCell = _obstacleMap.WorldToCell(agentPos);
                                var agentCellPos = new Vector2Int(worldToCell.x, worldToCell.z);

                                var dist = AllPairsShortestPaths.distances[agentCellPos][cell.Key];


                                if (!safetyDistances.ContainsKey(cell.Key))
                                {
                                    safetyDistances[cell.Key] = dist - myDistance;

                                }
                                else
                                {
                                    safetyDistances[cell.Key] = Math.Max(dist - myDistance, safetyDistances[cell.Key]);
                                }
                            }
                        }
                    }
                }
            }
            Vector2Int safestHome = safetyDistances.OrderBy(cell => cell.Value).Last().Key;

            GenerateWaypoints(safestHome);
        }

        private void ReturnHomeShortest()
        {
            Vector3Int myCell3D = _obstacleMap.WorldToCell(transform.position);
            Vector2Int myCell = new Vector2Int(myCell3D.x, myCell3D.z);
            Vector2Int closestHome = AllPairsShortestPaths.GetClosestHomeCell(myCell, isBlue);
            GenerateWaypoints(closestHome);
        }

        private void GoGetFood()
        {
            Vector3Int closestFood;
            List<Vector3Int> closestFoodCluster;
            if (isBlue) (closestFood, closestFoodCluster) = CentralGameTrackerBlue.FindFurthestAvailableCluster(transform.position);
            else (closestFood, closestFoodCluster) = CentralGameTrackerRed.FindFurthestAvailableCluster(transform.position);
            Vector2Int target = new Vector2Int(closestFood.x, closestFood.z);
            GenerateWaypointsCluster(target, closestFoodCluster);
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
                currentDefenseState = CentralGameTrackerBlue.DefenseState.Patrol;
            }
        }

        private void ContinuePatrol()
        {
            if (reachedDestination)
            {
                PatrolAroundDefensePosition();
            }
        }

        private void ChaseIntruder(CentralGameTrackerBlue.DefenseAssignment assignment)
        {
            if (assignment.TargetIntruder == null) return;

            Vector3 currentPosition = transform.position;
            Vector3 intruderPos = assignment.TargetIntruder.transform.position;
            Vector3 intruderVelocity = assignment.TargetIntruder.GetComponent<Rigidbody>().linearVelocity;
            Vector3 interceptTarget = intruderPos + intruderVelocity.normalized * 2.0f;

            Vector3 direction = (interceptTarget - currentPosition).normalized;
            float distance = Vector3.Distance(currentPosition, interceptTarget);

            // If close enough, just go straight to the intruder
            if (distance < 1.5f)
            {
                waypoints = new List<Vector3> { intruderPos };
                currentWaypointIndex = 0;
                return;
            }

            // Raycast to check line of sight
            bool hasLineOfSight = !Physics.Raycast(
                currentPosition,
                (intruderPos - currentPosition).normalized,
                Vector3.Distance(currentPosition, intruderPos),
                LayerMask.GetMask("Default")
            );

            // If intruder is visible and no obstacle between agent and intercept point
            if (hasLineOfSight && !Physics.Raycast(currentPosition, direction, distance, LayerMask.GetMask("Default")))
            {
                waypoints = new List<Vector3> { interceptTarget };
                currentWaypointIndex = 0;
            }
            else
            {
                // Predict future cell
                Vector3Int futureCell = _obstacleMap.WorldToCell(interceptTarget);
                Vector2Int futureGridCell = new Vector2Int(futureCell.x, futureCell.z);

                // If future cell is free, path to it
                if (_obstacleMap.traversabilityPerCell[futureGridCell] == ObstacleMap.Traversability.Free)
                {
                    GenerateWaypoints(futureGridCell);
                }
                else
                {
                    // Fallback to intruder's current position
                    Vector3Int currentCell = _obstacleMap.WorldToCell(intruderPos);
                    Vector2Int currentGridCell = new Vector2Int(currentCell.x, currentCell.z);
                    GenerateWaypoints(currentGridCell);
                }
            }
        }

    }
}