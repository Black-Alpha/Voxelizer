﻿/*
 *	Created by:  Peter @sHTiF Stefcek
 */

using g3;
using UnityEngine;

namespace BinaryEgo.Voxelizer
{
    public class VoxelUtils
    {
        public static Vector2 GetInterpolatedUVInTriangle(Vector3d p_p1, Vector3d p_p2, Vector3d p_p3, Vector3d p_point, Vector2 p_uv1,
            Vector2 p_uv2, Vector2 p_uv3)
        {
            var d1 = p_p1 - p_point;
            var d2 = p_p2 - p_point;
            var d3 = p_p3 - p_point;

            double a = Vector3d.Cross(p_p1 - p_p2, p_p1 - p_p3).Length;
            float a1 = (float) (Vector3d.Cross(d2, d3).Length / a);
            float a2 = (float) (Vector3d.Cross(d3, d1).Length / a);
            float a3 = (float) (Vector3d.Cross(d1, d2).Length / a);
            
            return p_uv1 * a1 + p_uv2 * a2 + p_uv3 * a3;
        }
        
        public static Color GetInterpolatedColorInTriangle(Vector3d p_p1, Vector3d p_p2, Vector3d p_p3, Vector3d p_point, Vector3f p_color1,
            Vector3f p_color2, Vector3f p_color3)
        {
            var d1 = p_p1 - p_point;
            var d2 = p_p2 - p_point;
            var d3 = p_p3 - p_point;

            double a = Vector3d.Cross(p_p1 - p_p2, p_p1 - p_p3).Length;
            float a1 = (float) (Vector3d.Cross(d2, d3).Length / a);
            float a2 = (float) (Vector3d.Cross(d3, d1).Length / a);
            float a3 = (float) (Vector3d.Cross(d1, d2).Length / a);
            
            return p_color1 * a1 + p_color2 * a2 + p_color3 * a3;
        }
        
        public static Color GetColorAtPoint(DMesh3 p_mesh, int p_triangleIndex, Vector3d p_point,
            Material[] p_materials, bool p_interpolate)
        {
            if (p_triangleIndex == DMesh3.InvalidID)
                return Color.black;

            DistPoint3Triangle3 dist = MeshQueries.TriangleDistance(p_mesh, p_triangleIndex, p_point);
            Vector3d nearestPoint = dist.TriangleClosest;
            Index3i ti = p_mesh.GetTriangle(p_triangleIndex);

            Color texColor = Color.white;
            
            if (p_materials != null)
            {
                int materialGroup = p_mesh.GetMaterialGroup(p_triangleIndex);

                if (materialGroup < p_materials.Length)
                {
                    var texture = (Texture2D)p_materials[materialGroup].mainTexture;

                    if (texture != null)
                    {
                        if (texture.isReadable)
                        {
                            Vector2d uv;
                            if (p_interpolate)
                            {
                                uv = GetInterpolatedUVInTriangle(
                                    p_mesh.GetVertex(ti[0]),
                                    p_mesh.GetVertex(ti[1]),
                                    p_mesh.GetVertex(ti[2]),
                                    nearestPoint,
                                    p_mesh.GetVertexUV(ti[0]),
                                    p_mesh.GetVertexUV(ti[1]),
                                    p_mesh.GetVertexUV(ti[2]));
                            }
                            else
                            {
                                uv = p_mesh.GetVertexUV(ti[0]);
                            }
                            
                            texColor = texture.GetPixelBilinear((float)uv.x, (float)uv.y);
                        }
                        else
                        {
                            texColor = Color.white;
                        }
                    }
                }
            }

            Color vertexColor = Color.white;
            if (p_mesh.HasVertexColors)
            {
                if (p_interpolate)
                {
                    vertexColor = GetInterpolatedColorInTriangle(
                        p_mesh.GetVertex(ti[0]),
                        p_mesh.GetVertex(ti[1]),
                        p_mesh.GetVertex(ti[2]),
                        nearestPoint,
                        p_mesh.GetVertexColor(ti[0]),
                        p_mesh.GetVertexColor(ti[1]),
                        p_mesh.GetVertexColor(ti[2]));
                }
                else
                {
                    vertexColor = p_mesh.GetVertexColor(ti[0]);
                }
            }
            
            return texColor * vertexColor;
        }
        
        public static bool CheckTextureReadability(Material[] p_materials)
        {
            var valid = true;
            foreach (var material in p_materials)
            {
                if (material.mainTexture != null)
                {
                    if (!material.mainTexture.isReadable)
                    {
                        Debug.LogWarning("Texture " + material.mainTexture.name +
                                         " is not readable cannot voxelize with color sampling.");
                        valid = false;
                    }
                }
            }

            return valid;
        }
    }
}