using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;
using EvoSim.Components;
using EvoSim.Systems;

namespace EvoSim.Systems
{
    /// <summary>
    /// Handles all lifeform movement including random exploration and directed movement
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct MovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationConfigComponent>();
            state.RequireForUpdate<LifeformTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<SimulationConfigComponent>();
            float deltaTime = SystemAPI.Time.DeltaTime;
            float halfWorld = config.WorldSize / 2f;

            // Create a job to handle movement for all lifeforms
            var movementJob = new MovementJob
            {
                DeltaTime = deltaTime,
                HalfWorldSize = halfWorld,
                Random = Unity.Mathematics.Random.CreateFromIndex((uint)(SystemAPI.Time.ElapsedTime * 1000))
            };

            movementJob.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct MovementJob : IJobEntity
        {
            public float DeltaTime;
            public float HalfWorldSize;
            public Unity.Mathematics.Random Random;

            private void Execute(
                ref LocalTransform transform,
                ref MovementComponent movement,
                in GenomeComponent genome,
                in SensingComponent sensing,
                in EnergyComponent energy)
            {
                // Don't move if starving (too weak)
                if (energy.IsStarving) return;

                // Update direction change timer
                movement.DirectionChangeTimer -= DeltaTime;

                // Determine movement direction based on sensing and genome
                float3 finalDirection = float3.zero;

                // Exploration vs Exploitation balance
                float explorationRoll = Random.NextFloat();
                bool shouldExplore = explorationRoll < genome.ExplorationBias;

                if (shouldExplore || !sensing.HasDetectedFood)
                {
                    // Random exploration mode
                    if (movement.DirectionChangeTimer <= 0)
                    {
                        // Pick a new random direction
                        movement.CurrentDirection = Random.NextFloat3Direction();
                        movement.DirectionChangeTimer = movement.DirectionChangeInterval;
                    }

                    finalDirection = movement.CurrentDirection;
                }
                else
                {
                    // Directed movement mode - move toward/away from detected objects
                    float3 directionToFood = math.normalizesafe(
                        sensing.NearestFoodPosition - transform.Position
                    );

                    // Blend between random exploration and food attraction based on genome
                    float attractionStrength = math.clamp(genome.FoodAttraction, -1f, 1f);
                    finalDirection = math.normalizesafe(
                        movement.CurrentDirection * (1f - math.abs(attractionStrength)) +
                        directionToFood * attractionStrength
                    );

                    movement.CurrentDirection = finalDirection;
                }

                // Apply hazard avoidance if hazard detected
                if (sensing.HasDetectedHazard && genome.HazardAvoidance > 0)
                {
                    float3 directionFromHazard = math.normalizesafe(
                        transform.Position - sensing.NearestHazardPosition
                    );

                    // Blend away from hazard based on avoidance strength
                    float avoidanceWeight = genome.HazardAvoidance *
                        (1f - sensing.NearestHazardDistance / sensing.Range);

                    finalDirection = math.normalizesafe(
                        finalDirection * (1f - avoidanceWeight) +
                        directionFromHazard * avoidanceWeight
                    );
                }

                // Apply social behavior (attraction/repulsion to other lifeforms)
                if (sensing.NearbyLifeformCount > 0 && math.abs(genome.SocialBehavior) > 0.01f)
                {
                    // This is a simplified version - real social behavior would need
                    // direction to center of mass of nearby lifeforms
                    // For now, just add some randomness based on social behavior
                    float socialInfluence = genome.SocialBehavior * 0.2f;
                    float3 socialPerturbation = Random.NextFloat3Direction() * socialInfluence;
                    finalDirection = math.normalizesafe(finalDirection + socialPerturbation);
                }

                // Calculate speed based on genome and energy
                float speedMultiplier = genome.SpeedMultiplier;

                // Slow down when low on energy
                float energyFactor = math.clamp(energy.EnergyPercent * 2f, 0.5f, 1f);
                speedMultiplier *= energyFactor;

                float finalSpeed = movement.BaseSpeed * speedMultiplier;

                // Update position
                float3 newPosition = transform.Position + finalDirection * finalSpeed * DeltaTime;

                // Boundary handling - bounce off walls
                if (math.abs(newPosition.x) > HalfWorldSize)
                {
                    newPosition.x = math.clamp(newPosition.x, -HalfWorldSize, HalfWorldSize);
                    movement.CurrentDirection.x *= -1f;
                }
                if (math.abs(newPosition.y) > HalfWorldSize)
                {
                    newPosition.y = math.clamp(newPosition.y, -HalfWorldSize, HalfWorldSize);
                    movement.CurrentDirection.y *= -1f;
                }
                if (math.abs(newPosition.z) > HalfWorldSize)
                {
                    newPosition.z = math.clamp(newPosition.z, -HalfWorldSize, HalfWorldSize);
                    movement.CurrentDirection.z *= -1f;
                }

                transform.Position = newPosition;
            }
        }
    }
}