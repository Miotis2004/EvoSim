using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Burst;
using EvoSim.Components;
using EvoSim.Systems;

namespace EvoSim.Systems
{
    /// <summary>
    /// Handles reproduction when lifeforms have sufficient energy
    /// Implements both asexual and sexual reproduction with mutations
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnergySystem))]
    public partial struct ReproductionSystem : ISystem
    {
        private Unity.Mathematics.Random _random;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationConfigComponent>();
            state.RequireForUpdate<LifeformTag>();
            _random = new Unity.Mathematics.Random((uint)System.DateTime.Now.Ticks);
        }

        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<SimulationConfigComponent>();
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Update reproduction cooldowns
            foreach (var reproduction in SystemAPI.Query<RefRW<ReproductionComponent>>().WithAll<LifeformTag>())
            {
                if (reproduction.ValueRO.CooldownTimer > 0)
                {
                    reproduction.ValueRW.CooldownTimer -= deltaTime;
                }
            }

            // Get current population count
            var lifeformQuery = SystemAPI.QueryBuilder()
                .WithAll<LifeformTag>()
                .Build();
            int currentPopulation = lifeformQuery.CalculateEntityCount();

            // Don't reproduce if at max population
            if (currentPopulation >= config.MaxPopulation)
                return;

            // Collect entities ready to reproduce
            var reproducingEntities = new NativeList<Entity>(Allocator.Temp);
            var reproducingData = new NativeList<ReproductionData>(Allocator.Temp);

            foreach (var (transform, genome, energy, reproduction, lifetime, entity) in
                SystemAPI.Query<
                    RefRO<LocalTransform>,
                    RefRO<GenomeComponent>,
                    RefRO<EnergyComponent>,
                    RefRO<ReproductionComponent>,
                    RefRO<LifetimeComponent>>()
                .WithAll<LifeformTag>()
                .WithEntityAccess())
            {
                // Check if ready to reproduce
                bool hasEnoughEnergy = energy.ValueRO.CanReproduce(genome.ValueRO.ReproductionThreshold);
                bool cooledDown = reproduction.ValueRO.IsReadyToReproduce;

                if (hasEnoughEnergy && cooledDown)
                {
                    reproducingEntities.Add(entity);
                    reproducingData.Add(new ReproductionData
                    {
                        Position = transform.ValueRO.Position,
                        Genome = genome.ValueRO,
                        Generation = lifetime.ValueRO.Generation,
                        MaxEnergy = energy.ValueRO.MaxEnergy
                    });
                }
            }

            // Perform reproduction
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < reproducingEntities.Length; i++)
            {
                if (currentPopulation >= config.MaxPopulation)
                    break;

                var parentEntity = reproducingEntities[i];
                var parentData = reproducingData[i];

                // Determine reproduction mode
                bool shouldUseSexual = parentData.Genome.ReproductionMode > _random.NextFloat();

                if (shouldUseSexual && reproducingEntities.Length > 1)
                {
                    // Sexual reproduction - find a mate
                    int mateIndex = _random.NextInt(0, reproducingEntities.Length);
                    if (mateIndex == i) mateIndex = (mateIndex + 1) % reproducingEntities.Length;

                    var mateData = reproducingData[mateIndex];

                    // Create offspring with combined genes
                    PerformSexualReproduction(
                        ref state,
                        ecb,
                        config,
                        parentData,
                        mateData,
                        ref _random
                    );
                }
                else
                {
                    // Asexual reproduction - clone with mutations
                    PerformAsexualReproduction(
                        ref state,
                        ecb,
                        config,
                        parentData,
                        ref _random
                    );
                }

                // Deduct energy and reset cooldown for parent
                ecb.SetComponent(parentEntity, new ReproductionComponent
                {
                    ReproductionCooldown = config.ReproductionCooldown,
                    CooldownTimer = config.ReproductionCooldown
                });

                var parentEnergy = state.EntityManager.GetComponentData<EnergyComponent>(parentEntity);
                parentEnergy.CurrentEnergy -= parentData.MaxEnergy * config.ReproductionEnergyCost;
                ecb.SetComponent(parentEntity, parentEnergy);

                // Increment children count
                var parentLifetime = state.EntityManager.GetComponentData<LifetimeComponent>(parentEntity);
                parentLifetime.ChildrenProduced++;
                ecb.SetComponent(parentEntity, parentLifetime);

                currentPopulation += parentData.Genome.OffspringCount;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            reproducingEntities.Dispose();
            reproducingData.Dispose();
        }

        private void PerformAsexualReproduction(
            ref SystemState state,
            EntityCommandBuffer ecb,
            SimulationConfigComponent config,
            ReproductionData parentData,
            ref Unity.Mathematics.Random random)
        {
            for (int i = 0; i < parentData.Genome.OffspringCount; i++)
            {
                // Clone parent genome
                var childGenome = parentData.Genome;

                // Apply mutations
                childGenome = MutateGenome(childGenome, config, ref random);

                // Create offspring
                CreateOffspring(ref state, ecb, config, parentData.Position, childGenome, parentData.Generation + 1, ref random);
            }
        }

        private void PerformSexualReproduction(
            ref SystemState state,
            EntityCommandBuffer ecb,
            SimulationConfigComponent config,
            ReproductionData parent1Data,
            ReproductionData parent2Data,
            ref Unity.Mathematics.Random random)
        {
            // Average offspring count from both parents
            int offspringCount = (parent1Data.Genome.OffspringCount + parent2Data.Genome.OffspringCount) / 2;
            int generation = math.max(parent1Data.Generation, parent2Data.Generation) + 1;

            for (int i = 0; i < offspringCount; i++)
            {
                // Combine genomes (simple crossover - each trait randomly from either parent)
                var childGenome = CombineGenomes(parent1Data.Genome, parent2Data.Genome, ref random);

                // Apply mutations
                childGenome = MutateGenome(childGenome, config, ref random);

                // Spawn near midpoint of parents
                float3 midpoint = (parent1Data.Position + parent2Data.Position) * 0.5f;

                CreateOffspring(ref state, ecb, config, midpoint, childGenome, generation, ref random);
            }
        }

        private GenomeComponent CombineGenomes(
            GenomeComponent parent1,
            GenomeComponent parent2,
            ref Unity.Mathematics.Random random)
        {
            return new GenomeComponent
            {
                Size = random.NextBool() ? parent1.Size : parent2.Size,
                SpeedMultiplier = random.NextBool() ? parent1.SpeedMultiplier : parent2.SpeedMultiplier,
                SensingRange = random.NextBool() ? parent1.SensingRange : parent2.SensingRange,
                FoodAttraction = random.NextBool() ? parent1.FoodAttraction : parent2.FoodAttraction,
                HazardAvoidance = random.NextBool() ? parent1.HazardAvoidance : parent2.HazardAvoidance,
                EnergyEfficiency = random.NextBool() ? parent1.EnergyEfficiency : parent2.EnergyEfficiency,
                MaxEnergyMultiplier = random.NextBool() ? parent1.MaxEnergyMultiplier : parent2.MaxEnergyMultiplier,
                Aggression = random.NextBool() ? parent1.Aggression : parent2.Aggression,
                SocialBehavior = random.NextBool() ? parent1.SocialBehavior : parent2.SocialBehavior,
                ExplorationBias = random.NextBool() ? parent1.ExplorationBias : parent2.ExplorationBias,
                ReproductionThreshold = random.NextBool() ? parent1.ReproductionThreshold : parent2.ReproductionThreshold,
                ReproductionMode = random.NextBool() ? parent1.ReproductionMode : parent2.ReproductionMode,
                OffspringCount = random.NextBool() ? parent1.OffspringCount : parent2.OffspringCount,
                MutationRate = random.NextBool() ? parent1.MutationRate : parent2.MutationRate,
                MutationStrength = random.NextBool() ? parent1.MutationStrength : parent2.MutationStrength
            };
        }

        private GenomeComponent MutateGenome(
            GenomeComponent genome,
            SimulationConfigComponent config,
            ref Unity.Mathematics.Random random)
        {
            // Check for catastrophic mutation (complete reroll)
            if (random.NextFloat() < config.CatastrophicMutationChance)
            {
                return GenomeComponent.CreateRandom(ref random);
            }

            // Apply normal mutations based on mutation rate
            float mutationRate = genome.MutationRate;
            float mutationStrength = genome.MutationStrength;

            // Helper function to mutate a float value within bounds
            float MutateFloat(float value, float min, float max)
            {
                if (random.NextFloat() < mutationRate)
                {
                    float range = max - min;
                    float change = random.NextFloat(-mutationStrength, mutationStrength) * range;
                    return math.clamp(value + change, min, max);
                }
                return value;
            }

            // Helper function to mutate an int value within bounds
            int MutateInt(int value, int min, int max)
            {
                if (random.NextFloat() < mutationRate)
                {
                    int change = random.NextInt(-1, 2); // -1, 0, or 1
                    return math.clamp(value + change, min, max);
                }
                return value;
            }

            return new GenomeComponent
            {
                Size = MutateFloat(genome.Size, 0.5f, 2.0f),
                SpeedMultiplier = MutateFloat(genome.SpeedMultiplier, 0.5f, 2.0f),
                SensingRange = MutateFloat(genome.SensingRange, 5f, 20f),
                FoodAttraction = MutateFloat(genome.FoodAttraction, -1f, 1f),
                HazardAvoidance = MutateFloat(genome.HazardAvoidance, 0f, 1f),
                EnergyEfficiency = MutateFloat(genome.EnergyEfficiency, 0.5f, 2.0f),
                MaxEnergyMultiplier = MutateFloat(genome.MaxEnergyMultiplier, 0.5f, 2.0f),
                Aggression = MutateFloat(genome.Aggression, 0f, 1f),
                SocialBehavior = MutateFloat(genome.SocialBehavior, -1f, 1f),
                ExplorationBias = MutateFloat(genome.ExplorationBias, 0f, 1f),
                ReproductionThreshold = MutateFloat(genome.ReproductionThreshold, 0.5f, 1.0f),
                ReproductionMode = MutateFloat(genome.ReproductionMode, 0f, 1f),
                OffspringCount = MutateInt(genome.OffspringCount, 1, 3),
                MutationRate = MutateFloat(genome.MutationRate, 0.01f, 0.3f),
                MutationStrength = MutateFloat(genome.MutationStrength, 0.05f, 0.4f)
            };
        }

        private void CreateOffspring(
            ref SystemState state,
            EntityCommandBuffer ecb,
            SimulationConfigComponent config,
            float3 parentPosition,
            GenomeComponent genome,
            int generation,
            ref Unity.Mathematics.Random random)
        {
            var entityManager = state.EntityManager;

            // Create entity with same archetype as parent
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

            var entity = ecb.CreateEntity(lifeformArchetype);

            // Spawn near parent with some random offset
            float3 offset = random.NextFloat3Direction() * random.NextFloat(1f, 3f);
            float3 position = parentPosition + offset;

            // Clamp to world bounds
            float halfWorld = config.WorldSize / 2f;
            position = math.clamp(position, -halfWorld, halfWorld);

            // Set transform
            ecb.SetComponent(entity, LocalTransform.FromPositionRotationScale(
                position,
                quaternion.identity,
                genome.Size
            ));

            // Set genome
            ecb.SetComponent(entity, genome);

            // Initialize energy
            float maxEnergy = config.BaseMaxEnergy * genome.MaxEnergyMultiplier;
            ecb.SetComponent(entity, new EnergyComponent
            {
                CurrentEnergy = maxEnergy * 0.5f, // Start at 50% energy
                MaxEnergy = maxEnergy,
                ConsumptionRate = config.BaseEnergyConsumption / genome.EnergyEfficiency
            });

            // Initialize movement
            ecb.SetComponent(entity, new MovementComponent
            {
                BaseSpeed = config.BaseMovementSpeed,
                CurrentDirection = random.NextFloat3Direction(),
                DirectionChangeTimer = 0,
                DirectionChangeInterval = config.DirectionChangeInterval,
                HasTarget = false
            });

            // Initialize sensing
            ecb.SetComponent(entity, new SensingComponent
            {
                Range = genome.SensingRange,
                HasDetectedFood = false,
                HasDetectedHazard = false,
                NearbyLifeformCount = 0
            });

            // Initialize reproduction
            ecb.SetComponent(entity, new ReproductionComponent
            {
                ReproductionCooldown = config.ReproductionCooldown,
                CooldownTimer = config.ReproductionCooldown // Start with cooldown
            });

            // Initialize lifetime
            ecb.SetComponent(entity, new LifetimeComponent
            {
                Age = 0,
                Generation = generation,
                ChildrenProduced = 0
            });

            // Initialize spatial hash
            var gridPos = SpatialHashComponent.WorldToGrid(position, config.SpatialGridCellSize);
            ecb.SetComponent(entity, new SpatialHashComponent
            {
                GridX = gridPos.x,
                GridY = gridPos.y,
                GridZ = gridPos.z
            });
        }

        private struct ReproductionData
        {
            public float3 Position;
            public GenomeComponent Genome;
            public int Generation;
            public float MaxEnergy;
        }
    }

    /// <summary>
    /// Updates lifetime statistics for all lifeforms
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct LifetimeUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<LifeformTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var lifetime in SystemAPI.Query<RefRW<LifetimeComponent>>().WithAll<LifeformTag>())
            {
                lifetime.ValueRW.Age += deltaTime;
            }
        }
    }
}