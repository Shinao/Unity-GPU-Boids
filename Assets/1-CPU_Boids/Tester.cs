using UnityEngine;
using System.Collections;

public class Tester : MonoBehaviour
{
    BoidController controller;

    void Start()
    {
        controller = FindObjectOfType<BoidController>();
    }

    void OnGUI()
    {
        if (GUILayout.Button("Spawn (nearby the target)"))
            controller.Spawn();

        if (GUILayout.Button("Spawn (off-screen)"))
        {
            controller.Spawn(controller.transform.position - controller.transform.forward * 8);
        }
    }
}
