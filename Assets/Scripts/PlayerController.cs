using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
	public Camera mainCamera;

	public float speed = 4.5f;
	public LayerMask whatIsGround;

	public float playerHealth = 1f;

	Rigidbody playerRigidbody;
	bool isDead;

	bool useECS;
	EntityManager manager;
	Entity playerDataEntity;

	void Awake()
	{
		playerRigidbody = GetComponent<Rigidbody>();
	}

	void Start()
	{
		useECS = Settings.Instance.useECSforBullets;
		CreateEntity();
	}

	void FixedUpdate()
	{
		if (isDead)
			return;
		float h = Input.GetAxis("Horizontal");
		float v = Input.GetAxis("Vertical");
		Vector3 inputDirection = new Vector3(h, 0, v);
		var cameraForward = mainCamera.transform.forward;
		var cameraRight = mainCamera.transform.right;
		cameraForward.y = 0f;
		cameraRight.y = 0f;
		Vector3 desiredDirection = cameraForward * inputDirection.z + cameraRight * inputDirection.x;
		MoveThePlayer(desiredDirection);
		TurnThePlayer();
		UpdateEntity();
	}

	void MoveThePlayer(Vector3 desiredDirection)
	{
		Vector3 movement = new Vector3(desiredDirection.x, 0f, desiredDirection.z);
		movement = movement.normalized * speed * Time.deltaTime;

		playerRigidbody.MovePosition(transform.position + movement);
	}

	void TurnThePlayer()
	{
		Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
		RaycastHit hit;

		if (Physics.Raycast(ray, out hit, whatIsGround))
		{
			Vector3 playerToMouse = hit.point - transform.position;
			playerToMouse.y = 0f;
			playerToMouse.Normalize();

			Quaternion newRotation = Quaternion.LookRotation(playerToMouse);
			playerRigidbody.MoveRotation(newRotation);
		}
	}
	void OnTriggerEnter(Collider theCollider)
	{
		if (!theCollider.CompareTag("Enemy"))
			return;

		playerHealth--;

		if(playerHealth <= 0)
		{
			PlayerDied();
		}
	}

	void PlayerDied()
	{
		if (isDead)
			return;

		isDead = true;
		playerRigidbody.isKinematic = true;
		GetComponent<Collider>().enabled = false;

		Settings.PlayerDied();
	}

	void CreateEntity()
	{
		if (!useECS) 
			return;
		manager = World.DefaultGameObjectInjectionWorld.EntityManager;
		playerDataEntity = manager.CreateEntity();
		manager.AddComponent<PlayerTag>(playerDataEntity);
		LocalTransform t = new LocalTransform
		{
			Position = transform.position
		};
		manager.AddComponentData(playerDataEntity, t);
		Health h = new Health
		{
			Value = playerHealth
		};
		manager.AddComponentData(playerDataEntity, h);
	}

	void UpdateEntity()
	{
		if (!useECS)
			return;
		if (playerDataEntity == Entity.Null)
			return;
		Health health = manager.GetComponentData<Health>(playerDataEntity);
		if (health.Value > 0)
		{
			LocalTransform t = new LocalTransform
			{
				Position = transform.position
			};
			manager.SetComponentData(playerDataEntity, t);
		}
		else
		{
			PlayerDied();
		}

	}
}