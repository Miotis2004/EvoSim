using Unity.Entities;
using UnityEngine;
using EvoSim.Data;
using EvoSim.Systems;

namespace EvoSim.Authoring
{
    /// <summary>
    /// MonoBehaviour authoring component to convert simulation config to ECS
    /// Place this on a GameObject in your scene to initialize the simulation
    /// </summary>
    public class SimulationAuthoring : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Reference to the Simulation Config ScriptableObject")]
        private SimulationConfig config;

        public SimulationConfig Config => config;

        private void OnValidate()
        {
            if (config == null)
            {
                Debug.LogWarning("[EvoSim] No SimulationConfig assigned! Create one via: Create -> EvoSim -> Simulation Config");
            }
        }
    }

    /// <summary>
    /// Baker to convert authoring component to ECS data
    /// </summary>
    public class SimulationBaker : Baker<SimulationAuthoring>
    {
        public override void Bake(SimulationAuthoring authoring)
        {
            if (authoring.Config == null)
            {
                Debug.LogError("[EvoSim] Cannot bake SimulationAuthoring without a config!");
                return;
            }

            var entity = GetEntity(TransformUsageFlags.None);

            // Convert ScriptableObject to ECS component
            var configComponent = SimulationConfigComponent.FromScriptableObject(authoring.Config);
            AddComponent(entity, configComponent);

            Debug.Log("[EvoSim] Simulation config baked successfully");
        }
    }
}