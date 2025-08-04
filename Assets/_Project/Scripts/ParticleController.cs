using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Mathematics;
using Random = UnityEngine.Random;

public class ParticleController : MonoBehaviour
{
    [Header("Configurações da Simulação")]
    [Range(1000, 10000)]public int particleCount = 100000;

    [Tooltip("Tamanho visual de cada partícula")]
    [Range(0.01f, 0.1f)]public float particleSize = 0.05f;
    public Vector2 gravity = new Vector2(0.0f, -0.5f);
    [Range(0.1f, 1)]public float drag = 0.1f;

    [Header("Interação e Repulsão")]
    [Range(1, 10)]public float interactionRadius = 2.0f;
    [Range(10, 30)]public float interactionStrength = 20.0f;
    [Tooltip("Raio de repulsão entre partículas")]
    [Range(0.1f, 1)]public float repulsionRadius = 0.1f;
    [Tooltip("Força com que as partículas se repelem")]
    [Range(1, 20)]public float repulsionStrength = 10.0f;

    [Header("Limites (Bounds)")]
    public Vector2 boundsSize = new Vector2(16, 9);
    public Vector2 boundsCenter = Vector2.zero;

    [Header("Referências")]
    public ComputeShader computeShader;
    public Material particleMaterial;

    // Buffers da GPU
    private ComputeBuffer positionsBuffer;
    private ComputeBuffer velocitiesBuffer;
    private ComputeBuffer gridIndicesBuffer;
    private ComputeBuffer gridOffsetsBuffer;

    // Handles dos Kernels
    private int clearGridKernel;
    private int buildGridKernel;
    private int simulateKernel;

    // IDs dos parâmetros do shader
    private static readonly int
        PositionsID = Shader.PropertyToID("_Positions"),
        VelocitiesID = Shader.PropertyToID("_Velocities"),
        GridIndicesID = Shader.PropertyToID("_GridIndices"),
        GridOffsetsID = Shader.PropertyToID("_GridOffsets"),
        DeltaTimeID = Shader.PropertyToID("_DeltaTime"),
        GravityID = Shader.PropertyToID("_Gravity"),
        DragID = Shader.PropertyToID("_Drag"),
        MousePosID = Shader.PropertyToID("_MousePos"),
        InteractionRadiusID = Shader.PropertyToID("_InteractionRadius"),
        InteractionStrengthID = Shader.PropertyToID("_InteractionStrength"),
        RepulsionRadiusID = Shader.PropertyToID("_RepulsionRadius"),
        RepulsionStrengthID = Shader.PropertyToID("_RepulsionStrength"),
        ParticleCountID = Shader.PropertyToID("_ParticleCount"),
        ParticleSizeID = Shader.PropertyToID("_ParticleSize"),
        BoundsMinID = Shader.PropertyToID("_BoundsMin"),
        BoundsMaxID = Shader.PropertyToID("_BoundsMax"),
        GridSizeID = Shader.PropertyToID("_GridSize");

    void Start()
    {
        InitializeBuffers();
        SetupShaderParameters();
    }

    void InitializeBuffers()
    {
        positionsBuffer = new ComputeBuffer(particleCount, sizeof(float) * 2);
        velocitiesBuffer = new ComputeBuffer(particleCount, sizeof(float) * 2);

        int gridWidth = Mathf.CeilToInt(boundsSize.x / repulsionRadius);
        int gridHeight = Mathf.CeilToInt(boundsSize.y / repulsionRadius);
        int totalCells = gridWidth * gridHeight;
        
        gridIndicesBuffer = new ComputeBuffer(particleCount, sizeof(uint) * 2);
        gridOffsetsBuffer = new ComputeBuffer(totalCells, sizeof(uint));

        Vector2[] initialPositions = new Vector2[particleCount];
        Vector2[] initialVelocities = new Vector2[particleCount];
        Vector2 boundsMin = boundsCenter - boundsSize * 0.5f;
        Vector2 boundsMax = boundsCenter + boundsSize * 0.5f;

        for (int i = 0; i < particleCount; i++)
        {
            initialPositions[i] = new Vector2(Random.Range(boundsMin.x, boundsMax.x), Random.Range(boundsMin.y, boundsMax.y));
            initialVelocities[i] = Vector2.zero;
        }

        positionsBuffer.SetData(initialPositions);
        velocitiesBuffer.SetData(initialVelocities);
    }

    void SetupShaderParameters()
    {
        clearGridKernel = computeShader.FindKernel("ClearGrid");
        buildGridKernel = computeShader.FindKernel("BuildGrid");
        simulateKernel = computeShader.FindKernel("Simulate");

        computeShader.SetBuffer(clearGridKernel, GridOffsetsID, gridOffsetsBuffer);
        computeShader.SetBuffer(buildGridKernel, PositionsID, positionsBuffer);
        computeShader.SetBuffer(buildGridKernel, GridIndicesID, gridIndicesBuffer);
        computeShader.SetBuffer(buildGridKernel, GridOffsetsID, gridOffsetsBuffer);
        computeShader.SetBuffer(simulateKernel, PositionsID, positionsBuffer);
        computeShader.SetBuffer(simulateKernel, VelocitiesID, velocitiesBuffer);
        computeShader.SetBuffer(simulateKernel, GridIndicesID, gridIndicesBuffer);
        computeShader.SetBuffer(simulateKernel, GridOffsetsID, gridOffsetsBuffer);

        particleMaterial.SetBuffer(PositionsID, positionsBuffer);
        particleMaterial.SetInt(ParticleCountID, particleCount);
    }

    void Update()
    {
        // --- Configuração dos parâmetros do shader ---
        particleMaterial.SetFloat(ParticleSizeID, particleSize); 

        Vector2 boundsMin = boundsCenter - boundsSize * 0.5f;
        Vector2 boundsMax = boundsCenter + boundsSize * 0.5f;
        int gridWidth = Mathf.CeilToInt(boundsSize.x / repulsionRadius);
        int gridHeight = Mathf.CeilToInt(boundsSize.y / repulsionRadius);
        uint2 gridSize = new uint2((uint)gridWidth, (uint)gridHeight);

        computeShader.SetFloat(DeltaTimeID, Time.deltaTime);
        computeShader.SetVector(GravityID, gravity);
        computeShader.SetFloat(DragID, drag);
        computeShader.SetVector(BoundsMinID, boundsMin);
        computeShader.SetVector(BoundsMaxID, boundsMax);
        computeShader.SetVector(GridSizeID, new Vector4(gridSize.x, gridSize.y, 0, 0));
        computeShader.SetInt(ParticleCountID, particleCount);

        Vector2 mousePos = GetMouseWorldPosition();
        computeShader.SetVector(MousePosID, mousePos);
        computeShader.SetFloat(InteractionRadiusID, interactionRadius);
        computeShader.SetFloat(InteractionStrengthID, interactionStrength);
        computeShader.SetFloat(RepulsionRadiusID, repulsionRadius);
        computeShader.SetFloat(RepulsionStrengthID, repulsionStrength);

        int threadGroups = Mathf.CeilToInt(particleCount / 256.0f);
        int gridThreadGroups = Mathf.CeilToInt(gridOffsetsBuffer.count / 256.0f);

        computeShader.Dispatch(clearGridKernel, gridThreadGroups, 1, 1);
        computeShader.Dispatch(buildGridKernel, threadGroups, 1, 1);
        computeShader.Dispatch(simulateKernel, threadGroups, 1, 1);
    }

    void OnRenderObject()
    {
        particleMaterial.SetPass(0);
        Graphics.DrawProceduralNow(MeshTopology.Points, particleCount);
    }

    void OnDestroy()
    {
        positionsBuffer?.Release();
        velocitiesBuffer?.Release();
        gridIndicesBuffer?.Release();
        gridOffsetsBuffer?.Release();
    }

    private Vector2 GetMouseWorldPosition()
    {
        if (Mouse.current == null || Camera.main == null) return Vector2.zero;
        Vector3 mouseScreenPos = Mouse.current.position.ReadValue();
        mouseScreenPos.z = Camera.main.nearClipPlane + 10;
        return Camera.main.ScreenToWorldPoint(mouseScreenPos);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(boundsCenter, boundsSize);
    }
}
