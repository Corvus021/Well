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
    public ResourceType resourceType = ResourceType.Plant;//

    public float amount = 10f;
    public float maxAmount = 10f;
    public float regrowDelay = 5f;
    public float regrowRate = 1f;

    [Header("Plant Growth Bonus")]
    public float corpseBoostRadius = 5f;
    public float corpseRegrowMultiplier = 2f;

    float emptyTime;

    public bool IsAvailable
    {
        get { return amount > 0f; }
    }
    // Update is called once per frame
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
    public float Consume(float value)
    {
        float consumed = Mathf.Min(amount, value);
        amount -= consumed;
        return consumed;
    }
    void UpdateVisual()
    {
        float scale = Mathf.Lerp(0.2f, 1f, amount / maxAmount);
        transform.localScale = Vector3.one * scale;
    }

    float GetCurrentRegrowRate()
    {
        if (resourceType != ResourceType.Plant)
        {
            return regrowRate;
        }

        NaturalResources[] resources = FindObjectsByType<NaturalResources>(FindObjectsSortMode.None);

        foreach (NaturalResources resource in resources)
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
