using EvoSim.Components;
using EvoSim.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace EvoSim.Systems
{
    /// <summary>
    /// Manages energy consumption and handles starvation deaths
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MovementSystem))]
    [BurstCompile]
    public partial struct EnergySystem : ISystem
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

            // First pass: consume energy
            var consumptionJob = new EnergyConsumptionJob
            {
                DeltaTime = deltaTime,
                SizeEnergyCostFactor = config.SizeEnergyCostFactor
            };
            consumptionJob.ScheduleParallel();

            // Second pass: destroy starved entities
            // This needs to run on main thread as we're destroying entities
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (energy, entity) in
                SystemAPI.Query<RefRO<EnergyComponent>>()
                    .WithAll<LifeformTag>()
                    .WithEntityAccess())
            {
                if (energy.ValueRO.IsStarving)
                {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        public partial struct EnergyConsumptionJob : IJobEntity
        {
            public float DeltaTime;
            public float SizeEnergyCostFactor;

            private void Execute(
                ref EnergyComponent energy,
                in GenomeComponent genome,
                in LifetimeComponent lifetime)
            {
                // Calculate energy consumption based on size and efficiency
                float sizeCost = 1f + (genome.Size - 1f) * SizeEnergyCostFactor;
                float totalConsumption = energy.ConsumptionRate * sizeCost * DeltaTime;

                // Consume energy
                energy.CurrentEnergy -= totalConsumption;

                // Clamp to valid range
                energy.CurrentEnergy = math.clamp(energy.CurrentEnergy, 0f, energy.MaxEnergy);
            }
        }
    }

    /// <summary>
    /// Handles food consumption when lifeforms are near food sources
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnergySystem))]
    public partial struct FoodConsumptionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationConfigComponent>();
            state.RequireForUpdate<LifeformTag>();
            state.RequireForUpdate<FoodSourceTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<SimulationConfigComponent>();
            float deltaTime = SystemAPI.Time.DeltaTime;
            float consumptionRadius = config.FoodConsumptionRadius;
            float consumptionRadiusSq = consumptionRadius * consumptionRadius;

            // For each lifeform, check nearby food sources
            foreach (var (transform, energy, entity) in
                SystemAPI.Query<RefRO<LocalTransform>, RefRW<EnergyComponent>>()
                    .WithAll<LifeformTag>()
                    .WithEntityAccess())
            {
                // Skip if already at max energy
                if (energy.ValueRO.CurrentEnergy >= energy.ValueRO.MaxEnergy)
                    continue;

                var lifeformPos = transform.ValueRO.Position;

                // Check all food sources
                foreach (var (foodTransform, foodSource) in
                    SystemAPI.Query<RefRO<LocalTransform>, RefRW<FoodSourceTag>>())
                {
                    float distanceSq = math.distancesq(lifeformPos, foodTransform.ValueRO.Position);

                    if (distanceSq <= consumptionRadiusSq && foodSource.ValueRO.CurrentEnergy > 0)
                    {
                        // Calculate how much energy to transfer
                        float energyNeeded = energy.ValueRO.MaxEnergy - energy.ValueRO.CurrentEnergy;
                        float energyToTransfer = math.min(
                            energyNeeded,
                            math.min(foodSource.ValueRO.CurrentEnergy, foodSource.ValueRO.EnergyProvided * deltaTime)
                        );

                        // Transfer energy
                        energy.ValueRW.CurrentEnergy += energyToTransfer;
                        foodSource.ValueRW.CurrentEnergy -= energyToTransfer;

                        // Only consume from one food source per frame
                        break;
                    }
                }
            }

            // Regenerate food sources
            foreach (var foodSource in SystemAPI.Query<RefRW<FoodSourceTag>>())
            {
                if (foodSource.ValueRO.CurrentEnergy < foodSource.ValueRO.MaxEnergy)
                {
                    foodSource.ValueRW.CurrentEnergy = math.min(
                        foodSource.ValueRO.MaxEnergy,
                        foodSource.ValueRO.CurrentEnergy + foodSource.ValueRO.RegenerationRate * deltaTime
                    );
                }
            }
        }
    }

    /// <summary>
    /// Applies damage from hazards to nearby lifeforms
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FoodConsumptionSystem))]
    public partial struct HazardSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationConfigComponent>();
            state.RequireForUpdate<LifeformTag>();
            state.RequireForUpdate<HazardTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // For each lifeform, check if it's in a hazard zone
            foreach (var (transform, energy) in
                SystemAPI.Query<RefRO<LocalTransform>, RefRW<EnergyComponent>>()
                    .WithAll<LifeformTag>())
            {
                var lifeformPos = transform.ValueRO.Position;

                // Check all hazards
                foreach (var (hazardTransform, hazard) in
                    SystemAPI.Query<RefRO<LocalTransform>, RefRO<HazardTag>>())
                {
                    float distance = math.distance(lifeformPos, hazardTransform.ValueRO.Position);

                    if (distance <= hazard.ValueRO.EffectRadius)
                    {
                        // Apply damage (more damage closer to center)
                        float damageMultiplier = 1f - (distance / hazard.ValueRO.EffectRadius);
                        float damage = hazard.ValueRO.DamagePerSecond * damageMultiplier * deltaTime;

                        energy.ValueRW.CurrentEnergy -= damage;
                    }
                }
            }
        }
    }
}