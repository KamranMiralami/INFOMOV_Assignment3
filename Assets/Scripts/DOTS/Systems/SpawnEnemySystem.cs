using UnityEngine;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
[UpdateBefore(typeof(TransformSystemGroup))]
partial struct SpawnEnemySystem : ISystem
{
    float timer;
    EntityQuery directoryQuery;
    EntityManager manager;
    
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Directory>();
        
        directoryQuery = SystemAPI.QueryBuilder().WithAll<Directory>().Build();
        
        timer = 0f;
    }
    
    public void OnUpdate(ref SystemState state)
    {
        if (!Settings.Instance.useECSforEnemies || 
            !Settings.Instance.spawnEnemies ||
            Settings.IsPlayerDead())
            return;
        
        Directory directory = directoryQuery.GetSingleton<Directory>();

        Entity enemyPrefab = directory.enemyPrefab;
        manager = state.EntityManager;

        timer += SystemAPI.Time.DeltaTime;

        if (timer > Settings.Instance.enemySpawnInterval)
        {
            for (int i = 0; i < Settings.Instance.enemySpawnsPerInterval; i++)
            {
                float3 newEnemyPosition =
                    Settings.GetPositionAroundPlayer(Settings.Instance.enemySpawnRadius);

                SpawnEnemy(
                    ref manager,
                    enemyPrefab,
                    newEnemyPosition);

                timer = 0f;
            }
        }
    }
    
    [BurstCompile]
    void SpawnEnemy(
        ref EntityManager manager,
        Entity enemyPrefab, 
        float3 position)
    {
        Entity newEnemy = manager.Instantiate(enemyPrefab);
        LocalTransform t = new LocalTransform()
        {
            Position = position,
            Rotation = Quaternion.identity,
            Scale = 1f
        };
        manager.SetComponentData(newEnemy, t);
    }
}