using UnityEngine;
using System.Collections;

public class BoidBehaviour_Test : MonoBehaviour
{
    // Reference to the controller.
    public BoidController controller;

    // Options for animation playback.
    public float animationSpeedVariation = 0.2f;

    // Random seed.
    float noiseOffset;

    public Vector3 Direction;

    // Caluculates the separation vector with a target.
    Vector3 GetSeparationVector(Transform target)
    {
        var diff = transform.position - target.transform.position;
        var diffLen = diff.magnitude;
        var scaler = Mathf.Clamp01(1.0f - diffLen / controller.neighborDist);
        return diff * (scaler / diffLen);
    }

    void Start()
    {
        Direction = transform.forward;

        noiseOffset = Random.value * 10.0f;

        var animator = GetComponent<Animator>();
        if (animator)
            animator.speed = Random.Range(-1.0f, 1.0f) * animationSpeedVariation + 1.0f;
    }

    void Update()
    {
        var currentPosition = transform.position;
        var currentRotation = transform.rotation;

        // Current velocity randomized with noise.
        var noise = Mathf.PerlinNoise(Time.time, noiseOffset) * 2.0f - 1.0f;
        var velocity = controller.velocity * (1.0f + noise * controller.velocityVariation);

        // Initializes the vectors.
        var separation = Vector3.zero;
        var alignment = controller.transform.forward;
        var cohesion = controller.transform.position;

        Debug.Log(alignment);

        // Looks up nearby boids.
        var nearbyBoids = Physics.OverlapSphere(currentPosition, controller.neighborDist, controller.searchLayer);

        // Accumulates the vectors.
        foreach (var boid in nearbyBoids)
        {
            if (boid.gameObject == gameObject) continue;
            var t = boid.transform;
            var tBoidBehaviour = t.GetComponent<BoidBehaviour_Test>();
            if (!tBoidBehaviour) continue;
            // separation += currentPosition - t.position;
            separation += GetSeparationVector(t);
            // alignment += t.forward;
            alignment += tBoidBehaviour.Direction;
            cohesion += t.position;
        }

        var avg = 1.0f / nearbyBoids.Length;
        alignment *= avg;
        cohesion *= avg;
        cohesion = (cohesion - currentPosition).normalized;

        // Calculates a rotation from the vectors.
        var direction = separation + alignment + cohesion;
        // var rotation = Quaternion.FromToRotation(Vector3.forward, direction.normalized);

        var ip = Mathf.Exp(-controller.rotationCoeff * Time.deltaTime);
        // transform.rotation = Quaternion.Lerp(rotation, currentRotation, ip);
        Direction = Vector3.Lerp(direction.normalized, Direction.normalized, ip);

        // Moves forawrd.
        // transform.position = currentPosition + transform.forward * (velocity * Time.deltaTime);
        transform.position += Direction * (velocity * Time.deltaTime);
        
        transform.LookAt(transform.position + Direction);
    }
}
