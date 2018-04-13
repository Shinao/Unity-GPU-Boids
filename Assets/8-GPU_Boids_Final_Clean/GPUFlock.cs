using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Runtime.InteropServices;

public class GPUFlock : MonoBehaviour {
    public struct GPUBoid
    {
        public Vector3 position;
        public Vector3 direction;
        public float noise_offset;
        public float speed;
        public float frame;
        public float next_frame;
        public float frame_interpolation;
        public float size;
    }

    public struct GPUAffector {
        public Vector3 position;
        public float force;
        public float distance;
        public int axis;
        public Vector2 padding;
    }

    public ComputeShader _ComputeFlock;
    public GameObject TargetBoidToGPUSkin;
    public Transform Target;
    public Mesh BoidMesh;
    public Material BoidMaterial;

    private SkinnedMeshRenderer BoidSMR;
    private Animator _Animator;
    public AnimationClip _AnimationClip;
    private int NbFramesInAnimation;

    public bool UseAffectors;
    public TextAsset DrawingAffectors;
    public bool UseMeshAffectors = false;
    public Mesh MeshAffectors;    
    public float ScaleDrawingAffectors = 0.03f;
    public bool ReverseYAxisDrawingAffectors = true;
    public Vector3 DrawingAffectorsOffset;
    public bool DrawDrawingAffectors = true;
    private int NbAffectors = 0;

    public int BoidsCount;
    public int StepBoidCheckNeighbours = 1;
    public float SpawnRadius;
    public float RotationSpeed = 4f;
    public float BoidSpeed = 6f;
    public float NeighbourDistance = 2f;
    public float BoidSpeedVariation = 0.9f;
    public float BoidFrameSpeed = 10f;
    public bool FrameInterpolation = true;
    public float AffectorForce = 2f;
    public float AffectorDistance = 2f;
    public float MaxAffectorFullAxisSize = 20f;
    private GPUBoid[] boidsData;
    private GPUAffector[] Affectors = new GPUAffector[1];

    private int kernelHandle;
    private ComputeBuffer BoidBuffer;
    private ComputeBuffer AffectorBuffer;
    private ComputeBuffer VertexAnimationBuffer;
    private ComputeBuffer _drawArgsBuffer;
    private Bounds InfiniteBounds = new Bounds(Vector3.zero, Vector3.one * 9999);

    private const int THREAD_GROUP_SIZE = 256;

    void Start()
    {
        BoidMaterial = new Material(BoidMaterial);
        
        _drawArgsBuffer = new ComputeBuffer(
            1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments
        );

        _drawArgsBuffer.SetData(new uint[5] {
            BoidMesh.GetIndexCount(0), (uint) BoidsCount, 0, 0, 0
        });

        this.boidsData = new GPUBoid[this.BoidsCount];
        this.kernelHandle = _ComputeFlock.FindKernel("CSMain");

        for (int i = 0; i < this.BoidsCount; i++)
            this.boidsData[i] = this.CreateBoidData();

        BoidBuffer = new ComputeBuffer(BoidsCount, Marshal.SizeOf(typeof(GPUBoid)));
        BoidBuffer.SetData(this.boidsData);

        GenerateSkinnedAnimationForGPUBuffer();

        if (UseAffectors) {
            if (UseMeshAffectors) {
                var bounds = MeshAffectors.bounds;
                var scaledVertices = MeshAffectors.vertices.Select(v => (v) * (ReverseYAxisDrawingAffectors ? -1 : 1)  * ScaleDrawingAffectors + DrawingAffectorsOffset).ToArray();
                GenerateDrawingAffectors(scaledVertices, 0, 0, 3);
            }
            else {
                var dataToPaths = new PointsFromData();
                dataToPaths.GeneratePointsFrom(DrawingAffectors, DrawingAffectorsOffset, new Vector3(0, 90, 0), ReverseYAxisDrawingAffectors, ScaleDrawingAffectors);
                GenerateDrawingAffectors(dataToPaths.Points.ToArray());
            }
        }
        else
            AffectorBuffer = new ComputeBuffer(1, Marshal.SizeOf(typeof(GPUAffector)));

        SetComputeData();
        SetMaterialData();

        if (DrawILoveUnity)
            StartCoroutine(DrawILoveUnityForever());
    }

    public bool DrawILoveUnity = false;
    public TextAsset EyeDrawing;
    public TextAsset HeartDrawing;
    public TextAsset UnityDrawing;
    IEnumerator DrawILoveUnityForever() {
        var dataToPaths = new PointsFromData();
        dataToPaths.GeneratePointsFrom(EyeDrawing, new Vector3(0, 2, -2), new Vector3(0, 90, 0), ReverseYAxisDrawingAffectors, 0.03f);
        var eyePoints = dataToPaths.Points.ToArray();
        dataToPaths.GeneratePointsFrom(HeartDrawing, new Vector3(0, 2, -2), new Vector3(0, 90, 0), ReverseYAxisDrawingAffectors, 0.05f);
        var heartPoints = dataToPaths.Points.ToArray();
        dataToPaths.GeneratePointsFrom(UnityDrawing, new Vector3(0, 0, -1), new Vector3(0, 90, 0), ReverseYAxisDrawingAffectors, 0.1f);
        var unityPoints = dataToPaths.Points.ToArray();
        yield return new WaitForSeconds(3f);
        while (true) {
            GenerateDrawingAffectors(eyePoints, 0, 0, 0);
            yield return new WaitForSeconds(3f);
            GenerateDrawingAffectors(new Vector3[1], 0, 0, 0);
            yield return new WaitForSeconds(0.5f);
            GenerateDrawingAffectors(heartPoints, 0, 0, 0);
            yield return new WaitForSeconds(3f);
            GenerateDrawingAffectors(new Vector3[1], 0, 0, 0);
            yield return new WaitForSeconds(0.5f);
            GenerateDrawingAffectors(unityPoints, 2, 0, 0);
            yield return new WaitForSeconds(4f);
            GenerateDrawingAffectors(new Vector3[1], 0, 0, 0);
            yield return new WaitForSeconds(2f);
        }
    }

    GPUBoid CreateBoidData()
    {
        GPUBoid boidData = new GPUBoid();
        Vector3 pos = transform.position + Random.insideUnitSphere * SpawnRadius;
        Quaternion rot = Quaternion.Slerp(transform.rotation, Random.rotation, 0.3f);
        boidData.position = pos;
        boidData.direction = rot.eulerAngles;
        boidData.noise_offset = Random.value * 1000.0f;
        boidData.size = Random.Range(0.5f, 1.5f);

        return boidData;
    }

    private void GenerateDrawingAffectors(Vector3[] points, float force = 0, float distance = 0, int axis = 0) {
        if (AffectorBuffer != null)
            AffectorBuffer.Release();

        NbAffectors = points.Length;
        System.Array.Resize(ref Affectors, NbAffectors);

        Affectors = points.Select(p => {
            var affector = new GPUAffector();
            affector.position = p;
            affector.force = force;
            affector.distance = distance;
            affector.axis = axis;
            return affector;
        }).ToArray();

        if (DrawDrawingAffectors) {
            foreach(var point in points) {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.localScale = new Vector3(1,1,1);
                go.transform.position = point;
            }
        }

        AffectorBuffer = new ComputeBuffer(NbAffectors, Marshal.SizeOf(typeof(GPUAffector)));
        AffectorBuffer.SetData(Affectors);
    }

    void SetComputeData() {
        _ComputeFlock.SetFloat("DeltaTime", Time.deltaTime);
        _ComputeFlock.SetFloat("RotationSpeed", RotationSpeed);
        _ComputeFlock.SetFloat("BoidSpeed", BoidSpeed);
        _ComputeFlock.SetFloat("BoidSpeedVariation", BoidSpeedVariation);
        _ComputeFlock.SetVector("FlockPosition", Target.transform.position);
        _ComputeFlock.SetFloat("NeighbourDistance", NeighbourDistance);
        _ComputeFlock.SetFloat("BoidFrameSpeed", BoidFrameSpeed);
        _ComputeFlock.SetInt("BoidsCount", BoidsCount);
        _ComputeFlock.SetInt("NbFrames", NbFramesInAnimation);
        _ComputeFlock.SetInt("NbAffectors", NbAffectors);
        _ComputeFlock.SetFloat("AffectorForce", AffectorForce);
        _ComputeFlock.SetFloat("AffectorDistance", AffectorDistance);
        _ComputeFlock.SetFloat("MaxAffectorFullAxisSize", MaxAffectorFullAxisSize);
        _ComputeFlock.SetInt("StepBoidCheckNeighbours", StepBoidCheckNeighbours);
        _ComputeFlock.SetBuffer(this.kernelHandle, "boidBuffer", BoidBuffer);
        _ComputeFlock.SetBuffer(this.kernelHandle, "affectorBuffer", AffectorBuffer);
    }

    void SetMaterialData() {
        BoidMaterial.SetBuffer("boidBuffer", BoidBuffer);

        if (FrameInterpolation && !BoidMaterial.IsKeywordEnabled("FRAME_INTERPOLATION"))
            BoidMaterial.EnableKeyword("FRAME_INTERPOLATION");
        if (!FrameInterpolation && BoidMaterial.IsKeywordEnabled("FRAME_INTERPOLATION"))
            BoidMaterial.DisableKeyword("FRAME_INTERPOLATION");

        BoidMaterial.SetInt("NbFrames", NbFramesInAnimation);
    }


    // Execution order should be the lowest possible
    void Update() {
    #if UNITY_EDITOR
        SetComputeData();
        SetMaterialData();
    #endif

        _ComputeFlock.Dispatch(this.kernelHandle, this.BoidsCount / THREAD_GROUP_SIZE + 1, 1, 1);

        GL.Flush(); // Make sure our Dispatch() execute right now
    }

    // Execution order should be the highest possible
    void LateUpdate() {
        Graphics.DrawMeshInstancedIndirect(BoidMesh, 0, BoidMaterial, InfiniteBounds, _drawArgsBuffer, 0);
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
        if (_AnimationClip == null) {
            CreateOneFrameAnimationData();
            return;
        }

        BoidSMR = TargetBoidToGPUSkin.GetComponentInChildren<SkinnedMeshRenderer>();
        _Animator = TargetBoidToGPUSkin.GetComponentInChildren<Animator>();
        int iLayer = 0;
        AnimatorStateInfo aniStateInfo = _Animator.GetCurrentAnimatorStateInfo(iLayer);

        Mesh bakedMesh = new Mesh();
        float sampleTime = 0;
        float perFrameTime = 0;

        NbFramesInAnimation = Mathf.ClosestPowerOfTwo((int)(_AnimationClip.frameRate * _AnimationClip.length));
        perFrameTime = _AnimationClip.length / NbFramesInAnimation;

        var vertexCount = BoidSMR.sharedMesh.vertexCount;
        VertexAnimationBuffer = new ComputeBuffer(vertexCount * NbFramesInAnimation, Marshal.SizeOf(typeof(Vector4)));
        Vector4[] vertexAnimationData = new Vector4[vertexCount * NbFramesInAnimation];
        for (int i = 0; i < NbFramesInAnimation; i++)
        {
            _Animator.Play(aniStateInfo.shortNameHash, iLayer, sampleTime);
            _Animator.Update(0f);

            BoidSMR.BakeMesh(bakedMesh);

            for(int j = 0; j < vertexCount; j++)
            {
                Vector3 vertex = bakedMesh.vertices[j];
                vertexAnimationData[(j * NbFramesInAnimation) +  i] = vertex;
            }

            sampleTime += perFrameTime;
        }

        VertexAnimationBuffer.SetData(vertexAnimationData);
        BoidMaterial.SetBuffer("vertexAnimation", VertexAnimationBuffer);

        TargetBoidToGPUSkin.SetActive(false);
    }

    private void CreateOneFrameAnimationData() {
        var vertexCount = BoidMesh.vertexCount;
        NbFramesInAnimation = 1;
        Vector4[] vertexAnimationData = new Vector4[vertexCount * NbFramesInAnimation];
        VertexAnimationBuffer = new ComputeBuffer(vertexCount * NbFramesInAnimation, Marshal.SizeOf(typeof(Vector4)));
        for(int j = 0; j < vertexCount; j++)
            vertexAnimationData[(j * NbFramesInAnimation)] = BoidMesh.vertices[j];

        VertexAnimationBuffer.SetData(vertexAnimationData);
        BoidMaterial.SetBuffer("vertexAnimation", VertexAnimationBuffer);
        TargetBoidToGPUSkin.SetActive(false);
    }
}