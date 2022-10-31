/*
 *	Created by:  Peter @sHTiF Stefcek
 */


using System;
using System.Collections.Generic;
using System.Linq;
using BinaryEgo.Voxelizer;
using g3;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.NotBurstCompatible;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace BinaryEgo.Voxelizer
{
    [Serializable]
    public class VoxelMesh : ISerializationCallbackReceiver
    {
        // public static bool IsCached(string p_name)
        // {
        //     return POSITION_CACHE != null ? POSITION_CACHE.ContainsKey(p_name) : false;
        // }

        // private static Dictionary<string, NativeList<Vector3>> POSITION_CACHE;
        // private static Dictionary<string, NativeList<Vector4>> COLOR_CACHE;
        // private static Dictionary<string, NativeList<int>> INDEX_CACHE;
        // private static bool _cacheDisposed = false;

        // public bool usePhysics = false;

        [SerializeField] private float _voxelSize = 1;

        [SerializeField] private Transform _transform;

        [SerializeField] private List<int> _indices = new List<int>();
        [SerializeField] private List<Matrix4x4> _matrices = new List<Matrix4x4>();
        [SerializeField] private List<Vector4> _colors = new List<Vector4>();

        // public bool _usingCache = false;
        // private string _cacheName;
        //
        // private Dictionary<Vector3i, Transform> _physicsLookup;
        // private Transform _physicsContainer;
        // private List<Transform> _physicsTransforms;
        // private TransformAccessArray _physicsTransformAccessArray;

        [SerializeField] private VoxelTransformBakeType _voxelTransformBakeType;

        [SerializeField] private int _voxelCount;
        public int VoxelCount => _voxelCount;

        [NonSerialized] private bool _forceInvalidateJobs;
        [NonSerialized] private Matrix4x4 _previousTransformMatrix;

        private NativeList<int> _nativeIndices;
        private NativeList<Matrix4x4> _nativeMatrices;
        private NativeList<Vector4> _nativeColors;

        public VoxelMesh(IBinaryVoxelGrid p_voxelGrid, Vector4[] p_colors, Transform p_transform,
            AxisAlignedBox3d p_bounds, Vector3 p_offset, bool p_generateInside, float p_voxelSize,
            VoxelTransformBakeType p_voxelTransformBakeType)
        {
            _transform = p_transform;
            _voxelSize = p_voxelSize;
            _voxelTransformBakeType = p_voxelTransformBakeType;

            if (p_voxelGrid == null)
                return;

            int colorIndex = 0;
            int voxelIndex = 0;
            foreach (Vector3i voxelPosition in p_voxelGrid.NonZeros())
            {
                if (p_generateInside || !VoxelUtils.IsInside(p_voxelGrid, voxelPosition))
                {
                    var position = new Vector3(voxelPosition.x, voxelPosition.y, voxelPosition.z) * _voxelSize +
                                   p_offset;

                    _matrices.Add(Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * _voxelSize));
                    _colors.Add(p_colors[colorIndex]);
                    _indices.Add(voxelIndex++);
                }

                colorIndex++;
            }

            // int index = 0;
            // foreach (Vector3i voxelPosition in voxels.NonZeros())
            // {
            //     if (p_generateInside || !IsInside(voxels, voxelPosition))
            //     {
            //         var body1 = _physicsLookup[voxelPosition].GetComponent<Rigidbody>();
            //         Rigidbody body2;
            //         Vector3i n = new Vector3i(voxelPosition.x + 1, voxelPosition.y, voxelPosition.z);
            //         if (_physicsLookup.ContainsKey(n))
            //         {
            //             body2 = _physicsLookup[n].GetComponent<Rigidbody>();
            //             VoxelRenderer.Instance.physics.AddConnection(body1, body2);
            //         }
            //         
            //         n = new Vector3i(voxelPosition.x, voxelPosition.y+1, voxelPosition.z);
            //         if (_physicsLookup.ContainsKey(n))
            //         {
            //             body2 = _physicsLookup[n].GetComponent<Rigidbody>();
            //             VoxelRenderer.Instance.physics.AddConnection(body1, body2);
            //         }
            //         voxelIndex++;
            //     }
            // }


            // if (usePhysics)
            // {
            //     _physicsTransformAccessArray = new TransformAccessArray(_physicsTransforms.ToArray());
            // }

            _voxelCount = _indices.Count;
            _forceInvalidateJobs = true;
            _previousTransformMatrix = Matrix4x4.identity;
        }

        private void CreateNative()
        {
            _nativeIndices = new NativeList<int>(Allocator.Persistent);
            _nativeMatrices = new NativeList<Matrix4x4>(Allocator.Persistent);
            _nativeColors = new NativeList<Vector4>(Allocator.Persistent);

            _nativeIndices.CopyFromNBC(_indices.ToArray());
            _nativeMatrices.CopyFromNBC(_matrices.ToArray());
            _nativeColors.CopyFromNBC(_colors.ToArray());
        }

        // public void CloneCache()
        // {
        //     //_vertices = new NativeList<Vector3>(_vertices.Length, Allocator.Persistent);
        //     //_vertices.AddRangeNoResize(POSITION_CACHE[_cacheName]);
        //     _colors = new NativeList<Vector4>(_colors.Length, Allocator.Persistent);
        //     _colors.AddRangeNoResize(COLOR_CACHE[_cacheName]);
        //     voxelIndices = new NativeList<int>(voxelIndices.Length, Allocator.Persistent);
        //     voxelIndices.AddRangeNoResize(INDEX_CACHE[_cacheName]);
        //     _usingCache = false;
        // }

        public void Invalidate(ComputeBuffer p_matrixBuffer, NativeArray<Matrix4x4> p_matrixArray,
            ComputeBuffer p_colorBuffer, NativeArray<Vector4> p_colorArray, int p_index)
        {
            if (!_nativeMatrices.IsCreated)
                CreateNative();

            if (_nativeIndices.Length == 0)
                return;

            if (_voxelTransformBakeType == VoxelTransformBakeType.SCALE_ROTATION_POSITION)
            {
                p_matrixBuffer.SetData(_nativeMatrices.AsArray(), 0, p_index, _nativeIndices.Length);
                p_colorBuffer.SetData(_nativeColors.AsArray(), 0, p_index, _nativeIndices.Length);
                return;
            }
            
            if (!_forceInvalidateJobs && (_transform == null || !_transform.hasChanged))
            {
                {
                    p_matrixBuffer.SetData(_nativeMatrices.AsArray(), 0, p_index, _nativeIndices.Length);
                    p_colorBuffer.SetData(_nativeColors.AsArray(), 0, p_index, _nativeIndices.Length);
                    return;
                }
            }

            Matrix4x4 transformMatrix;

            switch (_voxelTransformBakeType)
            {
                case VoxelTransformBakeType.SCALE:
                    transformMatrix = Matrix4x4.Translate(_transform.localToWorldMatrix.GetPosition()) *
                                      Matrix4x4.Rotate(_transform.localToWorldMatrix.rotation);
                    break;
                case VoxelTransformBakeType.SCALE_ROTATION:
                    transformMatrix = Matrix4x4.Translate(_transform.localToWorldMatrix.GetPosition());
                    break;
                default:
                    transformMatrix = _transform.localToWorldMatrix;
                    break;
            }
            
            VoxelPositionUpdateJob positionUpdateJob = new VoxelPositionUpdateJob()
            {
                previousTransformMatrixI = _previousTransformMatrix.inverse,
                transformMatrix = transformMatrix,
                matrices = _nativeMatrices,
            };

            // if (usePhysics)
            // {
            //     PhysicsUpdateJob physicsUpdateJob = new PhysicsUpdateJob
            //     {
            //         matrices = inMatrixSlice
            //     };
            //     JobHandle physicsJobHandle = physicsUpdateJob.Schedule(_physicsTransformAccessArray);
            //     
            //     JobHandle jobHandle = positionUpdateJob.Schedule(voxelIndices.Length, 100, physicsJobHandle);
            //     jobHandle.Complete();
            // }
            // else
            {
                JobHandle jobHandle = positionUpdateJob.Schedule(_nativeIndices.Length, 100);
                jobHandle.Complete();
            }

            _transform.hasChanged = false;
            _previousTransformMatrix = transformMatrix;

            p_matrixBuffer.SetData(_nativeMatrices.AsArray(), 0, p_index, _nativeIndices.Length);
            p_colorBuffer.SetData(_nativeColors.AsArray(), 0, p_index, _nativeIndices.Length);
        }

        // public void Erase(Vector3 p_point, float p_radius)
        // {
        //     Paint(p_point, p_radius, new Color(0,0,0,0));
        // }

        // public void Hit(Vector3 p_point, float p_radius)
        // {
        //     // if (_usingCache)
        //     //     CloneCache();
        //     
        //     // for (int i = 0; i<_matrices.Length; i++)
        //     // {
        //     //     if (Vector3.Distance(_matrices[i].GetColumn(3), p_point) < p_radius)
        //     //     {
        //     //         _physicsTransforms[i].GetComponent<Rigidbody>().isKinematic = false;
        //     //     }
        //     // }
        // }

        // public void Paint(Vector3 p_point, float p_radius, Color p_color)
        // {
        //     // if (_usingCache)
        //     //     CloneCache();
        //     
        //     for (int i = 0; i<_nativeMatrices.Length; i++)
        //     {
        //         if (Vector3.Distance(_nativeMatrices[i].GetColumn(3), p_point) < p_radius)
        //         {
        //             _nativeColors[i] = p_color;
        //         }
        //     }
        //     
        //     _transform.hasChanged = true;
        // }

        public void Dispose()
        {
            _forceInvalidateJobs = true;
            _previousTransformMatrix = Matrix4x4.identity;
            //if (!_usingCache && _matrices.IsCreated)
            if (_nativeMatrices.IsCreated)
            {
                _nativeColors.Dispose();
                _nativeColors = default;
                _nativeIndices.Dispose();
                _nativeIndices = default;
                _nativeMatrices.Dispose();
                _nativeMatrices = default;
            }
            // else
            // {
            //     // Warning I assume here that this dispose will be called when application exits/stopsplaying otherwise you shouldn't dispose cache :)
            //     if (!_cacheDisposed)
            //     {
            //         foreach (var key in POSITION_CACHE.Keys.ToArray())
            //         {
            //             if (POSITION_CACHE[key].IsCreated)
            //             {
            //                 POSITION_CACHE[key].Dispose();
            //                 POSITION_CACHE[key] = default;
            //             }
            //
            //             if (COLOR_CACHE[key].IsCreated) 
            //             {
            //                 COLOR_CACHE[key].Dispose();
            //                 COLOR_CACHE[key] = default;
            //             }
            //
            //             if (INDEX_CACHE[key].IsCreated) 
            //             {
            //                 INDEX_CACHE[key].Dispose();
            //                 INDEX_CACHE[key] = default;
            //             }
            //         }
            //     }
            // }

            // if (usePhysics)
            // {
            //     _physicsTransformAccessArray.Dispose();
            // }
        }


        [BurstCompile]
        struct PhysicsUpdateJob : IJobParallelForTransform
        {
            public NativeSlice<Matrix4x4> matrices;

            public void Execute(int p_index, TransformAccess transform)
            {
                matrices[p_index] = transform.localToWorldMatrix;
            }
        }

        #region SERIALIZATION

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            _forceInvalidateJobs = true;
            _previousTransformMatrix = Matrix4x4.identity;
        }

        #endregion
    }
}
