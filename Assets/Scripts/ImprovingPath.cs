using System.Collections.Generic;
using PacMan.PacMan;
using Scripts.Map;
using UnityEngine;

public class ImprovingPath
{
    /*
     * Returns a path from start to goal as a list of real worls positions
     * Simplifies the path by removing points in straight lines
     */
    public static List<Vector3> getPath(Vector3 start, Vector3 goal, ObstacleMap obstacleMap)
    {
        Vector3Int cellStart3D = obstacleMap.WorldToCell(start);
        Vector3Int cellGoal3D = obstacleMap.WorldToCell(goal);
        Vector2Int cellStart = new Vector2Int(cellStart3D.x, cellStart3D.z);
        Vector2Int cellGoal = new Vector2Int(cellGoal3D.x, cellGoal3D.z);
        List<Vector2Int> cellPath = AllPairsShortestPaths.ComputeShortestPath(cellStart, cellGoal);
        List<Vector3> worldPath = new List<Vector3>();

        foreach (Vector2Int cell in cellPath)
        {
            Vector3 worldCoordinates = obstacleMap.CellToWorld(new Vector3Int(cell.x, 0, cell.y)) + obstacleMap.trueScale / 2;
            worldPath.Add(worldCoordinates);
        }

        worldPath = simplifyPath(worldPath);
        return worldPath;
    }

    /*
     * Remove points in straight lines
     */
    public static List<Vector3> simplifyPath(List<Vector3> path)
    {
        if (path == null || path.Count < 3)
            return path;

        List<Vector3> simplifiedPath = new List<Vector3>();
        simplifiedPath.Add(path[0]);
        simplifyRecursive(path, 0, path.Count - 1, epsilon, simplifiedPath);
        simplifiedPath.Add(path[path.Count - 1]); // add last point

        return simplifiedPath;

    }

    /*
     * Helper function for simplifyPath
     */
    private void simplifyRecursive(List<Vector3> path, int startIndex, int endIndex, float epsilon, List<Vector3> new_path)
    {
        if (endIndex <= startIndex + 1) // can't simplify two points
            return;

        float maxDistance = 0;
        int indexFurthest = 0;

        Vector3 startPoint = path[startIndex];
        Vector3 endPoint = path[endIndex];

        // Find the point farthest from the line segment
        for (int i = startIndex + 1; i < endIndex; i++)
        {
            float distance = perpendicularDistance(path[i], startPoint, endPoint);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                indexFurthest = i;
            }
        }

        if (maxDistance > epsilon) // if points too far from line, keep it
        {
            simplifyRecursive(path, startIndex, indexFurthest, epsilon, new_path); // simplify first part of the segment
            new_path.Add(path[indexFurthest]); // add the point
            simplifyRecursive(path, indexFurthest, endIndex, epsilon, new_path); // simplify second part of the segment
        }
    }

    /*
     * Get the shortest distance from a point to the line defined by lineStart and lineEnd
     */
    private float perpendicularDistance(Vector3 point, Vector3 lineStart, Vector3 lineEnd) 
    {
        Vector3 line = lineEnd - lineStart;
        Vector3 projection = Vector3.Project(point - lineStart, line);
        Vector3 closestPoint = lineStart + projection;
        return Vector3.Distance(point, closestPoint);
    }
}