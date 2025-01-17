using System.Collections.Generic;
using PacMan.PacMan;
using Scripts.Map;
using UnityEngine;

namespace PacMan
{
    public interface IPacManAgent
    {
        bool IsGhost();
        bool IsScared();

        bool IsPoweredUp();
        float GetScaredRemainingDuration();
        int GetCarriedFoodCount();
        List<GameObject> GetFoodObjects();
        List<GameObject> GetCapsuleObjects();
        List<IPacManAgent> GetFriendlyAgents();

        List<IPacManAgent> GetVisibleEnemyAgents();
        PacManObservations GetEnemyObservations();

        Vector3 GetStartPosition();
        Vector3 GetVelocity();
        bool CompareTag(string tag);
        GameObject gameObject { get; }
        float GetTimeRemaining();
        int GetScore();
    }

    public interface IPacManAgentManager
    {
        bool IsGhost();
        bool IsScared();
        float GetScaredRemainingDuration();
        int GetCarriedFoodCount();
        List<GameObject> GetFoodObjects();
        List<GameObject> GetCapsuleObjects();
        List<IPacManAgent> GetFriendlyAgents();
        PacManObservations GetEnemyObservations();
        Vector3 GetVelocity();

        Vector3 GetStartPosition();
        bool CompareTag(string tag);
        GameObject gameObject { get; }
        void SetScared(bool b, double serverTimeFixedTime);
        public void SetFoodCount(int value);
    }
}