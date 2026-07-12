using Unity.Entities;
using Unity.Mathematics;

public struct UnitTag : IComponentData {}

//movement
public struct MoveDirection    : IComponentData { public float3 Value; }
public struct MoveSpeed        : IComponentData { public float  Value; }
public struct NeedsPathfinding : IComponentData, IEnableableComponent { public float3 Destination; }
public struct UsingPathfinding : IComponentData, IEnableableComponent { public int flowFieldId; }


//animation
public struct VisualEntity        : IComponentData { public Entity Value; }
public struct FacingDirection     : IComponentData { public float3 Value; }
public struct UnitMovementAnimTag : IComponentData {}
public struct OneShotAnimTag      : IComponentData, IEnableableComponent {}


//state machine
public struct SpawnState  : IComponentData, IEnableableComponent {}
public struct FollowState : IComponentData, IEnableableComponent {}
public struct AttackState : IComponentData, IEnableableComponent {}


