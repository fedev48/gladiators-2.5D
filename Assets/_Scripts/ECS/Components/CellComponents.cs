using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct CellComponents : IBufferElementData
{
    public int cost;
    public int bestCost;
    public float2 movingVector;
    public int currentBuriedBodies;
    
}




