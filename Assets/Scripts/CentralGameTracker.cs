using System.Collections.Generic;
using PacMan;
using PacMan.PacMan;
using Scripts.Map;
using UnityEditor.UI;
using UnityEngine;
using System.Linq;

public static class CentralGameTracker
{
    
    private static bool _initialized = false;
    
    private static int _nrAgents;
    private static int _nrFriendlyAgents;
    private static IPacManAgent agentAgentManager;
    private static ObstacleMap obstacleMap;

    public static List<Vector3> agentPositions;

    private static List<GameObject> foodPositions;
    public static List<List<Vector3Int>> positiveClusters;
    public static List<List<Vector3Int>> negativeClusters;


    public static void Initialize(IPacManAgent _agentAgentManager, ObstacleMap _obstacleMap)
    {
        if (_initialized) return;
        _nrFriendlyAgents = _agentAgentManager.GetFriendlyAgents().Count;
        _nrAgents = 2 * _nrFriendlyAgents;
        agentAgentManager = _agentAgentManager;
        obstacleMap = _obstacleMap;
        foodPositions = new List<GameObject>();
        checkFood();

        _initialized = true;
    }

    
    public static void updatePositions(List<IPacManAgent> friendlyAgents, PacManObservations enemyObservations)
    {
        var newPositions = new List<Vector3>(_nrAgents);
        
        
        for (int i = 0; i < _nrFriendlyAgents; i++)
        {
            newPositions[i] = friendlyAgents[i].gameObject.transform.position;
        }
        
        for (int i = 0; i < _nrFriendlyAgents; i++)
        {
            var observation = enemyObservations.Observations[i];
            
            if (observation.Visible)
            {
                newPositions[i + _nrFriendlyAgents] = observation.Position;
            }
            else
            {
                newPositions[i + _nrFriendlyAgents] = Vector3.zero;
            }
            
        }
        
        for (int i = _nrFriendlyAgents; i < _nrAgents; i++)
        {
            if (newPositions[i] == Vector3.zero)
            {
                
            }
            
        }
        agentPositions = newPositions;
                      
    }

    /*
     * Checks if the length of the food vector has changed
     * If it has recalculates the flood clusters
     */
    public static void checkFood()
    {
        List<GameObject> newFoodPositions = agentAgentManager.GetFoodObjects();
        if (newFoodPositions.Count != foodPositions.Count)
        {
            foodPositions = newFoodPositions;
            updateFood();
        }
    }

    /*
     * Creates the food clusters from the list of food
     * A cluster contains food that are on adjacent positions
     * Sorts the food from the fursthest to x=0 to the closest
     * Stores them in positivClusters and negativeClusters
     */ 
    private static void updateFood()
    {
        positiveClusters = new List<List<Vector3Int>>();
        negativeClusters = new List<List<Vector3Int>>();

        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();

        foreach (GameObject food in foodPositions)
        {
            Vector3Int position = obstacleMap.WorldToCell(food.transform.position);

            if (!visited.Contains(position) )
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

                // Add cluster to corresponding list
                if (position.x >= 0)
                {
                    positiveClusters.Add(cluster);
                }
                else
                {
                    negativeClusters.Add(cluster);
                }
            }
        }

        // Sort clusters by their furthest x distance from x = 0
        positiveClusters.Sort((a, b)=>CompareClusters(a, b));
        negativeClusters.Sort((a, b)=>CompareClusters(b, a)); // Reversed for x < 0

        // Store or use these sorted cluster lists as needed
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

    


    public static (Vector3Int, List<Vector3Int>) FindClosestFoodCluster(Vector3 startPos, bool isBlue)
    {            
        List<Vector3Int> closestCluster = new List<Vector3Int>();
        Vector3Int closestFood = new Vector3Int();

        if (isBlue)
        {
            var closestDistance = float.MaxValue;
            foreach (var cluster in positiveClusters)
            {
                foreach (var pos in cluster)
                {
                    if ((pos - startPos).magnitude < closestDistance)
                    {
                        closestCluster = cluster;
                        closestFood = pos;
                        closestDistance = (pos - startPos).magnitude;
                    }
                    
                }
            
            }

        }
        else
        {
            var closestDistance = float.MaxValue;
            foreach (var cluster in negativeClusters)
            {
                foreach (var pos in cluster)
                {   
                    if ((pos - startPos).magnitude < closestDistance)
                    {
                        closestCluster = cluster;
                        closestFood = pos;
                        closestDistance = (pos - startPos).magnitude;
                    }
                }
            }
        }

        return (closestFood, closestCluster);
    }
    
    
    
    

}