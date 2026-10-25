using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace RTXOn
{
    /// <summary>
    /// Reads a Wavefront OBJ (Blender: File > Export > Wavefront .obj) into a Mesh while the game runs, for Custom
    /// Obstacles picked from a file. Positions, UVs and normals; faces with any number of corners (fanned into
    /// triangles); negative indices. Materials and groups are ignored (one texture for the whole model). X is
    /// flipped like Unity's own importer, so the model is not mirrored.
    /// </summary>
    public static class ObjMesh
    {
        public static Mesh Parse(string text, string name = "OBJ")
        {
            if (string.IsNullOrEmpty(text)) return null;
            var positions = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var outPos = new List<Vector3>();
            var outUv = new List<Vector2>();
            var outNormal = new List<Vector3>();
            var triangles = new List<int>();
            var corners = new Dictionary<(int, int, int), int>();
            bool anyNormal = false;
            var face = new List<int>();

            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length < 2 || line[0] == '#') continue;
                var parts = line.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
                switch (parts[0])
                {
                    case "v":
                        if (parts.Length >= 4) positions.Add(new Vector3(-F(parts[1]), F(parts[2]), F(parts[3])));
                        break;
                    case "vt":
                        if (parts.Length >= 3) uvs.Add(new Vector2(F(parts[1]), F(parts[2])));
                        break;
                    case "vn":
                        if (parts.Length >= 4) normals.Add(new Vector3(-F(parts[1]), F(parts[2]), F(parts[3])));
                        break;
                    case "f":
                        face.Clear();
                        for (int i = 1; i < parts.Length; i++)
                        {
                            var idx = parts[i].Split('/');
                            int p = Index(idx, 0, positions.Count), t = Index(idx, 1, uvs.Count), n = Index(idx, 2, normals.Count);
                            if (p < 0) continue;
                            var key = (p, t, n);
                            if (!corners.TryGetValue(key, out int vi))
                            {
                                vi = outPos.Count;
                                corners[key] = vi;
                                outPos.Add(positions[p]);
                                outUv.Add(t >= 0 ? uvs[t] : Vector2.zero);
                                outNormal.Add(n >= 0 ? normals[n] : Vector3.zero);
                                if (n >= 0) anyNormal = true;
                            }
                            face.Add(vi);
                        }
                        // X was flipped, so the winding turns round too.
                        for (int i = 1; i + 1 < face.Count; i++)
                        {
                            triangles.Add(face[0]);
                            triangles.Add(face[i + 1]);
                            triangles.Add(face[i]);
                        }
                        break;
                }
            }
            if (triangles.Count == 0) return null;

            var mesh = new Mesh { name = name };
            if (outPos.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(outPos);
            mesh.SetUVs(0, outUv);
            mesh.SetTriangles(triangles, 0);
            if (anyNormal) mesh.SetNormals(outNormal);
            else mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>The 0-based index of corner part <paramref name="slot"/> (1-based in the file, negative = from the end), or -1.</summary>
        static int Index(string[] idx, int slot, int count)
        {
            if (slot >= idx.Length || idx[slot].Length == 0) return -1;
            if (!int.TryParse(idx[slot], NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) return -1;
            i = i < 0 ? count + i : i - 1;
            return i >= 0 && i < count ? i : -1;
        }

        static float F(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
    }
}
