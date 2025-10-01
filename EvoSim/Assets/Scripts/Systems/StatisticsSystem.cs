using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using EvoSim.Components;
using EvoSim.Systems;

namespace EvoSim.Systems
{
    /// <summary>
    /// Tracks and logs evolution statistics
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ReproductionSystem))]
    public partial class StatisticsSystem : SystemBase
    {
        private float _timeSinceLastRecord;
        private int _recordCount;

        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<SimulationConfigComponent>();
            RequireForUpdate<LifeformTag>();
            _timeSinceLastRecord = 0;
            _recordCount = 0;
        }

        protected override void OnUpdate()
        {
            var config = SystemAPI.GetSingleton<SimulationConfigComponent>();

            _timeSinceLastRecord += SystemAPI.Time.DeltaTime;

            if (_timeSinceLastRecord >= config.StatisticsRecordInterval)
            {
                RecordStatistics(config);
                _timeSinceLastRecord = 0;
                _recordCount++;
            }
        }

        private void RecordStatistics(SimulationConfigComponent config)
        {
            var stats = new SimulationStatistics();

            // Count population
            var lifeformQuery = SystemAPI.QueryBuilder()
                .WithAll<LifeformTag>()
                .Build();

            stats.Population = lifeformQuery.CalculateEntityCount();

            if (stats.Population == 0)
            {
                Debug.Log("[EvoSim] Population extinct!");
                return;
            }

            // Gather genome data
            var genomes = new NativeList<GenomeComponent>(Allocator.Temp);
            var energies = new NativeList<float>(Allocator.Temp);
            var generations = new NativeList<int>(Allocator.Temp);
            var ages = new NativeList<float>(Allocator.Temp);

            foreach (var (genome, energy, lifetime) in
                SystemAPI.Query<RefRO<GenomeComponent>, RefRO<EnergyComponent>, RefRO<LifetimeComponent>>()
                    .WithAll<LifeformTag>())
            {
                genomes.Add(genome.ValueRO);
                energies.Add(energy.ValueRO.EnergyPercent);
                generations.Add(lifetime.ValueRO.Generation);
                ages.Add(lifetime.ValueRO.Age);
            }

            // Calculate averages
            stats.AverageSize = CalculateAverage(genomes, g => g.Size);
            stats.AverageSpeed = CalculateAverage(genomes, g => g.SpeedMultiplier);
            stats.AverageSensingRange = CalculateAverage(genomes, g => g.SensingRange);
            stats.AverageFoodAttraction = CalculateAverage(genomes, g => g.FoodAttraction);
            stats.AverageHazardAvoidance = CalculateAverage(genomes, g => g.HazardAvoidance);
            stats.AverageEnergyEfficiency = CalculateAverage(genomes, g => g.EnergyEfficiency);
            stats.AverageAggression = CalculateAverage(genomes, g => g.Aggression);
            stats.AverageSocialBehavior = CalculateAverage(genomes, g => g.SocialBehavior);
            stats.AverageExplorationBias = CalculateAverage(genomes, g => g.ExplorationBias);
            stats.AverageReproductionThreshold = CalculateAverage(genomes, g => g.ReproductionThreshold);
            stats.AverageReproductionMode = CalculateAverage(genomes, g => g.ReproductionMode);
            stats.AverageMutationRate = CalculateAverage(genomes, g => g.MutationRate);

            // Energy and generation stats
            stats.AverageEnergyPercent = CalculateAverageFloat(energies);
            stats.AverageGeneration = CalculateAverageInt(generations);
            stats.MaxGeneration = CalculateMaxInt(generations);
            stats.AverageAge = CalculateAverageFloat(ages);

            // Log statistics
            LogStatistics(stats);

            // Cleanup
            genomes.Dispose();
            energies.Dispose();
            generations.Dispose();
            ages.Dispose();
        }

        private float CalculateAverage(NativeList<GenomeComponent> genomes, System.Func<GenomeComponent, float> selector)
        {
            if (genomes.Length == 0) return 0;

            float sum = 0;
            for (int i = 0; i < genomes.Length; i++)
            {
                sum += selector(genomes[i]);
            }
            return sum / genomes.Length;
        }

        private float CalculateAverageFloat(NativeList<float> values)
        {
            if (values.Length == 0) return 0;

            float sum = 0;
            for (int i = 0; i < values.Length; i++)
            {
                sum += values[i];
            }
            return sum / values.Length;
        }

        private float CalculateAverageInt(NativeList<int> values)
        {
            if (values.Length == 0) return 0;

            int sum = 0;
            for (int i = 0; i < values.Length; i++)
            {
                sum += values[i];
            }
            return (float)sum / values.Length;
        }

        private int CalculateMaxInt(NativeList<int> values)
        {
            if (values.Length == 0) return 0;

            int max = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] > max) max = values[i];
            }
            return max;
        }

        private void LogStatistics(SimulationStatistics stats)
        {
            Debug.Log($"[EvoSim Stats #{_recordCount}] " +
                $"Pop: {stats.Population} | " +
                $"Gen: {stats.AverageGeneration:F1}/{stats.MaxGeneration} | " +
                $"Age: {stats.AverageAge:F1}s | " +
                $"Energy: {stats.AverageEnergyPercent:P0}");

            Debug.Log($"  Traits: " +
                $"Size={stats.AverageSize:F2} " +
                $"Speed={stats.AverageSpeed:F2} " +
                $"Sense={stats.AverageSensingRange:F1} " +
                $"FoodAttr={stats.AverageFoodAttraction:F2} " +
                $"HazAvoid={stats.AverageHazardAvoidance:F2}");

            Debug.Log($"  Behavior: " +
                $"Explore={stats.AverageExplorationBias:F2} " +
                $"Social={stats.AverageSocialBehavior:F2} " +
                $"Aggro={stats.AverageAggression:F2} " +
                $"RepMode={stats.AverageReproductionMode:F2} " +
                $"MutRate={stats.AverageMutationRate:F3}");
        }

        private struct SimulationStatistics
        {
            public int Population;
            public float AverageGeneration;
            public int MaxGeneration;
            public float AverageAge;
            public float AverageEnergyPercent;

            // Physical traits
            public float AverageSize;
            public float AverageSpeed;
            public float AverageSensingRange;

            // Survival traits
            public float AverageFoodAttraction;
            public float AverageHazardAvoidance;
            public float AverageEnergyEfficiency;

            // Behavioral traits
            public float AverageAggression;
            public float AverageSocialBehavior;
            public float AverageExplorationBias;

            // Reproductive traits
            public float AverageReproductionThreshold;
            public float AverageReproductionMode;

            // Evolution traits
            public float AverageMutationRate;
        }
    }

    /// <summary>
    /// Manages population limits - culls excess or spawns emergency population
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(StatisticsSystem))]
    public partial class PopulationManagementSystem : SystemBase
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<SimulationConfigComponent>();
        }

        protected override void OnUpdate()
        {
            var config = SystemAPI.GetSingleton<SimulationConfigComponent>();

            var lifeformQuery = SystemAPI.QueryBuilder()
                .WithAll<LifeformTag>()
                .Build();

            int currentPopulation = lifeformQuery.CalculateEntityCount();

            // Cull if over max population
            if (currentPopulation > config.MaxPopulation)
            {
                int excessCount = currentPopulation - config.MaxPopulation;
                CullWeakestLifeforms(excessCount);
            }
            // Emergency spawn if below minimum
            else if (currentPopulation < config.MinPopulation && currentPopulation > 0)
            {
                Debug.LogWarning($"[EvoSim] Population critically low ({currentPopulation}). Emergency spawning disabled - let evolution handle it.");
                // We could implement emergency spawning here if desired
            }
            else if (currentPopulation == 0)
            {
                Debug.LogError("[EvoSim] Population extinct! Simulation ended.");
            }
        }

        private void CullWeakestLifeforms(int cullCount)
        {
            // Collect all lifeforms with their fitness scores
            var entities = new NativeList<Entity>(Allocator.Temp);
            var fitnessScores = new NativeList<float>(Allocator.Temp);

            foreach (var (energy, lifetime, entity) in
                SystemAPI.Query<RefRO<EnergyComponent>, RefRO<LifetimeComponent>>()
                    .WithAll<LifeformTag>()
                    .WithEntityAccess())
            {
                // Fitness = energy % + (age * 0.1) + (children * 5)
                float fitness = energy.ValueRO.EnergyPercent +
                               (lifetime.ValueRO.Age * 0.1f) +
                               (lifetime.ValueRO.ChildrenProduced * 5f);

                entities.Add(entity);
                fitnessScores.Add(fitness);
            }

            // Find indices of weakest lifeforms
            var cullIndices = new NativeList<int>(Allocator.Temp);
            for (int i = 0; i < cullCount && i < entities.Length; i++)
            {
                // Find minimum fitness
                int minIndex = 0;
                float minFitness = float.MaxValue;

                for (int j = 0; j < fitnessScores.Length; j++)
                {
                    if (!cullIndices.Contains(j) && fitnessScores[j] < minFitness)
                    {
                        minFitness = fitnessScores[j];
                        minIndex = j;
                    }
                }

                cullIndices.Add(minIndex);
            }

            // Destroy weakest entities
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            for (int i = 0; i < cullIndices.Length; i++)
            {
                ecb.DestroyEntity(entities[cullIndices[i]]);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();

            Debug.Log($"[EvoSim] Culled {cullCount} weakest lifeforms due to overpopulation");

            // Cleanup
            entities.Dispose();
            fitnessScores.Dispose();
            cullIndices.Dispose();
        }
    }
}