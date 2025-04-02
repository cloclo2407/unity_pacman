using System.Collections.Generic;
using PacMan.PacMan;
using Scripts.Map;
using UnityEngine;

public class AllPairsShortestPaths
{
    static public Dictionary<Vector2Int, Dictionary<Vector2Int, float>> distances; // distances between all nodes
    static Dictionary<Vector2Int, List<(Vector2Int, float)>> graph; // graph 
    static Dictionary<Vector2Int, Dictionary<Vector2Int, Vector2Int?>> predecessor; // Contains predecessor in FloydWharshall to recompute paths
    public static List<(Vector2Int, Vector2Int)> transitionPairs; // Stores pairs (leftCell, rightCell) where crossing is possible

    private static bool _initialized = false; // true if the distances have already been computed (to avoid recomputation by all agents)

    ////// WARNING: everything is in cell coordinates 
    ////// Go back to world coordinates with _obstacleMap.CellToWorld(new Vector3Int((int)node.x, 0, (int)node.y)) + _obstacleMap.trueScale / 2

    /*
     * Create graphs for the map and calculates shortests distances
     * Set _initialized to true when done so other agents don't recompute it
     */
    public static void ComputeAllPairsShortestPaths(ObstacleMap obstacleMap)
    {
        if (_initialized) return; // Prevent recomputation

        GenerateGraph(obstacleMap);
        predecessor = new Dictionary<Vector2Int, Dictionary<Vector2Int, Vector2Int?>>();
        FindTransitionPairs(obstacleMap);
        distances= FloydWarshall(graph);

        _initialized = true;
    }
    
    /*
     * Returns the dictionary containing all pairs shortests distances for x>0 is positiveX is true, for x<0 otherwise
     */
    public static Dictionary<Vector2Int, Dictionary<Vector2Int, float>> GetDistances()
    {
        return distances;
    }

    /*
     * Calculates all pairs shortest paths in a graph
     * Returns the dictionary contains the distances
     * Stores the predecessor of each node in predecessor for computing the path
     */
    public static Dictionary<Vector2Int, Dictionary<Vector2Int, float>> FloydWarshall(Dictionary<Vector2Int, List<(Vector2Int, float)>> graph)
    {
        var nodes = new List<Vector2Int>(graph.Keys);
        var distance = new Dictionary<Vector2Int, Dictionary<Vector2Int, float>>();
        float INF = float.MaxValue;

        // Initialize distance and predecessor matrices
        foreach (var u in nodes)
        {
            distance[u] = new Dictionary<Vector2Int, float>();
            predecessor[u] = new Dictionary<Vector2Int, Vector2Int?>();

            foreach (var v in nodes)
            {
                distance[u][v] = (u == v) ? 0 : INF;
                predecessor[u][v] = null;
            }

            foreach (var (v, weight) in graph[u])
            {
                distance[u][v] = weight;
                predecessor[u][v] = u;
            }
        }

        // Floyd-Warshall algorithm
        foreach (var k in nodes)
        {
            foreach (var i in nodes)
            {
                foreach (var j in nodes)
                {
                    if (distance[i][k] < INF && distance[k][j] < INF &&
                        distance[i][j] > distance[i][k] + distance[k][j])
                    {
                        distance[i][j] = distance[i][k] + distance[k][j];
                        predecessor[i][j] = predecessor[k][j];
                    }
                }
            }
        }

        return distance;
    }

    /*
     * Constructs two graphs to represent the map
     * graph_pos contains cells for x>0
     * graph_neg contains cells for x<0
     * Edge of weight 1 between adjacent empty cells
     * Edge of weight sqrt(2) between diagonals empty cells if the two cells around the diagonal are also free
     */
    public static void GenerateGraph(ObstacleMap obstacleMap)
    {
        graph = new();
        var currentGraph = graph;

        // 1. Get all free cells
        List<Vector2Int> freeCells = new();
        foreach (var cell in obstacleMap.traversabilityPerCell)
        {
            if (cell.Value != ObstacleMap.Traversability.Blocked)
            {
                freeCells.Add(cell.Key);
            }
        }

        Vector2Int[] directions = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };
        Vector2Int[] diagonals = { new(1, 1), new(1, -1), new(-1, 1), new(-1, -1) };

        // 2. Construct graph for each free cell
        foreach (Vector2Int current in freeCells)
        {
            currentGraph[current] = new List<(Vector2Int, float)>();

            // Add direct neighbors
            foreach (var dir in directions)
            {
                Vector2Int neighbor = current + dir;
                if (freeCells.Contains(neighbor)) // Only connect to other free cells
                {
                    currentGraph[current].Add((neighbor, 1f));
                }
            }

            // Add diagonal neighbors (only if both adjacent cells are free)
            foreach (var diag in diagonals)
            {
                Vector2Int neighbor = current + diag;
                Vector2Int adj1 = new Vector2Int(current.x, neighbor.y);
                Vector2Int adj2 = new Vector2Int(neighbor.x, current.y);

                if (freeCells.Contains(neighbor) && freeCells.Contains(adj1) && freeCells.Contains(adj2))
                {
                    currentGraph[current].Add((neighbor, Mathf.Sqrt(2)));
                }
            }
        }
    }

    /*
    * Finds all pairs of adjacent cells (x=-1 and x=0) that allow crossing between x<0 and x>0
    * Stores them in transitionPairs
    */
    private static void FindTransitionPairs(ObstacleMap obstacleMap)
    {
        transitionPairs = new List<(Vector2Int, Vector2Int)>();

        // Check all cells near x=0 (both x=-1 and x=0)
        foreach (var cell in obstacleMap.traversabilityPerCell)
        {
            Vector2Int pos = cell.Key;

            // Only consider cells at x=-1 or x=0
            if (pos.x == -1 || pos.x == 0)
            {
                // Check if the cell is traversable
                if (cell.Value != ObstacleMap.Traversability.Blocked)
                {
                    // If current cell is x=-1, check if x=0 at the same y is free
                    if (pos.x == -1)
                    {
                        Vector2Int rightCell = new Vector2Int(0, pos.y);
                        if (obstacleMap.traversabilityPerCell.ContainsKey(rightCell) &&
                            obstacleMap.traversabilityPerCell[rightCell] != ObstacleMap.Traversability.Blocked)
                        {
                            transitionPairs.Add((pos, rightCell));
                        }
                    }
                    // If current cell is x=0, check if x=-1 at the same y is free
                    else if (pos.x == 0)
                    {
                        Vector2Int leftCell = new Vector2Int(-1, pos.y);
                        if (obstacleMap.traversabilityPerCell.ContainsKey(leftCell) &&
                            obstacleMap.traversabilityPerCell[leftCell] != ObstacleMap.Traversability.Blocked)
                        {
                            transitionPairs.Add((pos, leftCell));
                        }
                    }
                }
            }
        }
    }

    /*
     * Computes a path from start to goal
     * Returns the path as a list of Vector2Int
     * To go from one side of the map to the other, finds the pairs of cells to cross the border that minimizes the total distance
     */
    public static List<Vector2Int> ComputeShortestPath(Vector2Int start, Vector2Int goal)
    {
        var path = new List<Vector2Int>();
        if (!predecessor.ContainsKey(start) || !predecessor[start].ContainsKey(goal))
        {
            Debug.Log("not in predecessor");
            return path;
        }

        Vector2Int? current = goal;
        while (current != null && current != start)
        {
            path.Add(current.Value);
            current = predecessor[start][current.Value];
        }

        if (current == null)
        {
            return new List<Vector2Int>(); // No path exists
        }

        path.Add(start);
        path.Reverse();
        return path;
    }

    /*
     * Helper function to compute the shortest path between start and goal IFF start and goal are in the same half of the map
     */
    private static List<Vector2Int> ReconstructPath(Vector2Int start, Vector2Int goal)
    {
        var path = new List<Vector2Int>();
        if (!predecessor.ContainsKey(start) || !predecessor[start].ContainsKey(goal))
        {
            Debug.Log("not in predecessor");
            return path;
        }

        Vector2Int? current = goal;
        while (current != null && current != start)
        {
            path.Add(current.Value);
            current = predecessor[start][current.Value];
        }

        if (current == null)
        {
            return new List<Vector2Int>(); // No path exists
        }

        path.Add(start);
        path.Reverse();
        return path;
    }

}