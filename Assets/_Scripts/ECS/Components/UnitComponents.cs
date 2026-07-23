using Unity.Entities;
using Unity.Mathematics;

public struct UnitTag : IComponentData {}

//movement


public struct CurrentVelocity       : IComponentData { public float3 Value; }
public struct KnockbackVelocity     : IComponentData { public float3 Value; }
public struct SeparationVelocity    : IComponentData { public float3 Value; }
public struct DesiredVelocity       : IComponentData { public float3 Value; }
public struct MoveSpeed             : IComponentData { public float  Value; }
public struct SeparationConfig      : IComponentData { public float  radius; public float speed; }
public struct NeedsPathfinding      : IComponentData, IEnableableComponent { public float3 Destination; }
public struct UsingPathfinding      : IComponentData, IEnableableComponent { public int flowFieldId; }
public struct ShouldSnapToFloorTag  : IComponentData, IEnableableComponent {}

//animation
public struct VisualEntity          : IComponentData { public Entity Value; }
public struct FacingDirection       : IComponentData { public float3 Value; }
public struct UnitMovementAnimTag   : IComponentData {}



//status effects
public struct MovementBlocked       : IComponentData, IEnableableComponent { public float remainingTime; }

//state machine
public struct SpawnState            : IComponentData, IEnableableComponent {}
public struct FollowState           : IComponentData, IEnableableComponent {}
public struct AttackState           : IComponentData, IEnableableComponent {}


