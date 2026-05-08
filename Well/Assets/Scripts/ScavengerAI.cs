using UnityEngine;

public enum ScavengerState
{
    Wander,
    FindCorpse,
    MoveToCorpse,
    CarryCorpseToStorage,
    DeliverCorpse
}

public class ScavengerAI : MonoBehaviour
{
    [Header("Carry")]
    public Transform carryAnchor;
    public Vector3 carryOffset = new Vector3(0f, 0.7f, 0.6f);

    [Header("Movement")]
    public float speed = 2f;
    public float searchRadius = 20f;
    public float interactDistance = 1.2f;
    public float wanderRadius = 5f;

    [Header("Search Timing")]
    public float corpseSearchInterval = 0.5f;
    public float storageSearchInterval = 1f;

    public float carriedFoodValue;

    ScavengerState state;
    NaturalResources targetCorpse;
    GameObject carriedCorpseObject;
    ScavengerStorage storage;
    NavMeshCreatureMotor motor;
    Vector3 wanderTarget;
    float corpseSearchTimer;
    float storageSearchTimer;

    // Register this scavenger with the ecosystem manager
    void OnEnable()
    {
        EcosystemManager.Instance.Register(this);
    }

    // Unregister this scavenger from the ecosystem manager
    void OnDisable()
    {
        if (EcosystemManager.HasInstance)
        {
            EcosystemManager.Instance.Unregister(this);
        }
    }

    // Initialize movement, storage target, and first wander point
    void Start()
    {
        motor = GetComponent<NavMeshCreatureMotor>();

        if (motor == null)
        {
            motor = gameObject.AddComponent<NavMeshCreatureMotor>();
        }

        corpseSearchTimer = corpseSearchInterval;
        storageSearchTimer = storageSearchInterval;
        storage = FindNearestStorage();
        PickWanderTarget();
    }

    // Run the current scavenger state
    void Update()
    {
        switch (state)
        {
            case ScavengerState.Wander:
                UpdateWander();
                break;

            case ScavengerState.FindCorpse:
                UpdateFindCorpse();
                break;

            case ScavengerState.MoveToCorpse:
                UpdateMoveToCorpse();
                break;

            case ScavengerState.CarryCorpseToStorage:
                UpdateCarryCorpseToStorage();
                break;

            case ScavengerState.DeliverCorpse:
                UpdateDeliverCorpse();
                break;
        }
    }

    // Wander and periodically search for corpses
    void UpdateWander()
    {
        MoveTo(wanderTarget);

        if (Vector3.Distance(transform.position, wanderTarget) < 0.5f)
        {
            state = ScavengerState.FindCorpse;
        }
    }

    // Find a corpse target or return to wandering
    void UpdateFindCorpse()
    {
        if (!IsSearchReady(ref corpseSearchTimer, corpseSearchInterval))
        {
            return;
        }

        targetCorpse = FindNearestCorpse();

        if (targetCorpse == null)
        {
            PickWanderTarget();
            return;
        }

        state = ScavengerState.MoveToCorpse;
    }

    // Move to the selected corpse and pick it up
    void UpdateMoveToCorpse()
    {
        if (targetCorpse == null || !targetCorpse.IsAvailable)
        {
            state = ScavengerState.FindCorpse;
            return;
        }

        MoveTo(targetCorpse.transform.position);

        if (Vector3.Distance(transform.position, targetCorpse.transform.position) <= interactDistance)
        {
            PickUpCorpse(targetCorpse);
            targetCorpse = null;
            state = ScavengerState.CarryCorpseToStorage;
        }
    }

    // Carry the corpse toward storage
    void UpdateCarryCorpseToStorage()
    {
        if (storage == null)
        {
            if (!IsSearchReady(ref storageSearchTimer, storageSearchInterval))
            {
                return;
            }

            storage = FindNearestStorage();
            PickWanderTarget();
            return;
        }

        MoveTo(storage.transform.position);

        if (Vector3.Distance(transform.position, storage.transform.position) <= interactDistance)
        {
            state = ScavengerState.DeliverCorpse;
        }
    }

    // Deliver carried corpse value to storage
    void UpdateDeliverCorpse()
    {
        if (storage != null)
        {
            storage.ReceiveCorpse(carriedFoodValue);
        }

        if (carriedCorpseObject != null)
        {
            Destroy(carriedCorpseObject);
        }

        carriedCorpseObject = null;
        carriedFoodValue = 0f;
        state = ScavengerState.FindCorpse;
    }

    // Find the nearest available corpse resource
    NaturalResources FindNearestCorpse()
    {
        NaturalResources nearest = null;
        float nearestDistance = searchRadius;

        foreach (NaturalResources resource in EcosystemManager.Instance.resources)
        {
            if (resource.resourceType != ResourceType.DeadBody || !resource.IsAvailable)
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

    // Find the nearest scavenger storage
    ScavengerStorage FindNearestStorage()
    {
        ScavengerStorage nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (ScavengerStorage storageCandidate in EcosystemManager.Instance.scavengerStorages)
        {
            if (storageCandidate == null)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, storageCandidate.transform.position);

            if (distance < nearestDistance)
            {
                nearest = storageCandidate;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    // Move toward a target through the shared motor
    void MoveTo(Vector3 target)
    {
        if (motor != null)
        {
            motor.MoveTo(target, speed);
        }
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

    // Attach a corpse to this scavenger for visible carrying
    void PickUpCorpse(NaturalResources corpse)
    {
        if (corpse == null)
        {
            return;
        }

        carriedFoodValue = corpse.amount;
        corpse.amount = 0f;
        corpse.enabled = false;

        carriedCorpseObject = corpse.gameObject;

        foreach (Collider corpseCollider in carriedCorpseObject.GetComponentsInChildren<Collider>())
        {
            corpseCollider.enabled = false;
        }

        Transform parent = carryAnchor != null ? carryAnchor : transform;
        carriedCorpseObject.transform.SetParent(parent);
        carriedCorpseObject.transform.localPosition = carryOffset;
        carriedCorpseObject.transform.localRotation = Quaternion.identity;
    }

    // Choose a random reachable wander target
    void PickWanderTarget()
    {
        Vector2 random = Random.insideUnitCircle * wanderRadius;
        Vector3 randomTarget = transform.position + new Vector3(random.x, 0f, random.y);

        if (motor != null && motor.TryGetNavMeshPoint(randomTarget, out Vector3 navMeshTarget))
        {
            wanderTarget = navMeshTarget;
        }
        else
        {
            wanderTarget = transform.position;
        }

        state = ScavengerState.Wander;
    }
}
