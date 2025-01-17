using System;
using UnityEngine;

namespace PacMan
{
    public enum Team
    {
        Red,
        Blue,
        Undefined
    }

    public static class TeamAssignmentUtil
    {
        public static Team CheckTeam(GameObject toCheck)
        {
            if (toCheck.CompareTag("Red")) return Team.Red;
            if (toCheck.CompareTag("Blue")) return Team.Blue;
            if (toCheck.transform.localPosition.x < 0) return Team.Blue; //TODO: Use collider zones instead? Performance?
            if (toCheck.transform.localPosition.x > 0) return Team.Red;
            return Team.Undefined;
        }
    }
}