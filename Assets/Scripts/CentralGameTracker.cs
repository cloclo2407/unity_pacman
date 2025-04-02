using System.Collections.Generic;
using PacMan;
using PacMan.PacMan;
using Scripts.Map;
using UnityEditor.UI;
using UnityEngine;

public class CentralGameTracker
{
    
    private bool _initialized = false;
    
    private int _nrAgents;
    private int _nrFriendlyAgents;

    public List<Vector3> agentPositions;


    public void initialize(int nrFriendlyAgents, List<Vector3> spawnPositions)
    {
        if (_initialized) return;
        this._nrFriendlyAgents = nrFriendlyAgents;
        this._nrAgents = 2 * nrFriendlyAgents;
        this.agentPositions = spawnPositions;

        _initialized = true;
    }

    
    public void updatePositions(List<IPacManAgent> friendlyAgents, PacManObservations enemyObservations)
    {
        var newPositons = new List<Vector3>(_nrAgents);
        
        
        for (int i = 0; i < _nrFriendlyAgents; i++)
        {
            agentPositions[i] = friendlyAgents[i].gameObject.transform.position;
        }
        
        for (int i = 0; i < _nrFriendlyAgents; i++)
        {
            var observation = enemyObservations.Observations[i];
            
            if (observation.Visible)
            {
                agentPositions[i + _nrFriendlyAgents] = observation.Position;
            }
            else
            {
                agentPositions[i + _nrFriendlyAgents] = Vector3.zero;
            }
            
        }
        
        for (int i = _nrFriendlyAgents; i < _nrAgents; i++)
        {
            if (newPositons[i] == Vector3.zero)
            {
                
            }
            
        }
        
        
        agentPositions = newPositons;
        
        
        
    }

    
    






}