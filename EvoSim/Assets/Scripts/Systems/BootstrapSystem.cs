using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using UnityEngine;
using EvoSim.Components;
using EvoSim.Data;

namespace EvoSim.Systems
{
    /// <summary>
    /// Initializes the simulation world on startup
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class BootstrapSystem : SystemBase
    {
        private bool _hasInitialized = false;

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<SimulationConfigComponent>();
        }

        protected override void OnUpdate()
        {
            if (_hasInitialized) return;

            // Get the config from a singleton entity (we'll create this in the authoring component)
            var config = SystemAPI.GetSingleton<SimulationConfigComponent>();

            Debug.Log($"[EvoSim] Initializing simulation with {config.InitialPopulation} lifeforms...");

            var random = new Unity.Mathematics.Random((uint)System.DateTime.Now.Ticks);

            // Spawn initial lifeforms
            SpawnInitialLifeforms(config, ref random);

            // Spawn food sources
            SpawnFoodSources(config, ref random);

            // Spawn hazards
            SpawnHazards(config, ref random);

            Debug.Log("[EvoSim] Initialization complete!");
            _hasInitialized = true;
        }

        private void SpawnInitialLifeforms(SimulationConfigComponent config, ref Unity.Mathematics.Random random)
        {
            var entityManager = EntityManager;

            // Create archetype for lifeforms
            var lifeformArchetype = entityManager.CreateArchetype(
                typeof(LocalTransform),
                typeof(GenomeComponent),
                typeof(EnergyComponent),
                typeof(MovementComponent),
                typeof(SensingComponent),
                typeof(ReproductionComponent),
                typeof(LifetimeComponent),
                typeof(SpatialHashComponent),
                typeof(LifeformTag)
            );

            float halfWorld = config.WorldSize / 2f;

            for (int i = 0; i < config.InitialPopulation; i++)
            {
                var entity = entityManager.CreateEntity(lifeformArchetype);

                // Generate random genome
                var genome = GenomeComponent.CreateRandom(ref random);

                // Random position within world bounds
                var position = new float3(
                    random.NextFloat(-halfWorld, halfWorld),
                    random.NextFloat(-halfWorld, halfWorld),
                    random.NextFloat(-halfWorld, halfWorld)
                );

                // Set transform
                entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(
                    position,
                    quaternion.identity,
                    genome.Size
                ));

                // Set genome
                entityManager.SetComponentData(entity, genome);

                // Initialize energy
                float maxEnergy = config.BaseMaxEnergy * genome.MaxEnergyMultiplier;
                entityManager.SetComponentData(entity, new EnergyComponent
                {
                    CurrentEnergy = maxEnergy * 0.8f, // Start at 80% energy
                    MaxEnergy = maxEnergy,
                    ConsumptionRate = config.BaseEnergyConsumption / genome.EnergyEfficiency
                });

                // Initialize movement
                entityManager.SetComponentData(entity, new MovementComponent
                {
                    BaseSpeed = config.BaseMovementSpeed,
                    CurrentDirection = random.NextFloat3Direction(),
                    DirectionChangeTimer = 0,
                    DirectionChangeInterval = config.DirectionChangeInterval,
                    HasTarget = false
                });

                // Initialize sensing
                entityManager.SetComponentData(entity, new SensingComponent
                {
                    Range = genome.SensingRange,
                    HasDetectedFood = false,
                    HasDetectedHazard = false,
                    NearbyLifeformCount = 0
                });

                // Initialize reproduction
                entityManager.SetComponentData(entity, new ReproductionComponent
                {
                    ReproductionCooldown = config.ReproductionCooldown,
                    CooldownTimer = 0
                });

                // Initialize lifetime
                entityManager.SetComponentData(entity, new LifetimeComponent
                {
                    Age = 0,
                    Generation = 0,
                    ChildrenProduced = 0
                });

                // Initialize spatial hash
                var gridPos = SpatialHashComponent.WorldToGrid(position, config.SpatialGridCellSize);
                entityManager.SetComponentData(entity, new SpatialHashComponent
                {
                    GridX = gridPos.x,
                    GridY = gridPos.y,
                    GridZ = gridPos.z
                });
            }
        }

        private void SpawnFoodSources(SimulationConfigComponent config, ref Unity.Mathematics.Random random)
        {
            var entityManager = EntityManager;

            var foodArchetype = entityManager.CreateArchetype(
                typeof(LocalTransform),
                typeof(FoodSourceTag)
            );

            float halfWorld = config.WorldSize / 2f;

            for (int i = 0; i < config.FoodSourceCount; i++)
            {
                var entity = entityManager.CreateEntity(foodArchetype);

                var position = new float3(
                    random.NextFloat(-halfWorld, halfWorld),
                    random.NextFloat(-halfWorld, halfWorld),
                    random.NextFloat(-halfWorld, halfWorld)
                );

                entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(
                    position,
                    quaternion.identity,
                    2f // Food source size
                ));

                entityManager.SetComponentData(entity, new FoodSourceTag
                {
                    EnergyProvided = config.FoodEnergyAmount,
                    RegenerationRate = config.FoodRegenerationRate,
                    CurrentEnergy = config.FoodMaxEnergy,
                    MaxEnergy = config.FoodMaxEnergy
                });
            }
        }

        private void SpawnHazards(SimulationConfigComponent config, ref Unity.Mathematics.Random random)
        {
            var entityManager = EntityManager;

            var hazardArchetype = entityManager.CreateArchetype(
                typeof(LocalTransform),
                typeof(HazardTag)
            );

            float halfWorld = config.WorldSize / 2f;

            for (int i = 0; i < config.HazardCount; i++)
            {
                var entity = entityManager.CreateEntity(hazardArchetype);

                var position = new float3(
                    random.NextFloat(-halfWorld, halfWorld),
                    random.NextFloat(-halfWorld, halfWorld),
                    random.NextFloat(-halfWorld, halfWorld)
                );

                entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(
                    position,
                    quaternion.identity,
                    config.HazardRadius
                ));

                entityManager.SetComponentData(entity, new HazardTag
                {
                    DamagePerSecond = config.HazardDamage,
                    EffectRadius = config.HazardRadius
                });
            }
        }
    }

    /// <summary>
    /// Component to hold simulation config in ECS world
    /// </summary>
    public struct SimulationConfigComponent : IComponentData
    {
        public int InitialPopulation;
        public int MaxPopulation;
        public int MinPopulation;
        public float WorldSize;
        public float SpatialGridCellSize;
        public float BaseMaxEnergy;
        public float BaseEnergyConsumption;
        public float SizeEnergyCostFactor;
        public float BaseMovementSpeed;
        public float DirectionChangeInterval;
        public float ReproductionCooldown;
        public float ReproductionEnergyCost;
        public int FoodSourceCount;
        public float FoodEnergyAmount;
        public float FoodRegenerationRate;
        public float FoodMaxEnergy;
        public float FoodConsumptionRadius;
        public int HazardCount;
        public float HazardDamage;
        public float HazardRadius;
        public float MaxMutationAmount;
        public float CatastrophicMutationChance;
        public int SensingUpdatesPerFrame;
        public float MaxSensingDistance;

        public static SimulationConfigComponent FromScriptableObject(SimulationConfig config)
        {
            return new SimulationConfigComponent
            {
                InitialPopulation = config.InitialPopulation,
                MaxPopulation = config.MaxPopulation,
                MinPopulation = config.MinPopulation,
                WorldSize = config.WorldSize,
                SpatialGridCellSize = config.SpatialGridCellSize,
                BaseMaxEnergy = config.BaseMaxEnergy,
                BaseEnergyConsumption = config.BaseEnergyConsumption,
                SizeEnergyCostFactor = config.SizeEnergyCostFactor,
                BaseMovementSpeed = config.BaseMovementSpeed,
                DirectionChangeInterval = config.DirectionChangeInterval,
                ReproductionCooldown = config.ReproductionCooldown,
                ReproductionEnergyCost = config.ReproductionEnergyCost,
                FoodSourceCount = config.FoodSourceCount,
                FoodEnergyAmount = config.FoodEnergyAmount,
                FoodRegenerationRate = config.FoodRegenerationRate,
                FoodMaxEnergy = config.FoodMaxEnergy,
                FoodConsumptionRadius = config.FoodConsumptionRadius,
                HazardCount = config.HazardCount,
                HazardDamage = config.HazardDamage,
                HazardRadius = config.HazardRadius,
                MaxMutationAmount = config.MaxMutationAmount,
                CatastrophicMutationChance = config.CatastrophicMutationChance,
                SensingUpdatesPerFrame = config.SensingUpdatesPerFrame,
                MaxSensingDistance = config.MaxSensingDistance
            };
        }
    }
}