using UnityEngine;
using UnityEngine.AI;

public class NavMeshCreatureMotor : MonoBehaviour
{
    NavMeshAgent agent;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    public void MoveTo(Vector3 target, float fallbackSpeed)
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

        transform.position += direction.normalized * fallbackSpeed * Time.deltaTime;
        transform.forward = direction.normalized;
    }

    public void Stop()
    {
        if (agent != null && agent.isOnNavMesh)
        {
            agent.ResetPath();
        }
    }

    public bool TryGetNavMeshPoint(Vector3 point, out Vector3 navMeshPoint, float sampleRadius = 2f)
    {
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
        {
            navMeshPoint = hit.position;
            return true;
        }

        navMeshPoint = point;
        return false;
    }

    public bool CanReach(Vector3 target)
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
}
