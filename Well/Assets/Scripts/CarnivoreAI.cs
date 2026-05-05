using UnityEngine;
using UnityEngine.AI;

public enum CarnivoreState
{
    Wander,
    FindPrey,
    ChasePrey,
    Attack,
    MoveToCorpse,
    EatCorpse
}

public class CarnivoreAI : MonoBehaviour
{
    [Header("Death")]
    [SerializeField] GameObject deadBodyPrefab;

    [Header("Stats")]
    public float hunger = 0f;
    public float maxHunger = 100f;
    public float hungerRate = 4f;
    public float hungerRestoredPerKill = 60f;

    [Header("Movement")]
    public float speed = 2.5f;
    public float searchRadius = 18f;
    public float attackDistance = 1.3f;
    public float wanderRadius = 6f;
    public float corpseEatRate = 15f;

    [Header("Hunting")]
    public float huntHungerThreshold = 0.35f;
    public float attackCooldown = 1f;

    CarnivoreState state;
    CreatureAI targetPrey;
    NaturalResources targetCorpse;
    NavMeshAgent agent;
    Vector3 wanderTarget;
    float attackTimer;
    bool isDead;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        PickWanderTarget();
    }

    void Update()
    {
        if (isDead)
        {
            return;
        }

        hunger += hungerRate * Time.deltaTime;
        hunger = Mathf.Clamp(hunger, 0f, maxHunger);
        attackTimer += Time.deltaTime;

        if (hunger >= maxHunger)
        {
            Die();
            return;
        }

        if (hunger > maxHunger * huntHungerThreshold && targetPrey == null && targetCorpse == null)
        {
            state = CarnivoreState.FindPrey;
        }

        switch (state)
        {
            case CarnivoreState.Wander:
                UpdateWander();
                break;

            case CarnivoreState.FindPrey:
                UpdateFindPrey();
                break;

            case CarnivoreState.ChasePrey:
                UpdateChasePrey();
                break;

            case CarnivoreState.Attack:
                UpdateAttack();
                break;

            case CarnivoreState.MoveToCorpse:
                UpdateMoveToCorpse();
                break;

            case CarnivoreState.EatCorpse:
                UpdateEatCorpse();
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

    void UpdateFindPrey()
    {
        targetPrey = FindNearestPrey();
        targetCorpse = FindNearestCorpse();

        if (targetPrey == null && targetCorpse == null)
        {
            PickWanderTarget();
            return;
        }

        if (targetCorpse != null && IsCorpseCloserThanPrey())
        {
            targetPrey = null;
            state = CarnivoreState.MoveToCorpse;
            return;
        }

        targetCorpse = null;
        state = CarnivoreState.ChasePrey;
    }

    void UpdateChasePrey()
    {
        if (targetPrey == null)
        {
            state = CarnivoreState.FindPrey;
            return;
        }

        MoveTo(targetPrey.transform.position);

        if (Vector3.Distance(transform.position, targetPrey.transform.position) <= attackDistance)
        {
            state = CarnivoreState.Attack;
        }
    }

    void UpdateMoveToCorpse()
    {
        if (targetCorpse == null || !targetCorpse.IsAvailable)
        {
            targetCorpse = null;
            state = CarnivoreState.FindPrey;
            return;
        }

        MoveTo(targetCorpse.transform.position);

        if (Vector3.Distance(transform.position, targetCorpse.transform.position) <= attackDistance)
        {
            state = CarnivoreState.EatCorpse;
        }
    }

    void UpdateEatCorpse()
    {
        if (targetCorpse == null || !targetCorpse.IsAvailable)
        {
            targetCorpse = null;
            state = CarnivoreState.FindPrey;
            return;
        }

        if (Vector3.Distance(transform.position, targetCorpse.transform.position) > attackDistance)
        {
            state = CarnivoreState.MoveToCorpse;
            return;
        }

        float eaten = targetCorpse.Consume(corpseEatRate * Time.deltaTime);
        hunger -= eaten;
        hunger = Mathf.Clamp(hunger, 0f, maxHunger);

        if (targetCorpse.amount <= 0f)
        {
            Destroy(targetCorpse.gameObject);
            targetCorpse = null;
            PickWanderTarget();
            return;
        }

        if (hunger <= maxHunger * 0.15f)
        {
            targetCorpse = null;
            PickWanderTarget();
        }
    }

    void UpdateAttack()
    {
        if (targetPrey == null)
        {
            state = CarnivoreState.FindPrey;
            return;
        }

        if (Vector3.Distance(transform.position, targetPrey.transform.position) > attackDistance)
        {
            state = CarnivoreState.ChasePrey;
            return;
        }

        if (attackTimer < attackCooldown)
        {
            return;
        }

        targetPrey.Kill();
        targetPrey = null;
        hunger -= hungerRestoredPerKill;
        hunger = Mathf.Clamp(hunger, 0f, maxHunger);
        attackTimer = 0f;

        if (hunger > maxHunger * huntHungerThreshold)
        {
            state = CarnivoreState.FindPrey;
        }
        else
        {
            PickWanderTarget();
        }
    }

    CreatureAI FindNearestPrey()
    {
        CreatureAI[] creatures = FindObjectsByType<CreatureAI>(FindObjectsSortMode.None);

        CreatureAI nearest = null;
        float nearestDistance = searchRadius;

        foreach (CreatureAI creature in creatures)
        {
            if (creature.foodType != ResourceType.Plant)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, creature.transform.position);

            if (distance > nearestDistance)
            {
                continue;
            }

            if (!CanReach(creature.transform.position))
            {
                continue;
            }

            nearest = creature;
            nearestDistance = distance;
        }

        return nearest;
    }

    NaturalResources FindNearestCorpse()
    {
        NaturalResources[] resources = FindObjectsByType<NaturalResources>(FindObjectsSortMode.None);

        NaturalResources nearest = null;
        float nearestDistance = searchRadius;

        foreach (NaturalResources resource in resources)
        {
            if (resource.resourceType != ResourceType.DeadBody || !resource.IsAvailable)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, resource.transform.position);

            if (distance > nearestDistance)
            {
                continue;
            }

            if (!CanReach(resource.transform.position))
            {
                continue;
            }

            nearest = resource;
            nearestDistance = distance;
        }

        return nearest;
    }

    bool IsCorpseCloserThanPrey()
    {
        if (targetCorpse == null)
        {
            return false;
        }

        if (targetPrey == null)
        {
            return true;
        }

        float corpseDistance = Vector3.Distance(transform.position, targetCorpse.transform.position);
        float preyDistance = Vector3.Distance(transform.position, targetPrey.transform.position);
        return corpseDistance <= preyDistance;
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
            state = CarnivoreState.Wander;
            return;
        }

        wanderTarget = transform.position;
        state = CarnivoreState.Wander;
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

    void Die()
    {
        isDead = true;

        if (deadBodyPrefab != null)
        {
            Instantiate(deadBodyPrefab, transform.position, transform.rotation, transform.parent);
        }

        Destroy(gameObject);
    }
}
