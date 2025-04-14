using System;
using System.Collections.Generic;
using System.Linq;
using PacMan.PacMan;
using Scripts.Map;
using Scripts.Utils;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace PacMan
{
    public class PacManAgentManagerNetwork : NetworkBehaviour, IPacManAgent, IPacManAgentManager
    {
        private static float _planningTimestampAllAgents = -1;
       
        public NetworkVariable<FixedString64Bytes> synctag = new NetworkVariable<FixedString64Bytes>();
        public NetworkVariable<Vector3> syncposition;
        public NetworkVariable<bool> isScared;
        public NetworkVariable<bool> isPowered;
        public NetworkVariable<double> scaredUntil;
        public NetworkVariable<Int32> foodCarried;
        public NetworkVariable<bool> isGhost;
        public NetworkVariable<Vector3> velocitySync;
        public NetworkVariable<float> matchTime;
        public NetworkVariable<int> score;

        private PacManAI pacManAI;
        private PacManAction m_action;
        private PacManMovementController _movement;
        private PacManGameManagerNetwork _pacManGameManager;
        private GameObject ghostObject;
        private GameObject pacManObject;

        public Vector3 globalStartPosition;

        private float nextObservationTime;
        private float observationUpdateInterval = 1;
        private float observationSpread = 10;

        private PacManObservations _latestKnownObservations;
        private bool initialized;
        private bool initializedAI;
        private int expectedNrOfFriendlyAgents = -1;
        private int expectedNrOfEnemyAgents = -1;


        public void OnTransformParentChanged()
        {
            //Called on client and server when the server instantiates the agent object.
            _pacManGameManager = gameObject.GetComponentInParent<PacManGameManagerNetwork>();
            if (_pacManGameManager != null)
            {
                NetworkManager.Singleton.NetworkTickSystem.Tick += NetworkTick;

                if (IsServer && !IsHost)
                {
                    NetworkObject.CheckObjectVisibility += CheckVisibility;
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            NetworkManager.Singleton.NetworkTickSystem.Tick -= NetworkTick;
            base.OnNetworkDespawn();
        }

        public virtual void LazyInitialize()
        {
            _movement = GetComponent<PacManMovementController>();
            pacManAI = GetComponent<PacManAI>();

            ghostObject = transform.Find("visuals/ghost").gameObject;
            pacManObject = transform.Find("visuals/pacman").gameObject;

            globalStartPosition = syncposition.Value;
            tag = synctag.Value.ToString();

            expectedNrOfFriendlyAgents = _pacManGameManager.mapManager.transform.FindAllChildrenWithTag("Start").FindAll(obj => TeamAssignmentUtil.CheckTeam(obj) == TeamAssignmentUtil.CheckTeam(gameObject)).Count;
            expectedNrOfEnemyAgents = _pacManGameManager.mapManager.transform.FindAllChildrenWithTag("Start").FindAll(obj => TeamAssignmentUtil.CheckTeam(obj) != TeamAssignmentUtil.CheckTeam(gameObject)).Count;
            initialized = true;
        }

        public void NetworkTick()
        {
            if (synctag.Value.IsEmpty) return;
            if (!initialized) LazyInitialize();

            ghostObject.SetActive(isGhost.Value);
            pacManObject.SetActive(!isGhost.Value);

            if (IsServer)
            {
                matchTime.Value = _pacManGameManager.matchLength - _pacManGameManager.matchTime;
                score.Value = TeamAssignmentUtil.CheckTeam(gameObject) == Team.Red ? _pacManGameManager.redScore : _pacManGameManager.blueScore;

                velocitySync.Value = GetComponent<Rigidbody>().linearVelocity;

                foreach (var clientId in NetworkManager.ConnectedClientsIds)
                {
                    var shouldBeVisibile = CheckVisibility(clientId);
                    var isVisibile = NetworkObject.IsNetworkVisibleTo(clientId);
                    if (shouldBeVisibile && !isVisibile && !IsHost)
                    {
                        // Note: This will invoke the CheckVisibility check again
                        NetworkObject.NetworkShow(clientId);
                    }
                    else if (!shouldBeVisibile && isVisibile && !IsHost)
                    {
                        NetworkObject.NetworkHide(clientId);
                    }

                    if (clientId != OwnerClientId || IsHost)
                    {
                        if (nextObservationTime < NetworkManager.Singleton.ServerTime.FixedTime)
                        {
                            var pacManObservations = ProducePacManObservations(clientId);
                            _latestKnownObservations = pacManObservations;
                            UpdateClientSideObservationsClientRpc(pacManObservations);
                            nextObservationTime = (float)(NetworkManager.Singleton.ServerTime.FixedTime + (nextObservationTime == 0 ? Random.value * observationUpdateInterval : observationUpdateInterval));
                        }
                    }
                }
            }

            //When reading this, keep in mind that NetworkBehaviors are executed on each instance of the game in parallel.
            if (IsOwner)
            {
                if (!initializedAI && AllValuesSynced())
                {
                    pacManAI.Initialize(_pacManGameManager.mapManager, GetPlanningTimeStamp());
                    initializedAI = true;
                    return; //Return, because if we chose to plan, we need a new tick to get an updated state.
                }

                if (initializedAI)
                {
                    var action = pacManAI.Tick(); // Calculate new action
                    if (NetworkManager.IsServer)
                    {
                        m_action = action; // If owned by server (host mode) store action directly.
                    }
                    else
                    {
                        UpdateServerSideActionServerRpc(action); // Send latest known action to server. Will be applied by the server.
                    }
                }
            }
        }

        private float GetPlanningTimeStamp()
        {
            if (_planningTimestampAllAgents < 0) _planningTimestampAllAgents = Time.realtimeSinceStartup + _pacManGameManager.planningTime - (_pacManGameManager.matchLength - matchTime.Value);
            return _planningTimestampAllAgents;
        }

        private bool AllValuesSynced()
        {
            return !synctag.Value.IsEmpty &&
                   _latestKnownObservations.Observations != null &&
                   GetFriendlyAgents().Count == expectedNrOfFriendlyAgents;
        }

        private PacManObservations ProducePacManObservations(ulong clientId)
        {
            var pacManObservations = new PacManObservations();
            pacManObservations.Index = _latestKnownObservations.Index + 1;
            pacManObservations.ObservationFixedTime = (float)NetworkManager.Singleton.ServerTime.FixedTime;
            var doCheckVisibility = DoCheckVisibility(clientId, transform);
            if (IsHost) doCheckVisibility = doCheckVisibility.ToList().FindAll(pair => !pair.agent.CompareTag(tag));
            pacManObservations.Observations = doCheckVisibility.Select(pair =>
                {
                    var agent = pair.agent;
                    var pacManObservation = new PacManObservation();
                    pacManObservation.Visible = pair.visible;
                    pacManObservation.IsGhost = isGhost.Value;
                    pacManObservation.HasFood = agent.foodCarried.Value > 0;
                    pacManObservation.Position = agent.gameObject.transform.localPosition;
                    pacManObservation.Velocity = agent.GetComponent<Rigidbody>().linearVelocity;
                    if (!pair.visible)
                    {
                        if (pacManObservation.Velocity.magnitude <= 0.71f)
                        {
                            pacManObservation.Position = Vector3.zero;
                        }
                        else
                        {
                            var dispersion = observationSpread * 0.5f + observationSpread * 0.5f * (2.34f - pacManObservation.Velocity.magnitude) / 1.63f; //1.63f = 2.34f - 0.71f i.e minus minimum OBSERVABLE velocity
                            pacManObservation.ReadingDispersion = dispersion;
                            pacManObservation.Position += new Vector3(Random.value * dispersion - dispersion / 2, 0, Random.value * dispersion - dispersion / 2);
                        }

                        pacManObservation.Velocity = Vector3.zero;
                    }

                    return pacManObservation;
                })
                .ToArray();
            return pacManObservations;
        }

        [ClientRpc]
        public void UpdateClientSideObservationsClientRpc(PacManObservations observations)
        {
            _latestKnownObservations = observations;
        }

        [ServerRpc]
        public void UpdateServerSideActionServerRpc(PacManAction action)
        {
            if (matchTime.Value > _pacManGameManager.planningTime) m_action = action; // Store latest known action on server side
        }

        public void FixedUpdate()
        {
            if (IsServer && initialized)
            {
                UpdateAgentState();

                _movement.ApplyDesiredControl(m_action.AccelerationDirection, m_action.AccelerationMagnitude); // Apply latest known action on server side fixed update.
            }
        }


        private void UpdateAgentState()
        {
            isPowered.Value = TeamAssignmentUtil.CheckTeam(gameObject) == Team.Red && _pacManGameManager.blueAgents[0].GetComponent<IPacManAgent>().IsScared() ||
                              TeamAssignmentUtil.CheckTeam(gameObject) == Team.Blue && _pacManGameManager.redAgents[0].GetComponent<IPacManAgent>().IsScared();


            if (isScared.Value && scaredUntil.Value < NetworkManager.Singleton.ServerTime.FixedTime)
            {
                isScared.Value = false;
                scaredUntil.Value = 0.0;
            }


            if (gameObject.CompareTag("Red") && gameObject.transform.localPosition.x > 0.3f)
            {
                isGhost.Value = true;
            }
            else if (gameObject.CompareTag("Red"))
            {
                isGhost.Value = false;
            }

            if (gameObject.CompareTag("Blue") && gameObject.transform.localPosition.x < -0.3f)
            {
                isGhost.Value = true;
            }
            else if (gameObject.CompareTag("Blue"))
            {
                isGhost.Value = false;
            }

            if (TeamAssignmentUtil.CheckTeam(gameObject) == Team.Red && gameObject.transform.localPosition.x > -0.3f ||
                TeamAssignmentUtil.CheckTeam(gameObject) == Team.Blue && gameObject.transform.localPosition.x < 0.3f)
            {
                if (foodCarried.Value > 0)
                {
                    _pacManGameManager.DropFood(this, true);
                }
            }
        }

        private bool CheckVisibility(ulong clientid)
        {
            if (NetworkObject.OwnerClientId == clientid) return true;

            return DoCheckVisibility(clientid, transform).Any(pair => pair.visible);
        }

        private IEnumerable<(PacManAgentManagerNetwork agent, bool visible)> DoCheckVisibility(ulong clientid, Transform checkTransform) // This client Id will always be the "enemy" client id.
        {
            return _pacManGameManager.agents.Select(agent => agent.GetComponent<PacManAgentManagerNetwork>()).ToList()
                .FindAll(agent => agent.OwnerClientId == clientid || !agent.gameObject.CompareTag(tag))
                .Select(agent =>
                    {
                        Physics.Raycast(agent.transform.position, checkTransform.position - agent.transform.position, out RaycastHit hit, Mathf.Infinity, -1, QueryTriggerInteraction.Ignore);
                        return (agent, hit.transform != null && hit.transform == checkTransform);
                    }
                );
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsServer)
            {
                if (!IsGhost() && other.gameObject.name == "NetworkFood" && TeamAssignmentUtil.CheckTeam(other.gameObject) != TeamAssignmentUtil.CheckTeam(gameObject))
                {
                    _pacManGameManager.EatFood(this, other.gameObject);
                }

                if (!IsGhost() && other.gameObject.name == "NetworkCapsule")
                {
                    _pacManGameManager.EatCapsule(this, other.gameObject);
                }

                if (IsGhost() && other.gameObject.name == "NetworkCapsule")
                {
                    transform.position = globalStartPosition;
                }
            }
        }

        private void OnCollisionStay(Collision other)
        {
            if (IsServer)
            {
                var otherAgent = other.gameObject.GetComponent<PacManAgentManagerNetwork>();
                if (other.gameObject.name == "NetworkPacmanAgent" && !other.gameObject.CompareTag(tag) && !IsGhost() && otherAgent.IsGhost() && !otherAgent.isScared.Value)
                {
                    _pacManGameManager.DropFood(this, false);
                    transform.position = globalStartPosition;
                }

                if (other.gameObject.name == "NetworkPacmanAgent" && !other.gameObject.CompareTag(tag) && IsGhost() && isScared.Value && !otherAgent.IsGhost())
                {
                    transform.position = globalStartPosition;
                }
            }
        }


        public bool IsGhost()
        {
            return isGhost.Value;
        }

        public List<IPacManAgent> GetFriendlyAgents()
        {
            return _pacManGameManager.agents
                .Select(agent => agent.GetComponent<PacManAgentManagerNetwork>()).ToList()
                .FindAll(obj => obj.CompareTag(tag) && OwnerClientId == obj.OwnerClientId)
                .Select(agent => agent.GetComponent<IPacManAgent>()).ToList();
        }

        public List<IPacManAgent> GetVisibleEnemyAgents()
        {
            if (IsHost)
            {
                return GetFriendlyAgents().SelectMany(friendly =>
                        DoCheckVisibility(OwnerClientId, friendly.gameObject.transform).ToList()
                            .FindAll(pair => pair.visible && !pair.agent.gameObject.CompareTag(tag))
                            .Select(pair => (IPacManAgent)pair.agent)
                            .ToList())
                    .Distinct()
                    .ToList();
            }

            return _pacManGameManager.agents
                .Select(agent => agent.GetComponent<IPacManAgent>())
                .ToList()
                .FindAll(obj => !obj.CompareTag(tag));
        }

        public bool IsScared()
        {
            return isScared.Value;
        }

        public bool IsPoweredUp()
        {
            return isPowered.Value;
        }

        public float GetScaredRemainingDuration()
        {
            return (float)(scaredUntil.Value - NetworkManager.Singleton.ServerTime.FixedTime);
        }

        public PacManObservations GetEnemyObservations()
        {
            return _latestKnownObservations;
        }

        public Vector3 GetStartPosition()
        {
            return _pacManGameManager.transform.InverseTransformPoint(globalStartPosition);
        }

        public int GetCarriedFoodCount()
        {
            return foodCarried.Value;
        }

        public List<GameObject> GetCapsuleObjects()
        {
            return _pacManGameManager.capsules.Select(obj => obj.gameObject).ToList();
        }

        public List<GameObject> GetFoodObjects()
        {
            return _pacManGameManager.foodList.Select(obj => obj.gameObject).ToList();
        }

        public Vector3 GetVelocity()
        {
            return velocitySync.Value;
        }

        public float GetTimeRemaining()
        {
            return matchTime.Value;
        }

        public int GetScore()
        {
            return score.Value;
        }

        public void SetScared(bool b, double serverTimeFixedTime)
        {
            isScared.Value = b;
            scaredUntil.Value = serverTimeFixedTime;
        }

        public void SetFoodCount(int value)
        {
            foodCarried.Value = value;
        }
    }
}