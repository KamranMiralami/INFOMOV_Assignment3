using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
[UpdateBefore(typeof(TransformSystemGroup))]
partial struct SpawnBulletSystem : ISystem
{
    float timer;
    
    EntityQuery directoryQuery;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Directory>();
        
        directoryQuery = SystemAPI.QueryBuilder().WithAll<Directory>().Build();

        timer = 0f;
    }
    
    public void OnUpdate(ref SystemState state)
    {
        if(Settings.IsPlayerDead())
            return;
		
        timer += Time.deltaTime;
        
        var directory = directoryQuery.GetSingleton<Directory>();

        Entity bulletEntityPrefab = directory.bulletPrefab;
        EntityManager manager = state.EntityManager;
        
        if (Settings.Instance.useECSforBullets && Input.GetButton("Fire1") && timer >= Settings.Instance.fireRate)
        {
            Vector3 rotation = Settings.PlayerGunBarrelRotationEuler;
            rotation.x = 0f;

            if (Settings.Instance.spreadShot)
            {
                SpawnBulletSpread(ref manager, 
                    bulletEntityPrefab,
                    Settings.Instance.spreadAmount, 
                    Settings.PlayerGunBarrelPosition, 
                    rotation);
            }
            else
            {
                SpawnBullet(
                    ref manager,
                    bulletEntityPrefab,
                    Settings.PlayerGunBarrelPosition,
                    Quaternion.Euler(rotation));   
            }
            
            timer = 0f;
        }
    }
    [BurstCompile(FloatPrecision = FloatPrecision.Medium, FloatMode = FloatMode.Fast)]
    private void SpawnBullet(
        ref EntityManager manager,
        Entity bulletEntityPrefab, 
        float3 position,
        Quaternion rotation)
    {
        Entity bullet = manager.Instantiate(bulletEntityPrefab);
        LocalTransform t = new LocalTransform()
        {
            Position = position,
            Rotation = rotation,
            Scale = 1f
        };
        manager.SetComponentData(bullet, t);
    }
    
    [BurstCompile(FloatPrecision = FloatPrecision.Medium, FloatMode = FloatMode.Fast)]
    private void SpawnBulletSpread(
        ref EntityManager manager, 
        Entity bulletEntityPrefab,
        int spreadAmount,
        float3 position,
        float3 rotation)
    {
        if (spreadAmount % 2 != 0)
            spreadAmount += 1;
        
        int max = spreadAmount / 2;
        int min = -max;
        int totalAmount = spreadAmount * spreadAmount;

        Vector3 tempRot = rotation;
        int index = 0;
        NativeArray<Entity> bullets = new NativeArray<Entity>(totalAmount, Allocator.Temp);
        manager.Instantiate(bulletEntityPrefab, bullets);
        
        for (int x = min; x < max; x++)
        {
            tempRot.x = (rotation.x + 3 * x) % 360;

            for (int y = min; y < max; y++)
            {
                tempRot.y = (rotation.y + 3 * y) % 360;
                LocalTransform t = new LocalTransform
                {
                    Position = position,
                    Rotation = Quaternion.Euler(tempRot),
                    Scale = 1f
                };
                manager.SetComponentData(bullets[index], t);

                index++;
            }
        }
        bullets.Dispose();
    }
}