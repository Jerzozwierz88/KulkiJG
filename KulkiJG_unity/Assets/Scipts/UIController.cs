using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UIElements;

public class UIController : MonoBehaviour
{
    private VisualElement ui;
    private Sim sim;
    private GameObject setup;
    private Displayer displayer;
    private InputHandler input;

    private Button playOn;
    private Button reset;
    private Slider gravity;
    private Slider density;
    Slider speedOfSound;
    Slider viscosity;
    Button quit;


    #region DisplayOptions
    private VisualElement submenuContainer;
    private Dictionary<string, VisualElement> displaySubmenus = new Dictionary<string, VisualElement>();
    Button leftArrow;
    Button rightArrow;
    internal readonly string[] submenuNames = new[] { "velocity", "density" };
    internal int submenuIndex;
    internal Slider velocityDisplaySlider;
    internal Slider densityDisplaySlider;
    #endregion
    #region Bucket
    Slider BucketRadiusSlider;
    Slider BucketForceSlider;
    #endregion
    private void Awake()
    {
        sim = GameObject.FindGameObjectWithTag("Sim").GetComponent<Sim>();
        input = GameObject.FindGameObjectWithTag("Sim").GetComponent<InputHandler>();
        setup = GameObject.FindGameObjectWithTag("Setup");
        displayer = GameObject.FindGameObjectWithTag("Sim").GetComponent<Displayer>();

        foreach (string submenuName in submenuNames)
        {
            displaySubmenus.Add(submenuName, LoadSubmenu(submenuName));
        }
        submenuIndex = 1;
    }

    private void Start()
    {
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        ui = GetComponent<UIDocument>().rootVisualElement;

        #region Setup Buttons

        playOn = ui.Q<Button>("PlayOn");
        playOn.RegisterCallback<MouseUpEvent>(OnPlayOnClick);

        reset = ui.Q<Button>("Reset");
        reset.RegisterCallback<MouseUpEvent>(OnResetClick);

        quit = ui.Q<Button>("Quit");
        quit.RegisterCallback<MouseUpEvent>((evt) =>
        {
            Application.Quit();
            Debug.Log("Closing the game");
        });

        #endregion

        #region Setup Sliders
        gravity = ui.Q<Slider>("GravityMenu");
        gravity.RegisterCallback<ChangeEvent<float>>(ChangeGravity);
        gravity.value = Mathf.Abs(sim.gravity);

        density = ui.Q<Slider>("Density");
        density.RegisterCallback<ChangeEvent<float>>(ChangeDensity);
        density.value = Mathf.Sqrt(sim.targetDensity);

        speedOfSound = ui.Q<Slider>("SpeedOfSound");
        speedOfSound.RegisterCallback<ChangeEvent<float>>((evt) =>
        {
            sim.speedOfSound = evt.newValue;
        });
        speedOfSound.value = sim.speedOfSound;

        viscosity = ui.Q<Slider>("Viscosity");
        viscosity.RegisterCallback<ChangeEvent<float>>((evt) =>
        {
            sim.viscosity = evt.newValue;
        });
        viscosity.value = sim.viscosity;


        BucketRadiusSlider = ui.Q<Slider>("BucketRadius");
        BucketRadiusSlider.RegisterCallback<ChangeEvent<float>>((evt) =>
        {
            input.bucket_radius = evt.newValue;
        });
        BucketRadiusSlider.value = input.bucket_radius;

        BucketForceSlider = ui.Q<Slider>("BucketForce");
        BucketForceSlider.RegisterCallback<ChangeEvent<float>>((evt) =>
        {
            input.force_strength = evt.newValue;
        });
        BucketForceSlider.value = input.force_strength;

        #endregion

        submenuContainer = ui.Q<VisualElement>("submenuContainer");
        rightArrow = ui.Q<Button>("RightArrow");
        leftArrow = ui.Q<Button>("LeftArrow");
        rightArrow.RegisterCallback<MouseUpEvent>(OnArrowRightClick);
        leftArrow.RegisterCallback<MouseUpEvent>(OnArrowLeftClick);
        ShowSubmenu(submenuNames[submenuIndex]);
    }
    private void OnDisable()
    {
        playOn.UnregisterCallback<MouseUpEvent>(OnPlayOnClick);
        reset.UnregisterCallback<MouseUpEvent>(OnResetClick);
    }

    private void OnPlayOnClick(MouseUpEvent evt)
    {
        sim.UpdateValuesInComputeShaders();
        gameObject.SetActive(false);
    }
    private void OnResetClick(MouseUpEvent evt)
    {
        sim.GetComponent<InputHandler>().running = false;
        sim.running = false;
        sim.GetComponent<Displayer>().running = false;
        sim.ReleaseBuffers();
        setup.SetActive(true);
        gameObject.SetActive(false);
    }

    private void ChangeGravity(ChangeEvent<float> evt)
    {
        Debug.Log("Changed gravity");
        sim.gravity = -evt.newValue;
    }

    private void ChangeDensity(ChangeEvent<float> evt)
    {
        Debug.Log("Changed density");
        float target_density = evt.newValue * evt.newValue;
        sim.targetDensity = target_density;
        GameObject.FindGameObjectWithTag("Sim").GetComponent<Displayer>().particleSimMaterial.SetFloat("targetDensity", target_density);
    }


    internal VisualElement LoadSubmenu(string submenuName)
    {
        VisualTreeAsset LoadUXML(string path)
        {
            var asset = Resources.Load<VisualTreeAsset>(path);
            if (asset == null)
            {
                Debug.LogError("Could not load UXML at path: " + path);
            }
            return asset;
        }
        // Map submenuName to UXML path in Resources folder (or use AssetDatabase in Editor)
        string uxmlPath = submenuName switch
        {
            "velocity" => "Submenus/Velocity",
            "density" => "Submenus/Density",
            _ => null
        };

        if (string.IsNullOrEmpty(uxmlPath)) return new VisualElement();

        var submenuAsset = LoadUXML(uxmlPath);
        if (submenuAsset == null) return new VisualElement();
        VisualElement submenu = submenuAsset.CloneTree();
        switch (submenuName)
        {
            case "velocity":
                velocityDisplaySlider = submenu.Q<Slider>("VelocityDisplay");
                velocityDisplaySlider.RegisterCallback<ChangeEvent<float>>((evt) =>
                {
                    displayer.velocityDisplayMax = sim.speedOfSound * evt.newValue;
                    displayer.needsUpdate = true;
                });
                velocityDisplaySlider.value = 0.3f;
                break;
            case "density":
                densityDisplaySlider = submenu.Q<Slider>("DensityDisplay");
                densityDisplaySlider.RegisterCallback<ChangeEvent<float>>((evt) =>
                {
                    displayer.densityRange = sim.targetDensity * evt.newValue;
                    displayer.needsUpdate = true;
                });
                densityDisplaySlider.value = 0.3f;
                break;
            default:
                Debug.LogError("Wrong name!");
                break;
        }
        return submenu;
    }

    internal void ShowSubmenu(string submenuName)
    {
        submenuContainer.Clear();
        submenuContainer.Add(displaySubmenus[submenuName]);
    }

    private void OnArrowRightClick(MouseUpEvent evt)
    {
        submenuIndex = (submenuIndex + 1) % submenuNames.Length;
        ShowSubmenu(submenuNames[submenuIndex]);

        // Update your displayer accordingly:
        displayer.what_to_display = submenuNames[submenuIndex];
        displayer.needsUpdate = true;

        Debug.Log("Right arrow clicked");
    }

    private void OnArrowLeftClick(MouseUpEvent evt)
    {
        submenuIndex = (submenuIndex - 1 + submenuNames.Length) % submenuNames.Length;
        ShowSubmenu(submenuNames[submenuIndex]);

        // Update your displayer accordingly:
        displayer.what_to_display = submenuNames[submenuIndex];
        displayer.needsUpdate = true;
    }


}
