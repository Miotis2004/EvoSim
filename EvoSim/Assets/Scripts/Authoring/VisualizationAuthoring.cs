using Unity.Entities;
using Unity.Rendering;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using EvoSim.Components;

namespace EvoSim.Authoring
{
    /// <summary>
    /// Authoring component for lifeform visualization
    /// Attach to a sphere prefab to use as the lifeform template
    /// </summary>
    public class LifeformVisualizationAuthoring : MonoBehaviour
    {
        [Header("Rendering")]
        [Tooltip("Material to use for lifeforms")]
        public Material LifeformMaterial;

        [Tooltip("Should color change based on energy?")]
        public bool ColorByEnergy = true;

        [Header("Color Scheme")]
        [Tooltip("Color when at full energy")]
        public Color HighEnergyColor = Color.green;

        [Tooltip("Color when at low energy")]
        public Color LowEnergyColor = Color.red;
    }

    public class LifeformVisualizationBaker : Baker<LifeformVisualizationAuthoring>
    {
        public override void Bake(LifeformVisualizationAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add rendering components
            var renderMesh = new RenderMeshArray(
                new Material[] { authoring.LifeformMaterial },
                new Mesh[] { GetComponent<MeshFilter>().sharedMesh }
            );

            RenderMeshUtility.AddComponents(
                entity,
                this,
                new RenderMeshDescription(ShadowCastingMode.On),
                renderMesh,
                MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0)
            );
        }
    }

    /// <summary>
    /// Authoring component for food source visualization
    /// </summary>
    public class FoodSourceVisualizationAuthoring : MonoBehaviour
    {
        [Tooltip("Material for food sources")]
        public Material FoodMaterial;
    }

    public class FoodSourceVisualizationBaker : Baker<FoodSourceVisualizationAuthoring>
    {
        public override void Bake(FoodSourceVisualizationAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            var renderMesh = new RenderMeshArray(
                new Material[] { authoring.FoodMaterial },
                new Mesh[] { GetComponent<MeshFilter>().sharedMesh }
            );

            RenderMeshUtility.AddComponents(
                entity,
                this,
                new RenderMeshDescription(ShadowCastingMode.Off),
                renderMesh,
                MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0)
            );
        }
    }

    /// <summary>
    /// Authoring component for hazard visualization
    /// </summary>
    public class HazardVisualizationAuthoring : MonoBehaviour
    {
        [Tooltip("Material for hazards")]
        public Material HazardMaterial;
    }

    public class HazardVisualizationBaker : Baker<HazardVisualizationAuthoring>
    {
        public override void Bake(HazardVisualizationAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            var renderMesh = new RenderMeshArray(
                new Material[] { authoring.HazardMaterial },
                new Mesh[] { GetComponent<MeshFilter>().sharedMesh }
            );

            RenderMeshUtility.AddComponents(
                entity,
                this,
                new RenderMeshDescription(ShadowCastingMode.Off),
                renderMesh,
                MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0)
            );
        }
    }
}

namespace EvoSim.Systems
{
    /// <summary>
    /// System to update lifeform colors based on energy (optional visual feedback)
    /// Note: This requires URPMaterialPropertyBaseColor component
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LifeformColorSystem : SystemBase
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<LifeformTag>();
        }

        protected override void OnUpdate()
        {
            // This is a simplified version - in practice you'd use MaterialPropertyBaseColor
            // from Unity.Rendering for better performance

            // For now, we'll skip runtime color changes to keep it simple
            // You can extend this later if you want energy-based coloring
        }
    }
}