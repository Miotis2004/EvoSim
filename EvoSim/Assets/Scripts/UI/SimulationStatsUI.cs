using UnityEngine;
using Unity.Entities;
using TMPro;
using EvoSim.Components;

namespace EvoSim.UI
{
    /// <summary>
    /// Simple UI display for showing simulation statistics in real-time
    /// Attach to a Canvas with TextMeshPro components
    /// </summary>
    public class SimulationStatsUI : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("TextMeshPro component for displaying stats")]
        public TextMeshProUGUI statsText;

        [Header("Update Settings")]
        [Tooltip("How often to update the UI (seconds)")]
        public float updateInterval = 1f;

        private float timeSinceUpdate = 0f;
        private World defaultWorld;

        void Start()
        {
            defaultWorld = World.DefaultGameObjectInjectionWorld;

            if (statsText == null)
            {
                Debug.LogWarning("[EvoSim UI] No TextMeshPro component assigned!");
            }
        }

        void Update()
        {
            timeSinceUpdate += Time.deltaTime;

            if (timeSinceUpdate >= updateInterval)
            {
                UpdateStats();
                timeSinceUpdate = 0f;
            }
        }

        void UpdateStats()
        {
            if (defaultWorld == null || !defaultWorld.IsCreated || statsText == null)
                return;

            var entityManager = defaultWorld.EntityManager;

            // Query for lifeforms
            var lifeformQuery = entityManager.CreateEntityQuery(typeof(LifeformTag));
            int population = lifeformQuery.CalculateEntityCount();

            if (population == 0)
            {
                statsText.text = "POPULATION EXTINCT";
                return;
            }

            // Calculate statistics
            int totalGeneration = 0;
            int maxGeneration = 0;
            float totalAge = 0;
            float totalEnergy = 0;
            float totalSize = 0;
            float totalSpeed = 0;
            float totalSensing = 0;
            int count = 0;

            var entities = lifeformQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

            foreach (var entity in entities)
            {
                if (entityManager.HasComponent<GenomeComponent>(entity) &&
                    entityManager.HasComponent<EnergyComponent>(entity) &&
                    entityManager.HasComponent<LifetimeComponent>(entity))
                {
                    var genome = entityManager.GetComponentData<GenomeComponent>(entity);
                    var energy = entityManager.GetComponentData<EnergyComponent>(entity);
                    var lifetime = entityManager.GetComponentData<LifetimeComponent>(entity);

                    totalGeneration += lifetime.Generation;
                    if (lifetime.Generation > maxGeneration) maxGeneration = lifetime.Generation;
                    totalAge += lifetime.Age;
                    totalEnergy += energy.EnergyPercent;
                    totalSize += genome.Size;
                    totalSpeed += genome.SpeedMultiplier;
                    totalSensing += genome.SensingRange;
                    count++;
                }
            }

            entities.Dispose();

            if (count == 0)
            {
                statsText.text = "No valid lifeform data";
                return;
            }

            // Calculate averages
            float avgGeneration = (float)totalGeneration / count;
            float avgAge = totalAge / count;
            float avgEnergy = totalEnergy / count;
            float avgSize = totalSize / count;
            float avgSpeed = totalSpeed / count;
            float avgSensing = totalSensing / count;

            // Get food and hazard counts
            var foodQuery = entityManager.CreateEntityQuery(typeof(FoodSourceTag));
            var hazardQuery = entityManager.CreateEntityQuery(typeof(HazardTag));
            int foodCount = foodQuery.CalculateEntityCount();
            int hazardCount = hazardQuery.CalculateEntityCount();

            // Format display
            statsText.text = $"<b>EVOSIM STATISTICS</b>\n\n" +
                $"<color=#00FF00>Population:</color> {population}\n" +
                $"<color=#FFFF00>Generation:</color> Avg {avgGeneration:F1} | Max {maxGeneration}\n" +
                $"<color=#00FFFF>Average Age:</color> {avgAge:F1}s\n" +
                $"<color=#FF00FF>Average Energy:</color> {avgEnergy:P0}\n\n" +
                $"<b>AVERAGE TRAITS</b>\n" +
                $"Size: {avgSize:F2}\n" +
                $"Speed: {avgSpeed:F2}\n" +
                $"Sensing: {avgSensing:F1}\n\n" +
                $"<b>ENVIRONMENT</b>\n" +
                $"Food Sources: {foodCount}\n" +
                $"Hazards: {hazardCount}\n\n" +
                $"<size=10><i>Time: {Time.time:F0}s | FPS: {(1f / Time.deltaTime):F0}</i></size>";
        }

        void OnDestroy()
        {
            defaultWorld = null;
        }
    }
}

namespace EvoSim.UI
{
    /// <summary>
    /// Quick setup script to create the UI canvas automatically
    /// Attach to any GameObject and it will create the UI for you
    /// </summary>
    public class QuickUISetup : MonoBehaviour
    {
        [Header("Setup Settings")]
        public bool createUIOnStart = true;
        public Vector2 uiPosition = new Vector2(10, -10);
        public Vector2 uiSize = new Vector2(350, 400);

        void Start()
        {
            if (createUIOnStart)
            {
                CreateStatsUI();
                // Destroy this component after setup
                Destroy(this);
            }
        }

        void CreateStatsUI()
        {
            // Create Canvas
            GameObject canvasGO = new GameObject("SimulationCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Create Panel
            GameObject panelGO = new GameObject("StatsPanel");
            panelGO.transform.SetParent(canvasGO.transform, false);

            UnityEngine.UI.Image panelImage = panelGO.AddComponent<UnityEngine.UI.Image>();
            panelImage.color = new Color(0, 0, 0, 0.7f);

            RectTransform panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 1);
            panelRect.anchorMax = new Vector2(0, 1);
            panelRect.pivot = new Vector2(0, 1);
            panelRect.anchoredPosition = uiPosition;
            panelRect.sizeDelta = uiSize;

            // Create Text
            GameObject textGO = new GameObject("StatsText");
            textGO.transform.SetParent(panelGO.transform, false);

            TextMeshProUGUI text = textGO.AddComponent<TextMeshProUGUI>();
            text.text = "Initializing...";
            text.fontSize = 14;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;

            RectTransform textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 10);
            textRect.offsetMax = new Vector2(-10, -10);

            // Add the stats display component
            SimulationStatsUI statsUI = panelGO.AddComponent<SimulationStatsUI>();
            statsUI.statsText = text;
            statsUI.updateInterval = 0.5f;

            Debug.Log("[EvoSim] Stats UI created successfully!");
        }
    }
}