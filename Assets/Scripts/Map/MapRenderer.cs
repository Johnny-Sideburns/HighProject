using System.Collections.Generic;
using UnityEngine;

namespace HighProject.Map
{
    // Draws the baked map with ordinary GameObjects: a ground plane with the baked ground texture and one mesh per
    // building tile. This is presentation only (the simulation reads the MapBlob, never these objects). It works in
    // edit mode too, so the map shows up in the Scene view as soon as an asset is assigned. The generated objects
    // are not saved in the scene.
    // Materials: assign your own, or leave them empty to get plain URP materials created at runtime.
    [ExecuteAlways]
    public sealed class MapRenderer : MonoBehaviour
    {
        const string GeneratedRootName = "Map (generated)";

        [SerializeField] MapAsset mapAsset = null;

        [Header("Materials (optional)")]
        [Tooltip("Needs a texture slot named _BaseMap or _MainTex. Empty = generated URP Unlit material.")]
        [SerializeField] Material groundMaterial = null;
        [Tooltip("Material for the roofs (submesh 0). Empty = generated URP Lit material.")]
        [SerializeField] Material roofMaterial = null;
        [Tooltip("Material for the walls (submesh 1). Empty = generated URP Lit material.")]
        [SerializeField] Material wallMaterial = null;

        [Header("Layout")]
        [Tooltip("Height of the ground plane in metres. Slightly below 0 so building walls never flicker against it.")]
        [SerializeField] float groundHeight = -0.05f;

        GameObject generatedRoot;
        readonly List<Object> generatedObjects = new List<Object>();

#if UNITY_EDITOR
        bool rebuildQueued;
#endif

        void OnEnable()
        {
            Rebuild();
        }

        void OnDisable()
        {
            Clear();
        }

#if UNITY_EDITOR
        // Called when a value changes in the Inspector (for example a new map asset is assigned). Objects must not be
        // created inside OnValidate, so the rebuild runs right after it.
        void OnValidate()
        {
            if (rebuildQueued)
            {
                return;
            }

            rebuildQueued = true;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                rebuildQueued = false;
                if (this != null && isActiveAndEnabled)
                {
                    Rebuild();
                }
            };
        }
#endif

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            Clear();

            if (mapAsset == null)
            {
                return;
            }

            if (!mapAsset.IsConsistent())
            {
                Debug.LogWarning($"MapRenderer: the map asset '{mapAsset.name}' is damaged or from another version. Bake and save it again.", this);
                return;
            }

            generatedRoot = new GameObject(GeneratedRootName) { hideFlags = HideFlags.DontSave };
            generatedRoot.transform.SetParent(transform, false);

            CreateGround();
            CreateBuildings();
        }

        void CreateGround()
        {
            float x0 = (float)mapAsset.OriginX;
            float z0 = (float)mapAsset.OriginZ;
            float x1 = x0 + mapAsset.Width * mapAsset.CellSize;
            float z1 = z0 + mapAsset.Height * mapAsset.CellSize;

            var mesh = new Mesh { name = "Ground (generated)", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[]
            {
                new Vector3(x0, groundHeight, z0), new Vector3(x1, groundHeight, z0),
                new Vector3(x1, groundHeight, z1), new Vector3(x0, groundHeight, z1)
            };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };

            // Clockwise as seen from above is the front side in Unity.
            mesh.triangles = new[] { 0, 3, 2, 0, 2, 1 };
            generatedObjects.Add(mesh);

            Material material = groundMaterial != null ? groundMaterial : CreateMaterial("Universal Render Pipeline/Unlit", Color.white);
            if (mapAsset.GroundTexture != null)
            {
                // The shader's main texture slot is _BaseMap in URP and _MainTex in the built-in shaders.
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", mapAsset.GroundTexture);
                }
                else
                {
                    material.mainTexture = mapAsset.GroundTexture;
                }
            }

            AddRenderer("Ground", mesh, new[] { material });
        }

        void CreateBuildings()
        {
            Material roof = roofMaterial != null ? roofMaterial : CreateMaterial("Universal Render Pipeline/Lit", new Color(0.82f, 0.82f, 0.86f));
            Material wall = wallMaterial != null ? wallMaterial : CreateMaterial("Universal Render Pipeline/Lit", new Color(0.72f, 0.68f, 0.62f));

            foreach (Mesh mesh in mapAsset.BuildingMeshes)
            {
                if (mesh != null)
                {
                    AddRenderer(mesh.name, mesh, new[] { roof, wall });
                }
            }
        }

        void AddRenderer(string objectName, Mesh mesh, Material[] materials)
        {
            var child = new GameObject(objectName) { hideFlags = HideFlags.DontSave };
            child.transform.SetParent(generatedRoot.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterials = materials;
        }

        // A simple material for the given shader, or the built-in Standard shader if that one is not available
        // (for example when the project does not use URP).
        Material CreateMaterial(string shaderName, Color color)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader) { name = shaderName + " (generated)", hideFlags = HideFlags.DontSave };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            generatedObjects.Add(material);
            return material;
        }

        void Clear()
        {
            foreach (Object generated in generatedObjects)
            {
                DestroyGenerated(generated);
            }

            generatedObjects.Clear();

            if (generatedRoot != null)
            {
                DestroyGenerated(generatedRoot);
                generatedRoot = null;
            }
        }

        // Destroy during play, DestroyImmediate in edit mode (where Destroy is not allowed).
        static void DestroyGenerated(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
