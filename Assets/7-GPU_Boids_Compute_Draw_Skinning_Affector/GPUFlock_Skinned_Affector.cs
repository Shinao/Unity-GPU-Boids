using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class GPUFlock_Skinned_Affector : MonoBehaviour {
    public struct GPUBoid_Skinned_Affector
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

    public struct GPUBoidAffector {
        public Vector3 position;
        public float force;
        public float distance;
    }

    public ComputeShader _ComputeFlock;

    private SkinnedMeshRenderer BoidSMR;
    public GameObject TargetBoidToGPUSkin;
    private Animator _Animator;
    public AnimationClip _AnimationClip;

    private int NbFrames;
    public int BoidsCount;
    public float SpawnRadius;
    public GPUBoid_Skinned_Affector[] boidsData;
    public Transform Target;

    public Mesh BoidMesh;

    private int kernelHandle;
    private ComputeBuffer BoidBuffer;
    private ComputeBuffer AffectorBuffer;
    private ComputeBuffer VertexAnimationBuffer;
    public Material BoidMaterial;
    ComputeBuffer _drawArgsBuffer;

    const int GROUP_SIZE = 256;

    void Start()
    {
        _drawArgsBuffer = new ComputeBuffer(
            1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments
        );

        _drawArgsBuffer.SetData(new uint[5] {
            BoidMesh.GetIndexCount(0), (uint) BoidsCount, 0, 0, 0
        });

        this.boidsData = new GPUBoid_Skinned_Affector[this.BoidsCount];
        this.kernelHandle = _ComputeFlock.FindKernel("CSMain");

        for (int i = 0; i < this.BoidsCount; i++)
            this.boidsData[i] = this.CreateBoidData();

        BoidBuffer = new ComputeBuffer(BoidsCount, 48);
        BoidBuffer.SetData(this.boidsData);

        AffectorBuffer = new ComputeBuffer(NbAffectorsPerRay, 20);

        GenerateSkinnedAnimationForGPUBuffer();

        var dataToPaths = new PointsFromData();
        dataToPaths.GeneratePointsFrom(DrawingAffectors, DrawingAffectorsOffset, new Vector3(0, 90, 0), ReverseYAxisDrawingAffectors, ScaleDrawingAffectors);
        GenerateDrawingAffectors(dataToPaths.Points.ToArray(), 2, 2);
    }

    GPUBoid_Skinned_Affector CreateBoidData()
    {
        GPUBoid_Skinned_Affector boidData = new GPUBoid_Skinned_Affector();
        Vector3 pos = transform.position + Random.insideUnitSphere * SpawnRadius;
        Quaternion rot = Quaternion.Slerp(transform.rotation, Random.rotation, 0.3f);
        boidData.position = pos;
        boidData.direction = rot.eulerAngles;
        boidData.noise_offset = Random.value * 1000.0f;

        return boidData;
    }

    public TextAsset DrawingAffectors;
    public float ScaleDrawingAffectors = 0.03f;
    public bool ReverseYAxisDrawingAffectors = true;
    public Vector3 DrawingAffectorsOffset;
    public bool DrawDrawingAffectors = true;
    GPUBoidAffector[] Affectors = new GPUBoidAffector[1];
    private void GenerateDrawingAffectors(Vector3[] points, float affectorForce, float affectorDistance) {
        if (AffectorBuffer != null)
            AffectorBuffer.Release();

        System.Array.Resize(ref Affectors, NbAffectors + points.Length);

        var new_affectors = points.Select(p => {
            var affector = new GPUBoidAffector();
            affector.position = p;
            affector.force = affectorForce;
            affector.distance = affectorDistance;
            return affector;
        }).ToArray();

        System.Array.Copy(new_affectors, 0, Affectors, NbAffectors, new_affectors.Length);

        if (DrawDrawingAffectors) {
            foreach(var point in points) {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.localScale = new Vector3(1,1,1);
                go.transform.position = point;
            }
        }

        NbAffectors += points.Length;

        AffectorBuffer = new ComputeBuffer(NbAffectors, 20);
        AffectorBuffer.SetData(Affectors);
    }

    public float RayAffectorDistance = 100f;
    public int NbAffectorsPerRay = 100;
    private int NbAffectors = 0;
    private void GenerateAffectors(float affectorForce, float affectorDistance) {
        var flockPosition = Target.transform.position;

        if (AffectorBuffer != null)
            AffectorBuffer.Release();

        System.Array.Resize(ref Affectors, NbAffectors + NbAffectorsPerRay);

        var rayDirection = Camera.main.ScreenPointToRay(Input.mousePosition).direction;
        var rayStep = RayAffectorDistance / NbAffectorsPerRay;
        var rayPosition = Camera.main.transform.position + rayDirection * Vector3.Distance(Camera.main.transform.position, Target.transform.position);
        for (int i = NbAffectors; i < NbAffectors + NbAffectorsPerRay; i++) {
            var affector = new GPUBoidAffector();
            affector.position = rayPosition;
            affector.force = affectorForce;
            affector.distance = affectorDistance;
            Affectors[i] = affector;

            GameObject.CreatePrimitive(PrimitiveType.Sphere).transform.position = rayPosition;
            
            rayPosition += rayDirection * rayStep;
        }
        NbAffectors += NbAffectorsPerRay;

        AffectorBuffer = new ComputeBuffer(NbAffectors, 20);
        AffectorBuffer.SetData(Affectors);
    }

    public float RotationSpeed = 1f;
    public float BoidSpeed = 1f;
    public float NeighbourDistance = 1f;
    public float BoidSpeedVariation = 1f;
    public float BoidFrameSpeed = 10f;
    public bool FrameInterpolation = true;
    public float AffectorForce = 2f;
    public float AffectorDistance = 2f;
    void Update()
    {
        // if (Input.GetMouseButtonDown(0))
        //     GenerateAffectors(-2, 2);
        // if (Input.GetMouseButtonDown(1))
        //     GenerateAffectors(2, 2);

        _ComputeFlock.SetFloat("DeltaTime", Time.deltaTime);
        _ComputeFlock.SetFloat("RotationSpeed", RotationSpeed);
        _ComputeFlock.SetFloat("BoidSpeed", BoidSpeed);
        _ComputeFlock.SetFloat("BoidSpeedVariation", BoidSpeedVariation);
        _ComputeFlock.SetVector("FlockPosition", Target.transform.position);
        _ComputeFlock.SetFloat("NeighbourDistance", NeighbourDistance);
        _ComputeFlock.SetFloat("BoidFrameSpeed", BoidFrameSpeed);
        _ComputeFlock.SetInt("BoidsCount", BoidsCount);
        _ComputeFlock.SetInt("NbFrames", NbFrames);
        _ComputeFlock.SetInt("NbAffectors", NbAffectors);
        _ComputeFlock.SetFloat("AffectorForce", AffectorForce);
        _ComputeFlock.SetFloat("AffectorDistance", AffectorDistance);
        _ComputeFlock.SetBuffer(this.kernelHandle, "boidBuffer", BoidBuffer);
        _ComputeFlock.SetBuffer(this.kernelHandle, "affectorBuffer", AffectorBuffer);
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
            _drawArgsBuffer, 0
        );
    }

    void OnDestroy()
    {
        if (BoidBuffer != null) BoidBuffer.Release();
        if (AffectorBuffer != null) AffectorBuffer.Release();
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
