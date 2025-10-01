using Unity.Entities;
using Unity.Mathematics;

namespace EvoSim.Components
{
    /// <summary>
    /// Contains all evolvable genetic traits for a lifeform
    /// </summary>
    public struct GenomeComponent : IComponentData
    {
        // Physical Traits
        public float Size;                      // 0.5 to 2.0 - affects visibility, energy cost
        public float SpeedMultiplier;           // 0.5 to 2.0 - movement speed modifier

        // Sensory Traits
        public float SensingRange;              // How far they can detect objects
        public float FoodAttraction;            // -1 to 1 - attraction/repulsion to food
        public float HazardAvoidance;           // 0 to 1 - how strongly they avoid hazards

        // Energy Traits
        public float EnergyEfficiency;          // 0.5 to 2.0 - affects consumption rate
        public float MaxEnergyMultiplier;       // 0.5 to 2.0 - affects max energy capacity

        // Behavioral Traits
        public float Aggression;                // 0 to 1 - future: affects predation
        public float SocialBehavior;            // -1 to 1 - attraction/repulsion to others
        public float ExplorationBias;           // 0 to 1 - random vs directed movement

        // Reproductive Traits
        public float ReproductionThreshold;     // 0.5 to 1.0 - % of max energy needed to reproduce
        public float ReproductionMode;          // 0 = asexual, 1 = sexual, 0.5 = facultative
        public int OffspringCount;              // 1 to 3

        // Evolution Traits
        public float MutationRate;              // 0.01 to 0.2 - chance of trait mutation
        public float MutationStrength;          // 0.05 to 0.3 - magnitude of mutations

        // Generate random genome with default values
        public static GenomeComponent CreateRandom(ref Unity.Mathematics.Random random)
        {
            return new GenomeComponent
            {
                Size = random.NextFloat(0.8f, 1.2f),
                SpeedMultiplier = random.NextFloat(0.8f, 1.2f),
                SensingRange = random.NextFloat(8f, 12f),
                FoodAttraction = random.NextFloat(0.5f, 1f),
                HazardAvoidance = random.NextFloat(0.5f, 1f),
                EnergyEfficiency = random.NextFloat(0.8f, 1.2f),
                MaxEnergyMultiplier = random.NextFloat(0.8f, 1.2f),
                Aggression = random.NextFloat(0f, 0.3f),
                SocialBehavior = random.NextFloat(-0.3f, 0.3f),
                ExplorationBias = random.NextFloat(0.3f, 0.7f),
                ReproductionThreshold = random.NextFloat(0.6f, 0.8f),
                ReproductionMode = random.NextFloat(0f, 0.5f),
                OffspringCount = random.NextInt(1, 3),
                MutationRate = random.NextFloat(0.05f, 0.15f),
                MutationStrength = random.NextFloat(0.1f, 0.2f)
            };
        }
    }

    /// <summary>
    /// Manages energy state for lifeform survival
    /// </summary>
    public struct EnergyComponent : IComponentData
    {
        public float CurrentEnergy;
        public float MaxEnergy;                 // Base 100, modified by genome
        public float ConsumptionRate;           // Energy per second, modified by size and efficiency

        public bool IsStarving => CurrentEnergy <= 0;
        public float EnergyPercent => CurrentEnergy / MaxEnergy;
        public bool CanReproduce(float threshold) => EnergyPercent >= threshold;
    }

    /// <summary>
    /// Controls movement behavior and state
    /// </summary>
    public struct MovementComponent : IComponentData
    {
        public float BaseSpeed;                 // Base movement speed
        public float3 CurrentDirection;         // Normalized direction vector
        public float DirectionChangeTimer;      // Time until next direction change
        public float DirectionChangeInterval;   // How often to pick new random direction
        public float3 TargetPosition;           // For directed movement toward food/away from hazards
        public bool HasTarget;
    }

    /// <summary>
    /// Tracks what the lifeform can sense in its environment
    /// </summary>
    public struct SensingComponent : IComponentData
    {
        public float Range;
        public float3 NearestFoodPosition;
        public float NearestFoodDistance;
        public bool HasDetectedFood;

        public float3 NearestHazardPosition;
        public float NearestHazardDistance;
        public bool HasDetectedHazard;

        public int NearbyLifeformCount;         // For social behavior
    }

    /// <summary>
    /// Manages reproduction state and cooldowns
    /// </summary>
    public struct ReproductionComponent : IComponentData
    {
        public float ReproductionCooldown;      // Time until can reproduce again
        public float CooldownTimer;
        public bool IsReadyToReproduce => CooldownTimer <= 0;
    }

    /// <summary>
    /// Tracks lifeform lifetime statistics
    /// </summary>
    public struct LifetimeComponent : IComponentData
    {
        public float Age;                       // Time alive in seconds
        public int Generation;                  // Generation number (0 = initial)
        public int ChildrenProduced;            // Fitness metric
    }

    /// <summary>
    /// Spatial hashing for efficient neighbor queries
    /// </summary>
    public struct SpatialHashComponent : IComponentData
    {
        public int GridX;
        public int GridY;
        public int GridZ;

        public static int3 WorldToGrid(float3 position, float cellSize)
        {
            return new int3(
                (int)math.floor(position.x / cellSize),
                (int)math.floor(position.y / cellSize),
                (int)math.floor(position.z / cellSize)
            );
        }
    }

    /// <summary>
    /// Tag component to identify lifeform entities
    /// </summary>
    public struct LifeformTag : IComponentData { }

    /// <summary>
    /// Tag component for food sources
    /// </summary>
    public struct FoodSourceTag : IComponentData
    {
        public float EnergyProvided;            // Amount of energy this food gives
        public float RegenerationRate;          // How fast it regenerates
        public float CurrentEnergy;             // Current available energy
        public float MaxEnergy;
    }

    /// <summary>
    /// Tag component for hazards
    /// </summary>
    public struct HazardTag : IComponentData
    {
        public float DamagePerSecond;
        public float EffectRadius;
    }
}