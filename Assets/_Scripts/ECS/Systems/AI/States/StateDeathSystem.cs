using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
partial struct StateDeathSystem : ISystem
{
    ComponentLookup<IsOneShot>              oneShotLookup;
    ComponentLookup<SpriteAnimationState>   animStateLookup;
    ComponentLookup<LeavesCorpseInCellTag>  leavesCorpseLookup;
    BufferLookup<AnimationClipData>         clipsLookup;
    
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        clipsLookup         = state.GetBufferLookup<AnimationClipData>(isReadOnly: true);
        oneShotLookup       = state.GetComponentLookup<IsOneShot>(isReadOnly: false);
        animStateLookup     = state.GetComponentLookup<SpriteAnimationState>(isReadOnly: true);
        leavesCorpseLookup  = state.GetComponentLookup<LeavesCorpseInCellTag>(isReadOnly: true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        float deltaTime = SystemAPI.Time.DeltaTime;

        clipsLookup.Update(ref state);
        oneShotLookup.Update(ref state);
        animStateLookup.Update(ref state);
        leavesCorpseLookup.Update(ref state);

        foreach (
            (RefRW<DeathState> deadState,
            RefRW<FSMState> fSMState,
            RefRW<MovementBlocked> movementBlockedData,
            RefRO<LocalTransform> localTransform,
            RefRO<VisualEntity> visualEntity,
            EnabledRefRW<MoveDestination> moveEnabled,
            EnabledRefRW<MovementBlocked> movementBlocked,
            Entity entity)
        in SystemAPI.Query<
                RefRW<DeathState>,
                RefRW<FSMState>,
                RefRW<MovementBlocked>,
                RefRO<LocalTransform>,
                RefRO<VisualEntity>,
                EnabledRefRW<MoveDestination>,
                EnabledRefRW<MovementBlocked>>()
                .WithPresent<MoveDestination>()
                .WithPresent<MovementBlocked>()
                .WithEntityAccess())
        {

            if (deadState.ValueRO.elapsed < 0)
            {
                float animationDuration;
                moveEnabled.ValueRW = false;
                
                SetAnimationStart(deadState, fSMState, visualEntity.ValueRO.value, out animationDuration);

                movementBlocked.ValueRW = true;
                movementBlockedData.ValueRW.remainingTime = animationDuration;
            }

            deadState.ValueRW.elapsed += deltaTime;

            if (deadState.ValueRO.elapsed <= deadState.ValueRO.duration) continue;
            if (!SystemAPI.HasBuffer<LinkedEntityGroup>(entity)) ecb.DestroyEntity(visualEntity.ValueRO.value); //for entities manually placed in the subscene
            if (leavesCorpseLookup.HasComponent(entity) && leavesCorpseLookup.IsComponentEnabled(entity))
            {
                Entity spawnRequest = ecb.CreateEntity();
                ecb.AddComponent(spawnRequest, new CorpseSpawRequest { position = localTransform.ValueRO.Position });
            }
            ecb.DestroyEntity(entity);

        }
    }

    private void SetAnimationStart(RefRW<DeathState> deadState, RefRW<FSMState> fSMState, Entity visualEntity, out float animationDuration)
    {
        float duration = 0f;
        AnimationDirection direction = animStateLookup[visualEntity].animationDirection;

        if (AnimationActions.TryPlayOneShot(visualEntity, Animation.Death, direction, ref clipsLookup, ref oneShotLookup, out AnimationClipData clip))
        {
            duration = clip.frameCount / clip.fps;
        }

        deadState.ValueRW.elapsed = 0f;
        deadState.ValueRW.duration = duration;
        animationDuration = duration;

    }

    

    
}
