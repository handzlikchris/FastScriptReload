using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RefOutEnum : MonoBehaviour
{
    private static float GetObjectAngleToRoad(Transform objectToSnap, ERRoad road, ref RoadSide roadSide)
    {
        road.GetClosestPoints(objectToSnap.position, 2, out var outDirection, ref roadSide);
        return Vector3.SignedAngle(objectToSnap.forward, outDirection, Vector3.up);
    }
    
    public enum RoadSide  
    {
        Right,
        Left,
        None
    }
}

public class ERRoad
{
    public void GetClosestPoints(Vector3 position, int number, out Vector3 outDirection, ref RefOutEnum.RoadSide roadSide)
    {
        outDirection = Vector3.zero; 
    }
}

