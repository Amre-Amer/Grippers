using System;
using UnityEngine;

public class CollisionMgr : MonoBehaviour
{
    public RobotMgr robotMgr;
    private UpperLower ynUpperLower;
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.name == "GripperUpperBody")
        {
            ynUpperLower = UpperLower.upper;
        }
        else
        {
            ynUpperLower = UpperLower.lower;
        }
        robotMgr.PressureOn(ynUpperLower);
    }

    void OnTriggerStay(Collider other)
    {
//        Debug.Log("... OnTriggerStay " + other.gameObject.name + "\n");
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.name == "GripperUpperBody")
        {
            ynUpperLower = UpperLower.upper;
        }
        else
        {
            ynUpperLower = UpperLower.lower;
        }
        robotMgr.PressureOff(ynUpperLower);
    }
}

public enum UpperLower
{
    upper,
    lower
}
