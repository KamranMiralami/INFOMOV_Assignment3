using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;


[BurstCompile(FloatPrecision = FloatPrecision.Medium, FloatMode = FloatMode.Fast)]
[UpdateAfter(typeof(TurnTowardsPlayerSystem))]
partial struct CollisionSystem : ISystem
{
	EntityQuery enemyQuery;
	EntityQuery bulletQuery;
	EntityQuery playerQuery;
	float enemyCollisionRadiusSqr;
	float playerCollisionRadiusSqr;

	[BurstCompile(FloatPrecision = FloatPrecision.Medium, FloatMode = FloatMode.Fast)]
	public void OnCreate(ref SystemState state)
	{
		state.RequireForUpdate<EnemyTag>(); 
		enemyQuery = SystemAPI.QueryBuilder().WithAll<Health, EnemyTag, LocalTransform>().Build();
		bulletQuery = SystemAPI.QueryBuilder().WithAll<TimeToLive, LocalTransform>().Build();
		playerQuery = SystemAPI.QueryBuilder().WithAll<Health, PlayerTag, LocalTransform>().Build();
		enemyCollisionRadiusSqr = Settings.EnemyCollisionRadius * Settings.EnemyCollisionRadius;
		playerCollisionRadiusSqr = Settings.PlayerCollisionRadius * Settings.PlayerCollisionRadius;
	}
    [BurstCompile(FloatPrecision = FloatPrecision.Medium, FloatMode = FloatMode.Fast)]
    public void OnUpdate(ref SystemState state)
    {
        bool useSIMD = true;
        bool useFixedPoint = false;

        if (useFixedPoint)
        {
            var rawBullets = bulletQuery.ToComponentDataArray<LocalTransform>(state.WorldUpdateAllocator);
            var rawEnemies = enemyQuery.ToComponentDataArray<LocalTransform>(state.WorldUpdateAllocator);
            var fpBullets = CollectionHelper.CreateNativeArray<fp2>(rawBullets.Length, state.WorldUpdateAllocator);
            var fpEnemies = CollectionHelper.CreateNativeArray<fp2>(rawEnemies.Length, state.WorldUpdateAllocator);

            // float to Q16.16
            var convertBullets = new ConvertToFixedPointJob { Source = rawBullets, Destination = fpBullets };
            var convertEnemies = new ConvertToFixedPointJob { Source = rawEnemies, Destination = fpEnemies };

            JobHandle packingHandle = JobHandle.CombineDependencies(convertBullets.Schedule(), convertEnemies.Schedule());
            JobHandle combinedDependency = JobHandle.CombineDependencies(state.Dependency, packingHandle);
            var fpJobEvB = new FixedPointCollisionJob
            {
                radiusSqrRaw = fp.FromFloat(enemyCollisionRadiusSqr).raw,
                transToTestAgainst = fpBullets
            };
            state.Dependency = fpJobEvB.ScheduleParallel(enemyQuery, combinedDependency);

            var fpJobPvE = new FixedPointCollisionJob
            {
                radiusSqrRaw = fp.FromFloat(playerCollisionRadiusSqr).raw,
                transToTestAgainst = fpEnemies
            };
            state.Dependency = fpJobPvE.ScheduleParallel(playerQuery, state.Dependency);
        }
        else if (useSIMD)
        {
            var rawBullets = bulletQuery.ToComponentDataArray<LocalTransform>(state.WorldUpdateAllocator);
            var rawEnemies = enemyQuery.ToComponentDataArray<LocalTransform>(state.WorldUpdateAllocator);
            int bulletCount = rawBullets.Length;
            int bulletChunks = bulletCount / 4;
            int bulletRem = bulletCount % 4;
            var packedBulletX = CollectionHelper.CreateNativeArray<float4>(bulletChunks, state.WorldUpdateAllocator);
            var packedBulletZ = CollectionHelper.CreateNativeArray<float4>(bulletChunks, state.WorldUpdateAllocator);
            var remBullets = CollectionHelper.CreateNativeArray<float2>(bulletRem, state.WorldUpdateAllocator);
            int enemyCount = rawEnemies.Length;
            int enemyChunks = enemyCount / 4;
            int enemyRem = enemyCount % 4;
            var packedEnemyX = CollectionHelper.CreateNativeArray<float4>(enemyChunks, state.WorldUpdateAllocator);
            var packedEnemyZ = CollectionHelper.CreateNativeArray<float4>(enemyChunks, state.WorldUpdateAllocator);
            var remEnemies = CollectionHelper.CreateNativeArray<float2>(enemyRem, state.WorldUpdateAllocator);

            // AOS to SOA Conversion
            var packBullets = new PackPositionsJob { Source = rawBullets, PackedX = packedBulletX, PackedZ = packedBulletZ, Remainder = remBullets };
            var packEnemies = new PackPositionsJob { Source = rawEnemies, PackedX = packedEnemyX, PackedZ = packedEnemyZ, Remainder = remEnemies };
            JobHandle packHandle = JobHandle.CombineDependencies(packBullets.Schedule(), packEnemies.Schedule());
            JobHandle combinedDependency = JobHandle.CombineDependencies(state.Dependency, packHandle);

            var simdEvB = new SIMDCollisionJob
            {
                radiusSqr = enemyCollisionRadiusSqr,
                packedX = packedBulletX,
                packedZ = packedBulletZ,
                remainder = remBullets
            };
            state.Dependency = simdEvB.ScheduleParallel(enemyQuery, combinedDependency);

            var simdPvE = new SIMDCollisionJob
            {
                radiusSqr = playerCollisionRadiusSqr,
                packedX = packedEnemyX,
                packedZ = packedEnemyZ,
                remainder = remEnemies
            };
            state.Dependency = simdPvE.ScheduleParallel(playerQuery, state.Dependency);
        }
        else
        {
            var jobEvB = new CollisionJob()
            {
                radiusSqr = enemyCollisionRadiusSqr,
                transToTestAgainst = bulletQuery.ToComponentDataArray<LocalTransform>(state.WorldUpdateAllocator)
            };
            state.Dependency = jobEvB.ScheduleParallel(enemyQuery, state.Dependency);
            var jobPvE = new CollisionJob()
            {
                radiusSqr = playerCollisionRadiusSqr,
                transToTestAgainst = enemyQuery.ToComponentDataArray<LocalTransform>(state.WorldUpdateAllocator)
            };
            state.Dependency = jobPvE.ScheduleParallel(playerQuery, state.Dependency);
        }
    }
}
public struct fp
{
    public int raw;
    public const int SHIFT = 16;
    public const int ONE = 1 << SHIFT;
    public static fp FromFloat(float v) => new fp { raw = (int)(v * ONE) };
}

public struct fp2
{
    public int xRaw;
    public int zRaw;

    public static fp2 FromFloat3(float3 v)
    {
        return new fp2
        {
            xRaw = (int)(v.x * fp.ONE),
            zRaw = (int)(v.z * fp.ONE)
        };
    }
}

[BurstCompile(FloatPrecision = FloatPrecision.Medium, FloatMode = FloatMode.Fast)]
struct ConvertToFixedPointJob : IJob
{
    [ReadOnly] public NativeArray<LocalTransform> Source;
    [WriteOnly] public NativeArray<fp2> Destination;

    public void Execute()
    {
        for (int i = 0; i < Source.Length; i++)
        {
            Destination[i] = fp2.FromFloat3(Source[i].Position);
        }
    }
}

[BurstCompile(FloatPrecision = FloatPrecision.Medium, FloatMode = FloatMode.Fast)]
partial struct FixedPointCollisionJob : IJobEntity
{
    public int radiusSqrRaw;
    [ReadOnly] public NativeArray<fp2> transToTestAgainst;

    public void Execute(ref Health health, in LocalTransform transform)
    {
        float damage = 0f;
        int posAx = (int)(transform.Position.x * fp.ONE);
        int posAz = (int)(transform.Position.z * fp.ONE);

        for (int i = 0; i < transToTestAgainst.Length; i++)
        {
            long deltaX = posAx - transToTestAgainst[i].xRaw;
            long deltaZ = posAz - transToTestAgainst[i].zRaw;

            long sqrX = (deltaX * deltaX) >> fp.SHIFT;
            long sqrZ = (deltaZ * deltaZ) >> fp.SHIFT;
            long distanceSquareRaw = sqrX + sqrZ;
            damage += math.select(0f, 1f, distanceSquareRaw <= radiusSqrRaw);
        }

        if (damage > 0f)
        {
            health.Value -= damage;
        }
    }
}

[BurstCompile(FloatPrecision = FloatPrecision.Medium, FloatMode = FloatMode.Fast)]
struct PackPositionsJob : IJob
{
    // all these read-only and write-only attributes are to avoid race conditions and to inform the job system about the intended usage of these arrays.
    [ReadOnly] public NativeArray<LocalTransform> Source;
    [WriteOnly] public NativeArray<float4> PackedX;
    [WriteOnly] public NativeArray<float4> PackedZ;
    [WriteOnly] public NativeArray<float2> Remainder;
    public void Execute()
    {
        int chunks = Source.Length / 4;
        for (int i = 0; i < chunks; i++)
        {
            int idx = i * 4;
            PackedX[i] = new float4(Source[idx].Position.x, Source[idx + 1].Position.x, Source[idx + 2].Position.x, Source[idx + 3].Position.x);
            PackedZ[i] = new float4(Source[idx].Position.z, Source[idx + 1].Position.z, Source[idx + 2].Position.z, Source[idx + 3].Position.z);
        }

        int remStart = chunks * 4;
        int remCount = Source.Length % 4;
        for (int i = 0; i < remCount; i++)
        {
            Remainder[i] = Source[remStart + i].Position.xz;
        }
    }
}

[BurstCompile(FloatPrecision = FloatPrecision.Medium, FloatMode = FloatMode.Fast)]
partial struct SIMDCollisionJob : IJobEntity
{
    public float radiusSqr;
    [ReadOnly] public NativeArray<float4> packedX;
    [ReadOnly] public NativeArray<float4> packedZ;
    [ReadOnly] public NativeArray<float2> remainder;

    public void Execute(ref Health health, in LocalTransform transform)
    {
        float damage = 0f;
        float4 posAx = transform.Position.x;
        float4 posAz = transform.Position.z;
        float4 rSqr4 = radiusSqr;
        for (int i = 0; i < packedX.Length; i++)
        {
            float4 deltaX = posAx - packedX[i];
            float4 deltaZ = posAz - packedZ[i];
            float4 distSq = (deltaX * deltaX) + (deltaZ * deltaZ);
            damage += math.csum(math.select(float4.zero, new float4(1f), distSq <= rSqr4));
        }
        for (int i = 0; i < remainder.Length; i++)
        {
            float2 delta = transform.Position.xz - remainder[i];
            float distSq = (delta.x * delta.x) + (delta.y * delta.y);
            damage += math.select(0f, 1f, distSq <= radiusSqr);
        }

        if (damage > 0f)
        {
            health.Value -= damage;
        }
    }
}

[BurstCompile(FloatPrecision = FloatPrecision.Medium, FloatMode = FloatMode.Fast)]
partial struct CollisionJob : IJobEntity
{
    public float radiusSqr;
    [ReadOnly] public NativeArray<LocalTransform> transToTestAgainst;
    public void Execute(ref Health health, in LocalTransform transform)
    {
        float damage = 0f;

        float3 posA = transform.Position;
        for (int i = 0; i < transToTestAgainst.Length; i++)
        {
            float3 posB = transToTestAgainst[i].Position;

            float distanceSquare = math.distancesq(posA.xz, posB.xz);

            damage += math.select(0f, 1f, distanceSquare <= radiusSqr);
        }
        if (damage > 0)
            health.Value -= damage;
    }
}