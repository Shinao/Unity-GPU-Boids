using UnityEngine;
using System.Collections;

public class SimpleRotation : MonoBehaviour
{
    public float angularVelocity = 10.0f;

    void Update ()
    {
        transform.localRotation = Quaternion.AngleAxis (angularVelocity * Time.deltaTime, Vector3.up) * transform.localRotation;
    }
}
