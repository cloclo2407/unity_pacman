using System;
using System.Linq;
using PacMan.Local;
using UnityEngine;

namespace PacMan
{
    public class PacManGUI : MonoBehaviour
    {
        private PacManGameManager gameManager;

        private void Awake()
        {
            gameManager = GetComponent<PacManGameManager>();
        }

        private void OnGUI()
        {
            StatusLabels(gameManager);
        }

        static void StatusLabels(PacManGameManager gameManager)
        {
            var style = new GUIStyle();
            style.fontSize = 20;
            style.normal.textColor = Color.white;

            GUILayout.Label("Time: " + gameManager.matchTime.ToString("0.00"), style);
            var redScore = gameManager.redScore;
            var blueScore = gameManager.blueScore;
            
            if (redScore > blueScore)
            {
                GUILayout.Label("Red Lead: " + (redScore - blueScore), style);
            }
            else if (blueScore > redScore)
            {
                GUILayout.Label("Blue Lead: " + (blueScore - redScore), style);
            }
            else
            {
                GUILayout.Label("Tied!", style);
            }
            
            GUILayout.Label("Red score: " + redScore, style);
            GUILayout.Label("Blue score: " + blueScore, style);
            GUILayout.Label("Red food: " + gameManager.redFood, style);
            GUILayout.Label("Blue food: " + gameManager.blueFood, style);
            GUILayout.Label("Red carried: " + gameManager.redAgents.Select(agn => agn.GetComponent<PacManAgentManager>().foodCarried).Sum(), style);
            GUILayout.Label("Blue carried: " + gameManager.blueAgents.Select(agn => agn.GetComponent<PacManAgentManager>().foodCarried).Sum(), style);
          

            GUILayout.Label("", style);
        }
    }
}