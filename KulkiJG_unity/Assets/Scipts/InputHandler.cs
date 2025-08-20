using UnityEngine;

public class InputHandler : MonoBehaviour
{
    public bool running;
    public Vector2 mouse_pos;
    public GameObject squareSprite;
    public bool SpaceDown;
    public bool paused;
    public GameObject gridSquareSprite;
    private GameObject ui;
    private GameObject setup;

    private void Awake()
    {
        running = false;
        ui = GameObject.FindGameObjectWithTag("UIDocument");
        setup = GameObject.FindGameObjectWithTag("Setup");
    }

    public void StartInputHandling()
    {
        running = true;
        paused = true;
        SpaceDown = false;
        isDragging = false;
    }

    private void Update()
    {
        if (!running) { return; }
        ManageInput();
        if (!paused)
        {
            ResolveDrag();
            return;
        }
    }

    public void ManageInput()
    {
        if (setup.activeSelf) { return; }
        if (ui.activeSelf) { paused = true; }
        if (!ui.activeSelf && !SpaceDown) { paused = false; }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (!SpaceDown)
            { 
                paused = true; 
                SpaceDown = true;
            }
            else
            {
                paused = false;
                SpaceDown = false;
            }
        }

        if (Input.GetMouseButtonDown(0)) {leftMouseButtonDown = true;}
        if (Input.GetMouseButtonDown(1)) {rightMouseButtonDown = true;}
        if (Input.GetMouseButtonUp(0)) {leftMouseButtonDown = false;}
        if (Input.GetMouseButtonUp(1)) {rightMouseButtonDown = false;}

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            paused = true;
            ui.SetActive(true);
        }
    }

    public bool isDragging;
    public GameObject bucketSprite;
    private GameObject bucket;
    public float bucket_radius;
    public float force_strength;
    public int sign;
    public bool leftMouseButtonDown = false;
    public bool rightMouseButtonDown = false;
    public void ResolveDrag()
    {
        if ((rightMouseButtonDown || leftMouseButtonDown) && !isDragging)
        {
            isDragging = true;
            bucket = Instantiate(bucketSprite, Camera.main.ScreenToWorldPoint(Input.mousePosition), Quaternion.identity);
            bucket.transform.localScale = Vector3.one * bucket_radius * 2;
        }
        if (!leftMouseButtonDown && !rightMouseButtonDown)
        {
            isDragging = false;
            if (bucket != null) { Destroy(bucket); }
        }
        if (!isDragging) { return; }
        if (leftMouseButtonDown) { sign = 1; }
        else if (rightMouseButtonDown) { sign = -1; }
        mouse_pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        bucket.transform.position = mouse_pos;
    }
}