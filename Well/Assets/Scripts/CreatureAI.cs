using UnityEngine;
using UnityEngine.AI;

public enum CreatureBehavior
{
    Wander,
    Hunting,
    Eat,
    Flee
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

    [Header("Search Timing")]
    public float foodSearchInterval = 0.5f;
    public float predatorSearchInterval = 0.25f;

    [Header("Flee")]
    public float dangerRadius = 8f;
    public float safeDistance = 12f;
    public float fleeDistance = 6f;
    public float fleeStuckCheckTime = 1f;
    public float fleeStuckDistance = 0.2f;
    public float fleeStuckTurnAngle = 30f;

    [Header("Breeding")]
    [SerializeField] GameObject offspringPrefab;
    public float breedCooldown = 20f;
    public float breedHungerThreshold = 0.25f;
    public int maxLocalPopulation = 4;
    public float populationCheckRadius = 8f;

    CreatureBehavior behavior;
    NaturalResources targetFood;
    CarnivoreAI targetPredator;
    Vector3 wanderTarget;
    Vector3 fleeTarget;
    Vector3 lastFleeCheckPosition;
    Vector3 lastFleeMoveDirection;
    NavMeshCreatureMotor motor;
    float breedTimer;
    float fleeStuckTimer;
    float foodSearchTimer;
    float predatorSearchTimer;
    bool isDead;

    // Register this herbivore with the ecosystem manager
    void OnEnable()
    {
        EcosystemManager.Instance.Register(this);
    }

    // Unregister this herbivore from the ecosystem manager
    void OnDisable()
    {
        if (EcosystemManager.HasInstance)
        {
            EcosystemManager.Instance.Unregister(this);
        }
    }

    // Initialize movement and first wander target
    void Start()
    {
        motor = GetComponent<NavMeshCreatureMotor>();

        if (motor == null)
        {
            motor = gameObject.AddComponent<NavMeshCreatureMotor>();
        }

        foodSearchTimer = foodSearchInterval;
        predatorSearchTimer = predatorSearchInterval;
        PickWanderTarget();
    }
    // Update hunger, danger awareness, breeding, and behavior
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

        UpdatePredatorAwareness();

        if (behavior != CreatureBehavior.Flee && hunger > maxHunger * 0.4f && targetFood == null)
        {
            behavior = CreatureBehavior.Hunting;
        }

        UpdateCurrentBehavior();
    }

    // Run behavior for the current herbivore state
    void UpdateCurrentBehavior()
    {
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

            case CreatureBehavior.Flee:
                UpdateFlee();
                break;
        }
    }

    // Wander while there is no urgent need
    void UpdateWander()
    {
        MoveTo(wanderTarget);

        if (Vector3.Distance(transform.position, wanderTarget) < 0.5f)
        {
            PickWanderTarget();
        }
    }
    // Find and move toward reachable food
    void UpdateFindFood()
    {
        if (targetFood == null || !targetFood.IsAvailable)
        {
            if (!IsSearchReady(ref foodSearchTimer, foodSearchInterval))
            {
                return;
            }

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
    // Consume food to reduce hunger
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

    // Move away from the nearest predator
    void UpdateFlee()
    {
        if (targetPredator == null)
        {
            PickWanderTarget();
            return;
        }

        float predatorDistance = Vector3.Distance(transform.position, targetPredator.transform.position);

        if (predatorDistance >= safeDistance)
        {
            targetPredator = null;
            targetFood = null;
            PickWanderTarget();
            return;
        }

        if (Vector3.Distance(transform.position, fleeTarget) < 0.5f)
        {
            PickFleeTarget();
        }

        UpdateFleeStuckCheck();

        MoveTo(fleeTarget);
    }

    // Detect stuck fleeing and choose a new escape point
    void UpdateFleeStuckCheck()
    {
        fleeStuckTimer += Time.deltaTime;

        if (fleeStuckTimer < fleeStuckCheckTime)
        {
            return;
        }

        float movedDistance = Vector3.Distance(transform.position, lastFleeCheckPosition);
        Vector3 movedDirection = transform.position - lastFleeCheckPosition;
        lastFleeCheckPosition = transform.position;
        fleeStuckTimer = 0f;

        if (movedDistance >= fleeStuckDistance)
        {
            lastFleeMoveDirection = movedDirection.normalized;
            return;
        }

        PickFleeTargetAfterStuck();
    }

    // Periodically search for nearby predators
    void UpdatePredatorAwareness()
    {
        if (!IsSearchReady(ref predatorSearchTimer, predatorSearchInterval))
        {
            return;
        }

        CarnivoreAI nearestPredator = FindNearestPredator();

        if (nearestPredator == null)
        {
            return;
        }

        if (behavior == CreatureBehavior.Flee && nearestPredator == targetPredator)
        {
            return;
        }

        targetPredator = nearestPredator;
        targetFood = null;
        PickFleeTarget();
    }

    // Find the closest reachable carnivore threat
    CarnivoreAI FindNearestPredator()
    {
        CarnivoreAI nearest = null;
        float nearestDistance = dangerRadius;

        foreach (CarnivoreAI predator in EcosystemManager.Instance.carnivores)
        {
            float distance = Vector3.Distance(transform.position, predator.transform.position);

            if (distance < nearestDistance && CanReach(predator.transform.position))
            {
                nearest = predator;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    // Choose an escape point away from the predator
    void PickFleeTarget()
    {
        if (targetPredator == null)
        {
            fleeTarget = transform.position;
            behavior = CreatureBehavior.Flee;
            return;
        }

        Vector3 awayDirection = transform.position - targetPredator.transform.position;
        awayDirection.y = 0f;

        if (awayDirection.sqrMagnitude < 0.01f)
        {
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            awayDirection = new Vector3(randomDirection.x, 0f, randomDirection.y);
        }

        awayDirection.Normalize();

        for (int i = 0; i < 12; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * 2f;
            Vector3 candidate = transform.position + awayDirection * fleeDistance + new Vector3(randomOffset.x, 0f, randomOffset.y);

            if (!TryGetNavMeshPoint(candidate, out Vector3 navMeshTarget))
            {
                continue;
            }

            if (!CanReach(navMeshTarget))
            {
                continue;
            }

            float oldDistance = Vector3.Distance(transform.position, targetPredator.transform.position);
            float newDistance = Vector3.Distance(navMeshTarget, targetPredator.transform.position);

            if (newDistance <= oldDistance)
            {
                continue;
            }

            fleeTarget = navMeshTarget;
            lastFleeCheckPosition = transform.position;
            fleeStuckTimer = 0f;
            behavior = CreatureBehavior.Flee;
            return;
        }

        fleeTarget = transform.position;
        lastFleeCheckPosition = transform.position;
        fleeStuckTimer = 0f;
        behavior = CreatureBehavior.Flee;
    }

    // Choose a new escape point after getting stuck
    void PickFleeTargetAfterStuck()
    {
        Vector3 baseDirection = -lastFleeMoveDirection;
        baseDirection.y = 0f;

        if (baseDirection.sqrMagnitude < 0.01f && targetPredator != null)
        {
            baseDirection = transform.position - targetPredator.transform.position;
            baseDirection.y = 0f;
        }

        if (baseDirection.sqrMagnitude < 0.01f)
        {
            baseDirection = -transform.forward;
            baseDirection.y = 0f;
        }

        baseDirection.Normalize();

        float side = Random.value < 0.5f ? -1f : 1f;
        float firstAngle = fleeStuckTurnAngle * side;

        if (TryPickFleeTargetInDirection(Quaternion.Euler(0f, firstAngle, 0f) * baseDirection))
        {
            return;
        }

        if (TryPickFleeTargetInDirection(Quaternion.Euler(0f, -firstAngle, 0f) * baseDirection))
        {
            return;
        }

        PickFleeTarget();
    }

    // Try to find a reachable flee target in one direction
    bool TryPickFleeTargetInDirection(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
        {
            return false;
        }

        direction.Normalize();

        for (int i = 0; i < 6; i++)
        {
            float distance = fleeDistance * Mathf.Lerp(0.6f, 1.2f, i / 5f);
            Vector3 candidate = transform.position + direction * distance;

            if (!TryGetNavMeshPoint(candidate, out Vector3 navMeshTarget))
            {
                continue;
            }

            if (!CanReach(navMeshTarget))
            {
                continue;
            }

            fleeTarget = navMeshTarget;
            lastFleeCheckPosition = transform.position;
            fleeStuckTimer = 0f;
            behavior = CreatureBehavior.Flee;
            return true;
        }

        return false;
    }

    // Find the closest reachable matching food resource
    NaturalResources FindNearestFood()
    {
        NaturalResources nearest = null;
        float nearestDistance = searchRadius;

        foreach (NaturalResources resource in EcosystemManager.Instance.resources)
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

    // Check whether this herbivore can reach a target
    bool CanReach(Vector3 target)
    {
        return motor == null || motor.CanReach(target);
    }

    // Update a timer and report when a search can run
    bool IsSearchReady(ref float timer, float interval)
    {
        timer += Time.deltaTime;

        if (timer < interval)
        {
            return false;
        }

        timer = 0f;
        return true;
    }

    // Move toward a target through the shared motor
    void MoveTo(Vector3 target)
    {
        if (motor != null)
        {
            motor.MoveTo(target, speed);
        }
    }

    // Choose a random reachable wander target
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

    // Project a point onto the NavMesh
    bool TryGetNavMeshPoint(Vector3 point, out Vector3 navMeshPoint)
    {
        if (motor == null)
        {
            navMeshPoint = point;
            return false;
        }

        return motor.TryGetNavMeshPoint(point, out navMeshPoint);
    }

    // Spawn a corpse and destroy this herbivore
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

    // External death entry point used by predators
    public void Kill()
    {
        if (isDead)
        {
            return;
        }

        Die();
    }

    // Spawn offspring when hunger, cooldown, and population allow it
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
    // Count nearby herbivores for local population limits
    int CountLocalPopulation()
    {
        int count = 0;

        foreach (CreatureAI creature in EcosystemManager.Instance.herbivores)
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
    // Find a reachable spawn point near this herbivore
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
