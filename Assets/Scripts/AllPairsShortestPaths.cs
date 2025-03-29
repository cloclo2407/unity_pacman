using System.Collections.Generic;
using PacMan.PacMan;
using Scripts.Map;
using UnityEngine;

public class AllPairsShortestPaths
{
    static Dictionary<Vector2, Dictionary<Vector2, float>> distances_pos; // distances between nodes with x>0
    static Dictionary<Vector2, Dictionary<Vector2, float>> distances_neg; // distances between nodes with x<0
    static Dictionary<Vector2, List<(Vector2, float)>> graph_neg; // graph for x<0
    static Dictionary<Vector2, List<(Vector2, float)>> graph_pos; // graph for x>0
    private static bool _initialized = false; // true if the distances have already been computed (to avoid recomputation by all agents)

    /*
     * Create graphs for the map and calculates shortests distances
     * Set _initialized to true when done so other agents don't recompute it
     */
    public static void ComputeAllPairsShortestPaths(ObstacleMap obstacleMap)
    {
        if (_initialized) return; // Prevent recomputation

        GenerateGraph(obstacleMap);
        distances_pos = FloydWarshall(graph_pos);
        distances_neg = FloydWarshall(graph_neg);
        _initialized = true;
    }
    
    /*
     * Returns the dictionary containing all pairs shortests distances for x>0 is positiveX is true, for x<0 otherwise
     */
    public static Dictionary<Vector2, Dictionary<Vector2, float>> GetDistances(bool positiveX)
    {
        return positiveX ? distances_pos : distances_neg;
    }

    /*
     * Calculates all pairs shortest paths in a graph
     * Returns the dictionary contains the distances
     */
    public static Dictionary<Vector2, Dictionary<Vector2, float>> FloydWarshall(Dictionary<Vector2, List<(Vector2, float)>> graph)
    {
        var nodes = new List<Vector2>(graph.Keys);
        int n = nodes.Count;
        var distance = new Dictionary<Vector2, Dictionary<Vector2, float>>();
        float INF = float.MaxValue;

        // Initialize distance matrix
        foreach (var node in nodes)
        {
            distance[node] = new Dictionary<Vector2, float>();
            foreach (var other in nodes)
            {
                distance[node][other] = node == other ? 0 : INF;
            }

            foreach (var (neighbor, weight) in graph[node])
            {
                distance[node][neighbor] = weight;
            }
        }

        // Floyd-Warshall algorithm
        foreach (var k in nodes)
        {
            foreach (var i in nodes)
            {
                foreach (var j in nodes)
                {
                    if (distance[i][k] < INF && distance[k][j] < INF)
                    {
                        distance[i][j] = Mathf.Min(distance[i][j], distance[i][k] + distance[k][j]);
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
        graph_pos = new();
        graph_neg = new();

        // 1. Get all free cells
        List<Vector2> freeCells = new();
        foreach (var cell in obstacleMap.traversabilityPerCell)
        {
            if (cell.Value != ObstacleMap.Traversability.Blocked)
            {
                freeCells.Add(cell.Key);
            }
        }

        Vector2[] directions = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };
        Vector2[] diagonals = { new(1, 1), new(1, -1), new(-1, 1), new(-1, -1) };

        // 2. Construct graph for each free cell
        foreach (Vector2 current in freeCells)
        {
            var currentGraph = current.x >= 0 ? graph_pos : graph_neg;
            currentGraph[current] = new List<(Vector2, float)>();

            // Add direct neighbors
            foreach (var dir in directions)
            {
                Vector2 neighbor = current + dir;
                if (freeCells.Contains(neighbor)) // Only connect to other free cells
                {
                    currentGraph[current].Add((neighbor, 1f));
                }
            }

            // Add diagonal neighbors (only if both adjacent cells are free)
            foreach (var diag in diagonals)
            {
                Vector2 neighbor = current + diag;
                Vector2 adj1 = new Vector2(current.x, neighbor.y);
                Vector2 adj2 = new Vector2(neighbor.x, current.y);

                if (freeCells.Contains(neighbor) && freeCells.Contains(adj1) && freeCells.Contains(adj2))
                {
                    currentGraph[current].Add((neighbor, Mathf.Sqrt(2)));
                }
            }
        }
    }

}