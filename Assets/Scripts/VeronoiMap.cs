using System.Collections.Generic;
using PacMan;
using PacMan.PacMan;
using Scripts.Map;
using UnityEngine;

public class VeronoiMap
{
    public int _nrAgents;
    public Dictionary<Vector2Int, int> closestAgent;

    ////// WARNING: everything is in cell coordinates 
    ////// Go back to world coordinates with _obstacleMap.CellToWorld(new Vector3Int((int)node.x, 0, (int)node.y)) + _obstacleMap.trueScale / 2

    /*
     * Create a copy of the map
     * For each point, check what agent is closest
        * Make use of the AllPairsShortestPaths? For each cell Find path to each agent and get its length
         *  Compare with just using euclidean distance both computation speed and performance
     * Color each gridcell according to what agent is closest
     */
    
    
    public void GenerateMap(ObstacleMap obstacleMap, List<Vector3> _allAgentPositions)
    {
        /*
         Inputs:
         * obstacleMap
         * shortestPaths between all points - pre-calculated
         * List<Vector3> the real world positions of all agents, should be kept track of in the AI somewhere else
         */
        //Initialize all Traversable cells as -1
        _nrAgents = _allAgentPositions.Count;
        closestAgent = new Dictionary<Vector2Int, int>();
        foreach (var cell in obstacleMap.traversabilityPerCell)
        {
            
            if (cell.Value != ObstacleMap.Traversability.Blocked)
            {
                var closestAgentIndex = -1;
                var closestAgentDistance = Mathf.Infinity;
                for (int i = 0; i < _nrAgents; i++)
                {

                    var agentPos = _allAgentPositions[i];
                    if (agentPos != Vector3.zero)
                    {
                        var worldToCell = obstacleMap.WorldToCell(agentPos);
                        var agentCellPos = new Vector2Int(worldToCell.x, worldToCell.z);
                    
                        var dist = AllPairsShortestPaths.ComputeShortestPath(agentCellPos, cell.Key).Count;
                        //var dist = Vector2Int.Distance(agentCellPos, cell.Key);
                        if (dist < closestAgentDistance)
                        {
                            closestAgentIndex = i;
                            closestAgentDistance = dist;
                        }
                    }
                    
                }

                closestAgent[cell.Key] = closestAgentIndex;
            }
        }
    }
    
    
    
    
    
    
    

}