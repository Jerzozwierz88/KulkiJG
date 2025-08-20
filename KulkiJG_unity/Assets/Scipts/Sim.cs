using static UnityEngine.Mathf;
using UnityEngine;
using System.Diagnostics;
using Unity.Mathematics;
public class Sim : MonoBehaviour
{
    #region Parametry symulacji
    [Header("Parametry symulacji")]
    public bool running;
    internal uint NumberOfParticles;
    public uint TotalNumberOfParticles;
    public float radius;
    public float dt;
    public float time;
    public float refreshRate;
    internal Vector2 box_size;
    public float gravity;
    public float centrifugal_force;
    internal const float gamma = 7;
    public float interaction_radius;
    public int evolutionsPerFrame;
    internal uint NumberOfDummies;
    private bool settingsNeedUpdate = false;
    float obstacle_radius;
    #endregion

    #region Parametry SPH
    [Header("SPH parametry")]
    public float targetDensity;
    public float speedOfSound;
    public float viscosity;
    public float artificialViscosityStrength;
    #endregion

    #region Compute Shader
    [Header("Compute Shader")]
    public ComputeShader compute;
    public ComputeShader bitonic;
    internal ComputeBuffer posBuffer;
    internal ComputeBuffer velBuffer;
    internal ComputeBuffer densityBuffer;
    internal ComputeBuffer pressureBuffer;
    internal ComputeBuffer lookupTable;
    internal ComputeBuffer startLookupIndexes;
    internal GPUSort gpuSort;
    ComputeBuffer pos_temp;
    ComputeBuffer vel_temp;
    ComputeBuffer rho_temp;
    internal ComputeBuffer massBuffer;
    internal ComputeBuffer neighboursTEST;
    #endregion

    #region Hash
    [Header("Hash")]
    internal uint HashCount;
    internal uint NumXCells;
    internal uint NumYCells;
    internal float2 CellSize;
    #endregion

    #region Kernels
    int runawayKernel;
    int halfStepKernel;
    int updateValuesKernel;
    int finalStepKernel;
    int hashKernel;
    public int neighboursTESTkernel;
    int setParticlesKernel;
    int setWallKernel;
    #endregion
    private SetupController setup;
    private void Awake()
    {
        running = false;
        setup = GameObject.FindGameObjectWithTag("Setup").GetComponent<SetupController>();
    }
    public void StartSim()
    {
        running = true;
        SetupController spw = setup.GetComponent<SetupController>();

        refreshRate = 1 / (float)Screen.currentResolution.refreshRateRatio.value;

        NumberOfParticles = spw.NumberOfParticles;
        NumberOfDummies = spw.NumberOfDummies;

        radius = spw.radius;
        interaction_radius = spw.interaction_radius;
        box_size = spw.box_size;
        gravity = spw.gravity;
        targetDensity = spw.targetDensity;
        // targetDensity = NumberOfParticles / (box_size[0] * box_size[1]);
        obstacle_radius = spw.obstacle_radius;
        TotalNumberOfParticles = NumberOfParticles + NumberOfDummies;

        HashCount = TotalNumberOfParticles + 13;
        NumXCells = (uint)(box_size[0] / interaction_radius);
        NumYCells = (uint)(box_size[1] / interaction_radius);
        CellSize.x = box_size[0] / NumXCells;
        CellSize.y = box_size[1] / NumYCells;

        time = 0f;

        // Create Compute Buffers
        InitiateComputeShaders(spw);
        UpdateValuesInComputeShaders();

        gpuSort.PerformAllHashingSteps();
        ComputeHelper.Dispatch(compute, (int)NumberOfParticles, kernelIndex: setParticlesKernel);
        ComputeHelper.Dispatch(compute, (int)NumberOfDummies, kernelIndex: setWallKernel);
    }
    void InitiateComputeShaders(SetupController spw)
    {
        #region Set Kernels
        runawayKernel = compute.FindKernel("Runaway");

        halfStepKernel = compute.FindKernel("HalfStep");
        updateValuesKernel = compute.FindKernel("UpdateBuffersWithTempValues");
        finalStepKernel = compute.FindKernel("FinalStep");

        neighboursTESTkernel = compute.FindKernel("FindNeighbours");

        setParticlesKernel = compute.FindKernel("SetParticleDensities");
        setWallKernel = compute.FindKernel("SetWallDensities");

        int[] allKernels = {runawayKernel, neighboursTESTkernel,
        halfStepKernel, updateValuesKernel, finalStepKernel,
        setParticlesKernel, setWallKernel};
        #endregion

        #region Set Compute Buffers
        neighboursTEST = new ComputeBuffer((int)TotalNumberOfParticles, sizeof(uint));
        posBuffer = new ComputeBuffer((int)TotalNumberOfParticles, sizeof(float) * 2);
        velBuffer = new ComputeBuffer((int)TotalNumberOfParticles, sizeof(float) * 2);
        densityBuffer = new ComputeBuffer((int)TotalNumberOfParticles, sizeof(float));
        pressureBuffer = new ComputeBuffer((int)TotalNumberOfParticles, sizeof(float));
        gpuSort = new GPUSort();
        startLookupIndexes = new ComputeBuffer((int)HashCount, sizeof(uint));
        lookupTable = new ComputeBuffer((int)TotalNumberOfParticles, 2 * sizeof(uint));
        gpuSort.SetBuffers(lookupTable, startLookupIndexes);


        pos_temp = new ComputeBuffer((int)TotalNumberOfParticles, sizeof(float) * 2);
        vel_temp = new ComputeBuffer((int)TotalNumberOfParticles, sizeof(float) * 2);
        rho_temp = new ComputeBuffer((int)TotalNumberOfParticles, sizeof(float));
        massBuffer = new ComputeBuffer((int)TotalNumberOfParticles, sizeof(float));

        posBuffer.SetData(spw.positions);
        velBuffer.SetData(spw.velocities);
        densityBuffer.SetData(spw.densities);
        float[] mass = new float[TotalNumberOfParticles];
        for (int i = 0; i < NumberOfParticles; i++)
        {
            // mass[i] = densities[i] * 16*radius*radius;
            mass[i] = 1f;
        }
        for (uint i = NumberOfParticles; i < TotalNumberOfParticles; i++)
        {
            mass[i] = 2f / SetupController.part_dummy_ratio / SetupController.part_dummy_ratio;
        }
        massBuffer.SetData(mass);

        ComputeHelper.SetBuffer(compute, neighboursTEST, "neighboursTest", neighboursTESTkernel);
        ComputeHelper.SetBuffer(compute, posBuffer, "positions", allKernels);
        ComputeHelper.SetBuffer(compute, velBuffer, "velocities", allKernels);
        ComputeHelper.SetBuffer(compute, densityBuffer, "densities", allKernels);
        ComputeHelper.SetBuffer(compute, lookupTable, "lookupTable", allKernels);
        ComputeHelper.SetBuffer(compute, startLookupIndexes, "startLookupIndexes", allKernels);
        ComputeHelper.SetBuffer(compute, pressureBuffer, "pressures", allKernels);

        ComputeHelper.SetBuffer(compute, pos_temp, "pos_temp", allKernels);
        ComputeHelper.SetBuffer(compute, vel_temp, "vel_temp", allKernels);
        ComputeHelper.SetBuffer(compute, rho_temp, "rho_temp", allKernels);
        ComputeHelper.SetBuffer(compute, massBuffer, "mass", allKernels);
        #endregion

        #region Set Parameters
        compute.SetInt("totalNumberOfParticles", (int)TotalNumberOfParticles);
        compute.SetInt("numberOfParticles", (int)NumberOfParticles);
        compute.SetVector("box_size", new Vector4(box_size[0], box_size[1], 0, 0));
        compute.SetFloat("interaction_radius", interaction_radius);
        // compute.SetFloat("h", interaction_radius * 0.5f);
        compute.SetFloats("cell_size", new float[2] { CellSize.x, CellSize.y });
        compute.SetFloat("tdamp", 1f);
        #endregion

        // Set GPU Hashing Compute Shader
        hashKernel = bitonic.FindKernel("Hash");
        bitonic.SetBuffer(hashKernel, "positions", posBuffer);
        bitonic.SetVector("box_size", new Vector4(box_size[0], box_size[1], 0, 0));
        bitonic.SetInt("totalNumberOfParticles", (int)TotalNumberOfParticles);
        bitonic.SetFloat("interaction_radius", interaction_radius);
        bitonic.SetFloats("cell_size", new float[2] { CellSize.x, CellSize.y });
    }
    public void UpdateValuesInComputeShaders()
    {
        compute.SetFloat("radius", radius);
        compute.SetFloat("gravity", gravity);
        compute.SetFloat("speedOfSound", speedOfSound);
        compute.SetFloat("artVisStrength", artificialViscosityStrength);
        compute.SetFloat("viscosity", viscosity);
        compute.SetFloat("splineZeroValue", 10 / (7 * PI * interaction_radius * interaction_radius / 4));
        compute.SetFloat("targetDensity", targetDensity);
        compute.SetFloat("referencePressure", targetDensity * speedOfSound * speedOfSound / gamma);
        compute.SetFloat("gamma", gamma);
        compute.SetInt("hashCount", (int)HashCount);
        compute.SetInt("numXCells", (int)NumXCells);
        compute.SetInt("numYCells", (int)NumYCells);
        compute.SetFloat("centrifugal_force", centrifugal_force);
        compute.SetFloat("time", time);

        float h = interaction_radius / 2;
        compute.SetFloat("h", h);
        compute.SetFloat("spline_scale", 7 / (64 * PI * h * h));
        compute.SetFloat("spline_derivative_scale", 35 / (32 * PI * h * h * h));
    }
    void OnValidate()
    {
        if (!running) { return; }
        settingsNeedUpdate = true;
    }
    void Update()
    {
        // Update Settings
        if (settingsNeedUpdate) { UpdateValuesInComputeShaders(); }
        // Check if simulation has started
        if (!running) { return; }
        // Display particles
        Stopwatch zegarek = new Stopwatch();
        zegarek.Start();
        Vector2 mouse_pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        compute.SetFloats("mouse_position", new float[2] { mouse_pos[0], mouse_pos[1] });
        GetComponent<Displayer>().DisplaySim();
        zegarek.Stop();
        // UnityEngine.Debug.Log("Time to display: " + zegarek.Elapsed.TotalMilliseconds);
        // Check if simulation is paused
        if (GetComponent<InputHandler>().paused) { return; }
        // Setup timestep (based on last frame time execution)
        float h = interaction_radius / 2;
        dt = 0.5f*Min(0.25f * h / speedOfSound, 0.125f * h*h / viscosity, 0.25f * Sqrt(h / Max(Abs(gravity), Abs(centrifugal_force*centrifugal_force))));
        evolutionsPerFrame = Max(FloorToInt(refreshRate / dt), 1);

        float dt_simulation = dt * evolutionsPerFrame;
        if (dt_simulation > Time.deltaTime) dt = Time.deltaTime / evolutionsPerFrame;
        // Skip frame if time step is to large
        if (dt > 0.1) { return; }
        // Perform evolution steps
        zegarek.Restart();
        for (int i = 0; i < evolutionsPerFrame; i++) { UpdateStep(); }
        zegarek.Stop();
        // UnityEngine.Debug.Log("Time to perform evolution steps: " + zegarek.Elapsed.TotalMilliseconds);
    }
    private void UpdateStep()
    {
        compute.SetFloat("dt", dt);
        time += dt;
        compute.SetFloat("time", time);
        compute.SetBool("dragging", false);
        // Resolve drag
        if (GetComponent<InputHandler>().isDragging)
        {
            float br = GetComponent<InputHandler>().bucket_radius;
            float force_value = GetComponent<InputHandler>().sign * GetComponent<InputHandler>().force_strength * speedOfSound;

            compute.SetBool("dragging", true);
            compute.SetFloat("bucket_radius", br);
            compute.SetFloat("bucket_force", force_value);
        }

        // Get Update Lookup Table - for nearest neighbour search
        gpuSort.PerformAllHashingSteps();

        // Perform single Verlet evolution in steps
        ComputeHelper.Dispatch(compute, (int)NumberOfParticles, kernelIndex: halfStepKernel);
        ComputeHelper.Dispatch(compute, (int)NumberOfParticles, kernelIndex: updateValuesKernel);
        ComputeHelper.Dispatch(compute, (int)NumberOfParticles, kernelIndex: finalStepKernel);
        // Fix runaway particles
        ComputeHelper.Dispatch(compute, (int)NumberOfParticles, kernelIndex: runawayKernel);
    }
    internal void ReleaseBuffers()
    {
        ComputeHelper.Release(posBuffer, velBuffer, densityBuffer, pressureBuffer,
         lookupTable, startLookupIndexes, GetComponent<Displayer>().particleMeshBuffer, GetComponent<Displayer>().dummyMeshBuffer,
         neighboursTEST, pos_temp, vel_temp, rho_temp, massBuffer);
    }
    void OnDestroy()
    {
        ReleaseBuffers();
    }
}