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

    // Initialize or enforce the ecosystem manager singleton
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    // Add a carnivore to the shared list
    public void Register(CarnivoreAI carnivore)
    {
        RegisterUnique(carnivores, carnivore);
    }

    // Remove a carnivore from the shared list
    public void Unregister(CarnivoreAI carnivore)
    {
        carnivores.Remove(carnivore);
    }

    // Add a herbivore to the shared list
    public void Register(CreatureAI herbivore)
    {
        RegisterUnique(herbivores, herbivore);
    }

    // Remove a herbivore from the shared list
    public void Unregister(CreatureAI herbivore)
    {
        herbivores.Remove(herbivore);
    }

    // Add a scavenger to the shared list
    public void Register(ScavengerAI scavenger)
    {
        RegisterUnique(scavengers, scavenger);
    }

    // Remove a scavenger from the shared list
    public void Unregister(ScavengerAI scavenger)
    {
        scavengers.Remove(scavenger);
    }

    // Add a scavenger storage to the shared list
    public void Register(ScavengerStorage storage)
    {
        RegisterUnique(scavengerStorages, storage);
    }

    // Remove a scavenger storage from the shared list
    public void Unregister(ScavengerStorage storage)
    {
        scavengerStorages.Remove(storage);
    }

    // Add a resource to the shared list
    public void Register(NaturalResources resource)
    {
        RegisterUnique(resources, resource);
    }

    // Remove a resource from the shared list
    public void Unregister(NaturalResources resource)
    {
        resources.Remove(resource);
    }

    // Add an item only if it is valid and not already registered
    void RegisterUnique<T>(List<T> list, T item) where T : Object
    {
        if (item != null && !list.Contains(item))
        {
            list.Add(item);
        }
    }
}
