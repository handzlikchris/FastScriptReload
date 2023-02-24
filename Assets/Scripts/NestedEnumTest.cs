using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum AreaType {
    None,
    North,
    East,
    South,
    West,
    Center,
    Exit,
}

public class GenerationGiveUpException: Exception { }  

public class SetupGenerationData {
    public CardinalDirection SpawnDirection;
    public bool NoNPCs;
    public int StageSize;
    public Vector2Int CowPosition; 
    public Vector2Int VampireBatPosition;

    public Vector2Int SpawnPlacement; 
    // public Dictionary<Vector2Int, List<ExitOverrideType>> ExitPlacements;
    // public Dictionary<Vector2Int, EnemyType> BossPlacements;

    public SetupGenerationData()
    {
        CowPosition = new(-1, -1); 
        VampireBatPosition = new();
        // ExitPlacements = new();
        // BossPlacements = new();
    }
}

public class NestedEnumTest : MonoBehaviour   
{
    public virtual void Deactivate()
    {
        gameObject.SetActive(false);     
    }
    
    void Update()
    {
        
    }

    void OnScriptHotReload()  
    {
        
    }
    
    private EdgeType GetEdgeType()  
    { 
        return EdgeType.NormalEntrance;
    }
    
    public enum EdgeType {
        NormalEntrance,
        Blocked,
        Exit,
    }
}
