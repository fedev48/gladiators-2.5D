using Unity.Burst;
using Unity.Entities;

[BurstCompile]
partial struct DamageSystem : ISystem
{
    const float damageAnimDuration = 0.2f;

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach ((RefRW<RecievingDamage> damage,
                  EnabledRefRW<RecievingDamage> damageEnabled,
                  RefRW<Health> health,
                  RefRO<VisualEntity> visual) in
            SystemAPI.Query<RefRW<RecievingDamage>,
                            EnabledRefRW<RecievingDamage>,
                            RefRW<Health>,
                            RefRO<VisualEntity>>())
        {
            health.ValueRW.value -= (int)damage.ValueRO.amount;
            damage.ValueRW.amount = 0f;
            damageEnabled.ValueRW = false;

            Entity visualEntity = visual.ValueRO.value;
            SystemAPI.SetComponent(visualEntity, new DamageAnimation { duration = damageAnimDuration });
            SystemAPI.SetComponentEnabled<DamageAnimation>(visualEntity, true);
        }
    }
}
