using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;


[BurstCompile]
[UpdateAfter(typeof(TurnTowardsPlayerSystem))]
partial struct CollisionSystem : ISystem
{
	EntityQuery enemyQuery;
	EntityQuery bulletQuery;
	EntityQuery playerQuery;
	float enemyCollisionRadiusSqr;
	float playerCollisionRadiusSqr;

	[BurstCompile]
	public void OnCreate(ref SystemState state)
	{
		state.RequireForUpdate<EnemyTag>(); 
		enemyQuery = SystemAPI.QueryBuilder().WithAll<Health, EnemyTag, LocalTransform>().Build();
		bulletQuery = SystemAPI.QueryBuilder().WithAll<TimeToLive, LocalTransform>().Build();
		playerQuery = SystemAPI.QueryBuilder().WithAll<Health, PlayerTag, LocalTransform>().Build();
		enemyCollisionRadiusSqr = Settings.EnemyCollisionRadius * Settings.EnemyCollisionRadius;
		playerCollisionRadiusSqr = Settings.PlayerCollisionRadius * Settings.PlayerCollisionRadius;
	}
	public void OnUpdate(ref SystemState state)
	{
		var useExtraOptimizations = Settings.Instance.useExtraOptimizations;
        //TODO : add simd branch 
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

[BurstCompile]
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