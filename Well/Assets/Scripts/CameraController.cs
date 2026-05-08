using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    enum CameraMode
    {
        TopDown,
        CreatureView
    }

    [Header("References")]
    [SerializeField] Transform boundsRoot;

    [Header("Top Down")]
    [SerializeField] float topDownHeight = 40f;
    [SerializeField] float moveSpeed = 20f;
    [SerializeField] float zoomSpeed = 8f;
    [SerializeField] float minZoom = 8f;
    [SerializeField] float maxZoom = 45f;

    [Header("Bounds")]
    [SerializeField] float boundsPadding = 2f;//Bounds padding

    [Header("Creature View")]
    [SerializeField] KeyCode changeCameraKey = KeyCode.R;
    [SerializeField] float creatureViewHeight = 1.4f;
    [SerializeField] float creatureFollowDistance = 3f;
    [SerializeField] float creatureFollowSmooth = 8f;
    [SerializeField] float creatureFov = 65f;

    Camera controlledCamera;
    CameraMode mode = CameraMode.TopDown;

    Bounds cameraBounds;//Merge boundaries
    bool hasBounds;

    Transform creatureTarget;
    float savedOrthographicSize;

    // Cache the camera component
    void Awake()
    {
        //get camera
        controlledCamera = GetComponent<Camera>();
    }

    // Initialize bounds and enter top-down view
    void Start()
    {
        //camera move bounds
        RefreshBounds();
        //set topdown camera
        EnterTopDownMode();
    }

    // Handle camera mode switching and per-mode updates
    void Update()
    {
        if (Input.GetKeyDown(changeCameraKey))
        {
            //change camera mode
            ToggleCameraMode();
        }

        if (mode == CameraMode.TopDown)
        {
            //topdown camera mode
            UpdateTopDownCamera();
        }
        else
        {
            //creature camera mode
            UpdateCreatureCamera();
        }
    }

    // Switch between top-down view and creature follow view
    void ToggleCameraMode()
    {
        if (mode == CameraMode.TopDown)
        {
            Transform target = PickRandomCreature();

            if (target != null)
            {
                EnterCreatureMode(target);
            }
        }
        else
        {
            EnterTopDownMode();
        }
    }

    // Restore orthographic top-down camera settings
    void EnterTopDownMode()
    {
        mode = CameraMode.TopDown;
        creatureTarget = null;

        controlledCamera.orthographic = true;
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        Vector3 position = transform.position;
        position.y = topDownHeight;
        transform.position = position;

        if (savedOrthographicSize > 0f)
        {
            controlledCamera.orthographicSize = savedOrthographicSize;
        }

        //keep the camera inside the bounds
        ClampTopDownPosition();
    }

    // Switch to perspective view following a creature
    void EnterCreatureMode(Transform target)
    {
        mode = CameraMode.CreatureView;
        creatureTarget = target;

        savedOrthographicSize = controlledCamera.orthographicSize;
        controlledCamera.orthographic = false;
        controlledCamera.fieldOfView = creatureFov;
    }

    // Move and zoom the top-down camera
    void UpdateTopDownCamera()
    {
        Vector3 input = Vector3.zero;

        if (Input.GetKey(KeyCode.W))
        {
            input.z += 1f;
        }

        if (Input.GetKey(KeyCode.S))
        {
            input.z -= 1f;
        }

        if (Input.GetKey(KeyCode.D))
        {
            input.x += 1f;
        }

        if (Input.GetKey(KeyCode.A))
        {
            input.x -= 1f;
        }

        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        transform.position += input * moveSpeed * Time.deltaTime;

        float scroll = Input.mouseScrollDelta.y;
        controlledCamera.orthographicSize -= scroll * zoomSpeed;
        controlledCamera.orthographicSize = Mathf.Clamp(controlledCamera.orthographicSize, minZoom, maxZoom);

        ClampTopDownPosition();
    }

    // Follow the selected creature from behind
    void UpdateCreatureCamera()
    {
        if (creatureTarget == null)
        {
            EnterTopDownMode();
            return;
        }

        Vector3 forward = creatureTarget.forward;

        if (forward.sqrMagnitude < 0.01f)
        {
            forward = Vector3.forward;
        }

        Vector3 targetPosition =
            creatureTarget.position
            + Vector3.up * creatureViewHeight
            - forward.normalized * creatureFollowDistance;

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            creatureFollowSmooth * Time.deltaTime
        );

        Vector3 lookPoint = creatureTarget.position + Vector3.up * creatureViewHeight;
        transform.rotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
    }

    // Pick a random living creature from the ecosystem
    Transform PickRandomCreature()
    {
        if (!EcosystemManager.HasInstance)
        {
            return null;
        }

        List<Transform> candidates = new List<Transform>();

        foreach (CreatureAI herbivore in EcosystemManager.Instance.herbivores)
        {
            if (herbivore != null)
            {
                candidates.Add(herbivore.transform);
            }
        }

        foreach (CarnivoreAI carnivore in EcosystemManager.Instance.carnivores)
        {
            if (carnivore != null)
            {
                candidates.Add(carnivore.transform);
            }
        }

        foreach (ScavengerAI scavenger in EcosystemManager.Instance.scavengers)
        {
            if (scavenger != null)
            {
                candidates.Add(scavenger.transform);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    // Rebuild the camera movement bounds from the bounds root
    void RefreshBounds()
    {
        hasBounds = false;

        if (boundsRoot != null)
        {
            Collider[] colliders = boundsRoot.GetComponentsInChildren<Collider>();

            foreach (Collider boundsCollider in colliders)
            {
                //try merge bounds
                TryAddBoundsCollider(boundsCollider);
            }
        }

        if (hasBounds)
        {
            //add padding
            cameraBounds.Expand(boundsPadding);
        }
    }

    // Add one collider to the combined camera bounds
    void TryAddBoundsCollider(Collider boundsCollider)
    {
        if (boundsCollider == null)
        {
            return;
        }

        if (!hasBounds)
        {
            cameraBounds = boundsCollider.bounds;
            hasBounds = true;
        }
        else
        {
            cameraBounds.Encapsulate(boundsCollider.bounds);//cameraBounds.Encapsulate(newBounds)
        }
    }

    // Keep the top-down camera inside the allowed bounds
    void ClampTopDownPosition()
    {
        if (!hasBounds)
        {
            RefreshBounds();
        }

        if (!hasBounds)
        {
            return;
        }

        Vector3 position = transform.position;

        position.x = Mathf.Clamp(position.x, cameraBounds.min.x, cameraBounds.max.x);
        position.z = Mathf.Clamp(position.z, cameraBounds.min.z, cameraBounds.max.z);
        position.y = topDownHeight;

        transform.position = position;
    }
}
