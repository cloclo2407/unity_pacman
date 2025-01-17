using Unity.Netcode;
using UnityEngine;

namespace PacMan
{
    public class PacManWorkerNetwork : MonoBehaviour, IPacManWorkerInterface
    {
        public GameObject CreateEdible(GameObject parent, GameObject prefab, Vector3 position)
        {
            GameObject spawn = Instantiate(prefab, position, Quaternion.identity);
            var networkObject = spawn.GetComponent<NetworkObject>();
            networkObject.Spawn();
            networkObject.TrySetParent(parent);
            spawn.name = prefab.name;

            return spawn;
        }

        public GameObject CreateAgent(GameObject prefab, GameObject parent, Vector3 position, string team_tag)
        {
            prefab.tag = team_tag;
            GameObject agent = Instantiate(prefab, Vector3.zero, Quaternion.identity);

            var manager = agent.GetComponent<PacManAgentManagerNetwork>();
            var networkObject = agent.GetComponent<NetworkObject>();

            agent.tag = team_tag;
            agent.name = prefab.name;
            agent.transform.position = position + prefab.transform.position;

            networkObject.Spawn();

            manager.synctag.Value = team_tag;
            manager.syncposition.Value = agent.transform.position;
            networkObject.TrySetParent(parent);

            return agent;
        }

        public void RemoveObject(GameObject toRemove)
        {
            var networkObject = toRemove.GetComponent<NetworkObject>();
            if (networkObject.IsSpawned)
            {
                networkObject.Despawn();
            }
        }

        public void ResetAgent(GameObject agent)
        {
            var manager = agent.GetComponent<PacManAgentManagerNetwork>();
            agent.transform.position = manager.globalStartPosition;
            manager.foodCarried.Value = 0;
            manager.isScared.Value = false;
            manager.isPowered.Value = false;
            manager.scaredUntil.Value = 0.0;

            agent.GetComponent<PacManMovementController>().enabled = true;
        }
    }
}