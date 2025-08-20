using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.Mathf;
using System.Collections.Generic;

public class SetupController : MonoBehaviour
{
    #region start_menu
    [Header("Start Menu")] 
    private GameObject simulation;
    private GameObject menu;
    private VisualElement setup;
    private Button buttonStart;
    private Slider sliderRadius;
    private Slider sliderGravity;
    private UnsignedIntegerField numberOfParticles;
    private Toggle obstacleSwitch;
    #endregion

    #region parameters
    [Header("Parameters")]
    private bool respawn;
    private Vector2 max_box_size;
    public Vector2 box_size;
    public Vector2 obstacle_center;
    public float obstacle_radius;
    public float radius;
    public uint NumberOfParticles;
    private uint TargetNumberOfParticles;
    public uint NumberOfDummies;
    public float targetDensity;
    public float gravity;

    public float interaction_radius;
    public Vector2[] positions;
    public Vector2[] velocities;
    public float[] densities;

    public bool showObstacle = false;
    #endregion

    #region constant_values
    const float max_speed = 0.03f;
    const float particle_spacing_multiplier = 4f;
    const float dummy_spacing_multiplier = 2f;
    internal const float part_dummy_ratio = particle_spacing_multiplier / dummy_spacing_multiplier;
    #endregion
    private void Awake()
    {
        setup = GetComponent<UIDocument>().rootVisualElement;
        simulation = GameObject.FindGameObjectWithTag("Sim");
        menu = GameObject.FindGameObjectWithTag("UIDocument");
        radius = 0.05f;
        NumberOfParticles = 3000;
        gravity = 0;
        TargetNumberOfParticles = NumberOfParticles;
        max_box_size = new Vector2(25f, 14f);
        respawn = false;
        TargetDensity();
        obstacle_radius = 0.2f * box_size[1];
        obstacle_center = Vector2.zero;
        showObstacle = false;
    }
    private void TargetDensity()
    {
        targetDensity = Pow(1 / (particle_spacing_multiplier * radius), 2);
    }
    private void InteractionRadius()
    {
        interaction_radius = 8 * radius;
        box_size = max_box_size - 2 * Vector2.one * interaction_radius;
    }
    List<Vector2> GetRectangle(Vector2 center, float a, float b, float dx, float dy)
    {
        List<Vector2> rectangle = new List<Vector2>();
        for (float x = center[0] - a/2 + Epsilon; x < center[0] + a/2; x += dx)
        {
            for (float y = center[1]-b/2 + Epsilon; y < center[1] + b/2; y += dy)
            {
                float jitter = radius/4;
                rectangle.Add(new Vector2(x + UnityEngine.Random.Range(-jitter, jitter), y + UnityEngine.Random.Range(-jitter, jitter)));
            }
        }
        return rectangle;
    }
    List<Vector2> GetCircle(Vector2 center, float r, float width, float dx)
    {
        List<Vector2> circle = new List<Vector2>();
        for (float a = r - width; a < r; a += dx)
        {
            float fi0 = Random.Range(0, 2 * PI);
            int nfi = FloorToInt(2 * PI / dx * a);
            float dfi = 2 * PI / nfi;
            for (float fi = fi0; fi < 2 * PI + fi0; fi += dfi)
            {
                float jitter = radius/2;
                Vector2 dummy_pos = center + a * new Vector2(Cos(fi), Sin(fi));
                dummy_pos.x += Random.Range(-jitter, jitter);
                dummy_pos.y += Random.Range(-jitter, jitter);
                circle.Add(dummy_pos);
            }
        }
        return circle;
    }
    private void OnEnable()
    {
        InteractionRadius();
        RespawnCircles();
        respawn = true;
        setup = GetComponent<UIDocument>().rootVisualElement;

        buttonStart = setup.Q<Button>("Play");
        buttonStart.clicked += ButtonPlayClicked;

        sliderRadius = setup.Q<Slider>("Radius");
        sliderRadius.value = Log10(radius);
        sliderRadius.RegisterCallback<ChangeEvent<float>>((evt) =>
        {
            radius = Pow(10f, evt.newValue);
            TargetDensity();
            InteractionRadius();
            respawn = true;
        });

        sliderGravity = setup.Q<Slider>("Gravity");
        sliderGravity.value = gravity;
        sliderGravity.RegisterCallback<ChangeEvent<float>>((evt) =>
        {
            gravity = -evt.newValue;
            respawn = true;
        });

        numberOfParticles = setup.Q<UnsignedIntegerField>("NumberOfParticles");
        numberOfParticles.value = TargetNumberOfParticles;
        numberOfParticles.RegisterCallback<ChangeEvent<uint>>((evt) =>
        {
            TargetNumberOfParticles = (uint)Clamp(evt.newValue, 1, 100000);
            respawn = true;
        });

        obstacleSwitch = setup.Q<Toggle>("ObstacleSwitch");
        obstacleSwitch.value = showObstacle;
        obstacleSwitch.RegisterCallback<ChangeEvent<bool>>((evt) =>
        {
            showObstacle = evt.newValue;
            respawn = true;
        });
    }
    private void OnValidate()
    {
        respawn = true;
    }
    public void ButtonPlayClicked()
    {
        simulation.GetComponent<Sim>().StartSim();
        simulation.GetComponent<InputHandler>().StartInputHandling();
        simulation.GetComponent<Displayer>().StartDisp(this);
        UIController ui = menu.GetComponent<UIController>();
        ui.ShowSubmenu(ui.submenuNames[ui.submenuIndex]);
        Debug.Log("Zaczynam symulacj�!");
        gameObject.SetActive(false);
    }
    public void Update()
    {
        if (respawn)
        {
            NumberOfParticles = TargetNumberOfParticles;
            RespawnCircles();
            respawn = false;
        }
        Displayer disp = simulation.GetComponent<Displayer>();
        disp.SetParameters(this);
        disp.DisplaySetup(positions, NumberOfParticles, NumberOfDummies);
        numberOfParticles.value = TargetNumberOfParticles;
    }
    public void RespawnCircles()
    {
        List<Vector2> SetupParticles()
        {
            // Arrange particles into starting positions
            NumberOfParticles = (uint)Clamp((float)TargetNumberOfParticles, 4, 100000);
            float dx = particle_spacing_multiplier * radius;
            float safe_space = dx + interaction_radius;
            int i = 0;
            List<Vector2> pos = new List<Vector2>();
            for (float y = -box_size[1] / 2 + safe_space; y < box_size[1] / 2 - safe_space; y += dx)
            {
                if (i >= NumberOfParticles) { break; }
                for (float x = -box_size[0] / 2 + safe_space; x < box_size[0] / 2 - safe_space; x += dx)
                {
                    if (i >= NumberOfParticles) { break; }
                    Vector2 point = new Vector2(x, y);
                    float jitter = radius / 8;
                    point += new Vector2(Random.Range(-jitter, jitter), Random.Range(-jitter, jitter));
                    if (showObstacle && (point - obstacle_center).magnitude < obstacle_radius + dx) { continue; }
                    pos.Add(point);
                    i++;
                }
            }
            // return GetCircle(Vector2.zero, obstacle_radius, obstacle_radius, dx);
            return pos;
        }
        List<Vector2> SetupDummies()
        {
            //Dummy Particles
            float dx = dummy_spacing_multiplier * radius;
            float dy = dx;
            // Setup box
            System.Func<Vector2, List<Vector2>> GetRect = center => GetRectangle(center, interaction_radius, interaction_radius, dx, dy);
            List<Vector2> left = GetRectangle(new Vector2(-box_size[0] / 2 + interaction_radius / 2, 0), interaction_radius, box_size[1], dx, dy);
            List<Vector2> right = GetRectangle(new Vector2(box_size[0] / 2 - interaction_radius / 2, 0), interaction_radius, box_size[1], dx, dy);
            List<Vector2> down = GetRectangle(new Vector2(0, -box_size[1] / 2 + interaction_radius / 2), box_size[0], interaction_radius, dx, dy);
            List<Vector2> up = GetRectangle(new Vector2(0, box_size[1] / 2 - interaction_radius / 2), box_size[0], interaction_radius, dx, dy);
            List<Vector2> dummyBox = new List<Vector2>();
            dummyBox.AddRange(left);
            dummyBox.AddRange(right);
            dummyBox.AddRange(down);
            dummyBox.AddRange(up);

            List<Vector2> obstacle = GetCircle(obstacle_center, obstacle_radius, 1.2f * interaction_radius, dx);
            List<Vector2> dummyParticles = new List<Vector2>();
            dummyParticles.AddRange(dummyBox);
            if (showObstacle) { dummyParticles.AddRange(obstacle); }
            return dummyParticles;
        }
        Vector2[] particlePositions = SetupParticles().ToArray();
        NumberOfParticles = (uint)particlePositions.Length;
        Vector2[] dummyPositions = SetupDummies().ToArray();
        NumberOfDummies = (uint)dummyPositions.Length;

        positions = new Vector2[NumberOfParticles + NumberOfDummies];
        particlePositions.CopyTo(positions, 0);
        dummyPositions.CopyTo(positions, NumberOfParticles);

        velocities = new Vector2[NumberOfParticles + NumberOfDummies];
        for (uint j = 0; j < NumberOfParticles; j++)
        {
            velocities[j].x = Random.Range(-max_speed, max_speed);
            velocities[j].y = Random.Range(-max_speed, max_speed);
        }
        for (uint j = NumberOfParticles; j < NumberOfParticles + NumberOfDummies; j++)
        {
            velocities[j] = Vector2.zero;
        }

        densities = new float[NumberOfParticles + NumberOfDummies];
        for (uint j = 0; j < NumberOfParticles; j++) { densities[j] = targetDensity; }
        // for (uint j = 0; j < NumberOfDummies + NumberOfParticles; j++) { densities[j] = targetDensity / part_dummy_ratio/part_dummy_ratio; }
        for (uint j = 0; j < NumberOfDummies + NumberOfParticles; j++) { densities[j] = targetDensity; }
    }
}
