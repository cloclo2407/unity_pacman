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

    public static List<Vector3> agentPositions;

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

        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();

        foreach (GameObject food in foodPositions)
        {
            Vector3Int position = obstacleMap.WorldToCell(food.transform.position);

            if (!visited.Contains(position) && position.x < 0)
            {
                List<Vector3Int> cluster = new List<Vector3Int>();
                Queue<Vector3Int> queue = new Queue<Vector3Int>();
                queue.Enqueue(position);
                visited.Add(position);

                // BFS to find all adjacent food positions
                while (queue.Count > 0)
                {
                    Vector3Int current = queue.Dequeue();
                    cluster.Add(current);
                    foreach (Vector3Int neighbor in GetAdjacentCells(current))
                    {
                        if (!visited.Contains(neighbor) && foodPositions.Any(f => obstacleMap.WorldToCell(f.transform.position) == neighbor))
                        {
                            queue.Enqueue(neighbor);
                            visited.Add(neighbor);
                        }
                    }
                }
                foodClusters.Add(cluster);
            }
        }
        // Sort clusters by their furthest x distance from x = 0
        foodClusters.Sort((a, b) => CompareClusters(b, a)); // Reversed for x < 0
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
        foreach (var cluster in foodClusters)
        {
            if (claimedClusters.Contains(cluster)) continue;

            // Mark this cluster as claimed so others don't pick it
            claimedClusters.Add(cluster);

            // Choose the food in the cluster closest to current position (as entry point)
            Vector3Int targetFood = cluster.OrderBy(pos => (pos - obstacleMap.WorldToCell(startPos)).sqrMagnitude).First();
            return (targetFood, cluster);
        }

        // No unclaimed cluster found
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
        List<Vector3> noisy = new();

        // 1. Find visible intruders on your side
        //foreach (var agent in defenders)
        {
            foreach (var enemy in defenders[0].GetVisibleEnemyAgents())
            {
                if (IsOnMySide(enemy.gameObject.transform.position))
                {
                    visible.Add((enemy.gameObject, enemy.gameObject.transform.position));
                }
            }
        }

        // 2. Assign one defender per visible intruder
        int i = PacManAI.attackers;
        foreach (var v in visible)
        {
            if (i >= defenders.Count) break;
            var defender = defenders[i];
            var pacIndex = defenders.IndexOf(defender);
            agentDefenseStates[pacIndex] = new CentralGameTrackerBlue.DefenseAssignment
            {
                State = CentralGameTrackerBlue.DefenseState.Chase,
                TargetPosition = v.position,
                TargetIntruder = v.intruder
            };                
            i++;
        }

        // 4. Lost food?
        /*Vector3Int? lostFood = CheckForFoodLoss(isBlue);
        if (lostFood != null)
        {
            Vector3 worldLost = obstacleMap.CellToWorld(lostFood.Value);
            if (i < defenders.Count)
            {
                var pacIndex = defenders.IndexOf(defenders[i]);
                agentDefenseStates[pacIndex] = new DefenseAssignment
                {
                    State = DefenseState.Investigate,
                    TargetPosition = worldLost
                };
                i++;
            }
        }*/

        // 5. Remaining defenders patrol/idle
        for (; i < defenders.Count; i++)
        {
            var pacIndex = defenders.IndexOf(defenders[i]);

            // If already in the dictionary and in Patrol state, keep it
            if (agentDefenseStates.TryGetValue(pacIndex, out var assignment) && assignment.State == CentralGameTrackerBlue.DefenseState.Patrol)
            {
                continue;
            }
            else {
                agentDefenseStates[pacIndex] = new CentralGameTrackerBlue.DefenseAssignment
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


