using System.Collections.Generic;
using PacMan;
using PacMan.PacMan;
using Scripts.Map;
using UnityEngine;
using System.Linq;

public static class CentralGameTrackerRed
{
    
    private static bool _initialized = false;
    
    private static int _nrAgents;
    private static int _nrFriendlyAgents;
    private static IPacManAgent agentAgentManager;
    private static ObstacleMap obstacleMap;

    private static List<GameObject> foodPositions;
    private static HashSet<Vector3Int> previousFoodCells = new HashSet<Vector3Int>();
    private static HashSet<Vector3Int> currentFoodCells = new HashSet<Vector3Int>();
    
    public static List<List<Vector3Int>> foodClusters;
    private static HashSet<List<Vector3Int>> claimedClusters = new HashSet<List<Vector3Int>>(new ClusterComparer());

    private static Dictionary<int, CentralGameTrackerBlue.DefenseAssignment> agentDefenseStates = new(); // PacmanIndex -> Assignment

    private static List<IPacManAgent> defenders;

    public static bool isBlue = false;

    public static void Initialize(IPacManAgent _agentAgentManager, ObstacleMap _obstacleMap)
    {
        if (_initialized) return;
        _nrFriendlyAgents = _agentAgentManager.GetFriendlyAgents().Count;
        defenders = _agentAgentManager.GetFriendlyAgents();
        _nrAgents = 2 * _nrFriendlyAgents;
        agentAgentManager = _agentAgentManager;
        obstacleMap = _obstacleMap;
        checkFood();

        _initialized = true;
    }

    /*
     * Checks if the food vector has changed
     * If it has recalculates the flood clusters
     */
    public static void checkFood()
    {
        List<GameObject> newFoodPositions = agentAgentManager.GetFoodObjects();

        // Convert current food to cell positions
        currentFoodCells = new HashSet<Vector3Int>(newFoodPositions.Select(f => obstacleMap.WorldToCell(f.transform.position)));

        // Detect any difference between previous and current food cells
        bool foodChanged = !currentFoodCells.SetEquals(previousFoodCells);

        if (foodChanged)
        {
            foodPositions = newFoodPositions;
            updateFood();
        }

        // Always update previousFoodCells at the end
        previousFoodCells = new HashSet<Vector3Int>(currentFoodCells);
    }

    /*
     * Creates the food clusters from the list of food
     * A cluster contains food that are on adjacent positions
     * Sorts the food from the fursthest to x=0 to the closest
     * Stores them in positivClusters and negativeClusters
     */
    private static void updateFood()
    {
        foodClusters = new List<List<Vector3Int>>();
        HashSet<Vector3Int> visited = new();
        HashSet<Vector3Int> foodCellsSet = new(foodPositions.Select(f => obstacleMap.WorldToCell(f.transform.position)));

        foreach (var position in foodCellsSet)
        {
            if (visited.Contains(position) || position.x >= 0) continue;

            List<Vector3Int> cluster = new();
            Queue<Vector3Int> queue = new();
            queue.Enqueue(position);
            visited.Add(position);

            while (queue.Count > 0)
            {
                Vector3Int current = queue.Dequeue();
                cluster.Add(current);

                foreach (Vector3Int neighbor in GetAdjacentCells(current))
                {
                    if (!visited.Contains(neighbor) && foodCellsSet.Contains(neighbor))
                    {
                        queue.Enqueue(neighbor);
                        visited.Add(neighbor);
                    }
                }
            }

            foodClusters.Add(cluster);
        }

        // Sort clusters by max absolute X only once
        foodClusters.Sort((a, b) =>
        {
            int maxXA = int.MinValue, maxXB = int.MinValue;
            foreach (var pos in a) maxXA = Mathf.Max(maxXA, Mathf.Abs(pos.x));
            foreach (var pos in b) maxXB = Mathf.Max(maxXB, Mathf.Abs(pos.x));
            return maxXB.CompareTo(maxXA);
        });
    }

    /*
     * Get a list containing the four adjacent cells of a cell position
     */
    private static List<Vector3Int> GetAdjacentCells(Vector3Int position)
    {
        List<Vector3Int> neighbors = new List<Vector3Int>();
        int cellSize = 1; 

        neighbors.Add(new Vector3Int(position.x + cellSize, position.y, position.z));
        neighbors.Add(new Vector3Int(position.x - cellSize, position.y, position.z));
        neighbors.Add(new Vector3Int(position.x, position.y, position.z + cellSize));
        neighbors.Add(new Vector3Int(position.x, position.y, position.z - cellSize));

        return neighbors;
    }

    /*
     * Comparator for sorting clusters based on their furthest x distance from x = 0
     */
    private static int CompareClusters(List<Vector3Int> a, List<Vector3Int> b)
    {
        float maxXA = a.Max(pos=>Mathf.Abs(pos.x));
        float maxXB = b.Max(pos=>Mathf.Abs(pos.x));
        return maxXB.CompareTo(maxXA);
    }

    public static (Vector3Int, List<Vector3Int>) FindFurthestAvailableCluster(Vector3 startPos)
    {
        Vector3Int startCell = obstacleMap.WorldToCell(startPos);

        foreach (var cluster in foodClusters)
        {
            if (claimedClusters.Contains(cluster)) continue;
            claimedClusters.Add(cluster);

            Vector3Int closest = cluster[0];
            float minDist = float.MaxValue;
            foreach (var pos in cluster)
            {
                float dist = (pos - startCell).sqrMagnitude;
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = pos;
                }
            }

            return (closest, cluster);
        }

        return (Vector3Int.zero, null);
    }


    public static Vector3Int? CheckForFoodLoss(bool isBlue)
    {
        // Any lost food in our half?
        HashSet<Vector3Int> lost = new HashSet<Vector3Int>(previousFoodCells);
        lost.ExceptWith(currentFoodCells);

        foreach (var cell in lost)
        {
            if (IsOnMySide(cell))
            {
                return cell;
            }
        }
        return null;
    }

    public static bool IsOnMySide(Vector3 pos)
    {
        return isBlue ? pos.x < 0 : pos.x >= 0;
    }

    public static void UpdateDefenseAssignments()
    {
        List<(GameObject intruder, Vector3 position)> visible = new();

        // Only get visible enemies from one agent (avoid repeated calls)
        foreach (var enemy in defenders[0].GetVisibleEnemyAgents())
        {
            if (IsOnMySide(enemy.gameObject.transform.position))
            {
                visible.Add((enemy.gameObject, enemy.gameObject.transform.position));
            }
        }

        // Sort defenders by distance to intruders for greedy assignment
        bool[] assigned = new bool[defenders.Count];
        foreach (var (intruder, position) in visible)
        {
            float minDist = float.MaxValue;
            int bestIdx = -1;

            for (int i = 0; i < defenders.Count; i++)
            {
                if (assigned[i]) continue;

                float dist = (defenders[i].gameObject.transform.position - position).sqrMagnitude;
                if (dist < minDist)
                {
                    minDist = dist;
                    bestIdx = i;
                }
            }

            if (bestIdx != -1)
            {
                assigned[bestIdx] = true;
                agentDefenseStates[bestIdx] = new CentralGameTrackerBlue.DefenseAssignment
                {
                    State = CentralGameTrackerBlue.DefenseState.Chase,
                    TargetPosition = position,
                    TargetIntruder = intruder
                };
            }
        }

        // Idle assignment for unassigned defenders
        for (int i = 0; i < defenders.Count; i++)
        {
            if (!assigned[i])
            {
                if (agentDefenseStates.TryGetValue(i, out var current) && current.State == CentralGameTrackerBlue.DefenseState.Patrol)
                    continue;

                agentDefenseStates[i] = new CentralGameTrackerBlue.DefenseAssignment
                {
                    State = CentralGameTrackerBlue.DefenseState.Idle
                };
            }
        }
    }


    public static CentralGameTrackerBlue.DefenseAssignment GetDefenseAssignment(int pacManIndex)
    {
        if (agentDefenseStates.TryGetValue(pacManIndex, out var assignment))
            return assignment;

        return new CentralGameTrackerBlue.DefenseAssignment { State = CentralGameTrackerBlue.DefenseState.Idle };
    }

    public static void SetDefenseAssignment(IPacManAgent pacman, CentralGameTrackerBlue.DefenseState assignment)
    {
        int pacManIndex = agentAgentManager.GetFriendlyAgents().IndexOf(pacman);
        agentDefenseStates[pacManIndex] = new CentralGameTrackerBlue.DefenseAssignment { State = assignment};
    }
}


