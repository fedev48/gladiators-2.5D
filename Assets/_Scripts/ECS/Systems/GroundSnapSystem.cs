using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct GroundSnapSystem : ISystem
{
    private int groundMask;

    public void OnCreate(ref SystemState state)
    {
        groundMask = LayerMask.GetMask("Ground");
    }

    public void OnUpdate(ref SystemState state)
    {

        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (transform, fall) in SystemAPI.Query<RefRW<LocalTransform>, RefRW<AffectedByGrativy>>())
        {
            fall.ValueRW.verticalVelocity -= SimConstants.GRAVITY * deltaTime;

            float3 pos = transform.ValueRO.Position;
            pos.y += fall.ValueRO.verticalVelocity * deltaTime;

            float3 origin = pos + new float3(0, 5f, 0);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 50f, groundMask) && pos.y <= hit.point.y)
            {
                pos.y = hit.point.y;
                fall.ValueRW.verticalVelocity = 0f;
            }

            transform.ValueRW.Position = pos;
        }
    }
}
