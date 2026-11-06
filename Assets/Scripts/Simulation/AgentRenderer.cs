using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

// The instanced drawing is a small part taken from an earlier project of mine, adapted here to read ECS data.
namespace HighProject.Simulation
{
    // Draws the agents: one GPU-instanced mesh per kind (humans, infected, zombies), straight from the simulation
    // world's data. Presentation only; it never changes the simulation. Add it to any GameObject in the scene.
    // Materials: assign your own (GPU instancing on), or leave them empty to get plain coloured URP materials.
    public sealed class AgentRenderer : MonoBehaviour
    {
        const int MaxInstancesPerCall = 1023;

        [Header("Look")]
        [Tooltip("Diameter of an agent in metres. Larger than life so they are visible from high above.")]
        [SerializeField] float agentSize = 2f;
        [Tooltip("Height above the ground in metres.")]
        [SerializeField] float height = 1f;
        [Tooltip("Empty = a sphere.")]
        [SerializeField] Mesh mesh = null;

        [Header("Materials (optional)")]
        [SerializeField] Material humanMaterial = null;
        [SerializeField] Material infectedMaterial = null;
        [SerializeField] Material zombieMaterial = null;

        World world;
        EntityQuery query;
        Mesh generatedMesh;
        readonly Material[] generatedMaterials = new Material[3];

        void OnDisable()
        {
            DestroyGenerated();
        }

        void LateUpdate()
        {
            if (world == null || !world.IsCreated)
            {
                world = FindSimulationWorld();
                if (world == null)
                {
                    return;
                }

                query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<AgentPosition>(), ComponentType.ReadOnly<AgentState>());
            }

            int count = query.CalculateEntityCount();
            if (count == 0)
            {
                return;
            }

            // Copying also waits for the simulation jobs that write the positions.
            NativeArray<AgentPosition> positions = query.ToComponentDataArray<AgentPosition>(Allocator.TempJob);
            NativeArray<AgentState> states = query.ToComponentDataArray<AgentState>(Allocator.TempJob);
            var matrices = new NativeArray<Matrix4x4>[3];
            var counts = new NativeArray<int>(3, Allocator.TempJob);
            for (int kind = 0; kind < 3; kind++)
            {
                matrices[kind] = new NativeArray<Matrix4x4>(count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            }

            new BuildMatricesJob
            {
                Positions = positions,
                States = states,
                Scale = agentSize,
                Height = height,
                Humans = matrices[0],
                Infected = matrices[1],
                Zombies = matrices[2],
                Counts = counts
            }.Run();

            EnsureResources();
            for (int kind = 0; kind < 3; kind++)
            {
                Draw(matrices[kind], counts[kind], MaterialFor(kind));
            }

            positions.Dispose();
            states.Dispose();
            counts.Dispose();
            for (int kind = 0; kind < 3; kind++)
            {
                matrices[kind].Dispose();
            }
        }

        void Draw(NativeArray<Matrix4x4> instances, int count, Material material)
        {
            var parameters = new RenderParams(material)
            {
                worldBounds = new Bounds(Vector3.zero, new Vector3(10000f, 1000f, 10000f)),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false
            };

            for (int start = 0; start < count; start += MaxInstancesPerCall)
            {
                Graphics.RenderMeshInstanced(parameters, mesh != null ? mesh : generatedMesh, 0, instances,
                    Mathf.Min(MaxInstancesPerCall, count - start), start);
            }
        }

        Material MaterialFor(int kind)
        {
            Material assigned = kind == 0 ? humanMaterial : kind == 1 ? infectedMaterial : zombieMaterial;
            return assigned != null ? assigned : generatedMaterials[kind];
        }

        void EnsureResources()
        {
            if (mesh == null && generatedMesh == null)
            {
                GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                generatedMesh = primitive.GetComponent<MeshFilter>().sharedMesh;
                Destroy(primitive);
            }

            Color[] colors = { new Color(0.2f, 0.8f, 0.3f), new Color(0.95f, 0.8f, 0.2f), new Color(0.9f, 0.15f, 0.15f) };
            for (int kind = 0; kind < 3; kind++)
            {
                if (generatedMaterials[kind] == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                    var material = new Material(shader) { name = "Agents (generated)", enableInstancing = true, hideFlags = HideFlags.HideAndDontSave };
                    material.SetColor("_BaseColor", colors[kind]);
                    generatedMaterials[kind] = material;
                }
            }
        }

        void DestroyGenerated()
        {
            for (int kind = 0; kind < 3; kind++)
            {
                if (generatedMaterials[kind] != null)
                {
                    Destroy(generatedMaterials[kind]);
                    generatedMaterials[kind] = null;
                }
            }

            // The primitive's mesh is Unity's shared built-in sphere; nothing to destroy.
            generatedMesh = null;
        }

        // The Netcode server world if there is one, otherwise a plain game world.
        static World FindSimulationWorld()
        {
            World local = null;
            foreach (World candidate in World.All)
            {
                if (!candidate.IsCreated)
                {
                    continue;
                }

                if ((candidate.Flags & WorldFlags.GameServer) == WorldFlags.GameServer)
                {
                    return candidate;
                }

                if (candidate.Flags == WorldFlags.Game)
                {
                    local = candidate;
                }
            }

            return local;
        }

        [BurstCompile]
        struct BuildMatricesJob : IJob
        {
            [ReadOnly] public NativeArray<AgentPosition> Positions;
            [ReadOnly] public NativeArray<AgentState> States;
            public float Scale;
            public float Height;
            public NativeArray<Matrix4x4> Humans;
            public NativeArray<Matrix4x4> Infected;
            public NativeArray<Matrix4x4> Zombies;
            public NativeArray<int> Counts;

            public void Execute()
            {
                int humans = 0;
                int infected = 0;
                int zombies = 0;
                for (int i = 0; i < Positions.Length; i++)
                {
                    float2 p = Positions[i].Value;
                    // Uniform scale plus translation, written out because Matrix4x4.TRS is not available inside Burst.
                    var matrix = new Matrix4x4(new Vector4(Scale, 0f, 0f, 0f), new Vector4(0f, Scale, 0f, 0f),
                        new Vector4(0f, 0f, Scale, 0f), new Vector4(p.x, Height, p.y, 1f));
                    switch (States[i].Kind)
                    {
                        case AgentKind.Human:
                            Humans[humans++] = matrix;
                            break;
                        case AgentKind.Infected:
                            Infected[infected++] = matrix;
                            break;
                        default:
                            Zombies[zombies++] = matrix;
                            break;
                    }
                }

                Counts[0] = humans;
                Counts[1] = infected;
                Counts[2] = zombies;
            }
        }
    }
}
