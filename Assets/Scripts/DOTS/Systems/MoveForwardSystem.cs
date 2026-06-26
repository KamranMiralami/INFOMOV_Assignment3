using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile(FloatPrecision = FloatPrecision.Medium, FloatMode = FloatMode.Fast)]
partial struct MoveForwardSystem : ISystem
{
    EntityQuery moveQuery;
    [BurstCompile(FloatPrecision = FloatPrecision.Medium, FloatMode = FloatMode.Fast)]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<MoveSpeed>();
        moveQuery = SystemAPI.QueryBuilder().WithAll<MoveForward, MoveSpeed, LocalTransform>().Build();
    }

    [BurstCompile(FloatPrecision = FloatPrecision.Medium, FloatMode = FloatMode.Fast)]
    public void OnUpdate(ref SystemState state)
    {
        bool useSIMDMovement = true;
        bool useFixedPointMovement = false;

        if (useFixedPointMovement)
        {
            var fixedPointMoveJob = new FixedPointMovementJob
            {
                dtRaw = (int)(SystemAPI.Time.DeltaTime * 65536f)
            };
            state.Dependency = fixedPointMoveJob.ScheduleParallel(moveQuery, state.Dependency);
        }
        else if (useSIMDMovement)
        {
            var simdChunkJob = new SIMDMovementChunkJob
            {
                dt = SystemAPI.Time.DeltaTime,
                TransformHandle = SystemAPI.GetComponentTypeHandle<LocalTransform>(false),
                SpeedHandle = SystemAPI.GetComponentTypeHandle<MoveSpeed>(true)
            };
            state.Dependency = simdChunkJob.ScheduleParallel(moveQuery, state.Dependency);
        }
        else
        {
            var MoveForwardJob = new MoveForwardJob
            {
                dt = SystemAPI.Time.DeltaTime
            };
            state.Dependency = MoveForwardJob.ScheduleParallel(moveQuery, state.Dependency);
        }
    }
}

[BurstCompile(FloatPrecision = FloatPrecision.Medium, FloatMode = FloatMode.Fast)]
struct SIMDMovementChunkJob : IJobChunk
{
    public float dt;
    public ComponentTypeHandle<LocalTransform> TransformHandle;
    [ReadOnly] public ComponentTypeHandle<MoveSpeed> SpeedHandle;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
    {
        NativeArray<LocalTransform> transforms = chunk.GetNativeArray(ref TransformHandle);
        NativeArray<MoveSpeed> speeds = chunk.GetNativeArray(ref SpeedHandle);
        int count = chunk.Count;
        int chunks = count / 4;
        int remainder = count % 4;
        for (int i = 0; i < chunks; i++)
        {
            int idx = i * 4;
            float4 px = new float4(transforms[idx].Position.x, transforms[idx + 1].Position.x, transforms[idx + 2].Position.x, transforms[idx + 3].Position.x);
            float4 py = new float4(transforms[idx].Position.y, transforms[idx + 1].Position.y, transforms[idx + 2].Position.y, transforms[idx + 3].Position.y);
            float4 pz = new float4(transforms[idx].Position.z, transforms[idx + 1].Position.z, transforms[idx + 2].Position.z, transforms[idx + 3].Position.z);
            float3 f0 = math.forward(transforms[idx].Rotation);
            float3 f1 = math.forward(transforms[idx + 1].Rotation);
            float3 f2 = math.forward(transforms[idx + 2].Rotation);
            float3 f3 = math.forward(transforms[idx + 3].Rotation);

            float4 fx = new float4(f0.x, f1.x, f2.x, f3.x);
            float4 fy = new float4(f0.y, f1.y, f2.y, f3.y);
            float4 fz = new float4(f0.z, f1.z, f2.z, f3.z);
            float4 s = new float4(speeds[idx].Value, speeds[idx + 1].Value, speeds[idx + 2].Value, speeds[idx + 3].Value);
            float4 velocityScale = dt * s;
            px += velocityScale * fx;
            py += velocityScale * fy;
            pz += velocityScale * fz;
            LocalTransform t0 = transforms[idx]; t0.Position = new float3(px.x, py.x, pz.x); transforms[idx] = t0;
            LocalTransform t1 = transforms[idx + 1]; t1.Position = new float3(px.y, py.y, pz.y); transforms[idx + 1] = t1;
            LocalTransform t2 = transforms[idx + 2]; t2.Position = new float3(px.z, py.z, pz.z); transforms[idx + 2] = t2;
            LocalTransform t3 = transforms[idx + 3]; t3.Position = new float3(px.w, py.w, pz.w); transforms[idx + 3] = t3;
        }
        int remStart = chunks * 4;
        for (int i = 0; i < remainder; i++)
        {
            int idx = remStart + i;
            LocalTransform t = transforms[idx];
            t.Position += dt * speeds[idx].Value * math.forward(t.Rotation);
            transforms[idx] = t;
        }
    }
}

[BurstCompile(FloatPrecision = FloatPrecision.Medium, FloatMode = FloatMode.Fast)]
[WithAll(typeof(MoveForward))]
partial struct FixedPointMovementJob : IJobEntity
{
    public int dtRaw; // 16:16

    void Execute(in MoveSpeed speed, ref LocalTransform transform)
    {
        const int SHIFT = 16;
        const int ONE = 1 << SHIFT;
        int speedRaw = (int)(speed.Value * ONE);
        float3 fwd = math.forward(transform.Rotation);
        int fwdXRaw = (int)(fwd.x * ONE);
        int fwdYRaw = (int)(fwd.y * ONE);
        int fwdZRaw = (int)(fwd.z * ONE);
        int posXRaw = (int)(transform.Position.x * ONE);
        int posYRaw = (int)(transform.Position.y * ONE);
        int posZRaw = (int)(transform.Position.z * ONE);
        long stepScale = ((long)dtRaw * speedRaw) >> SHIFT;
        int deltaX = (int)((stepScale * fwdXRaw) >> SHIFT);
        int deltaY = (int)((stepScale * fwdYRaw) >> SHIFT);
        int deltaZ = (int)((stepScale * fwdZRaw) >> SHIFT);
        posXRaw += deltaX;
        posYRaw += deltaY;
        posZRaw += deltaZ;

        transform.Position = new float3((float)posXRaw / ONE, (float)posYRaw / ONE, (float)posZRaw / ONE);
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