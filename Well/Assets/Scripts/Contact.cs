using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Contact : MonoBehaviour
{
    public Vector2 size = Vector2.one * 4f;//Vector2.one ->new Vector2(1f,1f)
    public bool isConnected;

    private bool isPlaying;

    void Start()
    {
        isPlaying = true;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = isConnected ? Color.green : Color.red;
        if(!isPlaying)
        {
            Gizmos.color = Color.white;
        }
        Vector2 lineHeight = size * 0.5f;
        Vector3 offset = transform.position + transform.up * lineHeight.y;
        Gizmos.DrawLine(offset, offset + transform.forward);//Gizmos.DrawLine(start point, end point)

        //define top and side vectors
        Vector3 top = transform.up * size.y;
        Vector3 side = transform.right * lineHeight.x;

        //define corner vectors
        Vector3 topRight = transform.position + top + side;
        Vector3 topLeft = transform.position + top - side;
        Vector3 bottomRight = transform.position + side;
        Vector3 bottomLeft = transform.position - side;
        Gizmos.DrawLine(topRight, topLeft);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(topLeft, bottomLeft);
        Gizmos.DrawLine(bottomRight, bottomLeft);
        Gizmos.color *= 0.5f;
        Gizmos.DrawLine(topRight, bottomLeft);
        Gizmos.DrawLine(topLeft, bottomRight);

    }
}
