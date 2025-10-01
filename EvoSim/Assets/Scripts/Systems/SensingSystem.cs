using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using EvoSim.Components;
using EvoSim.Systems;

namespace EvoSim.Systems
{
    /// <summary>
    /// Updates what each lifeform can sense in its environment
    /// Uses spatial partitioning and batched updates for performance
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(MovementSystem))]
    public partial struct SensingSystem : ISystem
    {
        private int _currentBatchOffset;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationConfigComponent>();
            state.RequireForUpdate<LifeformTag>();
            _currentBatchOffset = 0;
        }

        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<SimulationConfigComponent>();

            // Get total lifeform count
            var lifeformQuery = SystemAPI.QueryBuilder()
                .WithAll<LifeformTag, LocalTransform, SensingComponent>()
                .Build();

            int totalLifeforms = lifeformQuery.CalculateEntityCount();

            if (totalLifeforms == 0) return;

            // Update only a batch of lifeforms per frame for performance
            int batchSize = config.SensingUpdatesPerFrame;
            int startIndex = _currentBatchOffset;
            int endIndex = math.min(startIndex + batchSize, totalLifeforms);

            // Gather all food and hazard positions for quick lookup
            var foodPositions = new NativeList<float3>(Allocator.Temp);
            var hazardPositions = new NativeList<float3>(Allocator.Temp);
            var hazardRadii = new NativeList<float>(Allocator.Temp);

            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<FoodSourceTag>())
            {
                foodPositions.Add(transform.ValueRO.Position);
            }

            foreach (var (transform, hazard) in
                SystemAPI.Query<RefRO<LocalTransform>, RefRO<HazardTag>>())
            {
                hazardPositions.Add(transform.ValueRO.Position);
                hazardRadii.Add(hazard.ValueRO.EffectRadius);
            }

            // Gather lifeform positions for social sensing
            var lifeformPositions = new NativeList<float3>(Allocator.Temp);
            foreach (var transform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<LifeformTag>())
            {
                lifeformPositions.Add(transform.ValueRO.Position);
            }

            // Update sensing for the current batch
            int currentIndex = 0;
            foreach (var (transform, sensing, genome) in
                SystemAPI.Query<RefRO<LocalTransform>, RefRW<SensingComponent>, RefRO<GenomeComponent>>()
                    .WithAll<LifeformTag>())
            {
                if (currentIndex < startIndex)
                {
                    currentIndex++;
                    continue;
                }

                if (currentIndex >= endIndex)
                    break;

                UpdateLifeformSensing(
                    transform.ValueRO,
                    sensing,
                    genome.ValueRO,
                    foodPositions,
                    hazardPositions,
                    hazardRadii,
                    lifeformPositions
                );

                currentIndex++;
            }

            // Move to next batch
            _currentBatchOffset = (endIndex >= totalLifeforms) ? 0 : endIndex;

            // Cleanup
            foodPositions.Dispose();
            hazardPositions.Dispose();
            hazardRadii.Dispose();
            lifeformPositions.Dispose();
        }

        private void UpdateLifeformSensing(
            LocalTransform transform,
            RefRW<SensingComponent> sensing,
            GenomeComponent genome,
            NativeList<float3> foodPositions,
            NativeList<float3> hazardPositions,
            NativeList<float> hazardRadii,
            NativeList<float3> lifeformPositions)
        {
            float3 position = transform.Position;
            float sensingRange = genome.SensingRange;
            float sensingRangeSq = sensingRange * sensingRange;

            // Reset sensing data
            sensing.ValueRW.HasDetectedFood = false;
            sensing.ValueRW.NearestFoodDistance = float.MaxValue;
            sensing.ValueRW.HasDetectedHazard = false;
            sensing.ValueRW.NearestHazardDistance = float.MaxValue;
            sensing.ValueRW.NearbyLifeformCount = 0;

            // Find nearest food
            for (int i = 0; i < foodPositions.Length; i++)
            {
                float distanceSq = math.distancesq(position, foodPositions[i]);

                if (distanceSq <= sensingRangeSq)
                {
                    float distance = math.sqrt(distanceSq);

                    if (distance < sensing.ValueRO.NearestFoodDistance)
                    {
                        sensing.ValueRW.HasDetectedFood = true;
                        sensing.ValueRW.NearestFoodDistance = distance;
                        sensing.ValueRW.NearestFoodPosition = foodPositions[i];
                    }
                }
            }

            // Find nearest hazard (considering hazard radius)
            for (int i = 0; i < hazardPositions.Length; i++)
            {
                float distanceSq = math.distancesq(position, hazardPositions[i]);
                float detectionRange = sensingRange + hazardRadii[i];
                float detectionRangeSq = detectionRange * detectionRange;

                if (distanceSq <= detectionRangeSq)
                {
                    float distance = math.sqrt(distanceSq);

                    if (distance < sensing.ValueRO.NearestHazardDistance)
                    {
                        sensing.ValueRW.HasDetectedHazard = true;
                        sensing.ValueRW.NearestHazardDistance = distance;
                        sensing.ValueRW.NearestHazardPosition = hazardPositions[i];
                    }
                }
            }

            // Count nearby lifeforms (excluding self)
            int nearbyCount = 0;
            for (int i = 0; i < lifeformPositions.Length; i++)
            {
                // Skip if it's the same position (self)
                if (math.distancesq(position, lifeformPositions[i]) < 0.01f)
                    continue;

                float distanceSq = math.distancesq(position, lifeformPositions[i]);

                if (distanceSq <= sensingRangeSq)
                {
                    nearbyCount++;
                }
            }

            sensing.ValueRW.NearbyLifeformCount = nearbyCount;
        }
    }

    /// <summary>
    /// Updates spatial hash grid for each lifeform
    /// This can be used later for more efficient neighbor queries
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MovementSystem))]
    public partial struct SpatialHashUpdateSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationConfigComponent>();
            state.RequireForUpdate<LifeformTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<SimulationConfigComponent>();
            float cellSize = config.SpatialGridCellSize;

            foreach (var (transform, spatialHash) in
                SystemAPI.Query<RefRO<LocalTransform>, RefRW<SpatialHashComponent>>()
                    .WithAll<LifeformTag>())
            {
                var gridPos = SpatialHashComponent.WorldToGrid(transform.ValueRO.Position, cellSize);

                spatialHash.ValueRW.GridX = gridPos.x;
                spatialHash.ValueRW.GridY = gridPos.y;
                spatialHash.ValueRW.GridZ = gridPos.z;
            }
        }
    }
}