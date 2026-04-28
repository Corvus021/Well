using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    public GameObject[] startRoom;
    public GameObject[] randomRoom;
    public GameObject[] endRoom;
    public GameObject[] blockedWall;
    public GameObject[] Door;

    [Header("Debugging Options")]
    public bool useColliders;
    public bool useLights;
    public bool restoreLights;

    [Header("Key Options")]
    public KeyCode reloadKey = KeyCode.F;
    public KeyCode changeMapKey = KeyCode.R;

    [Header("Ceneration Limits")]
    [Range(2,100)]public int mainLength = 10;
    [Range(0, 50)] public int branchLength = 5;
    [Range(0, 100)] public int branchNumber = 10;
    [Range(0, 100)] public int doorPercent = 25;
    [Range(0,1f)]public float buildTime;

    [Header("Room List")]
    public DungeonGenerator dungeonGenerator = DungeonGenerator.inactive;
    public List<Tile> generatedTiles = new List<Tile>();

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
        GameObject createPath = new GameObject("Main Path");
        path = createPath.transform;
        path.SetParent(transform);
        tileRoot = CreateStartRoom();
        DebugRoomLighting(tileRoot, Color.purple);
        tileTo = tileRoot;
        dungeonGenerator = DungeonGenerator.generatingMain;
        for (int i = 0; i < mainLength - 1; i++)
        {
            yield return new WaitForSeconds(buildTime);
            tileFrom = tileTo;
            tileTo = CreateRandomRoom();
            DebugRoomLighting(tileTo, Color.yellow);
            ConnectTiles();
            CollisionCheck();
            if (attempts >= maxAttempts)
            {
                break;
            }
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
                for (int i = 0; i < branchLength - 1; i++)
                {
                    yield return new WaitForSeconds(buildTime);
                    tileFrom = tileTo;
                    tileTo = CreateRandomRoom();
                    DebugRoomLighting(tileTo, Color.green);
                    ConnectTiles();
                    CollisionCheck();
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
        DeleteBoxes();
        BlockedPassages();
        GenerateDoors();//There¡¯s a problem with the transform on doors :( it needs fixing
        dungeonGenerator = DungeonGenerator.completed;
        yield return null;
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
    void CollisionCheck()
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
                if(generatedTiles[toIndex].contact != null)
                {
                    generatedTiles[toIndex].contact.isConnected = false;
                }
                generatedTiles.RemoveAt(toIndex);
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
                    tileTo = CreateRandomRoom();
                    Color retryColor = path.name.Contains("Branch") ? Color.green : Color.yellow;
                    DebugRoomLighting(tileTo, retryColor * 2f);
                    ConnectTiles();
                    CollisionCheck();
                }
            }
            else
            {
                attempts = 0;
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
    Transform CreateRandomRoom()
    {
        int index = Random.Range(0, randomRoom.Length);
        GameObject tile = Instantiate(randomRoom[index], transform.position, Quaternion.identity, path);
        tile.name = randomRoom[index].name;
        Transform origin = generatedTiles[generatedTiles.FindIndex(includes => includes.tile == tileFrom)].tile;
        generatedTiles.Add(new Tile(tile.transform, origin));
        return tile.transform;
    }
}
