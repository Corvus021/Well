using UnityEngine;
using UnityEngine.AI;

public enum CreatureBehavior
{
    Wander,
    Hunting,
    Eat
}
public class CreatureAI : MonoBehaviour
{
    [Header("Death")]
    [SerializeField] GameObject deadBodyPrefab;

    [Header("Stats")]
    public ResourceType foodType = ResourceType.Plant;
    public float hunger = 0f;
    public float maxHunger = 100f;
    public float hungerRate = 5f;
    public float eatRate = 10f;

    public float speed = 2f;
    public float searchRadius = 12f;
    public float eatDistance = 1.2f;
    public float wanderRadius = 5f;

    CreatureBehavior behavior;
    NaturalResources targetFood;
    Vector3 wanderTarget;
    NavMeshAgent agent;
    bool isDead;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        PickWanderTarget();
    }
    // Update is called once per frame
    void Update()
    {
        if (isDead)
        {
            return;
        }

        hunger += hungerRate * Time.deltaTime;
        hunger = Mathf.Clamp(hunger, 0f, maxHunger);

        if (hunger >= maxHunger)
        {
            Die();
            return;
        }

        if (hunger > maxHunger * 0.4f && targetFood == null)
        {
            behavior = CreatureBehavior.Hunting;
        }

        switch (behavior)
        {
            case CreatureBehavior.Wander:
                UpdateWander();
                break;

            case CreatureBehavior.Hunting:
                UpdateFindFood();
                break;

            case CreatureBehavior.Eat:
                UpdateEat();
                break;
        }
    }
    void UpdateWander()
    {
        MoveTo(wanderTarget);

        if (Vector3.Distance(transform.position, wanderTarget) < 0.5f)
        {
            PickWanderTarget();
        }
    }
    void UpdateFindFood()
    {
        if (targetFood == null || !targetFood.IsAvailable)
        {
            targetFood = FindNearestFood();
        }

        if (targetFood == null)
        {
            behavior = CreatureBehavior.Wander;
            return;
        }

        MoveTo(targetFood.transform.position);

        if (Vector3.Distance(transform.position, targetFood.transform.position) <= eatDistance)
        {
            behavior = CreatureBehavior.Eat;
        }
    }
    void UpdateEat()
    {
        if (targetFood == null || !targetFood.IsAvailable)
        {
            targetFood = null;
            behavior = CreatureBehavior.Wander;
            return;
        }

        float eaten = targetFood.Consume(eatRate * Time.deltaTime);
        hunger -= eaten * 5f;
        hunger = Mathf.Clamp(hunger, 0f, maxHunger);

        if (hunger <= maxHunger * 0.15f)
        {
            targetFood = null;
            PickWanderTarget();
        }
    }
    NaturalResources FindNearestFood()
    {
        NaturalResources[] resources = FindObjectsByType<NaturalResources>(FindObjectsSortMode.None);

        NaturalResources nearest = null;
        float nearestDistance = searchRadius;

        foreach (NaturalResources resource in resources)
        {
            if (resource.resourceType != foodType || !resource.IsAvailable)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, resource.transform.position);

            if (distance < nearestDistance)
            {
                nearest = resource;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    void MoveTo(Vector3 target)
    {
        if (agent != null && agent.isOnNavMesh)
        {
            agent.SetDestination(target);
            return;
        }

        Vector3 direction = target - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
        {
            return;
        }

        transform.position += direction.normalized * speed * Time.deltaTime;
        transform.forward = direction.normalized;
    }

    void PickWanderTarget()
    {
        Vector2 random = Random.insideUnitCircle * wanderRadius;
        Vector3 randomTarget = transform.position + new Vector3(random.x, 0f, random.y);

        if (TryGetNavMeshPoint(randomTarget, out Vector3 navMeshTarget))
        {
            wanderTarget = navMeshTarget;
        }
        else
        {
            wanderTarget = randomTarget;
        }

        behavior = CreatureBehavior.Wander;
    }

    bool TryGetNavMeshPoint(Vector3 point, out Vector3 navMeshPoint)
    {
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            navMeshPoint = hit.position;
            return true;
        }

        navMeshPoint = point;
        return false;
    }

    void Die()
    {
        isDead = true;

        if (deadBodyPrefab != null)
        {
            Instantiate(deadBodyPrefab, transform.position, transform.rotation, transform.parent);
        }
        else
        {
            Debug.LogWarning("Dead body prefab is missing on: " + name);
        }

        Destroy(gameObject);
    }
}
