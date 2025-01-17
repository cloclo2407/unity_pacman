using System;
using System.Collections.Generic;
using System.Linq;
using PacMan.Local;
using UnityEngine;

namespace PacMan
{
    public static class GameStateParser
    {
        public static GameState WriteState(PacManGameManager _gameManager)
        {
            var storeState = new GameState();
            storeState.time = _gameManager.matchTime;
            storeState.mapName = _gameManager.mapManager.fileName;
            storeState.food = _gameManager.foodList.Select(food => Edible.FromObject(food)).ToList();
            storeState.capsules = _gameManager.capsules.Select(capsule => Edible.FromObject(capsule, true)).ToList();
            storeState.agents = _gameManager.agents.Select(agent => agent.GetComponent<PacManAgentManager>()).Select(PacManState.FromObject).ToList();
            return storeState;
        }

        public static GameState ReadState(string gameState)
        {
            var saveData = new GameState();
            JsonUtility.FromJsonOverwrite(gameState, saveData);
            return saveData;
        }


        [Serializable]
        public struct GameState
        {
            public float time;
            public List<Edible> food;
            public List<Edible> capsules;
            public List<PacManState> agents;
            public string mapName;
        }

        [Serializable]
        public struct PacManState
        {
            public LocalTransform transform;
            public int foodCarried;
            public bool isScared;
            public bool isGhost;
            public bool isPowered;
            public double scaredUntil;

            public Vector2 direction;
            public float magnitude;
            public Vector3 velocity;
            public Vector3 angularVelocity;

            public static PacManState FromObject(PacManAgentManager agent)
            {
                var pacManState = new PacManState();
                pacManState.transform = LocalTransform.FromTransform(agent.transform);
                pacManState.foodCarried = agent.foodCarried;
                pacManState.isScared = agent.isScared;
                pacManState.isGhost = agent.isGhost;
                pacManState.isPowered = agent.IsPoweredUp();
                pacManState.scaredUntil = agent.scaredUntil;

                pacManState.direction = agent.action.AccelerationDirection;
                pacManState.magnitude = agent.action.AccelerationMagnitude;
                pacManState.velocity = agent.GetComponent<Rigidbody>().linearVelocity;
                pacManState.angularVelocity = agent.GetComponent<Rigidbody>().angularVelocity;
                return pacManState;
            }
        }


        [Serializable]
        public struct Edible
        {
            public LocalTransform transform;
            public bool isCapsule;

            public static Edible FromObject(GameObject gameObject, bool isCapsule = false)
            {
                var fromObject = new Edible();
                fromObject.isCapsule = isCapsule;
                fromObject.transform = LocalTransform.FromTransform(gameObject.transform);
                return fromObject;
            }
        }

        [Serializable]
        public struct LocalTransform
        {
            public Vector3 position;
            public Quaternion rotation;

            public static LocalTransform FromTransform(Transform agentTransform)
            {
                var fromTransform = new LocalTransform();
                fromTransform.position = agentTransform.localPosition;
                fromTransform.rotation = agentTransform.localRotation;
                return fromTransform;
            }
        }
    }
}