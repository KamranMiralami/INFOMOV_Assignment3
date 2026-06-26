using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile(FloatPrecision = FloatPrecision.Medium, FloatMode = FloatMode.Fast)]
partial struct MoveForwardSystem : ISystem
{
    [BurstCompile(FloatPrecision = FloatPrecision.Medium, FloatMode = FloatMode.Fast)]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<MoveSpeed>();
    }

    [BurstCompile(FloatPrecision = FloatPrecision.Medium, FloatMode = FloatMode.Fast)]
    public void OnUpdate(ref SystemState state)
    {
        var MoveForwardJob = new MoveForwardJob
		{
            dt = SystemAPI.Time.DeltaTime
        };
        MoveForwardJob.ScheduleParallel();
    }
}

[BurstCompile(FloatPrecision = FloatPrecision.Medium, FloatMode = FloatMode.Fast)]
[WithAll(typeof(MoveForward))]
public partial struct MoveForwardJob : IJobEntity
{
    public float dt;
    void Execute(in MoveSpeed speed, ref LocalTransform transform)
    {
        transform.Position = transform.Position + dt * speed.Value * math.forward(transform.Rotation);
    }
}