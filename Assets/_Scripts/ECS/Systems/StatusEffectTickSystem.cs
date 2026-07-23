using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
partial struct StatusEffectTickSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((RefRW<MovementBlocked> movementBlocked, EnabledRefRW<MovementBlocked> enabled) in
            SystemAPI.Query<RefRW<MovementBlocked>, EnabledRefRW<MovementBlocked>>())
        {
            movementBlocked.ValueRW.remainingTime -= deltaTime;

            if (movementBlocked.ValueRO.remainingTime <= 0f)
            {
                movementBlocked.ValueRW.remainingTime = 0f; 
                enabled.ValueRW = false;
            }
        }
    }
}
