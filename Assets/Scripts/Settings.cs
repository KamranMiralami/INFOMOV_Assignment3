using System;
using UnityEngine;
using UnityEngine.Serialization;

public class Settings : MonoBehaviour
{
	public static Settings Instance { get; private set; }

	public bool useECSforBullets = false;
	public bool spreadShot = false;
	public float fireRate = .1f;
	public int spreadAmount = 20;
	
	public bool spawnEnemies = false;
	public bool useECSforEnemies = true;
	public float enemySpawnRadius = 17f;

	public bool useExtraOptimizations = true;

	[Range(1, 100)] public int enemySpawnsPerInterval = 1;
	[Range(.1f, 2f)] public float enemySpawnInterval = 1f;
	
	public Transform player;
	
	private PlayerShooting _playerShooting;
	
	public readonly static float PlayerCollisionRadius = .5f;
	public readonly static float EnemyCollisionRadius = .3f;
	public static Vector3 PlayerPosition => Instance.player.position;
	
	public static Vector3 PlayerGunBarrelPosition => 
		Instance._playerShooting.gunBarrel.position;
	
	public static Vector3 PlayerGunBarrelRotationEuler => 
		Instance._playerShooting.gunBarrel.rotation.eulerAngles;
	
	void Awake()
	{
		if (Instance != null && Instance != this)
			Destroy(gameObject);
		else
			Instance = this;
	}

	private void Start()
	{
		_playerShooting = player.GetComponent<PlayerShooting>();
	}

	public static Vector3 GetPositionAroundPlayer(float radius)
	{
		Vector3 playerPos = Instance.player.position;

		float angle = UnityEngine.Random.Range(0f, 2 * Mathf.PI);
		float s = Mathf.Sin(angle);
		float c = Mathf.Cos(angle);
		
		return new Vector3(c * radius, 1.1f, s * radius) + playerPos;
	}

	public static void PlayerDied()
	{
		if (Instance.player == null)
			return;

		Instance.player = null;
	}

	public static bool IsPlayerDead()
	{
		return Instance.player == null;
	}
}