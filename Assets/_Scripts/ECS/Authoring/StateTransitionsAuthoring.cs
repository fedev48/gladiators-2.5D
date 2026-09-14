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
                    //baking runs in the editor, so a TypeIndex baked here pointed at a different type once loaded in the
                    //player build. StableTypeHash is derived from the type itself and matches on both sides.
                    // fromState     = TypeManager.GetTypeIndex(fromType),
                    // toState       = TypeManager.GetTypeIndex(toType),
                    fromState        = TypeManager.GetTypeInfo(TypeManager.GetTypeIndex(fromType)).StableTypeHash,
                    toState          = TypeManager.GetTypeInfo(TypeManager.GetTypeIndex(toType)).StableTypeHash,
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
//states are stored as StableTypeHash: TypeIndex is a runtime value and does not survive baking into a player build
public struct StateTransition : IBufferElementData
{
    // public TypeIndex   fromState;
    // public TypeIndex   toState;
    public ulong          fromState;
    public ulong          toState;
    public StateCondition conditions;
    public float          rangeThreshold;  
    public float          healthThreshold;  
    public int            enemiesThreshold; 
    public byte           priority;
}