using System.Collections.Generic;
using System.Linq;
using PacMan.PacMan;
using UnityEngine;
using Random = UnityEngine.Random;

namespace PacMan.Local
{
    public class PacManAgentManager : MonoBehaviour, IPacManAgentManager, IPacManAgent
    {
        public bool isScared;
        public double scaredUntil;
        public int foodCarried;
        public bool isGhost;

        private PacManAI pacManAI;
        private PacManAction m_action;
        protected PacManMovementController _movement;
        private PacManGameManager _pacManGameManager;
        protected GameObject ghostObject;
        protected GameObject pacManObject;

        public Vector3 globalStartPosition;

        private float _nextObservationTime;
        private readonly float _observationUpdateInterval = 1;
        private readonly float _observationSpread = 10;

        private PacManObservations _latestKnownObservation;
        private bool _ready;
        public PacManAction action;

        public void Initialize(PacManGameManager pacManGameManager)
        {
            _pacManGameManager = pacManGameManager;

            _movement = GetComponent<PacManMovementController>();
            pacManAI = GetComponent<PacManAI>();

            ghostObject = transform.Find("visuals/ghost").gameObject;
            pacManObject = transform.Find("visuals/pacman").gameObject;

            pacManAI.Initialize(_pacManGameManager.mapManager);
            _ready = true;
        }

        public void FixedUpdate()
        {
            if (!_ready) return;

            if (_nextObservationTime <= Time.fixedTime)
            {
                var pacManObservations = ProducePacManObservations();
                _latestKnownObservation = pacManObservations;
                _nextObservationTime = Time.fixedTime + (_nextObservationTime == 0 ? Random.value * _observationUpdateInterval : _observationUpdateInterval);
            }

            UpdateAgentState();
            action = pacManAI.Tick();
            _movement.ApplyDesiredControl(action.AccelerationDirection, action.AccelerationMagnitude);

            ghostObject.SetActive(isGhost);
            pacManObject.SetActive(!isGhost);
        }

        private PacManObservations ProducePacManObservations()
        {
            var pacManObservations = new PacManObservations();
            pacManObservations.Index = _latestKnownObservation.Index + 1;
            pacManObservations.ObservationFixedTime = Time.fixedTime;
            var doCheckVisibility = DoCheckVisibility(transform);
            doCheckVisibility = doCheckVisibility.ToList().FindAll(pair => !pair.agent.CompareTag(tag));
            pacManObservations.Observations = doCheckVisibility
                .Select(pair =>
                {
                    var agent = pair.agent;
                    var pacManObservation = new PacManObservation();
                    pacManObservation.Visible = pair.visible;
                    pacManObservation.IsGhost = isGhost;
                    pacManObservation.HasFood = agent.foodCarried > 0;
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
                            var dispersion = _observationSpread * 0.5f + _observationSpread * 0.5f * (2.34f - pacManObservation.Velocity.magnitude) / 1.63f; //1.63f = 2.34f - 0.71f i.e minus minimum OBSERVABLE velocity
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

        protected void UpdateAgentState()
        {
            if (isScared && scaredUntil < Time.fixedTime)
            {
                isScared = false;
                scaredUntil = 0.0;
            }


            if (gameObject.CompareTag("Red") && gameObject.transform.localPosition.x > 0.3f)
            {
                isGhost = true;
            }
            else if (gameObject.CompareTag("Red"))
            {
                isGhost = false;
            }

            if (gameObject.CompareTag("Blue") && gameObject.transform.localPosition.x < -0.3f)
            {
                isGhost = true;
            }
            else if (gameObject.CompareTag("Blue"))
            {
                isGhost = false;
            }

            if (TeamAssignmentUtil.CheckTeam(gameObject) == Team.Red && gameObject.transform.localPosition.x > -0.3f ||
                TeamAssignmentUtil.CheckTeam(gameObject) == Team.Blue && gameObject.transform.localPosition.x < 0.3f)
            {
                if (foodCarried > 0)
                {
                    _pacManGameManager.DropFood(this, true);
                }
            }
        }

        private IEnumerable<(PacManAgentManager agent, bool visible)> DoCheckVisibility(Transform sourceTransform) // This client Id will always be the "enemy" client id.
        {
            return _pacManGameManager.agents.Select(agent => agent.GetComponent<PacManAgentManager>()).ToList()
                .FindAll(agent => !agent.gameObject.CompareTag(tag))
                .Select(agent =>
                    {
                        Physics.Raycast(agent.transform.position, sourceTransform.position - agent.transform.position, out RaycastHit hit, Mathf.Infinity, -1, QueryTriggerInteraction.Ignore);
                        return (agent, hit.transform != null && hit.transform == sourceTransform);
                    }
                );
        }

        public void OnTriggerEnter(Collider other)
        {
            if (!IsGhost() && other.gameObject.name == "Food" && TeamAssignmentUtil.CheckTeam(other.gameObject) != TeamAssignmentUtil.CheckTeam(gameObject))
            {
                _pacManGameManager.EatFood(this, other.gameObject);
            }

            if (!IsGhost() && other.gameObject.name == "Capsule")
            {
                _pacManGameManager.EatCapsule(this, other.gameObject);
            }

            if (IsGhost() && other.gameObject.name == "Capsule")
            {
                transform.position = globalStartPosition;
            }
        }

        public void OnCollisionStay(Collision other)
        {
            var otherAgent = other.gameObject.GetComponent<PacManAgentManager>();
            if (other.gameObject.name == "PacManAgent" && other.gameObject.tag != tag && !IsGhost() && otherAgent.IsGhost() && !otherAgent.isScared)
            {
                _pacManGameManager.DropFood(this, false);
                transform.position = globalStartPosition;
            }

            if (other.gameObject.name == "PacManAgent" && other.gameObject.tag != tag && IsGhost() && isScared && !otherAgent.IsGhost())
            {
                transform.position = globalStartPosition;
            }
        }


        public bool IsGhost()
        {
            return isGhost;
        }

        public List<IPacManAgent> GetFriendlyAgents()
        {
            return _pacManGameManager?.agents
                .Select(agent => (IPacManAgent)agent.GetComponent<PacManAgentManager>())
                .ToList()
                .FindAll(obj => obj.CompareTag(tag));
        }

        public List<IPacManAgent> GetVisibleEnemyAgents()
        {
            return GetFriendlyAgents().SelectMany(friendly =>
                    DoCheckVisibility(friendly.gameObject.transform).ToList()
                        .FindAll(pair => pair.visible && !pair.agent.gameObject.CompareTag(tag))
                        .Select(pair => (IPacManAgent)pair.agent)
                        .ToList())
                .Distinct()
                .ToList();
        }

        public bool IsScared()
        {
            return isScared;
        }

        public bool IsPoweredUp()
        {
            return TeamAssignmentUtil.CheckTeam(gameObject) == Team.Red && _pacManGameManager.blueAgents[0].GetComponent<IPacManAgent>().IsScared() ||
                   TeamAssignmentUtil.CheckTeam(gameObject) == Team.Blue && _pacManGameManager.redAgents[0].GetComponent<IPacManAgent>().IsScared();
        }

        public float GetScaredRemainingDuration()
        {
            return (float)(scaredUntil - Time.fixedTime);
        }

        public PacManObservations GetEnemyObservations()
        {
            return _latestKnownObservation;
        }

        public Vector3 GetStartPosition()
        {
            return _pacManGameManager.transform.InverseTransformPoint(globalStartPosition);
        }

        public int GetCarriedFoodCount()
        {
            return foodCarried;
        }

        public List<GameObject> GetCapsuleObjects()
        {
            return _pacManGameManager.capsules.Select(obj => obj.gameObject).ToList();
        }

        public List<GameObject> GetFoodObjects()
        {
            return _pacManGameManager?.foodList.Select(obj => obj.gameObject).ToList();
        }

        public Vector3 GetVelocity()
        {
            return GetComponent<Rigidbody>().linearVelocity;
        }

        public float GetTimeRemaining()
        {
            return _pacManGameManager.matchLength - _pacManGameManager.matchTime;
        }

        public int GetScore()
        {
            return TeamAssignmentUtil.CheckTeam(gameObject) == Team.Blue ? _pacManGameManager.blueScore : _pacManGameManager.redScore;
        }

        public void SetFoodCount(int value)
        {
            foodCarried = value;
        }

        public void SetScared(bool b, double serverTimeFixedTime)
        {
            isScared = true;
            scaredUntil = serverTimeFixedTime;
        }
    }
}