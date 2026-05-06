using System.Collections.Generic;
using UnityEngine;

public class EcosystemManager : MonoBehaviour
{
    static EcosystemManager instance;

    public static EcosystemManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<EcosystemManager>();
            }

            if (instance == null)
            {
                GameObject managerObject = new GameObject("EcosystemManager");
                instance = managerObject.AddComponent<EcosystemManager>();
            }

            return instance;
        }
    }

    public static bool HasInstance
    {
        get { return instance != null; }
    }

    public readonly List<CarnivoreAI> carnivores = new List<CarnivoreAI>();
    public readonly List<CreatureAI> herbivores = new List<CreatureAI>();
    public readonly List<ScavengerAI> scavengers = new List<ScavengerAI>();
    public readonly List<ScavengerStorage> scavengerStorages = new List<ScavengerStorage>();
    public readonly List<NaturalResources> resources = new List<NaturalResources>();

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    public void Register(CarnivoreAI carnivore)
    {
        RegisterUnique(carnivores, carnivore);
    }

    public void Unregister(CarnivoreAI carnivore)
    {
        carnivores.Remove(carnivore);
    }

    public void Register(CreatureAI herbivore)
    {
        RegisterUnique(herbivores, herbivore);
    }

    public void Unregister(CreatureAI herbivore)
    {
        herbivores.Remove(herbivore);
    }

    public void Register(ScavengerAI scavenger)
    {
        RegisterUnique(scavengers, scavenger);
    }

    public void Unregister(ScavengerAI scavenger)
    {
        scavengers.Remove(scavenger);
    }

    public void Register(ScavengerStorage storage)
    {
        RegisterUnique(scavengerStorages, storage);
    }

    public void Unregister(ScavengerStorage storage)
    {
        scavengerStorages.Remove(storage);
    }

    public void Register(NaturalResources resource)
    {
        RegisterUnique(resources, resource);
    }

    public void Unregister(NaturalResources resource)
    {
        resources.Remove(resource);
    }

    void RegisterUnique<T>(List<T> list, T item) where T : Object
    {
        if (item != null && !list.Contains(item))
        {
            list.Add(item);
        }
    }
}
