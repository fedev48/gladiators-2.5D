using Unity.Burst;
using Unity.Entities;
[BurstCompile]
[UpdateAfter(typeof(StateTransitionSystem))]
partial struct StateManagerSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        EntityManager entityManager = state.EntityManager;
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (RefRW<FSMState> fSMState in SystemAPI.Query<RefRW<FSMState>>())
            fSMState.ValueRW.timeInState += deltaTime;

        foreach ((
            DynamicBuffer<ChangeStateRequest> requests,
            RefRW<FSMState> fSMState,
            Entity entity)
                in SystemAPI.Query<
                    DynamicBuffer<ChangeStateRequest>,
                    RefRW<FSMState>>()
                .WithEntityAccess())
        {
            TypeIndex winningState = TypeIndex.Null;
            int winningPriority = -1;

            foreach (ChangeStateRequest request in requests)
            {
                if (request.priority <= winningPriority) continue;
                winningState = request.targetState;
                winningPriority = request.priority;
            }

            requests.Clear();
            SystemAPI.SetBufferEnabled<ChangeStateRequest>(entity, false);

            if (winningState == TypeIndex.Null) continue;
            if (winningState == fSMState.ValueRO.current) continue;

            if (fSMState.ValueRO.current != TypeIndex.Null)
            {
                entityManager.SetComponentEnabled(entity, ComponentType.ReadWrite(fSMState.ValueRO.current), false);
            }

            entityManager.SetComponentEnabled(entity, ComponentType.ReadWrite(winningState), true);

            CancelOneShot(entityManager, entity);

            fSMState.ValueRW.previous      = fSMState.ValueRO.current;
            fSMState.ValueRW.current       = winningState;
            fSMState.ValueRW.timeInState   = 0;
            fSMState.ValueRW.stateDuration = -1;
        }
    }

    static void CancelOneShot(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.HasComponent<VisualEntity>(entity)) return;

        Entity visualEntity = entityManager.GetComponentData<VisualEntity>(entity).value;

        if (entityManager.HasComponent<IsOneShot>(visualEntity))
            entityManager.SetComponentEnabled<IsOneShot>(visualEntity, false);
    }
}
