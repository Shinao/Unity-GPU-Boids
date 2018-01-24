using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct GPUBoid_Skinned
{
    public Vector3 position;
    public Vector3 direction;
    public float noise_offset;
    public Vector3 padding;
}

public class GPUFlock_Skinned : MonoBehaviour {
    public ComputeShader _ComputeFlock;

    public SkinnedMeshRenderer BoidSMR;
    public AnimationClip _AnimationClip;

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

        BoidBuffer = new ComputeBuffer(BoidsCount, 40);
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
        _ComputeFlock.Dispatch(this.kernelHandle, this.BoidsCount / GROUP_SIZE, 1, 1);

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
        if (VertexAnimationBuffer != null) VertexAnimationBuffer.Release();
    }

    private void GenerateSkinnedAnimationForGPUBuffer()
    {
        var animator = BoidSMR.GetComponent<Animator>();
        int iLayer = 0;
        float fNormalizedTime = .5f;
        //Get Current State
        AnimatorStateInfo aniStateInfo = animator.GetCurrentAnimatorStateInfo(iLayer);

        Mesh bakedMesh = new Mesh();
        int curClipFrame = 0;
        float sampleTime = 0;
        float perFrameTime = 0;

        curClipFrame = Mathf.ClosestPowerOfTwo((int)(_AnimationClip.frameRate * _AnimationClip.length));
        perFrameTime = _AnimationClip.length / curClipFrame;

        // Texture2D animMap = new Texture2D(this.animData.Value.mapWidth, curClipFrame, TextureFormat.RGBAHalf, false);
        // animMap.name = string.Format("{0}_{1}.animMap", this.animData.Value.name, curAnim.name);
        // this.animData.Value.AnimationPlay(curAnim.name);

        var vertexCount = BoidSMR.sharedMesh.vertexCount;
        VertexAnimationBuffer = new ComputeBuffer(vertexCount * curClipFrame, 16);
        Vector4[] vertexAnimationData = new Vector4[vertexCount * curClipFrame];
        Debug.Log("nb frame: " + curClipFrame);
        for (int i = 0; i < curClipFrame; i++)
        {
            Debug.Log("Bake " + i);
            animator.Play(aniStateInfo.shortNameHash, iLayer,  sampleTime);
            animator.Update(0f);

        //     animation.Sample();
            BoidSMR.BakeMesh(bakedMesh);

            for(int j = 0; j < vertexCount; j++)
            {
                Vector3 vertex = bakedMesh.vertices[j];
                vertexAnimationData[(j * curClipFrame) +  i] = vertex;
            }

            sampleTime += perFrameTime;
        }

        VertexAnimationBuffer.SetData(vertexAnimationData);
        BoidMaterial.SetBuffer("vertexAnimation", VertexAnimationBuffer);

    }
}
