using Unity.Burst;
using Unity.Entities;
using Unity.Collections;

[BurstCompile(FloatPrecision = FloatPrecision.Medium, FloatMode = FloatMode.Fast)]
partial struct RemoveDeadSystem : ISystem
{
	[BurstCompile(FloatPrecision = FloatPrecision.Medium, FloatMode = FloatMode.Fast)]
	public void OnCreate(ref SystemState state)
	{
		state.RequireForUpdate<Health>();
		state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
	}

	[BurstCompile(FloatPrecision = FloatPrecision.Medium, FloatMode = FloatMode.Fast)]
	public void OnUpdate(ref SystemState state)
	{
		var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
		var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
		
		var removeDeadJob = new RemoveDeadJob
		{
			ecbParallelWriter = ecb.AsParallelWriter()
		};
		
		state.Dependency = removeDeadJob.ScheduleParallel(state.Dependency);
	}
}

[BurstCompile(FloatPrecision = FloatPrecision.Medium, FloatMode = FloatMode.Fast)]
[WithAll(typeof(EnemyTag))]
partial struct RemoveDeadJob : IJobEntity
{
	public EntityCommandBuffer.ParallelWriter ecbParallelWriter;
	
	void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, in Health health)
	{
		if (health.Value <= 0f)
		{
			ecbParallelWriter.DestroyEntity(chunkIndex, entity);
		}
	}
}