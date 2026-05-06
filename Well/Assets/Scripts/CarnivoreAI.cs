using System.Collections.Generic;
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

public enum CarnivoreGender
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

    [Header("Search Timing")]
    public float targetSearchInterval = 0.4f;
    public float rivalSearchInterval = 0.5f;
    public float breedingSearchInterval = 1f;
    public float genderBalanceInterval = 2f;

    [Header("Gender And Rivalry")]
    public CarnivoreGender gender = CarnivoreGender.Female;
    public bool randomizeGenderOnStart = true;
    public float rivalSearchRadius = 10f;
    public float rivalFightDistance = 1.4f;
    public float rivalAttackDamage = 15f;
    public float rivalAttackCooldown = 1.5f;
    public float rivalRetreatDistance = 3f;
    public float rivalRetreatDuration = 1f;

    [Header("Separation")]
    public float separationRadius = 6f;
    public float separationWeight = 4f;
    public float birthScatterRadius = 8f;

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
    NavMeshCreatureMotor motor;
    Vector3 wanderTarget;
    Vector3 rivalRetreatTarget;
    float attackTimer;
    float rivalAttackTimer;
    float rivalRetreatTimer;
    float breedTimer;
    float birthProtectionTimer;
    float targetSearchTimer;
    float rivalSearchTimer;
    float breedingSearchTimer;
    float genderBalanceTimer;
    bool isDead;

    void OnEnable()
    {
        EcosystemManager.Instance.Register(this);
    }

    void OnDisable()
    {
        if (EcosystemManager.HasInstance)
        {
            EcosystemManager.Instance.Unregister(this);
        }
    }

    void Start()
    {
        motor = GetComponent<NavMeshCreatureMotor>();

        if (motor == null)
        {
            motor = gameObject.AddComponent<NavMeshCreatureMotor>();
        }

        if (randomizeGenderOnStart)
        {
            gender = Random.value < 0.5f ? CarnivoreGender.Male : CarnivoreGender.Female;
        }

        if (randomizeHealthOnStart)
        {
            maxHealth = Random.Range(minRandomHealth, maxRandomHealth);
            health = maxHealth;
        }

        health = Mathf.Clamp(health, 0f, maxHealth);
        targetSearchTimer = targetSearchInterval;
        rivalSearchTimer = rivalSearchInterval;
        breedingSearchTimer = breedingSearchInterval;
        genderBalanceTimer = genderBalanceInterval;
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

        MaintainBreedingGenderBalance();

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

        UpdateCurrentState();
    }

    void UpdateCurrentState()
    {
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
        if (!IsSearchReady(ref targetSearchTimer, targetSearchInterval))
        {
            return;
        }

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
        if (IsBirthProtected)
        {
            return;
        }

        if (gender != CarnivoreGender.Male)
        {
            return;
        }

        if (state == CarnivoreState.ChaseRival || state == CarnivoreState.FightRival ||
            state == CarnivoreState.RetreatFromRival)
        {
            return;
        }

        if (!IsSearchReady(ref rivalSearchTimer, rivalSearchInterval))
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

        if (!IsSearchReady(ref breedingSearchTimer, breedingSearchInterval))
        {
            return;
        }

        List<CarnivoreAI> carnivores = EcosystemManager.Instance.carnivores;

        if (carnivores.Count >= targetCarnivorePopulation)
        {
            return;
        }

        EnsureBothGendersExist(carnivores);

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

    void MaintainBreedingGenderBalance()
    {
        if (!IsSearchReady(ref genderBalanceTimer, genderBalanceInterval))
        {
            return;
        }

        List<CarnivoreAI> carnivores = EcosystemManager.Instance.carnivores;

        if (carnivores.Count <= 0 || carnivores.Count >= targetCarnivorePopulation)
        {
            return;
        }

        EnsureBothGendersExist(carnivores);
    }

    void UpdateFindMate()
    {
        if (!IsSearchReady(ref breedingSearchTimer, breedingSearchInterval))
        {
            return;
        }

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

        if (gender == CarnivoreGender.Female)
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

        if (gender != CarnivoreGender.Female)
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
        CreatureAI nearest = null;
        float bestScore = searchRadius;

        foreach (CreatureAI creature in EcosystemManager.Instance.herbivores)
        {
            if (creature.foodType != ResourceType.Plant)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, creature.transform.position);

            if (distance > searchRadius)
            {
                continue;
            }

            if (!CanReach(creature.transform.position))
            {
                continue;
            }

            float score = distance + GetCarnivoreCrowdingPenalty(creature.transform.position);

            if (score < bestScore)
            {
                nearest = creature;
                bestScore = score;
            }
        }

        return nearest;
    }

    NaturalResources FindNearestCorpse()
    {
        NaturalResources nearest = null;
        float bestScore = searchRadius;

        foreach (NaturalResources resource in EcosystemManager.Instance.resources)
        {
            if (resource.resourceType != ResourceType.DeadBody || !resource.IsAvailable)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, resource.transform.position);

            if (distance > searchRadius)
            {
                continue;
            }

            if (!CanReach(resource.transform.position))
            {
                continue;
            }

            float score = distance + GetCarnivoreCrowdingPenalty(resource.transform.position);

            if (score < bestScore)
            {
                nearest = resource;
                bestScore = score;
            }
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
        CarnivoreAI nearest = null;
        float nearestDistance = rivalSearchRadius;

        foreach (CarnivoreAI carnivore in EcosystemManager.Instance.carnivores)
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
        CarnivoreAI nearest = null;
        float nearestDistance = mateSearchRadius;

        foreach (CarnivoreAI carnivore in EcosystemManager.Instance.carnivores)
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

        return !IsBirthProtected && gender == CarnivoreGender.Male && carnivore.gender == CarnivoreGender.Male &&
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

        if (gender == carnivore.gender)
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

    void EnsureBothGendersExist(List<CarnivoreAI> carnivores)
    {
        bool hasMale = false;
        bool hasFemale = false;

        foreach (CarnivoreAI carnivore in carnivores)
        {
            if (carnivore == null || carnivore.isDead)
            {
                continue;
            }

            if (carnivore.gender == CarnivoreGender.Male)
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

        chosen.gender = hasMale ? CarnivoreGender.Female : CarnivoreGender.Male;
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

        if (EcosystemManager.Instance.carnivores.Count >= targetCarnivorePopulation)
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
            offspringAI.ScatterFromBirth(transform.position, mate.transform.position);
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

    public void ScatterFromBirth(Vector3 parentA, Vector3 parentB)
    {
        Vector3 parentCenter = (parentA + parentB) * 0.5f;
        Vector3 awayDirection = transform.position - parentCenter;
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
            Vector3 candidate = transform.position + awayDirection * birthScatterRadius + new Vector3(randomOffset.x, 0f, randomOffset.y);

            if (!TryGetNavMeshPoint(candidate, out Vector3 navMeshTarget))
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

    float GetCarnivoreCrowdingPenalty(Vector3 targetPosition)
    {
        int nearbyCount = 0;

        foreach (CarnivoreAI carnivore in EcosystemManager.Instance.carnivores)
        {
            if (carnivore == null || carnivore == this || carnivore.isDead)
            {
                continue;
            }

            if (Vector3.Distance(targetPosition, carnivore.transform.position) <= separationRadius)
            {
                nearbyCount++;
            }
        }

        return nearbyCount * separationWeight;
    }

    void MoveTo(Vector3 target)
    {
        if (motor != null)
        {
            motor.MoveTo(target, speed);
        }
    }

    void StopMoving()
    {
        if (motor != null)
        {
            motor.Stop();
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
        if (motor == null)
        {
            navMeshPoint = point;
            return false;
        }

        return motor.TryGetNavMeshPoint(point, out navMeshPoint);
    }

    bool CanReach(Vector3 target)
    {
        return motor == null || motor.CanReach(target);
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

