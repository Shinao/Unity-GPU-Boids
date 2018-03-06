using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct GPUBoid_Skinned
{
    public Vector3 position;
    public Vector3 direction;
    public float noise_offset;
    public float speed;
    public float frame;
    public float next_frame;
    public float frame_interpolation;
    public float padding;
}

public class GPUFlock_Skinned : MonoBehaviour {
    public ComputeShader _ComputeFlock;

    private SkinnedMeshRenderer BoidSMR;
    public GameObject TargetBoidToGPUSkin;
    private Animator _Animator;
    public AnimationClip _AnimationClip;

    private int NbFrames;
    public int BoidsCount;
    public float SpawnRadius;
    public GPUBoid_Skinned[] boidsData;
    public Transform Target;

    public Mesh BoidMesh;

    private int kernelHandle;
    private ComputeBuffer BoidBuffer;
    private ComputeBuffer VertexAnimationBuffer;
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

        this.boidsData = new GPUBoid_Skinned[this.BoidsCount];
        this.kernelHandle = _ComputeFlock.FindKernel("CSMain");

        for (int i = 0; i < this.BoidsCount; i++)
        {
            this.boidsData[i] = this.CreateBoidData();
            this.boidsData[i].noise_offset = Random.value * 1000.0f;
        }

        BoidBuffer = new ComputeBuffer(BoidsCount, 48);
        BoidBuffer.SetData(this.boidsData);

        GenerateSkinnedAnimationForGPUBuffer();
    }

    GPUBoid_Skinned CreateBoidData()
    {
        GPUBoid_Skinned boidData = new GPUBoid_Skinned();
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
    public float BoidFrameSpeed = 10f;
    public bool FrameInterpolation = true;
    void Update()
    {
        _ComputeFlock.SetFloat("DeltaTime", Time.deltaTime);
        _ComputeFlock.SetFloat("RotationSpeed", RotationSpeed);
        _ComputeFlock.SetFloat("BoidSpeed", BoidSpeed);
        _ComputeFlock.SetFloat("BoidSpeedVariation", BoidSpeedVariation);
        _ComputeFlock.SetVector("FlockPosition", Target.transform.position);
        _ComputeFlock.SetFloat("NeighbourDistance", NeighbourDistance);
        _ComputeFlock.SetFloat("BoidFrameSpeed", BoidFrameSpeed);
        _ComputeFlock.SetInt("BoidsCount", BoidsCount);
        _ComputeFlock.SetInt("NbFrames", NbFrames);
        _ComputeFlock.SetBuffer(this.kernelHandle, "boidBuffer", BoidBuffer);
        _ComputeFlock.Dispatch(this.kernelHandle, this.BoidsCount / GROUP_SIZE + 1, 1, 1);

        BoidMaterial.SetBuffer("boidBuffer", BoidBuffer);

        if (FrameInterpolation && !BoidMaterial.IsKeywordEnabled("FRAME_INTERPOLATION"))
            BoidMaterial.EnableKeyword("FRAME_INTERPOLATION");
        if (!FrameInterpolation && BoidMaterial.IsKeywordEnabled("FRAME_INTERPOLATION"))
            BoidMaterial.DisableKeyword("FRAME_INTERPOLATION");

        BoidMaterial.SetInt("NbFrames", NbFrames);

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
        if (VertexAnimationBuffer != null) VertexAnimationBuffer.Release();
    }

    private void GenerateSkinnedAnimationForGPUBuffer()
    {
        BoidSMR = TargetBoidToGPUSkin.GetComponentInChildren<SkinnedMeshRenderer>();
        _Animator = TargetBoidToGPUSkin.GetComponentInChildren<Animator>();
        int iLayer = 0;
        AnimatorStateInfo aniStateInfo = _Animator.GetCurrentAnimatorStateInfo(iLayer);

        Mesh bakedMesh = new Mesh();
        float sampleTime = 0;
        float perFrameTime = 0;

        NbFrames = Mathf.ClosestPowerOfTwo((int)(_AnimationClip.frameRate * _AnimationClip.length));
        perFrameTime = _AnimationClip.length / NbFrames;

        var vertexCount = BoidSMR.sharedMesh.vertexCount;
        VertexAnimationBuffer = new ComputeBuffer(vertexCount * NbFrames, 16);
        Vector4[] vertexAnimationData = new Vector4[vertexCount * NbFrames];
        for (int i = 0; i < NbFrames; i++)
        {
            _Animator.Play(aniStateInfo.shortNameHash, iLayer, sampleTime);
            _Animator.Update(0f);

            BoidSMR.BakeMesh(bakedMesh);

            for(int j = 0; j < vertexCount; j++)
            {
                Vector3 vertex = bakedMesh.vertices[j];
                vertexAnimationData[(j * NbFrames) +  i] = vertex;
            }

            sampleTime += perFrameTime;
        }

        VertexAnimationBuffer.SetData(vertexAnimationData);
        BoidMaterial.SetBuffer("vertexAnimation", VertexAnimationBuffer);

        TargetBoidToGPUSkin.SetActive(false);
    }
}
