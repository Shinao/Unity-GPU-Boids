using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct GPUBoid_Draw
{
    public Vector3 position;
    public Vector3 direction;
    public float noise_offset;
    public Vector3 padding;
}

public class GPUFlock_Draw : MonoBehaviour {
    public ComputeShader _ComputeFlock;

    public int BoidsCount;
    public float SpawnRadius;
    public GPUBoid_Draw[] boidsData;
    public Transform Target;

    public Mesh BoidMesh;

    private int kernelHandle;
    private ComputeBuffer BoidBuffer;
    public Material BoidMaterial;
    ComputeBuffer _drawArgsBuffer;
    MaterialPropertyBlock _props;

    const int GROUP_SIZE = 256;

    void Start()
    {
        // Initialize the indirect draw args buffer.
        _drawArgsBuffer = new ComputeBuffer(
            1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments
        );

        _drawArgsBuffer.SetData(new uint[5] {
            BoidMesh.GetIndexCount(0), (uint) BoidsCount, 0, 0, 0
        });

        // This property block is used only for avoiding an instancing bug.
        _props = new MaterialPropertyBlock();
        _props.SetFloat("_UniqueID", Random.value);

        this.boidsData = new GPUBoid_Draw[this.BoidsCount];
        this.kernelHandle = _ComputeFlock.FindKernel("CSMain");

        for (int i = 0; i < this.BoidsCount; i++)
        {
            this.boidsData[i] = this.CreateBoidData();
            this.boidsData[i].noise_offset = Random.value * 1000.0f;
        }

        BoidBuffer = new ComputeBuffer(BoidsCount, 40);
        BoidBuffer.SetData(this.boidsData);
    }

    GPUBoid_Draw CreateBoidData()
    {
        GPUBoid_Draw boidData = new GPUBoid_Draw();
        Vector3 pos = transform.position + Random.insideUnitSphere * SpawnRadius;
        Quaternion rot = Quaternion.Slerp(transform.rotation, Random.rotation, 0.3f);
        boidData.position = pos;
        boidData.direction = rot.eulerAngles;

        return boidData;
    }

    public float RotationSpeed = 1f;
    public float BoidSpeed = 1f;
    public float NeighbourDistance = 1f;
    public float BoidSpeedVariation = 1f;
    void Update()
    {
        _ComputeFlock.SetFloat("DeltaTime", Time.deltaTime);
        _ComputeFlock.SetFloat("RotationSpeed", RotationSpeed);
        _ComputeFlock.SetFloat("BoidSpeed", BoidSpeed);
        _ComputeFlock.SetFloat("BoidSpeedVariation", BoidSpeedVariation);
        _ComputeFlock.SetVector("FlockPosition", Target.transform.position);
        _ComputeFlock.SetFloat("NeighbourDistance", NeighbourDistance);
        _ComputeFlock.SetInt("BoidsCount", BoidsCount);
        _ComputeFlock.SetBuffer(this.kernelHandle, "boidBuffer", BoidBuffer);
        _ComputeFlock.Dispatch(this.kernelHandle, this.BoidsCount / GROUP_SIZE + 1, 1, 1);

        BoidMaterial.SetBuffer("boidBuffer", BoidBuffer);
        Graphics.DrawMeshInstancedIndirect(
            BoidMesh, 0, BoidMaterial,
            new Bounds(Vector3.zero, Vector3.one * 1000),
            _drawArgsBuffer, 0, _props
        );
    }

    void OnDestroy()
    {
        if (BoidBuffer != null) BoidBuffer.Release();
        if (_drawArgsBuffer != null) _drawArgsBuffer.Release();
    }
}
