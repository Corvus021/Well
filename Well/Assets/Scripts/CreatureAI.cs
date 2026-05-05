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

    [Header("Breeding")]
    [SerializeField] GameObject offspringPrefab;
    public float breedCooldown = 20f;
    public float breedHungerThreshold = 0.25f;
    public int maxLocalPopulation = 4;
    public float populationCheckRadius = 8f;

    CreatureBehavior behavior;
    NaturalResources targetFood;
    Vector3 wanderTarget;
    NavMeshAgent agent;
    float breedTimer;
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

        breedTimer += Time.deltaTime;

        TryBreed();

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

            if (!CanReach(resource.transform.position))
            {
                continue;
            }

            if (distance < nearestDistance)
            {
                nearest = resource;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    bool CanReach(Vector3 target)
    {
        if (agent == null || !agent.isOnNavMesh)
        {
            return true;
        }

        NavMeshPath path = new NavMeshPath();

        if (!agent.CalculatePath(target, path))
        {
            return false;
        }

        return path.status == NavMeshPathStatus.PathComplete;
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
        for (int i = 0; i < 20; i++)
        {
            Vector2 random = Random.insideUnitCircle * wanderRadius;
            Vector3 randomTarget = transform.position + new Vector3(random.x, 0f, random.y);

            if (!TryGetNavMeshPoint(randomTarget, out Vector3 navMeshTarget))
            {
                continue;
            }

            if (!CanReach(navMeshTarget))
            {
                continue;
            }

            wanderTarget = navMeshTarget;
            behavior = CreatureBehavior.Wander;
            return;
        }

        wanderTarget = transform.position;
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

    public void Kill()
    {
        if (isDead)
        {
            return;
        }

        Die();
    }

    void TryBreed()
    {
        if (offspringPrefab == null)
        {
            return;
        }

        if (breedTimer < breedCooldown)
        {
            return;
        }

        if (hunger > maxHunger * breedHungerThreshold)
        {
            return;
        }

        int localPopulation = CountLocalPopulation();

        if (localPopulation >= maxLocalPopulation)
        {
            return;
        }

        if (!TryGetSpawnNearSelf(out Vector3 spawnPosition))
        {
            return;
        }

        Instantiate(offspringPrefab, spawnPosition, transform.rotation, transform.parent);
        breedTimer = 0f;
    }
    int CountLocalPopulation()
    {
        CreatureAI[] creatures = FindObjectsByType<CreatureAI>(FindObjectsSortMode.None);
        int count = 0;

        foreach (CreatureAI creature in creatures)
        {
            if (creature.foodType != foodType)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, creature.transform.position);

            if (distance <= populationCheckRadius)
            {
                count++;
            }
        }

        return count;
    }
    bool TryGetSpawnNearSelf(out Vector3 spawnPosition)
    {
        for (int i = 0; i < 20; i++)
        {
            Vector2 random = Random.insideUnitCircle * 2f;
            Vector3 candidate = transform.position + new Vector3(random.x, 0f, random.y);

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                continue;
            }

            if (!CanReach(hit.position))
            {
                continue;
            }

            spawnPosition = hit.position;
            return true;
        }

        spawnPosition = transform.position;
        return false;
    }
}
