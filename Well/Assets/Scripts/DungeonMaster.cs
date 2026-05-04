using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using UnityEngine.AI;

public enum DungeonGenerator
{
    inactive,
    generatingMain,
    generatingBranches,
    cleanup,
    completed
}
public class DungeonMaster : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField] NavMeshSurface navMeshSurface;

    [Header("Ecosystem")]
    [SerializeField] GameObject plantPrefab;
    [SerializeField] GameObject herbivorePrefab;
    [SerializeField] GameObject scavengerPrefab;

    [Header("Dungeon")]
    [SerializeField] GameObject[] startRoom;
    [SerializeField] GameObject[] randomRoom;
    [SerializeField] GameObject[] hallRoom;
    [SerializeField] GameObject[] beginnerRoom;
    [SerializeField] GameObject[] intermediateRoom;
    [SerializeField] GameObject[] advancedRoom;
    //[SerializeField] GameObject[] restRoom;
    //[SerializeField] GameObject[] npcRoom;
    [SerializeField] GameObject[] endRoom;
    [SerializeField] GameObject[] blockedWall;
    [SerializeField] GameObject[] Door;

    [Header("Debugging Options")]
    [SerializeField] bool useColliders;
    [SerializeField] bool useLights;
    [SerializeField] bool restoreLights;

    [Header("Key Options")]
    public KeyCode reloadKey = KeyCode.F;
    public KeyCode changeMapKey = KeyCode.R;

    [Header("Ceneration Limits")]
    [Range(2,100)] [SerializeField] int mainLength = 10;
    [Range(0, 50)] [SerializeField] int branchLength = 5;
    [Range(0, 100)] [SerializeField] int branchNumber = 10;
    [Range(0, 100)] [SerializeField] int doorPercent = 25;
    [Range(0,1f)] [SerializeField] float buildTime;

    [Header("Room List")]
    public DungeonGenerator dungeonGenerator = DungeonGenerator.inactive;
    [System.NonSerialized]
    public List<Tile> generatedTiles = new List<Tile>();
    [System.NonSerialized]
    public List<DungeonNode> dungeonNodes = new List<DungeonNode>();
    int nextNodeID;

    List<Contact> availableContacts = new List<Contact>();
    Color defaultLightColor = Color.white;
    Transform tileFrom, tileTo, tileRoot;
    Transform path;
    int attempts;
    int maxAttempts = 50;

    void Start()
    {
        StartCoroutine(DungeonBuild());
    }
    void Update()
    {
        if (Input.GetKeyDown(reloadKey))
        {
            SceneManager.LoadScene("Map");
        }
    }
    IEnumerator DungeonBuild()
    {
        List<DungeonNode> mainPathGraph = BuildMainPathGraph();

        foreach (DungeonNode node in mainPathGraph)
        {
            Debug.Log("Node " + node.id + " " + node.type + " depth:" + node.depth);
        }
        GameObject createPath = new GameObject("Main Path");
        path = createPath.transform;
        path.SetParent(transform);
        tileRoot = CreateRoomForNode(mainPathGraph[0]);
        BindNodeToRoom(mainPathGraph[0], tileRoot);
        DebugRoomLightingForNode(tileRoot, mainPathGraph[0]);
        tileTo = tileRoot;
        dungeonGenerator = DungeonGenerator.generatingMain;
        while (generatedTiles.Count < mainPathGraph.Count)
        {
            yield return new WaitForSeconds(buildTime);

            tileFrom = tileTo;

            DungeonNode nextNode = mainPathGraph[generatedTiles.Count];
            tileTo = CreateRoomForNode(nextNode);
            BindNodeToRoom(nextNode, tileTo);
            DebugRoomLightingForNode(tileTo, nextNode);

            ConnectTiles();
            CollisionCheck(nextNode);
        }
        //put all not connected contact(in the mainPath) in "mainPath"
        foreach (Contact contact in path.GetComponentsInChildren<Contact>())
        {
            if(!contact.isConnected)
            {
                if(!availableContacts.Contains(contact))
                {
                    availableContacts.Add(contact);
                }
            }
        }
        dungeonGenerator = DungeonGenerator.generatingBranches;
        //braching
        for (int x = 0; x < branchNumber; x++)
        {
            if(availableContacts.Count>0)
            {
                createPath = new GameObject("Branch" + (x + 1));
                path = createPath.transform;
                path.SetParent(transform);
                int availableIndex = Random.Range(0, availableContacts.Count);
                tileRoot = availableContacts[availableIndex].transform.parent.parent;//Find the root node of the tiles
                availableContacts.RemoveAt(availableIndex);//remove roottile
                tileTo = tileRoot;
                DungeonNode branchRootNode = FindNodeByRoom(tileRoot);
                DungeonNode previousBranchNode = branchRootNode;
                for (int i = 0; i < branchLength - 1; i++)
                {
                    yield return new WaitForSeconds(buildTime);

                    tileFrom = tileTo;

                    RoomType branchRoomType = PickBranchRoomType(i, branchLength - 1);
                    DungeonNode branchNode = CreateNode(branchRoomType, i + 1, false);
                    if (previousBranchNode != null)
                    {
                        ConnectNodes(previousBranchNode, branchNode);
                    }

                    previousBranchNode = branchNode;

                    tileTo = CreateRoomForNode(branchNode);
                    BindNodeToRoom(branchNode, tileTo);
                    DebugRoomLightingForNode(tileTo, branchNode);

                    ConnectTiles();
                    CollisionCheck(branchNode);

                    if (attempts >= maxAttempts)
                    {
                        break;
                    }
                }
            }
            else
            {
                break;
            }
        }
        dungeonGenerator = DungeonGenerator.cleanup;
        RestoreLight();
        yield return new WaitForSeconds(buildTime);

        RemoveUnboundNodesFromGraph();
        WarnIsolatedNodes();

        BlockedPassages();
        GenerateDoors();//There¡¯s a problem with the transform on doors :( it needs fixing

        BuildNavigationMesh();

        GenerateEcosystem();

        DeleteBoxes();
        foreach (DungeonNode node in dungeonNodes)
        {
            string connections = "";

            foreach (DungeonNode connectedNode in node.connections)
            {
                connections += connectedNode.id + " ";
            }

            Debug.Log("Graph Node " + node.id + " " + node.type + " -> " + connections);
        }
        dungeonGenerator = DungeonGenerator.completed;
        yield return null;
    }
    DungeonNode CreateNode(RoomType type,int depth,bool isMainPath)
    {
        DungeonNode node = new DungeonNode(nextNodeID, type, depth, isMainPath);
        nextNodeID++;
        dungeonNodes.Add(node);
        return node;
    }
    DungeonNode FindNodeByRoom(Transform room)
    {
        if (room == null)
        {
            return null;
        }

        Tile tile = generatedTiles.Find(includes => includes.tile == room);

        if (tile != null)
        {
            return tile.node;
        }

        return null;
    }
    void ConnectNodes(DungeonNode from,DungeonNode to)
    {
        if(!from.connections.Contains(to))
        {
            from.connections.Add(to);
        }
        if(!to.connections.Contains(from))
        {
            to.connections.Add(from);
        }
    }
    RoomType PickMainPathRoomType(int encounterIndex, int encounterCount)
    {
        float progress = encounterIndex / (float)Mathf.Max(1, encounterCount - 1);

        if (progress < 0.4f)
        {
            return RoomType.Beginner;
        }

        if (progress < 0.75f)
        {
            return RoomType.Intermediate;
        }

        return RoomType.Advanced;
    }
    List<DungeonNode> BuildMainPathGraph()
    {
        dungeonNodes.Clear();
        nextNodeID = 0;

        List<DungeonNode> mainPath = new List<DungeonNode>();

        DungeonNode start = CreateNode(RoomType.Start, 0, true);
        mainPath.Add(start);

        int encounterIndex = 0;
        int encounterCount = 0;

        for (int i = 1; i < mainLength - 1; i++)
        {
            if (i % 2 == 0)
            {
                encounterCount++;
            }
        }

        encounterCount = Mathf.Max(1, encounterCount);

        for (int i = 1; i < mainLength - 1; i++)
        {
            RoomType roomType;

            if (i % 2 == 1)
            {
                roomType = RoomType.Hall;
            }
            else
            {
                roomType = PickMainPathRoomType(encounterIndex, encounterCount);
                encounterIndex++;
            }

            DungeonNode room = CreateNode(roomType, i, true);

            ConnectNodes(mainPath[mainPath.Count - 1], room);
            mainPath.Add(room);
        }

        DungeonNode boss = CreateNode(RoomType.Boss, mainLength - 1, true);
        ConnectNodes(mainPath[mainPath.Count - 1], boss);
        mainPath.Add(boss);

        return mainPath;
    }
    RoomType PickBranchRoomType(int index, int length)
    {
        bool isLastRoom = index == length - 1;

        if (isLastRoom)
        {
            int roll = Random.Range(0, 100);

            if (roll < 50)
            {
                return RoomType.Rest;
            }

            return RoomType.NPC;
        }

        int hallwayRoll = Random.Range(0, 100);

        if (hallwayRoll < 40)
        {
            return RoomType.Hall;
        }

        return RoomType.Beginner;
    }
    Transform CreateRoomForNode(DungeonNode node)
    {
        switch (node.type)
        {
            case RoomType.Start:
                return CreateStartRoom();

            case RoomType.Hall:
                return CreateRoomFromList(hallRoom);

            case RoomType.Beginner:
                return CreateRoomFromList(beginnerRoom);

            case RoomType.Intermediate:
                return CreateRoomFromList(intermediateRoom);

            case RoomType.Advanced:
                return CreateRoomFromList(advancedRoom);

            case RoomType.Rest:
                return CreateRandomRoom();

            case RoomType.NPC:
                return CreateRandomRoom();

            case RoomType.Boss:
                return CreateEndRoom();

            default:
                return CreateRandomRoom();
        }
    }
    Transform CreateRoomFromList(GameObject[] roomList)
    {
        int index = Random.Range(0, roomList.Length);
        GameObject tile = Instantiate(roomList[index], transform.position, Quaternion.identity, path);
        tile.name = roomList[index].name;

        Transform origin = generatedTiles[generatedTiles.FindIndex(includes => includes.tile == tileFrom)].tile;
        generatedTiles.Add(new Tile(tile.transform, origin));

        return tile.transform;
    }
    void DebugRoomLightingForNode(Transform tile, DungeonNode node)
    {
        switch (node.type)
        {
            case RoomType.Start:
                DebugRoomLighting(tile, Color.purple);
                break;

            case RoomType.Hall:
                DebugRoomLighting(tile, Color.white);
                break;

            case RoomType.Beginner:
                DebugRoomLighting(tile, Color.yellow);
                break;

            case RoomType.Intermediate:
                DebugRoomLighting(tile, Color.cyan);
                break;

            case RoomType.Advanced:
                DebugRoomLighting(tile, Color.red);
                break;

            case RoomType.Boss:
                DebugRoomLighting(tile, Color.blue);
                break;

            default:
                DebugRoomLighting(tile, Color.white);
                break;
        }
    }
    void BindNodeToRoom(DungeonNode node, Transform room)
    {
        if (node != null)
        {
            node.roomTransform = room;
        }

        if (room != null)
        {
            int tileIndex = generatedTiles.FindIndex(includes => includes.tile == room);

            if (tileIndex >= 0)
            {
                generatedTiles[tileIndex].node = node;
            }
        }
    }
    void GenerateDoors()
    {
        if(doorPercent > 0)
        {
            Contact[] allContacts = transform.GetComponentsInChildren<Contact>();
            for(int i=0;i<allContacts.Length;i++)
            {
                Contact generatedContact = allContacts[i];
                if(generatedContact.isConnected)
                {
                    int roll = Random.Range(1, 101);
                    if (roll <= doorPercent)
                    {
                        Vector3 halfExtents = new Vector3(generatedContact.size.x, 1f, generatedContact.size.x);
                        Vector3 generatedContactPosition = generatedContact.transform.position;
                        Vector3 offset = Vector3.up * 0.5f;
                        Collider[] hits = Physics.OverlapBox(generatedContactPosition + offset, halfExtents, Quaternion.identity, LayerMask.GetMask("Door"));
                        if (hits.Length == 0)
                        {
                            int doorIndex = Random.Range(0, Door.Length);
                            GameObject generatedDoor = Instantiate(Door[doorIndex], generatedContactPosition, generatedContact.transform.rotation * Quaternion.Euler(90f, 0f, 0f), generatedContact.transform);
                            generatedDoor.name = Door[doorIndex].name;
                        }
                    }
                }
            }
        }
    }
    void BlockedPassages()
    {
        foreach(Contact contact in transform.GetComponentsInChildren<Contact>())
        {
            if (!contact.isConnected)
            {
                Vector3 unconnectedPosition = contact.transform.position;
                int wallIndex = Random.Range(0, blockedWall.Length);
                GameObject generatedWall = Instantiate(blockedWall[wallIndex], unconnectedPosition, contact.transform.rotation * Quaternion.Euler(-90f, 0f, 0f), contact.transform);
                generatedWall.name = blockedWall[wallIndex].name;
            }
        }
    }
    void CollisionCheck(DungeonNode node)
    {
        BoxCollider box = tileTo.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = tileTo.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
        }
        Vector3 offset = (tileTo.right * box.center.x) + (tileTo.up * box.center.y) + (tileTo.forward * box.center.z);
        Vector3 halfExtents = box.bounds.extents;
        List<Collider> hits = Physics.OverlapBox(tileTo.position + offset, halfExtents, Quaternion.identity, LayerMask.GetMask("Tile")).ToList();
        if(hits.Count>0)
        {
            if(hits.Exists(x=>x.transform!=tileFrom&&x.transform!=tileTo))
            {
                //his somethings not tileFrom or tileTo
                attempts++;
                int toIndex = generatedTiles.FindIndex(x => x.tile == tileTo);

                if (toIndex >= 0)
                {
                    Tile generatedTileTo = generatedTiles[toIndex];

                    if (generatedTileTo.contact != null)
                    {
                        generatedTileTo.contact.isConnected = false;
                    }

                    BindNodeToRoom(generatedTileTo.node, null);
                    generatedTiles.RemoveAt(toIndex);
                }

                DestroyImmediate(tileTo.gameObject);
                //backtracking
                if (attempts >= maxAttempts)
                {
                    int fromIndex = generatedTiles.FindIndex(x => x.tile == tileFrom);
                    Tile generatedTileFrom = generatedTiles[fromIndex];
                    if(tileFrom!=tileRoot)
                    {
                        if (generatedTileFrom.contact != null)
                        {
                            generatedTileFrom.contact.isConnected = false;
                        }
                        availableContacts.RemoveAll(x => x.transform.parent.parent == tileFrom);
                        BindNodeToRoom(generatedTileFrom.node, null);
                        generatedTiles.RemoveAt(fromIndex);
                        DestroyImmediate(tileFrom.gameObject);
                        if (generatedTileFrom.origin != tileRoot)
                        {
                            tileFrom = generatedTileFrom.origin;
                        }
                        else if (path.name.Contains("Main"))
                        {
                            if (generatedTileFrom.origin != null)
                            {
                                tileRoot = generatedTileFrom.origin;
                                tileFrom = tileRoot;
                            }
                        }
                        else if (availableContacts.Count > 0)
                        {
                            int availableIndex = Random.Range(0, availableContacts.Count);
                            tileRoot = availableContacts[availableIndex].transform.parent.parent;
                            availableContacts.RemoveAt(availableIndex);
                            tileFrom = tileRoot;
                        }
                        else
                        {
                            return;
                        }
                    }
                    else if(path.name.Contains("Main"))
                    {
                        if (generatedTileFrom.origin != null)
                        {
                            tileRoot = generatedTileFrom.origin;
                            tileFrom = tileRoot;
                        }
                    }
                    else if(availableContacts.Count>0)
                    {
                        int availableIndex = Random.Range(0, availableContacts.Count);
                        tileRoot = availableContacts[availableIndex].transform.parent.parent;
                        availableContacts.RemoveAt(availableIndex);
                        tileFrom = tileRoot;
                    }
                    else
                    {
                        return;
                    }
                }
                //retry
                if (tileFrom != null)
                {
                    tileTo = CreateRoomForNode(node);
                    BindNodeToRoom(node, tileTo);
                    DebugRoomLightingForNode(tileTo, node);

                    ConnectTiles();
                    CollisionCheck(node);
                }
            }
            else
            {
                attempts = 0;
            }
        }
    }
    void RemoveUnboundNodesFromGraph()
    {
        List<DungeonNode> unboundNodes = dungeonNodes.Where(node => node.roomTransform == null).ToList();

        if (unboundNodes.Count == 0)
        {
            Debug.Log("Dungeon graph cleanup: no unbound nodes.");
            return;
        }

        foreach (DungeonNode node in dungeonNodes)
        {
            node.connections.RemoveAll(connectedNode => unboundNodes.Contains(connectedNode));
        }

        foreach (DungeonNode unboundNode in unboundNodes)
        {
            dungeonNodes.Remove(unboundNode);
        }

        Debug.Log("Dungeon graph cleanup removed unbound nodes: " + unboundNodes.Count);
    }
    void WarnIsolatedNodes()
    {
        foreach (DungeonNode node in dungeonNodes)
        {
            if (node.connections.Count == 0)
            {
                Debug.LogWarning("Dungeon graph isolated node: " + node.id + " " + node.type);
            }
        }
    }
    void RestoreLight()
    {
        if(useLights && restoreLights && Application.isEditor)
        {
            Light[] lights = transform.GetComponentsInChildren<Light>();
            foreach(Light light in lights)
            {
                light.color = defaultLightColor;
            }
        }
    }
    void DeleteBoxes()
    {
        if(!useColliders)
        {
            foreach(Tile generatedTile in generatedTiles)
            {
                BoxCollider box = generatedTile.tile.GetComponent<BoxCollider>();
                if(box != null)
                {
                    Destroy(box);
                }
            }
        }
    }
    void DebugRoomLighting(Transform tile, Color lightColor)
    {
        //Application.isEditor : enable this feature in the editor only
        if (useLights && Application.isEditor)
        {
            Light[] lights = tile.GetComponentsInChildren<Light>();
            if(lights.Length > 0)
            {
                if (defaultLightColor == Color.white)
                {
                    defaultLightColor = lights[0].color;
                }
                foreach(Light light in lights)
                {
                    light.color = lightColor;
                }
            }
        }
    }
    void ConnectTiles()
    {
        Transform connectFrom = GetRandomConnect(tileFrom);
        if(connectFrom==null)
        {
            return;
        }
        Transform connectTo = GetRandomConnect(tileTo);
        if (connectTo == null)
        {
            return;
        }
        connectTo.SetParent(connectFrom);
        tileTo.SetParent(connectTo);
        connectTo.localPosition = Vector3.zero;
        connectTo.localRotation = Quaternion.identity;
        connectTo.Rotate(0, 180f, 0);
        tileTo.SetParent(path);
        connectTo.SetParent(tileTo.Find("Contactor"));
        generatedTiles.Last().contact = connectFrom.GetComponent<Contact>();
    }
    Transform GetRandomConnect(Transform tile)
    {
        if(tile==null)
        {
            return null;
        }
        List<Contact> connectList = tile.GetComponentsInChildren<Contact>().ToList().FindAll(includes => includes.isConnected==false);
        if(connectList.Count>0)
        {
            int contactIndex = Random.Range(0, connectList.Count);
            connectList[contactIndex].isConnected = true;
            if(tile==tileFrom)
            {
                BoxCollider box = tile.GetComponent<BoxCollider>();
                if(box == null)
                {
                    box = tile.gameObject.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                }
            }
            return connectList[contactIndex].transform;
        }
        return null;
    }
    Transform CreateStartRoom()
    {
        int index = Random.Range(0, startRoom.Length);//the max number not in this range.(0,3)->got 0,1 or 2
        GameObject tile = Instantiate(startRoom[index], transform.position, Quaternion.identity, path);
        tile.name = "StartRoom";
        float roomRotation = Random.Range(0, 4) * 90f;
        tile.transform.Rotate(0, roomRotation, 0);//give a random 90 rotation
        //add to generatedTile
        generatedTiles.Add(new Tile(tile.transform, null));
        return tile.transform;
    }
    Transform CreateEndRoom()
    {
        int index = Random.Range(0, endRoom.Length);
        GameObject tile = Instantiate(endRoom[index], transform.position, Quaternion.identity, path);
        tile.name = "EndRoom";
        Transform origin = generatedTiles[generatedTiles.FindIndex(includes => includes.tile == tileFrom)].tile;
        generatedTiles.Add(new Tile(tile.transform, origin));
        return tile.transform;
    }
    Transform CreateRandomRoom()
    {
        int index = Random.Range(0, randomRoom.Length);
        GameObject tile = Instantiate(randomRoom[index], transform.position, Quaternion.identity, path);
        tile.name = randomRoom[index].name;
        Transform origin = generatedTiles[generatedTiles.FindIndex(includes => includes.tile == tileFrom)].tile;
        generatedTiles.Add(new Tile(tile.transform, origin));
        return tile.transform;
    }
    void BuildNavigationMesh()
    {
        if (navMeshSurface == null)
        {
            Debug.LogWarning("NavMeshSurface is missing.");
            return;
        }

        navMeshSurface.BuildNavMesh();
    }
    void GenerateEcosystem()
    {
        foreach (DungeonNode node in dungeonNodes)
        {
            if (node.roomTransform == null)
            {
                continue;
            }

            if (node.type == RoomType.Start || node.type == RoomType.Boss)
            {
                continue;
            }

            int plantCount = GetPlantCountForRoom(node.type);
            int herbivoreCount = GetHerbivoreCountForRoom(node.type);
            int scavengerCount = GetScavengerCountForRoom(node.type);

            for (int i = 0; i < plantCount; i++)
            {
                SpawnInRoom(plantPrefab, node.roomTransform);
            }

            for (int i = 0; i < herbivoreCount; i++)
            {
                SpawnInRoom(herbivorePrefab, node.roomTransform);
            }

            for (int i = 0; i < scavengerCount; i++)
            {
                SpawnInRoom(scavengerPrefab, node.roomTransform);
            }
        }
        int GetPlantCountForRoom(RoomType type)
        {
            switch (type)
            {
                case RoomType.Hall:
                    return Random.Range(0, 2);

                case RoomType.Beginner:
                    return Random.Range(1, 4);

                case RoomType.Intermediate:
                    return Random.Range(2, 5);

                case RoomType.Advanced:
                    return Random.Range(0, 3);

                case RoomType.Rest:
                    return Random.Range(3, 6);

                default:
                    return 0;
            }
        }

        int GetHerbivoreCountForRoom(RoomType type)
        {
            switch (type)
            {
                case RoomType.Beginner:
                    return Random.Range(1, 3);

                case RoomType.Intermediate:
                    return Random.Range(1, 4);

                case RoomType.Advanced:
                    return Random.Range(0, 2);

                default:
                    return 0;
            }
        }
        int GetScavengerCountForRoom(RoomType type)
        {
            switch (type)
            {
                case RoomType.Intermediate:
                    return Random.Range(0, 2);

                case RoomType.Advanced:
                    return Random.Range(1, 3);

                default:
                    return 0;
            }
        }
    }
    GameObject SpawnInRoom(GameObject prefab, Transform room)
    {
        if (prefab == null || room == null)
        {
            return null;
        }

        if (!TryGetSpawnPositionInRoom(room, out Vector3 position))
        {
            Debug.LogWarning("Could not find valid ecosystem spawn point in room: " + room.name);
            return null;
        }

        GameObject spawned = Instantiate(prefab, position, Quaternion.identity, room);
        SnapSpawnedObjectToNavMesh(spawned);
        return spawned;
    }

    bool TryGetSpawnPositionInRoom(Transform room, out Vector3 position)
    {
        position = Vector3.zero;

        BoxCollider roomBox = room.GetComponent<BoxCollider>();

        if (roomBox == null)
        {
            return false;
        }

        Bounds roomBounds = roomBox.bounds;
        float paddingX = roomBounds.size.x * 0.1f;
        float paddingZ = roomBounds.size.z * 0.1f;

        for (int attempt = 0; attempt < 100; attempt++)
        {
            float x = Random.Range(roomBounds.min.x + paddingX, roomBounds.max.x - paddingX);
            float z = Random.Range(roomBounds.min.z + paddingZ, roomBounds.max.z - paddingZ);

            Vector3 samplePoint = new Vector3(x, roomBounds.min.y + 0.2f, z);

            if (!NavMesh.SamplePosition(samplePoint, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                continue;
            }

            if (!roomBounds.Contains(new Vector3(hit.position.x, roomBounds.min.y + 0.2f, hit.position.z)))
            {
                continue;
            }

            if (NavMesh.FindClosestEdge(hit.position, out NavMeshHit edgeHit, NavMesh.AllAreas) && edgeHit.distance < 0.2f)
            {
                continue;
            }

            position = hit.position;
            return true;
        }

        return false;
    }

    void SnapSpawnedObjectToNavMesh(GameObject spawned)
    {
        if (spawned == null)
        {
            return;
        }

        if (!NavMesh.SamplePosition(spawned.transform.position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
        {
            return;
        }

        NavMeshAgent agent = spawned.GetComponent<NavMeshAgent>();

        if (agent != null)
        {
            agent.Warp(hit.position);
            return;
        }

        spawned.transform.position = hit.position;
    }
}





