using System.Collections.Generic;
using System.Linq;
using PacMan.Local;
using Unity.Netcode;
using UnityEngine;

namespace PacMan
{
    public class NetworkGUI : MonoBehaviour
    {
        private List<PacManGameManager> gameManagerPacManNetworks;

        public void Awake()
        {
            gameManagerPacManNetworks = FindObjectsByType<PacManGameManager>(FindObjectsSortMode.None).ToList();
        }

        void OnGUI()
        {
            if (NetworkManager.Singleton == null) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 300));
            if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                StartButtons();
            }

            if (gameManagerPacManNetworks.Count == 1)
            {
                var gameManager = gameManagerPacManNetworks[0];
                StatusLabels(gameManager.redFood, gameManager.blueFood, gameManager.teamName, gameManager.matchTime);
            }

            foreach (var gameManagerPacManNetwork in gameManagerPacManNetworks)
            {
                if (!gameManagerPacManNetwork.started)
                {
                    gameManagerPacManNetwork.StartGame();
                }
            }

            GUILayout.EndArea();
        }

        static void StartButtons()
        {
            if (GUILayout.Button("Host")) NetworkManager.Singleton.StartHost();
            if (GUILayout.Button("Client")) NetworkManager.Singleton.StartClient();
            if (GUILayout.Button("Server")) NetworkManager.Singleton.StartServer();
        }

        static void StatusLabels(int redScore, int blueScore, string team_name, float match_time)
        {
            var mode = NetworkManager.Singleton.IsHost ? "Host" : NetworkManager.Singleton.IsServer ? "Server" : "Client";
            var style = new GUIStyle();
            style.fontSize = 20;
            style.normal.textColor = Color.white;

            GUILayout.Label("Transport: " + NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetType().Name, style);
            GUILayout.Label("Mode: " + mode, style);
            if (NetworkManager.Singleton.IsServer)
            {
                GUILayout.Label("Clients: " + NetworkManager.Singleton.ConnectedClients.Count, style);
                GUILayout.Label("Time: " + match_time.ToString("0.00"), style);
                GUILayout.Label("Red Food: " + redScore, style);
                GUILayout.Label("Blue Food: " + blueScore, style);
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

                GUILayout.Label("", style);
            }

            if (NetworkManager.Singleton.IsClient)
            {
                if (NetworkManager.Singleton.LocalClientId == 1) GUILayout.Label("Team: Blue", style);
                if (NetworkManager.Singleton.LocalClientId == 2) GUILayout.Label("Team: Red", style);
                GUILayout.Label("Team Name: " + team_name, style);
            }
        }
    }
}