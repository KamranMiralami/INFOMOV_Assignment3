using Unity.Burst;
using Unity.Entities;
using Unity.Collections;

[BurstCompile]
partial struct RemoveDeadSystem : ISystem
{
	[BurstCompile]
	public void OnCreate(ref SystemState state)
	{
		state.RequireForUpdate<Health>();
	}

	[BurstCompile]
	public void OnUpdate(ref SystemState state)
	{
		using (var commandBuffer = new EntityCommandBuffer(Allocator.TempJob))
		{
			foreach (var (health, entity) in SystemAPI.Query<RefRO<Health>>().WithAll<EnemyTag>().WithEntityAccess())
			{
				if (health.ValueRO.Value <= 0f)
				{
					commandBuffer.DestroyEntity(entity);
				}
			}
			commandBuffer.Playback(state.EntityManager);
		}
	}
}