using System.Collections.Generic;
using PacMan.PacMan;
using Scripts.Map;
using UnityEngine;

public class Defense
{
    static public List<List<(Vector2Int, Vector2Int)>> passageGroups; // Stores groups of adjacent passages

    /*
     * Groups transition pairs into adjacent passages and sorts them by size.
     */
    private static void ComputePassageGroups()
    {
        passageGroups = new();

        // Filter transition pairs based on the team side
        List<(Vector2Int, Vector2Int)> filteredPairs = AllPairsShortestPaths.transitionPairs;

        // Sort by y-coordinate
        filteredPairs.Sort((a, b) => a.Item1.y.CompareTo(b.Item1.y));

        List<(Vector2Int, Vector2Int)> currentGroup = new();
        int prevY = int.MinValue;

        foreach (var pair in filteredPairs)
        {
            if (currentGroup.Count == 0 || pair.Item1.y == prevY + 1) // Adjacent y-values
            {
                currentGroup.Add(pair);
            }
            else
            {
                if (currentGroup.Count > 0)
                    passageGroups.Add(new List<(Vector2Int, Vector2Int)>(currentGroup));

                currentGroup.Clear();
                currentGroup.Add(pair);
            }
            prevY = pair.Item1.y;
        }

        // Add last group
        if (currentGroup.Count > 0)
            passageGroups.Add(new List<(Vector2Int, Vector2Int)>(currentGroup));

        // Sort passage groups by size (largest first)
        passageGroups.Sort((a, b) => b.Count.CompareTo(a.Count));
    }

    /*
     * Return a list of position where we should put the defense pacman
     * The positions are equi situated among all the positions that allow crossing between both sides
     */
    public static List<Vector2Int> GetDefensePositions(int n, bool isBlue)
    {
        List<Vector2Int> selectedPositions = new();

        // Ensure passageGroups are computed
        ComputePassageGroups();

        // Flatten passage groups into a single sorted list of unique positions
        List<Vector2Int> allPositions = new();
        foreach (var group in passageGroups)
        {
            foreach (var pair in group)
            {
                Vector2Int position = isBlue ? pair.Item2 : pair.Item1;
                if (!allPositions.Contains(position))
                    allPositions.Add(position);
            }
        }

        if (allPositions.Count == 0 || n <= 0) return selectedPositions;

        // Sort by y to distribute selections evenly
        allPositions.Sort((a, b) => a.y.CompareTo(b.y));

        // Select n positions that minimize max distance
        int step = allPositions.Count / n;
        for (int i = 0; i < n; i++)
        {
            int index = i * step + step / 2; // Centered selection
            index = Mathf.Clamp(index, 0, allPositions.Count - 1);
            selectedPositions.Add(allPositions[index]);
        }

        return selectedPositions;
    }



    public static int GetNumberOfPassages(bool isBlue)
    {
        ComputePassageGroups();
        return passageGroups.Count;
    }
}