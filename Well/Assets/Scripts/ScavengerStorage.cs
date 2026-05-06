using UnityEngine;

public class ScavengerStorage : MonoBehaviour
{
    public float hunger = 0f;
    public float maxHunger = 100f;
    public float hungerRate = 2f;

    public float storedFood = 0f;
    public float foodConsumeRate = 5f;

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

    void Update()
    {
        hunger += hungerRate * Time.deltaTime;
        hunger = Mathf.Clamp(hunger, 0f, maxHunger);

        if (storedFood > 0f && hunger > 0f)
        {
            float consumed = Mathf.Min(storedFood, foodConsumeRate * Time.deltaTime);
            storedFood -= consumed;
            hunger -= consumed;
            hunger = Mathf.Clamp(hunger, 0f, maxHunger);
        }
    }

    public void ReceiveCorpse(float foodValue)
    {
        storedFood += foodValue;
    }
}
