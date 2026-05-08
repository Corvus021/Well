using UnityEngine;

public enum ResourceType
{
    Plant,
    MeatPlant,
    Water,
    DeadBody
}
public class NaturalResources : MonoBehaviour
{
    public ResourceType resourceType = ResourceType.Plant;

    public float amount = 10f;
    public float maxAmount = 10f;
    public float regrowDelay = 5f;
    public float regrowRate = 1f;

    [Header("Plant Growth Bonus")]
    public float corpseBoostRadius = 5f;
    public float corpseRegrowMultiplier = 2f;

    float emptyTime;

    // Register this resource with the ecosystem manager
    void OnEnable()
    {
        EcosystemManager.Instance.Register(this);
    }

    // Unregister this resource from the ecosystem manager
    void OnDisable()
    {
        if (EcosystemManager.HasInstance)
        {
            EcosystemManager.Instance.Unregister(this);
        }
    }

    public bool IsAvailable
    {
        get { return amount > 0f; }
    }
    // Regrow plant resources over time
    void Update()
    {
        if (resourceType != ResourceType.Plant)
        {
            return;
        }
        if (amount <= 0f)
        {
            emptyTime += Time.deltaTime;
            if (emptyTime >= regrowDelay)
            {
                amount += GetCurrentRegrowRate() * Time.deltaTime;
            }
        }
        else
        {
            emptyTime = 0f;
            amount += GetCurrentRegrowRate() * Time.deltaTime;
        }
        amount = Mathf.Clamp(amount, 0f, maxAmount);
        UpdateVisual();
    }
    // Consume resource amount and return the amount taken
    public float Consume(float value)
    {
        float consumed = Mathf.Min(amount, value);
        amount -= consumed;
        return consumed;
    }
    // Scale the object based on remaining resource amount
    void UpdateVisual()
    {
        float scale = Mathf.Lerp(0.2f, 1f, amount / maxAmount);
        transform.localScale = Vector3.one * scale;
    }

    // Calculate regrowth speed, boosted by nearby corpses
    float GetCurrentRegrowRate()
    {
        if (resourceType != ResourceType.Plant)
        {
            return regrowRate;
        }

        foreach (NaturalResources resource in EcosystemManager.Instance.resources)
        {
            if (resource == this || resource.resourceType != ResourceType.DeadBody || !resource.IsAvailable)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, resource.transform.position);

            if (distance <= corpseBoostRadius)
            {
                return regrowRate * corpseRegrowMultiplier;
            }
        }

        return regrowRate;
    }
}
