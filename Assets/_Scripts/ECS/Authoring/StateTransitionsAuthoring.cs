using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class StateTransitionsAuthoring : MonoBehaviour
{
    public List<StateTransitionEntry> transitions = new List<StateTransitionEntry>();

    public class Baker : Baker<StateTransitionsAuthoring>
    {
        public override void Bake(StateTransitionsAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            DynamicBuffer<StateTransition> buffer = AddBuffer<StateTransition>(entity);

            foreach (StateTransitionEntry entry in authoring.transitions)
            {
                Type fromType = Type.GetType(entry.fromState);
                Type toType   = Type.GetType(entry.toState);

                if (fromType == null || toType == null)
                {
                    Debug.LogError($"{authoring.name}: transition with unresolved state ({entry.fromState} -> {entry.toState})", authoring);
                    continue;
                }

                buffer.Add(new StateTransition
                {
                    fromState        = TypeManager.GetTypeIndex(fromType),
                    toState          = TypeManager.GetTypeIndex(toType),
                    conditions       = entry.conditions,
                    rangeThreshold   = entry.rangeThreshold,
                    healthThreshold  = entry.healthThreshold,
                    enemiesThreshold = entry.enemiesThreshold,
                    priority         = (byte)entry.priority
                });
            }
        }
    }
}

[Serializable]
public class StateTransitionEntry
{
    public string         fromState;
    public string         toState;
    public StateCondition conditions;
    public float          rangeThreshold;
    public float          healthThreshold;
    public int            enemiesThreshold;
    public int            priority;
}


public interface IFSMState : IComponentData, IEnableableComponent { }

[Flags]
public enum StateCondition
{
    None               = 0,
    StateFinished      = 1 << 0,
    HasTarget          = 1 << 1,
    NoTarget           = 1 << 2,
    TargetInRange      = 1 << 3,
    TargetOutOfRange   = 1 << 4,
    HealthBelow        = 1 << 5,
    EnemiesAroundAbove = 1 << 6,
    TargetVisible      = 1 << 7,
}

//conditions within one entry are AND. two entries sharing fromState are OR
public struct StateTransition : IBufferElementData
{
    public TypeIndex      fromState;
    public TypeIndex      toState;
    public StateCondition conditions;
    public float          rangeThreshold;  
    public float          healthThreshold;  
    public int            enemiesThreshold; 
    public byte           priority;
}