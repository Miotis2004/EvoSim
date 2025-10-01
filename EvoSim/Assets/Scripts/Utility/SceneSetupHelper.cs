using UnityEngine;
using EvoSim.Authoring;
using EvoSim.Data;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace EvoSim.Utilities
{
    /// <summary>
    /// Helper script to quickly set up a basic EvoSim scene
    /// Add to an empty GameObject and run SetupScene() from the Inspector
    /// </summary>
    public class SceneSetupHelper : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("The simulation config to use (leave empty to create default)")]
        public SimulationConfig simulationConfig;

        [Header("Materials (Optional)")]
        [Tooltip("Leave empty to create basic materials")]
        public Material lifeformMaterial;
        public Material foodMaterial;
        public Material hazardMaterial;

        [Header("Setup Options")]
        public bool setupCamera = true;
        public bool setupLighting = true;
        public bool setupUI = true;

        [Header("Camera Settings")]
        public Vector3 cameraPosition = new Vector3(0, 50, -80);
        public Vector3 cameraRotation = new Vector3(30, 0, 0);

#if UNITY_EDITOR
        [ContextMenu("Setup Complete Scene")]
        public void SetupScene()
        {
            Debug.Log("[EvoSim Setup] Starting scene setup...");

            // Create config if needed
            if (simulationConfig == null)
            {
                Debug.Log("[EvoSim Setup] Creating default simulation config...");
                simulationConfig = CreateDefaultConfig();
            }

            // Create materials if needed
            if (lifeformMaterial == null || foodMaterial == null || hazardMaterial == null)
            {
                Debug.Log("[EvoSim Setup] Creating default materials...");
                CreateDefaultMaterials();
            }

            // Assign materials to config
            simulationConfig.LifeformMaterial = lifeformMaterial;
            simulationConfig.FoodMaterial = foodMaterial;
            simulationConfig.HazardMaterial = hazardMaterial;
            EditorUtility.SetDirty(simulationConfig);

            // Setup simulation manager
            SetupSimulationManager();

            // Setup camera
            if (setupCamera)
            {
                SetupMainCamera();
            }

            // Setup lighting
            if (setupLighting)
            {
                SetupSceneLighting();
            }

            // Setup UI
            if (setupUI)
            {
                SetupStatsUI();
            }

            Debug.Log("[EvoSim Setup] Scene setup complete! Press Play to start simulation.");

            // Mark scene as dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene()
            );
        }

        private SimulationConfig CreateDefaultConfig()
        {
            string path = "Assets/Settings";

            // Create Settings folder if it doesn't exist
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder("Assets", "Settings");
            }

            // Create config asset
            SimulationConfig config = ScriptableObject.CreateInstance<SimulationConfig>();
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{path}/DefaultSimulationConfig.asset");
            AssetDatabase.CreateAsset(config, assetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[EvoSim Setup] Created config at: {assetPath}");
            return config;
        }

        private void CreateDefaultMaterials()
        {
            string path = "Assets/Materials";

            // Create Materials folder if it doesn't exist
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }

            // Create Lifeform Material
            if (lifeformMaterial == null)
            {
                lifeformMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                lifeformMaterial.color = new Color(0.2f, 0.8f, 0.2f); // Green
                AssetDatabase.CreateAsset(lifeformMaterial, $"{path}/LifeformMaterial.mat");
            }

            // Create Food Material
            if (foodMaterial == null)
            {
                foodMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                foodMaterial.color = new Color(1f, 0.8f, 0.2f); // Yellow/Gold
                foodMaterial.SetFloat("_Smoothness", 0.8f);
                AssetDatabase.CreateAsset(foodMaterial, $"{path}/FoodMaterial.mat");
            }

            // Create Hazard Material
            if (hazardMaterial == null)
            {
                hazardMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                hazardMaterial.color = new Color(0.9f, 0.2f, 0.1f); // Red
                hazardMaterial.EnableKeyword("_EMISSION");
                hazardMaterial.SetColor("_EmissionColor", new Color(0.5f, 0.1f, 0f) * 2f);
                AssetDatabase.CreateAsset(hazardMaterial, $"{path}/HazardMaterial.mat");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[EvoSim Setup] Materials created");
        }

        private void SetupSimulationManager()
        {
            // Check if manager already exists
            SimulationAuthoring existing = FindObjectOfType<SimulationAuthoring>();
            if (existing != null)
            {
                Debug.Log("[EvoSim Setup] SimulationManager already exists, updating config...");
                existing.GetType().GetField("config",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance)?.SetValue(existing, simulationConfig);
                EditorUtility.SetDirty(existing);
                return;
            }

            // Create new manager
            GameObject managerGO = new GameObject("SimulationManager");
            SimulationAuthoring authoring = managerGO.AddComponent<SimulationAuthoring>();

            // Use reflection to set private field (since config is private)
            var field = authoring.GetType().GetField("config",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            field?.SetValue(authoring, simulationConfig);

            Debug.Log("[EvoSim Setup] SimulationManager created");
        }

        private void SetupMainCamera()
        {
            Camera mainCam = Camera.main;

            if (mainCam == null)
            {
                GameObject camGO = new GameObject("Main Camera");
                mainCam = camGO.AddComponent<Camera>();
                camGO.tag = "MainCamera";
                camGO.AddComponent<AudioListener>();
            }

            mainCam.transform.position = cameraPosition;
            mainCam.transform.eulerAngles = cameraRotation;
            mainCam.clearFlags = CameraClearFlags.Skybox;
            mainCam.backgroundColor = new Color(0.1f, 0.1f, 0.15f);

            // Add camera controller script if not present
            if (mainCam.GetComponent<SimpleCameraController>() == null)
            {
                mainCam.gameObject.AddComponent<SimpleCameraController>();
            }

            Debug.Log("[EvoSim Setup] Camera configured");
        }

        private void SetupSceneLighting()
        {
            Light directional = FindObjectOfType<Light>();

            if (directional == null || directional.type != LightType.Directional)
            {
                GameObject lightGO = new GameObject("Directional Light");
                directional = lightGO.AddComponent<Light>();
                directional.type = LightType.Directional;
            }

            directional.transform.rotation = Quaternion.Euler(50, -30, 0);
            directional.intensity = 1f;
            directional.color = Color.white;

            // Set ambient lighting
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 0.5f;

            Debug.Log("[EvoSim Setup] Lighting configured");
        }

        private void SetupStatsUI()
        {
            // Check if UI already exists
            if (FindObjectOfType<EvoSim.UI.SimulationStatsUI>() != null)
            {
                Debug.Log("[EvoSim Setup] Stats UI already exists");
                return;
            }

            // Create temporary GameObject with QuickUISetup
            GameObject uiSetupGO = new GameObject("UISetup_Temp");
            uiSetupGO.AddComponent<EvoSim.UI.QuickUISetup>();

            Debug.Log("[EvoSim Setup] Stats UI will be created on Play");
        }
#endif
    }

    /// <summary>
    /// Simple camera controller for navigating the simulation
    /// </summary>
    public class SimpleCameraController : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 30f;
        public float fastMoveMultiplier = 3f;
        public float smoothTime = 0.1f;

        [Header("Rotation")]
        public float lookSpeed = 2f;
        public float minPitch = -80f;
        public float maxPitch = 80f;

        private Vector3 velocity = Vector3.zero;
        private float pitch = 0f;
        private float yaw = 0f;

        void Start()
        {
            // Initialize rotation
            Vector3 euler = transform.eulerAngles;
            pitch = euler.x;
            yaw = euler.y;
        }

        void Update()
        {
            HandleMovement();
            HandleRotation();
        }

        void HandleMovement()
        {
            // Get input
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            float u = 0;

            if (Input.GetKey(KeyCode.E)) u = 1;
            if (Input.GetKey(KeyCode.Q)) u = -1;

            // Calculate target velocity
            Vector3 targetVelocity = transform.right * h +
                                    transform.up * u +
                                    transform.forward * v;
            targetVelocity = targetVelocity.normalized;

            // Apply speed
            float speed = moveSpeed;
            if (Input.GetKey(KeyCode.LeftShift))
                speed *= fastMoveMultiplier;

            targetVelocity *= speed;

            // Smooth movement
            Vector3 smoothVelocity = Vector3.SmoothDamp(velocity, targetVelocity, ref velocity, smoothTime);
            transform.position += smoothVelocity * Time.deltaTime;
        }

        void HandleRotation()
        {
            // Right-click or middle-click to rotate
            if (Input.GetMouseButton(1) || Input.GetMouseButton(2))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                yaw += Input.GetAxis("Mouse X") * lookSpeed;
                pitch -= Input.GetAxis("Mouse Y") * lookSpeed;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

                transform.eulerAngles = new Vector3(pitch, yaw, 0);
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}