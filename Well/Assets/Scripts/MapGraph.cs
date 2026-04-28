using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MapGraph : MonoBehaviour
{
    public List<MapGraph> connectRoom = new List<MapGraph>();

    public int depth;
    public Transform roomPosition;
}
