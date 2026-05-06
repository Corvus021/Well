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

        corpseSearchTimer = corpseSearchInterval;
        storageSearchTimer = storageSearchInterval;
        storage = FindNearestStorage();
        PickWanderTarget();
    }

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

    void UpdateWander()
    {
        MoveTo(wanderTarget);

        if (Vector3.Distance(transform.position, wanderTarget) < 0.5f)
        {
            state = ScavengerState.FindCorpse;
        }
    }

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

    void MoveTo(Vector3 target)
    {
        if (motor != null)
        {
            motor.MoveTo(target, speed);
        }
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
