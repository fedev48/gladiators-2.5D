using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

// [UpdateAfter(typeof(PhysicsSystemGroup))]
partial struct StateMeleeAttackSystem : ISystem
{
    TypeIndex fleeState;
    TypeIndex deathState;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PhysicsWorldSingleton>();

        fleeState = TypeManager.GetTypeIndex<FollowState>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
