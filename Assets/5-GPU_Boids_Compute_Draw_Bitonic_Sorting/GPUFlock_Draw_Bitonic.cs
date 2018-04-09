using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;

public struct GPUBoid_Advanced_Draw_Bitonic
{
    public Vector3 position, position_for_parallelism, direction, direction_for_parallelism;
    // public Vector3 position, direction;
    public float noise_offset;
    public float distance_to_station;
    public Vector2 padding;
}

public class GPUFlock_Draw_Bitonic : MonoBehaviour {
    public ComputeShader _ComputeFlock;

    public int BoidsCount;
    public float SpawnRadius;
    public GPUBoid_Advanced_Draw_Bitonic[] boidsData;
    public Transform Target;

    public Mesh BoidMesh;

    private int _kernelMove;
    private int _kernelInitSort;
    private int _kernelSort;
    private int _kernelPrepareNextFrame;
    private ComputeBuffer BoidBuffer;
    private ComputeBuffer TestBuffer;
    private ComputeBuffer PositionRankedByDistanceBuffer;
    private ComputeBuffer DirectionRankedByDistanceBuffer;
    private ComputeBuffer keysBuffer;
    private ComputeBuffer valuesBuffer;
    private ComputeBuffer valueIdxToKeyIdxBuffer;
    public Material BoidMaterial;
    ComputeBuffer _drawArgsBuffer;
    MaterialPropertyBlock _props;

    public const string KERNEL_SORT = "BitonicSort";
    public const string KERNEL_SORT_INT = "BitonicSortInt";
    public const string KERNEL_INIT = "InitBitonicSort";

    public const string PROP_BLOCK = "block";
    public const string PROP_DIM = "dim";
    public const string PROP_COUNT = "count";

    public const string BUF_KEYS = "Keys";
    public const string BUF_VALUES = "Values";
    public const string BUF_VALUEIDXTOKEYIDX = "ValueIdxToKeyIdx";
    public const string BUF_INT_VALUES = "IntValues";

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

        this.boidsData = new GPUBoid_Advanced_Draw_Bitonic[this.BoidsCount];
        this._kernelMove = _ComputeFlock.FindKernel("Move");
        this._kernelInitSort = _ComputeFlock.FindKernel("InitBitonicSort");
        this._kernelSort = _ComputeFlock.FindKernel("BitonicSort");
        this._kernelPrepareNextFrame = _ComputeFlock.FindKernel("PrepareNextFrame");

        for (int i = 0; i < this.BoidsCount; i++)
        {
            this.boidsData[i] = this.CreateBoidData();
            this.boidsData[i].noise_offset = Random.value * 1000.0f;
        }

        BoidBuffer = new ComputeBuffer(BoidsCount, 64);
        BoidBuffer.SetData(this.boidsData);

        PositionRankedByDistanceBuffer = new ComputeBuffer(BoidsCount, 16);
        _ComputeFlock.SetBuffer(this._kernelPrepareNextFrame, "PositionRankedByDistance", PositionRankedByDistanceBuffer);
        _ComputeFlock.SetBuffer(this._kernelMove, "PositionRankedByDistance", PositionRankedByDistanceBuffer);
        DirectionRankedByDistanceBuffer = new ComputeBuffer(BoidsCount, 16);
        _ComputeFlock.SetBuffer(this._kernelPrepareNextFrame, "DirectionRankedByDistance", DirectionRankedByDistanceBuffer);
        _ComputeFlock.SetBuffer(this._kernelMove, "DirectionRankedByDistance", DirectionRankedByDistanceBuffer);

        TestBuffer = new ComputeBuffer(BoidsCount, 4);
        _ComputeFlock.SetBuffer(this._kernelMove, "TestBuffer", TestBuffer);

        _ComputeFlock.SetInt("BoidsCount", BoidsCount);

        int x, y, z;
        CalcWorkSize(BoidsCount, out x, out y, out z);
        keysBuffer = new ComputeBuffer(BoidsCount, Marshal.SizeOf(typeof(uint)));
        valuesBuffer = new ComputeBuffer(BoidsCount, Marshal.SizeOf(typeof(float)));
        valueIdxToKeyIdxBuffer = new ComputeBuffer(BoidsCount, Marshal.SizeOf(typeof(uint)));
        _ComputeFlock.SetInt(PROP_COUNT, BoidsCount);
        _ComputeFlock.SetBuffer(_kernelInitSort, BUF_KEYS, keysBuffer);
        _ComputeFlock.SetBuffer(_kernelInitSort, BUF_VALUES, valuesBuffer);
        _ComputeFlock.SetBuffer(_kernelInitSort, BUF_VALUEIDXTOKEYIDX, valueIdxToKeyIdxBuffer);
    }

    GPUBoid_Advanced_Draw_Bitonic CreateBoidData()
    {
        GPUBoid_Advanced_Draw_Bitonic boidData = new GPUBoid_Advanced_Draw_Bitonic();
        Vector3 pos = transform.position + Random.insideUnitSphere * SpawnRadius;
        Quaternion rot = Quaternion.Slerp(transform.rotation, Random.rotation, 0.3f);
        boidData.position = pos;
        boidData.position_for_parallelism = pos;
        boidData.direction = rot.eulerAngles;
        boidData.direction_for_parallelism = rot.eulerAngles;

        return boidData;
    }

    public float RotationSpeed = 1f;
    public float BoidSpeed = 1f;
    public float NeighbourDistance = 1f;
    public float BoidSpeedVariation = 1f;
    // Test variables debug compute
    public float Test = 1.0f;
    public List<Vector4> ListOfDistances = new List<Vector4>();
    public List<Vector3> ListOfPositions = new List<Vector3>();
    public List<uint> ListOfKeys = new List<uint>();
    public List<float> ListOfValues = new List<float>();
    public List<uint> ListOfValueIdxToKeyIdx = new List<uint>();
    public List<float> ListOfTests = new List<float>();
    public List<Vector3> ListOfPositionsRanked = new List<Vector3>();
    public float AvgOfTests;
    public int NbErrored = 0;
    public int NotSorted;
    void Update()
    {
        var deltaTime = Time.deltaTime;

        _ComputeFlock.SetBuffer(this._kernelInitSort, "Boids", BoidBuffer);
        _ComputeFlock.Dispatch(this._kernelInitSort, this.BoidsCount, 1, 1);

        _ComputeFlock.SetBuffer(this._kernelSort, "Boids", BoidBuffer);
        _ComputeFlock.SetBuffer(this._kernelSort, BUF_KEYS, keysBuffer);
        _ComputeFlock.SetBuffer(this._kernelSort, BUF_VALUES, valuesBuffer);
        _ComputeFlock.SetBuffer(this._kernelSort, BUF_VALUEIDXTOKEYIDX, valueIdxToKeyIdxBuffer);
        var count = BoidsCount;
        int x, y, z;
        CalcWorkSize(count, out x, out y, out z);
        _ComputeFlock.SetInt(PROP_COUNT, count);
        for (var dim = 2; dim <= count; dim <<= 1) {
            _ComputeFlock.SetInt(PROP_DIM, dim);
            for (var block = dim >> 1; block > 0; block >>= 1) {
                _ComputeFlock.SetInt(PROP_BLOCK, block);
                _ComputeFlock.SetBuffer(_kernelSort, BUF_KEYS, keysBuffer);
                _ComputeFlock.SetBuffer(_kernelSort, BUF_VALUES, valuesBuffer);
                _ComputeFlock.SetBuffer(_kernelSort, BUF_VALUEIDXTOKEYIDX, valueIdxToKeyIdxBuffer);
                _ComputeFlock.Dispatch(_kernelSort, x, y, z);
            }
        }

        _ComputeFlock.SetBuffer(this._kernelPrepareNextFrame, "Boids", BoidBuffer);
        _ComputeFlock.SetBuffer(this._kernelPrepareNextFrame, BUF_KEYS, keysBuffer);
        _ComputeFlock.SetBuffer(this._kernelPrepareNextFrame, "PositionRankedByDistance", PositionRankedByDistanceBuffer);
        _ComputeFlock.SetBuffer(this._kernelPrepareNextFrame, "DirectionRankedByDistance", DirectionRankedByDistanceBuffer);
        _ComputeFlock.Dispatch(this._kernelPrepareNextFrame, this.BoidsCount / GROUP_SIZE + 1, 1, 1);


        // var key_data = new uint[BoidsCount];
        // keysBuffer.GetData(key_data);
        // ListOfKeys = key_data.ToList();
        // var values_data = new float[BoidsCount];
        // valuesBuffer.GetData(values_data);
        // ListOfValues = values_data.ToList();
        // var valueIdxToKeyIdx_data = new uint[BoidsCount];
        // valueIdxToKeyIdxBuffer.GetData(valueIdxToKeyIdx_data);
        // ListOfValueIdxToKeyIdx = valueIdxToKeyIdx_data.ToList();
        // var test_data = new float[BoidsCount];
        // TestBuffer.GetData(test_data);
        // ListOfTests = test_data.ToList();
        // NbErrored = test_data.Where(nb => nb > 0).Count();
        // AvgOfTests = ListOfTests.Average();
        // NotSorted = 0;
        // for (int i = 1; i < ListOfValues.Count() - 2; ++i) {
        //     if (ListOfValues[(int) ListOfKeys[i]] < ListOfValues[(int) ListOfKeys[i + 1]]) {
        //         NotSorted++;
        //     }
        // }
        // PositionRankedByDistanceBuffer.GetData(test_data);
        // ListOfPositionsRanked = test_data.ToList();

        // BoidBuffer.GetData(this.boidsData);
        // ListOfDistances = boidsData.Select(boid => boid.distance_to_station).ToList();
        // ListOfPositions = boidsData.Select(boid => boid.position).ToList();
        

        _ComputeFlock.SetFloat("Test", Test);
        _ComputeFlock.SetFloat("DeltaTime", deltaTime);
        _ComputeFlock.SetFloat("RotationSpeed", RotationSpeed);
        _ComputeFlock.SetFloat("BoidSpeed", BoidSpeed);
        _ComputeFlock.SetFloat("BoidSpeedVariation", BoidSpeedVariation);
        _ComputeFlock.SetVector("FlockPosition", Target.transform.position);
        _ComputeFlock.SetFloat("NeighbourDistance", NeighbourDistance);

        _ComputeFlock.SetBuffer(this._kernelMove, BUF_KEYS, keysBuffer);
        _ComputeFlock.SetBuffer(this._kernelMove, BUF_VALUES, valuesBuffer);
        _ComputeFlock.SetBuffer(this._kernelMove, BUF_VALUEIDXTOKEYIDX, valueIdxToKeyIdxBuffer);
        _ComputeFlock.SetBuffer(this._kernelMove, "Boids", BoidBuffer);
        _ComputeFlock.SetBuffer(this._kernelMove, "PositionRankedByDistance", PositionRankedByDistanceBuffer);
        _ComputeFlock.SetBuffer(this._kernelMove, "DirectionRankedByDistance", DirectionRankedByDistanceBuffer);

        _ComputeFlock.Dispatch(this._kernelMove, BoidsCount / GROUP_SIZE + 1, 1, 1);

        BoidMaterial.SetBuffer("boidBuffer", BoidBuffer);
        BoidMaterial.SetFloat("Test", Test);
        // BoidMaterial.SetMatrix("_LocalToWorld", transform.localToWorldMatrix);
        // BoidMaterial.SetMatrix("_WorldToLocal", transform.worldToLocalMatrix);
        Graphics.DrawMeshInstancedIndirect(
            BoidMesh, 0, BoidMaterial,
            new Bounds(Vector3.zero, Vector3.one * 1000),
            _drawArgsBuffer, 0, _props
        );
    }

    public const int GROUP_SIZE = 256;
    public const int MAX_DIM_GROUPS = 1024;
    public const int MAX_DIM_THREADS = (GROUP_SIZE * MAX_DIM_GROUPS);

    public static void CalcWorkSize(int length, out int x, out int y, out int z) {
        if (length <= MAX_DIM_THREADS) {
            x = (length - 1) / GROUP_SIZE + 1;
            y = z = 1;
        } else {
            x = MAX_DIM_GROUPS;
            y = (length - 1) / MAX_DIM_THREADS + 1;
            z = 1;
        }
        //Debug.LogFormat("WorkSize {0}x{1}x{2}", x, y, z);
    }
    public static int AlignBufferSize(int length) {
        return ((length - 1) / GROUP_SIZE + 1) * GROUP_SIZE;
    }

    void OnDestroy()
    {
        if (BoidBuffer != null) BoidBuffer.Release();
        if (_drawArgsBuffer != null) _drawArgsBuffer.Release();
        if (valuesBuffer != null) valuesBuffer.Release();
        if (keysBuffer != null) keysBuffer.Release();
        if (valueIdxToKeyIdxBuffer != null) valueIdxToKeyIdxBuffer.Release();
        if (TestBuffer != null) TestBuffer.Release();
        if (PositionRankedByDistanceBuffer != null) PositionRankedByDistanceBuffer.Release();
        if (DirectionRankedByDistanceBuffer != null) DirectionRankedByDistanceBuffer.Release();
    }
}
