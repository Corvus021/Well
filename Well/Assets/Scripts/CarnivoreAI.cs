using UnityEngine;
using UnityEngine.AI;

public enum CarnivoreState
{
    Wander,
    FindPrey,
    ChasePrey,
    Attack,
    MoveToCorpse,
    EatCorpse,
    ChaseRival,
    FightRival,
    RetreatFromRival,
    FindMate,
    MoveToMate,
    Breed
}

public enum CarnivoreSex
{
    Male,
    Female
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
    public float health = 100f;
    public float maxHealth = 100f;
    public bool randomizeHealthOnStart = true;
    public float minRandomHealth = 80f;
    public float maxRandomHealth = 120f;
    public float healthRegenRate = 3f;

    [Header("Movement")]
    public float speed = 2.5f;
    public float searchRadius = 18f;
    public float attackDistance = 1.3f;
    public float wanderRadius = 6f;
    public float corpseEatRate = 15f;

    [Header("Hunting")]
    public float huntHungerThreshold = 0.35f;
    public float attackCooldown = 1f;

    [Header("Sex And Rivalry")]
    public CarnivoreSex sex = CarnivoreSex.Female;
    public bool randomizeSexOnStart = true;
    public float rivalSearchRadius = 10f;
    public float rivalFightDistance = 1.4f;
    public float rivalAttackDamage = 15f;
    public float rivalAttackCooldown = 1.5f;
    public float rivalRetreatDistance = 3f;
    public float rivalRetreatDuration = 1f;

    [Header("Breeding")]
    [SerializeField] GameObject offspringPrefab;
    public int targetCarnivorePopulation = 6;
    public float breedCooldown = 30f;
    public float breedHungerThreshold = 0.35f;
    public float mateSearchRadius = 30f;
    public float breedDistance = 3f;
    public float birthProtectionDuration = 20f;

    CarnivoreState state;
    CreatureAI targetPrey;
    NaturalResources targetCorpse;
    CarnivoreAI targetRival;
    CarnivoreAI targetMate;
    NavMeshAgent agent;
    Vector3 wanderTarget;
    Vector3 rivalRetreatTarget;
    float attackTimer;
    float rivalAttackTimer;
    float rivalRetreatTimer;
    float breedTimer;
    float birthProtectionTimer;
    bool isDead;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        if (randomizeSexOnStart)
        {
            sex = Random.value < 0.5f ? CarnivoreSex.Male : CarnivoreSex.Female;
        }

        if (randomizeHealthOnStart)
        {
            maxHealth = Random.Range(minRandomHealth, maxRandomHealth);
            health = maxHealth;
        }

        health = Mathf.Clamp(health, 0f, maxHealth);
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
        rivalAttackTimer += Time.deltaTime;
        breedTimer += Time.deltaTime;
        UpdateBirthProtection();

        if (hunger >= maxHunger)
        {
            Die();
            return;
        }

        RegenerateHealthWhileWandering();

        MaintainBreedingSexBalance();

        UpdateRivalAwareness();

        if (state != CarnivoreState.ChaseRival && state != CarnivoreState.FightRival &&
            state != CarnivoreState.RetreatFromRival &&
            hunger > maxHunger * huntHungerThreshold && targetPrey == null && targetCorpse == null)
        {
            state = CarnivoreState.FindPrey;
        }
        else
        {
            UpdateBreedingAwareness();
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

            case CarnivoreState.ChaseRival:
                UpdateChaseRival();
                break;

            case CarnivoreState.FightRival:
                UpdateFightRival();
                break;

            case CarnivoreState.RetreatFromRival:
                UpdateRetreatFromRival();
                break;

            case CarnivoreState.FindMate:
                UpdateFindMate();
                break;

            case CarnivoreState.MoveToMate:
                UpdateMoveToMate();
                break;

            case CarnivoreState.Breed:
                UpdateBreed();
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

    void UpdateChaseRival()
    {
        if (!IsValidRival(targetRival))
        {
            targetRival = null;
            PickWanderTarget();
            return;
        }

        MoveTo(targetRival.transform.position);

        if (Vector3.Distance(transform.position, targetRival.transform.position) <= rivalFightDistance)
        {
            state = CarnivoreState.FightRival;
        }
    }

    void UpdateFightRival()
    {
        if (!IsValidRival(targetRival))
        {
            targetRival = null;
            PickWanderTarget();
            return;
        }

        if (Vector3.Distance(transform.position, targetRival.transform.position) > rivalFightDistance)
        {
            state = CarnivoreState.ChaseRival;
            return;
        }

        if (rivalAttackTimer < rivalAttackCooldown)
        {
            return;
        }

        targetRival.TakeDamage(rivalAttackDamage);
        rivalAttackTimer = 0f;

        PickRivalRetreatTarget();
        state = CarnivoreState.RetreatFromRival;
    }

    void UpdateRetreatFromRival()
    {
        if (!IsValidRival(targetRival))
        {
            targetRival = null;
            PickWanderTarget();
            return;
        }

        rivalRetreatTimer += Time.deltaTime;
        MoveTo(rivalRetreatTarget);

        if (rivalRetreatTimer >= rivalRetreatDuration ||
            Vector3.Distance(transform.position, rivalRetreatTarget) < 0.5f)
        {
            state = CarnivoreState.ChaseRival;
        }
    }

    void UpdateRivalAwareness()
    {
        if (sex != CarnivoreSex.Male)
        {
            return;
        }

        if (state == CarnivoreState.ChaseRival || state == CarnivoreState.FightRival ||
            state == CarnivoreState.RetreatFromRival)
        {
            return;
        }

        CarnivoreAI nearestRival = FindNearestRival();

        if (nearestRival == null)
        {
            return;
        }

        targetRival = nearestRival;
        targetPrey = null;
        targetCorpse = null;
        targetMate = null;
        state = CarnivoreState.ChaseRival;
    }

    void UpdateBreedingAwareness()
    {
        if (state == CarnivoreState.FindMate || state == CarnivoreState.MoveToMate ||
            state == CarnivoreState.Breed)
        {
            return;
        }

        if (!CanStartOrContinueBreeding())
        {
            return;
        }

        CarnivoreAI[] carnivores = FindObjectsByType<CarnivoreAI>(FindObjectsSortMode.None);

        if (carnivores.Length >= targetCarnivorePopulation)
        {
            return;
        }

        EnsureBothSexesExist(carnivores);

        if (IsValidMate(targetMate))
        {
            state = CarnivoreState.MoveToMate;
            return;
        }

        targetMate = FindNearestMate();

        if (targetMate == null)
        {
            return;
        }

        if (targetMate.targetMate == null && targetMate.CanStartOrContinueBreeding())
        {
            targetMate.targetMate = this;
        }

        targetPrey = null;
        targetCorpse = null;
        state = CarnivoreState.MoveToMate;
    }

    void MaintainBreedingSexBalance()
    {
        CarnivoreAI[] carnivores = FindObjectsByType<CarnivoreAI>(FindObjectsSortMode.None);

        if (carnivores.Length <= 0 || carnivores.Length >= targetCarnivorePopulation)
        {
            return;
        }

        EnsureBothSexesExist(carnivores);
    }

    void UpdateFindMate()
    {
        targetMate = FindNearestMate();

        if (targetMate == null)
        {
            PickWanderTarget();
            return;
        }

        state = CarnivoreState.MoveToMate;
    }

    void UpdateMoveToMate()
    {
        if (!IsValidMate(targetMate))
        {
            targetMate = null;
            state = CarnivoreState.FindMate;
            return;
        }

        if (sex == CarnivoreSex.Female)
        {
            StopMoving();

            if (Vector3.Distance(transform.position, targetMate.transform.position) <= breedDistance)
            {
                state = CarnivoreState.Breed;
            }

            return;
        }

        if (Vector3.Distance(transform.position, targetMate.transform.position) <= breedDistance)
        {
            state = CarnivoreState.Breed;
            return;
        }

        MoveTo(targetMate.transform.position);
    }

    void UpdateBreed()
    {
        if (!IsValidMate(targetMate))
        {
            targetMate = null;
            state = CarnivoreState.FindMate;
            return;
        }

        if (Vector3.Distance(transform.position, targetMate.transform.position) > breedDistance)
        {
            state = CarnivoreState.MoveToMate;
            return;
        }

        CarnivoreAI mate = targetMate;

        if (sex != CarnivoreSex.Female)
        {
            StopMoving();
            return;
        }

        SpawnOffspringWithMate(mate);
        FinishBreeding();
        mate.FinishBreeding();
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

    CarnivoreAI FindNearestRival()
    {
        CarnivoreAI[] carnivores = FindObjectsByType<CarnivoreAI>(FindObjectsSortMode.None);

        CarnivoreAI nearest = null;
        float nearestDistance = rivalSearchRadius;

        foreach (CarnivoreAI carnivore in carnivores)
        {
            if (!IsValidRival(carnivore))
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, carnivore.transform.position);

            if (distance > nearestDistance)
            {
                continue;
            }

            if (!CanReach(carnivore.transform.position))
            {
                continue;
            }

            nearest = carnivore;
            nearestDistance = distance;
        }

        return nearest;
    }

    CarnivoreAI FindNearestMate()
    {
        CarnivoreAI[] carnivores = FindObjectsByType<CarnivoreAI>(FindObjectsSortMode.None);

        CarnivoreAI nearest = null;
        float nearestDistance = mateSearchRadius;

        foreach (CarnivoreAI carnivore in carnivores)
        {
            if (!IsValidMate(carnivore))
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, carnivore.transform.position);

            if (distance > nearestDistance)
            {
                continue;
            }

            if (!CanReach(carnivore.transform.position))
            {
                continue;
            }

            nearest = carnivore;
            nearestDistance = distance;
        }

        return nearest;
    }

    bool IsValidRival(CarnivoreAI carnivore)
    {
        if (carnivore == null || carnivore == this)
        {
            return false;
        }

        return sex == CarnivoreSex.Male && carnivore.sex == CarnivoreSex.Male &&
            !carnivore.isDead && !carnivore.IsBirthProtected;
    }

    bool IsValidMate(CarnivoreAI carnivore)
    {
        if (carnivore == null || carnivore == this || carnivore.isDead)
        {
            return false;
        }

        if (IsBirthProtected || carnivore.IsBirthProtected)
        {
            return false;
        }

        if (sex == carnivore.sex)
        {
            return false;
        }

        return carnivore.CanStartOrContinueBreeding();
    }

    bool CanStartOrContinueBreeding()
    {
        if (isDead)
        {
            return false;
        }

        if (IsBirthProtected)
        {
            return false;
        }

        if (breedTimer < breedCooldown)
        {
            return false;
        }

        if (hunger > maxHunger * breedHungerThreshold)
        {
            return false;
        }

        if (state == CarnivoreState.ChaseRival || state == CarnivoreState.FightRival ||
            state == CarnivoreState.RetreatFromRival || state == CarnivoreState.Attack ||
            state == CarnivoreState.EatCorpse)
        {
            return false;
        }

        return true;
    }

    void EnsureBothSexesExist(CarnivoreAI[] carnivores)
    {
        bool hasMale = false;
        bool hasFemale = false;

        foreach (CarnivoreAI carnivore in carnivores)
        {
            if (carnivore == null || carnivore.isDead)
            {
                continue;
            }

            if (carnivore.sex == CarnivoreSex.Male)
            {
                hasMale = true;
            }
            else
            {
                hasFemale = true;
            }
        }

        if (hasMale && hasFemale)
        {
            return;
        }

        CarnivoreAI chosen = null;

        foreach (CarnivoreAI carnivore in carnivores)
        {
            if (carnivore == null || carnivore.isDead)
            {
                continue;
            }

            chosen = carnivore;
            break;
        }

        if (chosen == null)
        {
            return;
        }

        chosen.sex = hasMale ? CarnivoreSex.Female : CarnivoreSex.Male;
        chosen.targetMate = null;
        chosen.targetRival = null;
        chosen.PickWanderTarget();
    }

    void SpawnOffspringWithMate(CarnivoreAI mate)
    {
        if (offspringPrefab == null)
        {
            return;
        }

        if (FindObjectsByType<CarnivoreAI>(FindObjectsSortMode.None).Length >= targetCarnivorePopulation)
        {
            return;
        }

        Vector3 spawnPosition = (transform.position + mate.transform.position) * 0.5f;

        if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            spawnPosition = hit.position;
        }

        GameObject offspring = Instantiate(offspringPrefab, spawnPosition, transform.rotation, transform.parent);
        CarnivoreAI offspringAI = offspring.GetComponent<CarnivoreAI>();

        if (offspringAI != null)
        {
            offspringAI.ApplyBirthProtection();
        }
    }

    void FinishBreeding()
    {
        breedTimer = 0f;
        targetMate = null;
        targetPrey = null;
        targetCorpse = null;
        PickWanderTarget();
    }

    public bool IsBirthProtected
    {
        get { return birthProtectionTimer > 0f; }
    }

    public void ApplyBirthProtection()
    {
        birthProtectionTimer = birthProtectionDuration;
        targetRival = null;
        targetMate = null;
        PickWanderTarget();
    }

    void UpdateBirthProtection()
    {
        if (birthProtectionTimer <= 0f)
        {
            return;
        }

        birthProtectionTimer -= Time.deltaTime;
        birthProtectionTimer = Mathf.Max(0f, birthProtectionTimer);
    }

    void PickRivalRetreatTarget()
    {
        rivalRetreatTimer = 0f;

        if (targetRival == null)
        {
            rivalRetreatTarget = transform.position;
            return;
        }

        Vector3 awayDirection = transform.position - targetRival.transform.position;
        awayDirection.y = 0f;

        if (awayDirection.sqrMagnitude < 0.01f)
        {
            awayDirection = -transform.forward;
            awayDirection.y = 0f;
        }

        awayDirection.Normalize();

        float side = Random.value < 0.5f ? -1f : 1f;
        Vector3 retreatDirection = Quaternion.Euler(0f, 35f * side, 0f) * awayDirection;

        for (int i = 0; i < 8; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * 1.5f;
            Vector3 candidate = transform.position + retreatDirection * rivalRetreatDistance + new Vector3(randomOffset.x, 0f, randomOffset.y);

            if (!TryGetNavMeshPoint(candidate, out Vector3 navMeshTarget))
            {
                continue;
            }

            if (!CanReach(navMeshTarget))
            {
                continue;
            }

            rivalRetreatTarget = navMeshTarget;
            return;
        }

        rivalRetreatTarget = transform.position;
    }

    void RegenerateHealthWhileWandering()
    {
        if (state != CarnivoreState.Wander || health >= maxHealth)
        {
            return;
        }

        health += healthRegenRate * Time.deltaTime;
        health = Mathf.Clamp(health, 0f, maxHealth);
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

    void StopMoving()
    {
        if (agent != null && agent.isOnNavMesh)
        {
            agent.ResetPath();
        }
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
        if (isDead)
        {
            return;
        }

        isDead = true;

        if (deadBodyPrefab != null)
        {
            Instantiate(deadBodyPrefab, transform.position, transform.rotation, transform.parent);
        }

        Destroy(gameObject);
    }

    public void TakeDamage(float damage)
    {
        if (isDead)
        {
            return;
        }

        health -= damage;

        if (health <= 0f)
        {
            Die();
        }
    }
}
