using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;


[BurstCompile(FloatPrecision = FloatPrecision.Medium, FloatMode = FloatMode.Fast)]
[RequireMatchingQueriesForUpdate]
public partial struct TimedDestroySystem : ISystem
{ 
	[BurstCompile(FloatPrecision = FloatPrecision.Medium, FloatMode = FloatMode.Fast)]
	public void OnUpdate(ref SystemState state)
	{
		using (var commandBuffer = new EntityCommandBuffer(Allocator.TempJob))
		{
			foreach (var (timer, entity) in SystemAPI.Query<RefRW<TimeToLive>>().WithEntityAccess())
			{
				timer.ValueRW.Value -= Time.fixedDeltaTime;
				if (timer.ValueRO.Value < 0f)
				{
					commandBuffer.DestroyEntity(entity);
				}
			}
			commandBuffer.Playback(state.EntityManager);
		}
	}
}