using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using NVector = System.Numerics.Vector3;
using Object = UnityEngine.Object;

namespace ApexUrpLab
{
    public sealed class LabWorld : MonoBehaviour
    {
        private const int GroundLayer = 8, WallLayer = 9;
        private readonly List<Mesh> privateMeshes = new();
        private readonly Dictionary<Material, Material> instancedMaterials = new();
        private readonly List<PlantCell> cells = new();
        private readonly Plane[] viewVolume = new Plane[6];
        private static readonly string[] Species = { "fir", "broadleaf", "shrub", "fern", "grass" };
        private PlantPrototype[] plants;
        private Renderer[] visualTiles;
        private Camera view;
        private bool isForest;
        private float tileCheckAt;
        public int Detail { get; set; }
        public LapCourse Course { get; private set; }

        private sealed class PlantCell
        {
            public Bounds Bounds;
            public int Species;
            public Matrix4x4[] Positions;
        }

        private sealed class PlantPrototype
        {
            public Mesh Mesh;
            public Material[] Materials;
        }

        public static Vector3 UnityPoint(NVector p) => new Vector3(p.X, p.Y, p.Z);
        public static NVector CorePoint(Vector3 p) => new NVector(p.x, p.y, p.z);
        public static T Load<T>(string id) where T : Object
        {
            var resource = Resources.Load<T>("APEX/" + id);
            if (!resource) throw new InvalidOperationException("Missing APEX resource: " + id);
            return resource;
        }

        public void Configure(bool forest, Camera camera)
        {
            isForest = forest;
            view = camera;
            string title = forest ? "Forest" : "Marina";
            Course = LapCourse.Decode(Load<TextAsset>(title + "Route").bytes);
            var scenery = Instantiate(Load<GameObject>(title), transform);
            visualTiles = scenery.GetComponentsInChildren<Renderer>(true);
            ApplyScans();
            if (forest)
            {
                BuildGround();
                BuildPlants();
            }
            else
            {
                var flat = new GameObject("Marina ground") { layer = GroundLayer };
                flat.transform.SetParent(transform, false);
                var ground = flat.AddComponent<BoxCollider>();
                ground.center = new Vector3(0, -.5f, 0);
                ground.size = new Vector3(900f, 1f, 980f);
            }
            BuildRails();
        }

        private void ApplyScans()
        {
            Material road = Load<Material>("Materials/Road");
            Material water = Load<Material>("Materials/Water");
            Material waterfall = Load<Material>("Materials/Falls");
            Material verge = Load<Material>("Materials/Verge");
            Material floor = Load<Material>("Materials/Floor");
            Material apron = Load<Material>("Materials/Apron");
            foreach (var tile in visualTiles)
            {
                Material[] materials = tile.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (!materials[i]) continue;
                    string name = materials[i].name.ToLowerInvariant();
                    if (name == "asphalt") materials[i] = road;
                    else if (name is "water" or "river") materials[i] = water;
                    else if (name == "falls") materials[i] = waterfall;
                    else if (isForest && name == "duff") materials[i] = verge;
                    else if (isForest && name == "forest") materials[i] = floor;
                    else if (isForest && name == "pit") materials[i] = apron;
                }
                tile.sharedMaterials = materials;
            }
        }

        private void BuildGround()
        {
            byte[] source = Load<TextAsset>("ground").bytes;
            if (source.Length % 36 != 0) throw new InvalidDataException("Forest triangle payload is invalid.");
            // Preserve the authored collision surfaces. glTFast reflects X on visuals,
            // so reflect X and reverse the affected triangles in the collider too.
            var vertices = new List<Vector3>();
            var indices = new int[source.Length / 12];
            var unique = new Dictionary<Vector3, int>();
            for (int i = 0; i < indices.Length; i++)
            {
                int offset = 12 * i;
                var point = new Vector3(-BitConverter.ToSingle(source, offset),
                    BitConverter.ToSingle(source, offset + 4), BitConverter.ToSingle(source, offset + 8));
                if (!unique.TryGetValue(point, out int index))
                {
                    index = vertices.Count;
                    unique.Add(point, index);
                    vertices.Add(point);
                }
                indices[i] = index;
            }
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                if (Vector3.Cross(b - a, c - a).y < 0)
                    (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);
            }
            var mesh = new Mesh { name = "V1.12 authored road and support", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.triangles = indices;
            mesh.RecalculateBounds();
            privateMeshes.Add(mesh);
            var node = new GameObject("Road and terrain collision") { layer = GroundLayer };
            node.transform.SetParent(transform, false);
            node.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        private void BuildRails()
        {
            // Thin solid prisms preserve both sides of every authored rail.
            var points = new List<Vector3>(Course.Rails.Length * 8);
            var triangles = new List<int>(Course.Rails.Length * 36);
            foreach (var wall in Course.Rails)
            {
                Vector3 a = UnityPoint(wall.A), b = UnityPoint(wall.B), top = Vector3.up * wall.Height;
                Vector3 edge = Vector3.Cross(Vector3.up, b - a).normalized * .055f;
                if (edge.sqrMagnitude < .0001f) continue;
                int i = points.Count;
                points.Add(a + edge); points.Add(b + edge); points.Add(b - edge); points.Add(a - edge);
                points.Add(a + edge + top); points.Add(b + edge + top);
                points.Add(b - edge + top); points.Add(a - edge + top);
                Vector3 center = (a + b + top) * .5f;
                Face(0, 1, 2, 3); Face(4, 5, 6, 7);
                Face(0, 4, 5, 1); Face(1, 5, 6, 2); Face(2, 6, 7, 3); Face(3, 7, 4, 0);

                void Face(int v0, int v1, int v2, int v3)
                {
                    int x = i + v0, y = i + v1, z = i + v2, w = i + v3;
                    Vector3 normal = Vector3.Cross(points[y] - points[x], points[z] - points[x]);
                    if (Vector3.Dot(normal, points[x] - center) < 0) (y, w) = (w, y);
                    triangles.Add(x); triangles.Add(y); triangles.Add(z);
                    triangles.Add(x); triangles.Add(z); triangles.Add(w);
                }
            }
            var mesh = new Mesh { name = "V1.12 walls", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(points);
            mesh.SetTriangles(triangles, 0);
            privateMeshes.Add(mesh);
            var node = new GameObject("Guard and building collision") { layer = WallLayer };
            node.transform.SetParent(transform, false);
            var collider = node.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }

        private void BuildPlants()
        {
            plants = new PlantPrototype[2 * Species.Length];
            var prototypes = Load<GameObject>("Plants").GetComponentsInChildren<MeshFilter>(true);
            foreach (var mesh in prototypes)
            {
                string name = mesh.gameObject.name;
                int index = Array.FindIndex(Species, species => name.StartsWith(species + "_", StringComparison.Ordinal));
                if (index < 0) continue;
                int tier = name.EndsWith("_high", StringComparison.Ordinal) ? 1 : 0;
                Material[] materials = mesh.GetComponent<MeshRenderer>().sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material original = materials[i];
                    if (!instancedMaterials.TryGetValue(original, out Material copy))
                    {
                        copy = new Material(original) { enableInstancing = true };
                        instancedMaterials.Add(original, copy);
                    }
                    materials[i] = copy;
                }
                plants[2 * index + tier] = new PlantPrototype { Mesh = mesh.sharedMesh, Materials = materials };
            }
            if (Array.Exists(plants, prototype => prototype == null))
                throw new InvalidDataException("The Forest plant prefab is missing one or more LOD nodes.");
            byte[] packed = Load<TextAsset>("plants").bytes;
            var groups = new Dictionary<Vector3Int, List<Matrix4x4>>();
            for (int i = 0; i + 28 <= packed.Length; i += 28)
            {
                float Read(int field) => BitConverter.ToSingle(packed, i + 4 * field);
                var pos = new Vector3(-Read(0), Read(1), Read(2));
                var cell = new Vector3Int(Mathf.FloorToInt(pos.x / 40f), Mathf.FloorToInt(pos.z / 40f), (int)Read(6));
                float height = Read(4), width = height * Read(5);
                Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.Euler(0, -Read(3) * Mathf.Rad2Deg, 0),
                    new Vector3(width, height, width));
                if (!groups.TryGetValue(cell, out List<Matrix4x4> group))
                {
                    group = new List<Matrix4x4>();
                    groups.Add(cell, group);
                }
                group.Add(matrix);
            }
            foreach (var group in groups)
            {
                var poses = group.Value;
                var bounds = new Bounds(poses[0].GetColumn(3), Vector3.zero);
                foreach (var pose in poses) bounds.Encapsulate((Vector3)pose.GetColumn(3) + Vector3.up * pose.m11);
                foreach (var pose in poses) bounds.Encapsulate((Vector3)pose.GetColumn(3));
                bounds.Expand(16f);
                // Instanced draws use bounded chunks and must never exceed Unity's per-call limit.
                for (int i = 0; i < poses.Count; i += 480)
                    cells.Add(new PlantCell { Bounds = bounds, Species = group.Key.z,
                        Positions = poses.GetRange(i, Mathf.Min(480, poses.Count - i)).ToArray() });
            }
        }

        private void LateUpdate()
        {
            if (!view || visualTiles == null) return;
            Vector3 position = view.transform.position;
            if (Time.unscaledTime >= tileCheckAt)
            {
                tileCheckAt = Time.unscaledTime + .24f;
                foreach (var tile in visualTiles)
                    tile.enabled = tile.bounds.SqrDistance(position) < 540f * 540f;
            }
            if (!isForest) return;
            GeometryUtility.CalculateFrustumPlanes(view, viewVolume);
            foreach (var cell in cells)
            {
                bool tall = cell.Species <= 1;
                float distance2 = cell.Bounds.SqrDistance(position);
                float maximum = tall ? (Detail == 0 ? 270f : 420f) : (Detail == 0 ? 56f : 82f);
                if (distance2 > maximum * maximum || !GeometryUtility.TestPlanesAABB(viewVolume, cell.Bounds)) continue;
                float near = Detail == 0 ? 17f : 36f;
                int quality = distance2 < near * near ? 1 : 0;
                PlantPrototype prototype = plants[cell.Species * 2 + quality];
                for (int surface = 0; surface < prototype.Mesh.subMeshCount; surface++)
                {
                    var config = new RenderParams(prototype.Materials[surface])
                    {
                        camera = view, worldBounds = cell.Bounds,
                        shadowCastingMode = tall && quality > 0 ? ShadowCastingMode.On : ShadowCastingMode.Off
                    };
                    Graphics.RenderMeshInstanced(config, prototype.Mesh, surface, cell.Positions);
                }
            }
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in privateMeshes) Destroy(mesh);
            foreach (Material material in instancedMaterials.Values) Destroy(material);
        }
    }
}
