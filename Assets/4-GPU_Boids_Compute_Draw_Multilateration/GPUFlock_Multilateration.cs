using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using System.Linq;
using System.Threading;

public struct GPUBoid_Multilateration
{
    public Vector3 position, direction;
    public float noise_offset;
    public Vector4 distance_to_station;
    public float padding;
}

public class GPUFlock_Multilateration : MonoBehaviour {
    public ComputeShader _ComputeFlock;
    const int GROUP_SIZE = 256;

    public int BoidsCount;
    public float SpawnRadius;
    public GPUBoid_Multilateration[] boidsData;
    public Transform Target;

    public Mesh BoidMesh;

    private int kernelMoveHandle;
    private int kernelMultilaterationHandle;
    private ComputeBuffer BoidBuffer;
    public Material BoidMaterial;
    ComputeBuffer _drawArgsBuffer;
    MaterialPropertyBlock _props;

    void Start()
    {
        Random.InitState(42);
        
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

        this.boidsData = new GPUBoid_Multilateration[this.BoidsCount];
        this.kernelMoveHandle = _ComputeFlock.FindKernel("Move");
        this.kernelMultilaterationHandle = _ComputeFlock.FindKernel("ComputeMultilateration");

        for (int i = 0; i < this.BoidsCount; i++)
        {
            this.boidsData[i] = this.CreateBoidData();
            this.boidsData[i].noise_offset = Random.value * 1000.0f;
        }

        BoidBuffer = new ComputeBuffer(BoidsCount, 48);
        BoidBuffer.SetData(this.boidsData);

        _ComputeFlock.SetInt("BoidsCount", BoidsCount);
    }

    GPUBoid_Multilateration CreateBoidData()
    {
        GPUBoid_Multilateration boidData = new GPUBoid_Multilateration();
        Vector3 pos = transform.position + Random.insideUnitSphere * SpawnRadius;
        Quaternion rot = Quaternion.Slerp(transform.rotation, Random.rotation, 0.3f);
        boidData.position = pos;
        boidData.direction = rot.eulerAngles;
        // boidData.distance_to_station = new float[4];

        return boidData;
    }

    public float RotationSpeed = 1f;
    public float BoidSpeed = 1f;
    public float NeighbourDistance = 1f;
    public float BoidSpeedVariation = 1f;
    public List<Vector4> ListOfDistances = new List<Vector4>();
    public List<Vector3> ListOfPositions = new List<Vector3>();
    void Update()
    {
        _ComputeFlock.SetBuffer(this.kernelMultilaterationHandle, "boidBuffer", BoidBuffer);
        _ComputeFlock.Dispatch(this.kernelMultilaterationHandle, this.BoidsCount, 1, 1);
        
        // BoidBuffer.GetData(this.boidsData);
        // ListOfDistances = boidsData.Select(boid => boid.distance_to_station).ToList();
        // ListOfPositions = boidsData.Select(boid => boid.position).ToList();

        _ComputeFlock.SetFloat("DeltaTime", Time.deltaTime);
        _ComputeFlock.SetFloat("RotationSpeed", RotationSpeed);
        _ComputeFlock.SetFloat("BoidSpeed", BoidSpeed);
        _ComputeFlock.SetFloat("BoidSpeedVariation", BoidSpeedVariation);
        _ComputeFlock.SetVector("FlockPosition", Target.transform.position);
        _ComputeFlock.SetFloat("NeighbourDistance", NeighbourDistance);
        
        _ComputeFlock.SetBuffer(this.kernelMoveHandle, "boidBuffer", BoidBuffer);
        _ComputeFlock.Dispatch(this.kernelMoveHandle, this.BoidsCount / GROUP_SIZE + 1, 1, 1);

        BoidMaterial.SetBuffer("boidBuffer", BoidBuffer);
        // BoidMaterial.SetMatrix("_LocalToWorld", transform.localToWorldMatrix);
        // BoidMaterial.SetMatrix("_WorldToLocal", transform.worldToLocalMatrix);
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
