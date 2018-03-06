using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct GPUBoid_Compute
{
    public Vector3 position, direction;
    public float noise_offset;
}

public class GPUFlockCompute : MonoBehaviour {
    public ComputeShader cshader;

    public GameObject boidPrefab;
    public int BoidsCount;
    public float SpawnRadius;
    public GameObject[] boidsGo;
    public GPUBoid_Compute[] boidsData;
    public Transform Target;

    private int kernelHandle;

    void Start()
    {
        this.boidsGo = new GameObject[this.BoidsCount];
        this.boidsData = new GPUBoid_Compute[this.BoidsCount];
        this.kernelHandle = cshader.FindKernel("CSMain");

        for (int i = 0; i < this.BoidsCount; i++)
        {
            this.boidsData[i] = this.CreateBoidData();
            this.boidsGo[i] = Instantiate(boidPrefab, this.boidsData[i].position, Quaternion.Euler(this.boidsData[i].direction)) as GameObject;
            this.boidsData[i].direction = this.boidsGo[i].transform.forward;
            this.boidsData[i].noise_offset = Random.value * 1000.0f;
        }
    }

    GPUBoid_Compute CreateBoidData()
    {
        GPUBoid_Compute boidData = new GPUBoid_Compute();
        Vector3 pos = transform.position + Random.insideUnitSphere * SpawnRadius;
        // Quaternion rot = Quaternion.Slerp(transform.rotation, Random.rotation, 0.3f);
        boidData.position = pos;

        return boidData;
    }

    public float RotationSpeed = 1f;
    public float BoidSpeed = 1f;
    public float NeighbourDistance = 1f;
    public float BoidSpeedVariation = 1f;
    void Update()
    {
        ComputeBuffer buffer = new ComputeBuffer(BoidsCount, 28);
        buffer.SetData(this.boidsData);

        cshader.SetBuffer(this.kernelHandle, "boidBuffer", buffer);
        cshader.SetFloat("DeltaTime", Time.deltaTime);
        cshader.SetFloat("RotationSpeed", RotationSpeed);
        cshader.SetFloat("BoidSpeed", BoidSpeed);
        cshader.SetFloat("BoidSpeedVariation", BoidSpeedVariation);
        cshader.SetVector("FlockPosition", Target.transform.position);
        cshader.SetFloat("NeighbourDistance", NeighbourDistance);
        cshader.SetInt("BoidsCount", BoidsCount);

        cshader.Dispatch(this.kernelHandle, this.BoidsCount, 1, 1);

        buffer.GetData(this.boidsData);

        buffer.Release();

        for (int i = 0; i < this.boidsData.Length; i++)
        {
            this.boidsGo[i].transform.localPosition = this.boidsData[i].position;

            if(!this.boidsData[i].direction.Equals(Vector3.zero))
            {
                this.boidsGo[i].transform.rotation = Quaternion.LookRotation(this.boidsData[i].direction);
            }

        }
    }
}
