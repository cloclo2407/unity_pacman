using System;
using System.Collections.Generic;
using System.Linq;
using PacMan.Local;
using Unity.Netcode;
using UnityEngine;

namespace PacMan
{
    public class PacManGameManagerNetwork : PacManGameManager
    {
        public float planningTime = 10f;

        public override void Start()
        {
        }

        public override void StartGame()
        {
            if (NetworkManager.Singleton.IsServer && NetworkManager.Singleton.ConnectedClients.Count == 2 || NetworkManager.Singleton.IsHost)
            {
                base.StartGame();
                StartTime = NetworkManager.Singleton.ServerTime.TimeAsFloat;

                var singletonConnectedClients = NetworkManager.Singleton.ConnectedClients;
                blueAgents.ForEach(agent => agent.GetComponent<NetworkObject>().ChangeOwnership(singletonConnectedClients.Keys.First()));
                redAgents.ForEach(agent => agent.GetComponent<NetworkObject>().ChangeOwnership(singletonConnectedClients.Keys.Last()));
                started = true;
            }

            if (NetworkManager.Singleton.IsClient)
            {
                NetworkManager.Singleton.NetworkTickSystem.Tick += NetworkTick;
                started = true;
            }
        }

        public void NetworkTick()
        {
            if (NetworkManager.Singleton.IsClient) UpdateItemsForClient();
        }

        new void FixedUpdate()
        {
            if (NetworkManager.Singleton == null) return;
            
            if (started && NetworkManager.Singleton.IsServer)
            {
                if (!finished)
                {
                    matchTime = (float)(NetworkManager.Singleton.ServerTime.FixedTime - StartTime);
                    blueFood = foodList.FindAll(food => TeamAssignmentUtil.CheckTeam(food) == Team.Blue).Count;
                    redFood = foodList.FindAll(food => TeamAssignmentUtil.CheckTeam(food) == Team.Red).Count;
                    blueScore = blueFood - redFood;
                    redScore = redFood - blueFood;
                    if (matchTime > matchLength) finished = true;
                }
            }

            if (NetworkManager.Singleton.IsServer && finished && AutomaticRestart)
            {
                RestartGame();
            }
        }

        private void UpdateItemsForClient()
        {
            agents = new List<GameObject>();
            foodList = new List<GameObject>();
            capsules = new List<GameObject>();
            foreach (Transform transf in transform)
            {
                if (transf.name.ToLower().Contains("pacman")) agents.Add(transf.gameObject);
                if (transf.name.ToLower().Contains("food")) foodList.Add(transf.gameObject);
                if (transf.name.ToLower().Contains("capsule")) capsules.Add(transf.gameObject);
            }
        }
    }
}