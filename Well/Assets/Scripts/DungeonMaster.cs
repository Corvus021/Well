using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DungeonMaster : MonoBehaviour
{
    public float buildTime;
    public GameObject[] startRoom;
    public GameObject[] randomRoom;
    public KeyCode reloadKey = KeyCode.F;
    public List<Tile> generatedTiles = new List<Tile>();

    Transform tileFrom, tileTo, tileRoot;

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
        tileRoot = CreateStartRoom();
        tileTo = tileRoot;
        ConnectTiles();
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForSeconds(buildTime);
            tileFrom = tileTo;
            tileTo = CreateRandomRoom();
            ConnectTiles();
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
        tileTo.SetParent(transform);
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
            return connectList[contactIndex].transform;
        }
        return null;
    }
    Transform CreateStartRoom()
    {
        int index = Random.Range(0, startRoom.Length);//the max number not in this range.(0,3)->got 0,1 or 2
        GameObject tile = Instantiate(startRoom[index], transform.position, Quaternion.identity, transform);
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
        GameObject tile = Instantiate(randomRoom[index], transform.position, Quaternion.identity, transform);
        tile.name = randomRoom[index].name;
        Transform origin = generatedTiles[generatedTiles.FindIndex(includes => includes.tile == tileFrom)].tile;
        generatedTiles.Add(new Tile(tile.transform, origin));
        return tile.transform;
    }
}
