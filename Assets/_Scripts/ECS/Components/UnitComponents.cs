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
public struct Health                : IComponentData { public int value; }
public struct MoveDestination       : IComponentData, IEnableableComponent { public float3 value; }
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
public struct AffectedByGrativy     : IComponentData, IEnableableComponent { public float verticalVelocity; }

//status effects
public struct MovementBlocked       : IComponentData, IEnableableComponent { public float remainingTime; }
public struct RecievingDamage       : IComponentData, IEnableableComponent { public float amount; }

//FSM states
public struct SpawnState            : IFSMState {}
public struct WanderState           : IFSMState {}
public struct FollowState           : IFSMState {}
public struct StunnedState          : IFSMState {}
public struct FleeState             : IFSMState {}
public struct RangeAttack : IFSMState
{
    public float distanceTolerance;
    public float straightShotRange;   //used when the bullet has no gravity, since there is no ballistic range to compute
    public float recovery;

    public float elapsed;
    public float duration;
    public float shotTime;
    public bool  shotFired;
}

public struct MeleeAttackState : IFSMState
{
    public float attackRange;
    public float stopDistance;
    public float hitRadius;        //if 0, it hits only the target
    public float damage;
    public float knockbackStrength;
    public float recovery;

    public float elapsed;
    public float duration;
    public float hitTime;
    public bool  hitLanded;
}


public struct FSMBlackBoard         : IComponentData
{
    public Entity target;
    public float3 targetLocation;
    public int enemiesSurrounding;
}

public struct HasTarget : IComponentData, IEnableableComponent {}

public struct BlackboardSensorConfigAndState : IComponentData
{
    //state
    public float clock;

    //config
    public float searchRadiusForTarget;
    public float searchRadiusForSurrouded;
    public float scanInterval;
    public float distanceTargetReleaseMultiplier;   //must be > 1 or the target oscillates (multiplies the target distance, to get a valua at which release the target)
    public float distanceDifferenceToSwitchTarget;    //number > 0 avoids jumping between targets when they're close in distance
    public float retaliationDamageThreshold;   //accumulated damage needed before switching target to the attacker
    public float retaliationDamageDecay;       //accumulated damage lost per second
}

//written by whoever deals the damage
public struct LastAttacker : IComponentData
{
    public Entity entity;
    public float  accumulatedDamage;
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




