using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

public static class AttackActions
{
    public static void QueryHits(
        in CollisionWorld collisionWorld,
        float3 center,
        float radius,
        CollisionFilter filter,
        Entity attacker,
        ref NativeList<DistanceHit> hits)
    {
        hits.Clear();
        collisionWorld.OverlapSphere(center, radius, ref hits, filter);

        for (int i = hits.Length - 1; i >= 0; i--)
        {
            if (hits[i].Entity == attacker)
                hits.RemoveAtSwapBack(i);
        }
    }

    public static void ResolveHit(
        Entity target,
        Entity attacker,
        float3 knockbackVector,
        float damage,
        float knockbackStrenght,
        ref ComponentLookup<Health> healthLookup,
        ref ComponentLookup<RecievingDamage> damageLookup,
        ref ComponentLookup<KnockbackVelocity> knockbackLookup,
        ref ComponentLookup<LastAttacker> lastAttackerLookup)
    {
        if (!healthLookup.HasComponent(target)) return;

        if (damageLookup.HasComponent(target))
        {
            RecievingDamage recievingDamage = damageLookup[target];
            recievingDamage.amount += damage;
            damageLookup[target] = recievingDamage;
            damageLookup.SetComponentEnabled(target, true);
        }

        if (attacker != Entity.Null && lastAttackerLookup.HasComponent(target))
        {
            lastAttackerLookup[target] = new LastAttacker { entity = attacker, damage = damage };
            lastAttackerLookup.SetComponentEnabled(target, true);
        }

        if (!knockbackLookup.HasComponent(target)) return;

        knockbackVector.y = 0f;
        float3 direction = math.lengthsq(knockbackVector) > 0.0001f
            ? math.normalize(knockbackVector)
            : new float3(0f, 0f, 1f);

        KnockbackVelocity knockback = knockbackLookup[target];
        knockback.Value = direction * knockbackStrenght;
        knockbackLookup[target] = knockback;
    }
}
