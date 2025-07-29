using UnityEngine;
using UnityEngine.InputSystem;

public class ParticleController : MonoBehaviour
{
    [Header("Configurações")]
    public int particleCount = 100000;
    public float particleSize = 0.05f;
    public Vector2 gravity = new Vector2(0.0f, -0.5f);
    public float drag = 0.1f; // Um pouco de atrito

    [Header("Limites (Bounds)")]
    public Vector2 boundsSize = new Vector2(16, 9);
    public Vector2 boundsCenter = Vector2.zero;

    [Header("Interação com Mouse")]
    public float interactionRadius = 2.0f;
    public float interactionStrength = 20.0f;

    [Header("Referências")]
    public ComputeShader computeShader;
    public Material particleMaterial;

    // Buffers que viverão na memória da GPU
    private ComputeBuffer positionsBuffer;
    private ComputeBuffer velocitiesBuffer;

    private int kernelHandle;
    private static readonly int
        PositionsID = Shader.PropertyToID("_Positions"),
        VelocitiesID = Shader.PropertyToID("_Velocities"),
        DeltaTimeID = Shader.PropertyToID("_DeltaTime"),
        GravityID = Shader.PropertyToID("_Gravity"),
        DragID = Shader.PropertyToID("_Drag"),
        MousePosID = Shader.PropertyToID("_MousePos"),
        InteractionRadiusID = Shader.PropertyToID("_InteractionRadius"),
        InteractionStrengthID = Shader.PropertyToID("_InteractionStrength"),
        ParticleCountID = Shader.PropertyToID("_ParticleCount"),
        ParticleSizeID = Shader.PropertyToID("_ParticleSize"),
        BoundsMinID = Shader.PropertyToID("_BoundsMin"),
        BoundsMaxID = Shader.PropertyToID("_BoundsMax");


    void Start()
    {
        InitializeBuffers();
        SetupShaderParameters();
    }

    void InitializeBuffers()
    {
        positionsBuffer = new ComputeBuffer(particleCount, sizeof(float) * 2);
        velocitiesBuffer = new ComputeBuffer(particleCount, sizeof(float) * 2);

        Vector2[] initialPositions = new Vector2[particleCount];
        Vector2[] initialVelocities = new Vector2[particleCount];

        // Calcula os limites para usar na inicialização
        Vector2 boundsMin = boundsCenter - boundsSize * 0.5f;
        Vector2 boundsMax = boundsCenter + boundsSize * 0.5f;

        for (int i = 0; i < particleCount; i++)
        {
            // Gera partículas dentro dos limites definidos
            initialPositions[i] = new Vector2(
                Random.Range(boundsMin.x, boundsMax.x),
                Random.Range(boundsMin.y, boundsMax.y)
            );
            initialVelocities[i] = Vector2.zero;
        }

        positionsBuffer.SetData(initialPositions);
        velocitiesBuffer.SetData(initialVelocities);
    }

    void SetupShaderParameters()
    {
        kernelHandle = computeShader.FindKernel("CSMain");
        computeShader.SetBuffer(kernelHandle, PositionsID, positionsBuffer);
        computeShader.SetBuffer(kernelHandle, VelocitiesID, velocitiesBuffer);
        particleMaterial.SetBuffer(PositionsID, positionsBuffer);
        particleMaterial.SetInt(ParticleCountID, particleCount);
        particleMaterial.SetFloat(ParticleSizeID, particleSize);
    }

    void Update()
    {
        // Envia as variáveis que mudam a cada frame para o compute shader
        computeShader.SetFloat(DeltaTimeID, Time.deltaTime);
        computeShader.SetVector(GravityID, gravity);
        computeShader.SetFloat(DragID, drag);

        // Interação com o mouse
        Vector2 mousePos = GetMouseWorldPosition();
        computeShader.SetVector(MousePosID, mousePos);
        computeShader.SetFloat(InteractionRadiusID, interactionRadius);
        computeShader.SetFloat(InteractionStrengthID, interactionStrength);

        // Envia os limites para o shader
        Vector2 boundsMin = boundsCenter - boundsSize * 0.5f;
        Vector2 boundsMax = boundsCenter + boundsSize * 0.5f;
        computeShader.SetVector(BoundsMinID, boundsMin);
        computeShader.SetVector(BoundsMaxID, boundsMax);

        // Dispara o compute shader!
        int threadGroups = Mathf.CeilToInt(particleCount / 256.0f);
        computeShader.Dispatch(kernelHandle, threadGroups, 1, 1);
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
    }

    private Vector2 GetMouseWorldPosition()
    {
        if (Mouse.current == null || Camera.main == null)
        {
            return Vector2.zero;
        }
        Vector3 mouseScreenPos = Mouse.current.position.ReadValue();
        mouseScreenPos.z = Camera.main.nearClipPlane + 10;
        return Camera.main.ScreenToWorldPoint(mouseScreenPos);
    }

    // Desenha os Gizmos na Scene View para visualização
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(boundsCenter, boundsSize);
    }
}
