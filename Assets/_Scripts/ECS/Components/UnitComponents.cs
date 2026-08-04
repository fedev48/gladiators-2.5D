using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct UnitTag : IComponentData {}

public struct Team : IComponentData { public byte value; }

public static class Teams
{
    public const byte ALLY  = 0;
    public const byte ENEMY = 1;
}

//movement state
public struct CurrentVelocity       : IComponentData { public float3 value; }
public struct KnockbackVelocity     : IComponentData { public float3 Value; public float multiplier; public float durationMultiplier; }//multiplier is aplied in VelocityComposerSystem, giving the weight to the KB. durationMultiplier is applied in KnockbackVelocity that applies the decay (how long it takes for the unit to stop)
public struct SeparationVelocity    : IComponentData { public float3 value; }
public struct DesiredVelocity       : IComponentData { public float3 value; }
public struct MoveDestination       : IComponentData, IEnableableComponent { public float3 value; }
public struct Health                : IComponentData { public int value; }
public struct NeedsPathfinding      : IComponentData, IEnableableComponent { public float3 destination; }
public struct UsingPathfinding      : IComponentData, IEnableableComponent { public int flowFieldId; }

//animation
public struct VisualEntity          : IComponentData { public Entity value; }
public struct FacingDirection       : IComponentData { public float3 value; }
public struct UnitMovementAnimTag   : IComponentData {}
public struct DamageAnimation       : IComponentData, IEnableableComponent { public float duration; }


//unit config
public struct MoveSpeed             : IComponentData { public float  value; }
public struct MovementConfig        : IComponentData { public float  acceleration; public float maxSpeed; }
public struct SeparationConfig      : IComponentData { public float  radius; public float strenght; }
public struct ShouldSnapToFloorTag  : IComponentData, IEnableableComponent {}

//status effects
public struct MovementBlocked       : IComponentData, IEnableableComponent { public float remainingTime; }
public struct RecievingDamage       : IComponentData, IEnableableComponent { public float amount; }

//FSM states
public struct SpawnState            : IFSMState {}
public struct WanderState           : IFSMState {}
public struct FollowState           : IFSMState {}
public struct StunnedState          : IFSMState {}
public struct FleeState             : IFSMState {}
public struct MeleeAttackState      : IFSMState {}
public struct RangeAttack           : IFSMState {}


public struct FSMBlackBoard         : IComponentData
{
    public Entity target;
    public float3 targetLocation;
    public int enemiesSurrounding;
}

public struct HasTarget : IComponentData, IEnableableComponent {}

public struct TargetingConfig : IComponentData
{
    public int   searchCellRadius;
    public float scanInterval;
    public float retentionMultiplier;   //must be > 1 or the target oscillates
    public float switchImprovement;    //number > 0 avoids jumping between targets when they're close in distance
    public float attackerLockDuration;
}

public struct TargetingState : IComponentData
{
    public float scanCooldown;
    public float lockRemaining;
}

//written by whoever deals the damage
public struct LastAttacker : IComponentData, IEnableableComponent
{
    public Entity entity;
    public float  damage;
}

public struct FSMState : IComponentData
{
    public TypeIndex current;
    public TypeIndex previous;
    public float timeInState;
    public float stateDuration; //written by the running state system. -1 means the state does not end by time
}

[InternalBufferCapacity(4)]
public struct ChangeStateRequest: IBufferElementData, IEnableableComponent
{
    public TypeIndex targetState;
    public byte priority;
}




