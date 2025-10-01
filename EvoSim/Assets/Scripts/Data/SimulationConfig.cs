using UnityEngine;

namespace EvoSim.Data
{
    [CreateAssetMenu(fileName = "SimulationConfig", menuName = "EvoSim/Simulation Config")]
    public class SimulationConfig : ScriptableObject
    {
        [Header("Population Settings")]
        [Tooltip("Number of lifeforms to spawn at start")]
        public int InitialPopulation = 100;

        [Tooltip("Maximum allowed population before culling")]
        public int MaxPopulation = 5000;

        [Tooltip("Minimum population before emergency spawning")]
        public int MinPopulation = 10;

        [Header("World Settings")]
        [Tooltip("Size of the simulation world (cube)")]
        public float WorldSize = 100f;

        [Tooltip("Cell size for spatial partitioning grid")]
        public float SpatialGridCellSize = 10f;

        [Header("Energy Settings")]
        [Tooltip("Base maximum energy for lifeforms")]
        public float BaseMaxEnergy = 100f;

        [Tooltip("Base energy consumption per second")]
        public float BaseEnergyConsumption = 1f;

        [Tooltip("Energy cost multiplier based on size (larger = more costly)")]
        public float SizeEnergyCostFactor = 0.5f;

        [Header("Movement Settings")]
        [Tooltip("Base movement speed")]
        public float BaseMovementSpeed = 5f;

        [Tooltip("How often lifeforms change random direction (seconds)")]
        public float DirectionChangeInterval = 2f;

        [Header("Reproduction Settings")]
        [Tooltip("Cooldown after reproduction (seconds)")]
        public float ReproductionCooldown = 10f;

        [Tooltip("Energy cost per offspring (% of max energy)")]
        public float ReproductionEnergyCost = 0.4f;

        [Header("Food Source Settings")]
        [Tooltip("Number of food sources to spawn")]
        public int FoodSourceCount = 50;

        [Tooltip("Energy provided by each food source")]
        public float FoodEnergyAmount = 30f;

        [Tooltip("How fast food sources regenerate (energy per second)")]
        public float FoodRegenerationRate = 5f;

        [Tooltip("Maximum energy a food source can hold")]
        public float FoodMaxEnergy = 100f;

        [Tooltip("Radius around food source where energy can be consumed")]
        public float FoodConsumptionRadius = 2f;

        [Header("Hazard Settings")]
        [Tooltip("Number of hazards to spawn")]
        public int HazardCount = 20;

        [Tooltip("Damage per second from hazards")]
        public float HazardDamage = 10f;

        [Tooltip("Effect radius of hazards")]
        public float HazardRadius = 5f;

        [Header("Mutation Settings")]
        [Tooltip("Maximum amount a trait can mutate (as % of range)")]
        public float MaxMutationAmount = 0.3f;

        [Tooltip("Chance of catastrophic mutation (complete reroll)")]
        public float CatastrophicMutationChance = 0.01f;

        [Header("Performance Settings")]
        [Tooltip("Update only this many lifeforms per frame for sensing")]
        public int SensingUpdatesPerFrame = 100;

        [Tooltip("Maximum distance to check for neighbors")]
        public float MaxSensingDistance = 20f;

        [Header("Visualization")]
        [Tooltip("Material for lifeforms")]
        public Material LifeformMaterial;

        [Tooltip("Material for food sources")]
        public Material FoodMaterial;

        [Tooltip("Material for hazards")]
        public Material HazardMaterial;

        [Tooltip("Show sensing range gizmos in editor")]
        public bool ShowSensingGizmos = false;

        [Header("Statistics")]
        [Tooltip("How often to record statistics (seconds)")]
        public float StatisticsRecordInterval = 5f;

        [Tooltip("How many generations to keep in history")]
        public int MaxGenerationHistory = 100;

        // Validation
        private void OnValidate()
        {
            InitialPopulation = Mathf.Max(1, InitialPopulation);
            MaxPopulation = Mathf.Max(InitialPopulation, MaxPopulation);
            MinPopulation = Mathf.Max(1, Mathf.Min(MinPopulation, InitialPopulation));
            WorldSize = Mathf.Max(10f, WorldSize);
            SpatialGridCellSize = Mathf.Max(1f, SpatialGridCellSize);
            BaseMaxEnergy = Mathf.Max(10f, BaseMaxEnergy);
            BaseEnergyConsumption = Mathf.Max(0.1f, BaseEnergyConsumption);
            BaseMovementSpeed = Mathf.Max(0.1f, BaseMovementSpeed);
            DirectionChangeInterval = Mathf.Max(0.5f, DirectionChangeInterval);
            ReproductionCooldown = Mathf.Max(1f, ReproductionCooldown);
            FoodSourceCount = Mathf.Max(0, FoodSourceCount);
            HazardCount = Mathf.Max(0, HazardCount);
            SensingUpdatesPerFrame = Mathf.Max(1, SensingUpdatesPerFrame);
        }

        // Helper methods
        public bool IsWithinWorldBounds(Vector3 position)
        {
            float halfSize = WorldSize / 2f;
            return Mathf.Abs(position.x) <= halfSize &&
                   Mathf.Abs(position.y) <= halfSize &&
                   Mathf.Abs(position.z) <= halfSize;
        }

        public Vector3 GetRandomPositionInWorld(System.Random random)
        {
            float halfSize = WorldSize / 2f;
            return new Vector3(
                (float)(random.NextDouble() * WorldSize - halfSize),
                (float)(random.NextDouble() * WorldSize - halfSize),
                (float)(random.NextDouble() * WorldSize - halfSize)
            );
        }
    }
}