using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaypointContainer : MonoBehaviour
{
    public Transform[] waypoints;

    private void Awake()
    {

        int count = transform.childCount;
        waypoints = new Transform[count];
        for (int i = 0; i < count; i++)
            waypoints[i] = transform.GetChild(i);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
