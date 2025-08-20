using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UIElements;

public class Displayer : MonoBehaviour
{
    UIController menu;
    public bool running;
    public Mesh mesh;
    #region DisplaySetup
    [Header("DisplaySetup")]
    public Material particleSetupMaterial;
    public Material dummySetupMaterial;
    public Material hashGridMaterial;
    public Material neighbourMaterial;
    #endregion

    #region DisplaySim
    [Header("Display Simulation")]
    public Material particleSimMaterial;
    public Material dummySimMaterial;
    #endregion

    #region Buffers
    [Header("Buffers")]
    internal ComputeBuffer particleMeshBuffer;
    internal ComputeBuffer dummyMeshBuffer;
    public Texture2D gradientTexture;
    public int gradientResolution = 64;
    public Gradient colourMap;
    Bounds bounds;
    #endregion
    public bool needsUpdate = true;
    public float scale;
    public float velocityDisplayMax = 3f;
    public float densityRange = 2f;
    private uint NumberOfParticles;
    private uint NumberOfDummies;
    private uint TotalNumberOfParticles;
    Sim sim;
    public string what_to_display;
    Dictionary<string, int> disp_translation = new Dictionary<string,int>{ { "density", 1 }, { "velocity", 2 } };

    private void Awake()
    {
        running = false;
        menu = GameObject.FindGameObjectWithTag("UIDocument").GetComponent<UIController>();
    }
    public void SetParameters(SetupController spw)
    {
        NumberOfParticles = spw.NumberOfParticles;
        NumberOfDummies = spw.NumberOfDummies;
        TotalNumberOfParticles = NumberOfDummies + NumberOfParticles;
        scale = spw.radius * 2;
    }
    public void StartDisp(SetupController spw)
    {
        running = true;
        SetParameters(spw);
        sim = GetComponent<Sim>();

        particleSimMaterial.SetBuffer("positions", sim.posBuffer);
        particleSimMaterial.SetBuffer("velocities", sim.velBuffer);
        particleSimMaterial.SetBuffer("densities", sim.densityBuffer);

        dummySimMaterial.SetBuffer("positions", sim.posBuffer);

        particleMeshBuffer = ComputeHelper.CreateArgsBuffer(mesh, (int)NumberOfParticles);
        dummyMeshBuffer = ComputeHelper.CreateArgsBuffer(mesh, (int)NumberOfDummies);
        bounds = new Bounds(Vector3.zero, Vector3.one * 10000);
        needsUpdate = true;
        what_to_display = "density";
        
        densityRange = menu.densityDisplaySlider.value * sim.targetDensity;
        velocityDisplayMax = menu.velocityDisplaySlider.value * sim.speedOfSound;

        UpdateSettings();
    }

    public void DisplaySim()
    {
        Graphics.DrawMeshInstancedIndirect(mesh, 0, particleSimMaterial, bounds, particleMeshBuffer);
        Graphics.DrawMeshInstancedIndirect(mesh, 0, dummySimMaterial, bounds, dummyMeshBuffer);

        if (running && GetComponent<InputHandler>().paused)
        {
            ResolvePause();
        }
    }

    public void ResolvePause()
    {
        Matrix4x4[] gridMatrixes = new Matrix4x4[sim.NumXCells * sim.NumYCells];
        int i = 0;
        for (int xNum = 0; xNum < sim.NumXCells; xNum++)
        {
            for (int yNum = 0; yNum < sim.NumYCells; yNum++)
            {
                float xcoord = -sim.box_size[0] / 2 + (xNum + 0.5f) * sim.CellSize.x;
                float ycoord = -sim.box_size[1] / 2 + (yNum + 0.5f) * sim.CellSize.y;
                gridMatrixes[i] = Matrix4x4.TRS(new Vector2(xcoord, ycoord), Quaternion.identity, new Vector3(sim.CellSize.x, sim.CellSize.y, 1));
                i++;
            }
        }
        Graphics.DrawMeshInstanced(mesh, 0, hashGridMaterial, gridMatrixes);

        if (Input.GetMouseButton(0))
        {
            ComputeHelper.Dispatch(sim.compute, 1, kernelIndex: sim.neighboursTESTkernel);
            Vector2[] positions = new Vector2[TotalNumberOfParticles];
            sim.posBuffer.GetData(positions);
            uint[] nIDX = new uint[TotalNumberOfParticles];
            sim.neighboursTEST.GetData(nIDX);
            List<Matrix4x4> neighboursMatrixes = new List<Matrix4x4>();
            foreach (uint idx in nIDX)
            {
                if (idx == TotalNumberOfParticles) break;
                neighboursMatrixes.Add(Matrix4x4.TRS(positions[idx], Quaternion.identity, Vector2.one * sim.radius * 2.5f));
            }
            Graphics.DrawMeshInstanced(mesh, 0, neighbourMaterial, neighboursMatrixes);
        }
    }
    public void DisplaySetup(Vector2[] positions, uint NumberOfParticles, uint NumberOfDummies)
    {
        // Draw particles
            Matrix4x4[] particleMatrices = new Matrix4x4[NumberOfParticles];
            for (int i = 0; i < NumberOfParticles; i++)
            { particleMatrices[i] = Matrix4x4.TRS(positions[i], Quaternion.identity, Vector3.one * scale); }
            Graphics.DrawMeshInstanced(mesh, 0, particleSetupMaterial, particleMatrices);
            // Draw dummies
            Matrix4x4[] dummyMatrices = new Matrix4x4[NumberOfDummies];
            for (int i = 0; i < NumberOfDummies; i++)
            { dummyMatrices[i] = Matrix4x4.TRS(positions[i + NumberOfParticles], Quaternion.identity, Vector3.one * scale); }
            Graphics.DrawMeshInstanced(mesh, 0, dummySetupMaterial, dummyMatrices);
    }
    void LateUpdate()
    {
        if (running && needsUpdate)
        {
            UpdateSettings();
        }
    }

    void UpdateSettings()
    {
        needsUpdate = false;
        TextureFromGradient(ref gradientTexture, gradientResolution, colourMap);
        particleSimMaterial.SetTexture("ColourMap", gradientTexture);
        particleSimMaterial.SetFloat("scale", scale);
        dummySimMaterial.SetFloat("scale", scale);
        dummySimMaterial.SetInt("numberOfParticles", (int)NumberOfParticles);
        particleSimMaterial.SetFloat("velocityMax", velocityDisplayMax);
        particleSimMaterial.SetFloat("targetDensity", GetComponent<Sim>().targetDensity);
        particleSimMaterial.SetInt("what_to_display", disp_translation[what_to_display]);
        particleSimMaterial.SetFloat("densityRange", densityRange);
    }

    public static void TextureFromGradient(ref Texture2D texture, int width, Gradient gradient, FilterMode filterMode = FilterMode.Bilinear)
    {
        if (texture == null)
        {
            texture = new Texture2D(width, 1);
        }
        else if (texture.width != width)
        {
            texture.Reinitialize(width, 1);
        }
        if (gradient == null)
        {
            gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.black, 0), new GradientColorKey(Color.black, 1) },
                new GradientAlphaKey[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) }
            );
        }
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = filterMode;

        Color[] cols = new Color[width];
        for (int i = 0; i < cols.Length; i++)
        {
            float t = i / (cols.Length - 1f);
            cols[i] = gradient.Evaluate(t);
        }
        texture.SetPixels(cols);
        texture.Apply();
    }

    void OnValidate()
    {
        needsUpdate = true;
    }
}
