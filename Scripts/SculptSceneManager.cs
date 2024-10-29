using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System.IO;
using System.Linq;
using System.Threading;

namespace SDFSculptor
{
    public class SculptSceneManager : MonoBehaviour
    {
        [Tooltip("The Occlusion Culling Compute Shader, handles frustum / occlusion culling and LODs.")]
        public ComputeShader myComputeShader;
        [Tooltip("The Fuzzy Compute Shader, converts a sculpt into a fuzzy sculpt.")]
        public ComputeShader myFuzzyComputeShader;
        //[Tooltip("The Shader used by SDF sculpts.")]
        Material mySDFShader;

        //[Tooltip("The Fuzzy Shader, when far away. Turn off alpha clipping for performance.")]
        //public Material farAwaySDFFuzzyShader;

        //[Tooltip("The Shader used by Fuzzy SDF sculpts.")]
        //public Material myFuzzySDFShader;
        [Tooltip("The texture used when texture warp is enabled on an SDF Brush.")]
        public Texture noise;

        //public Camera mainCam;
        [Tooltip("List of all cameras that can see the GPU instanced sculpts. If no GPU instancing, there should only be 1 camera.")]
        public List<Camera> myCameras;
        [Tooltip("Cull objects not directly visible.")]
        public bool enableOcclusionCulling;
        [Tooltip("Handles instancing manually over just checking the Enable Instancing checkbox on a shader. Max of 1022 instances per sculpt, per LOD.")]
        public bool enableGPUInstancing;
        //public GameObject test;
        //public Material test2;
        [Tooltip("Margin of error when determining the bounds of a mesh during frustum / occlusion culling.")]
        [Range(0, 0.5f)]
        public float boundsMarginOfError = 0.1f;
        [Tooltip("Margin of error when for frustum occlusion on the top and bottom of the screen (needed for VR, 0 is fine without VR).")]
        [Range(0, 0.5f)]
        public float verticalMarginOfError = 0;
        [Tooltip("Margin of error when depth testing in occlusion culling.")]
        public float depthMarginOfError = 10;
        [Tooltip("The parent sculpts are used to determine mesh resolution, scale, levels of detail needed for each copy / instance of this sculpt assuming the name, recipe path, and mesh path are the same. Try to not delete these in game")]
        public List<SDFSculpt> parentSculpts = new List<SDFSculpt>();

        List<List<int>> childSculpts = new List<List<int>>();
        List<List<int>> dynamicChildSculpts = new List<List<int>>();
        List<List<int>> nonInstancedChildSculpts = new List<List<int>>();
        List<SDFSculpt> allSculpts = new List<SDFSculpt>();
        List<SDFSculpt> dynamicSculpts = new List<SDFSculpt>();
        List<SDFSculpt> staticSculpts = new List<SDFSculpt>();

        List<Renderer> allSculptRenderers = new List<Renderer>();
        List<Renderer> dynamicSculptRenderers = new List<Renderer>();
        List<Renderer> staticSculptRenderers = new List<Renderer>();

        [HideInInspector]
        public SDFSculpt editedSculpt;

        //public float[] Output = new float[0];

        struct BBStruct
        {
            public Vector3 center;

            public Vector3 drb;
            public Vector3 drf;
            public Vector3 dlb;
            public Vector3 dlf;

            public Vector3 urb;
            public Vector3 urf;
            public Vector3 ulb;
            public Vector3 ulf;

            public float radius;
        };

        struct MyInstanceData
        {
            public Matrix4x4 objectToWorld;
            public Color tint;
            public float tintAmount;
            public float metallic;
            public float smoothness;
            public float emission;
            public float cmpA;
            public float cmpB;
            public float cmpC;
            public float cmpD;
        };

        List<BBStruct> staticBoundingBoxes = new List<BBStruct>();
        List<BBStruct> dynamicBoundingBoxes = new List<BBStruct>();
        List<int> staticCullingResults = new List<int>();
        List<int> dynamicCullingResults = new List<int>();
        List<float> staticLODTransitionScalars = new List<float>();
        List<float> dynamicLODTransitionScalars = new List<float>();
        List<float> staticLODFirstThresholds = new List<float>();
        List<float> dynamicLODFirstThresholds = new List<float>();


        List<string> usedRecipes = new List<string>();
        List<int> firstUsageIndex = new List<int>();
        //List<Mesh> existingMeshes = new List<Mesh>();

        List<Camera> camerasToTrack = new List<Camera>();
        List<RenderTexture> trackedDepthTextures = new List<RenderTexture>();

        List<List<List<RenderTexture>>> matTint = new List<List<List<RenderTexture>>>();
        List<List<List<RenderTexture>>> matProps = new List<List<List<RenderTexture>>>();
        List<List<List<RenderTexture>>> matProps2 = new List<List<List<RenderTexture>>>();
        List<List<List<RenderTexture>>> dynamicMatTint = new List<List<List<RenderTexture>>>();
        List<List<List<RenderTexture>>> dynamicMatProps = new List<List<List<RenderTexture>>>();
        List<List<List<RenderTexture>>> dynamicMatProps2 = new List<List<List<RenderTexture>>>();

        [Tooltip("Generate manager info when the scene begins. Can be disabled if you want to trigger this manually / through code.")]
        public bool generateManagerInfoWhenSceneBegins = true;
        bool generatedManagerInfo = false;
        [Tooltip("Show how many instances there are of each static sculpt LOD (if GPU instancing enabled)  (Max of 1022).")]
        public bool debugStaticGPUInstanceCounts;
        [Tooltip("Show how many instances there are of each dynamic sculpt LOD (if GPU instancing enabled) (Max of 1022).")]
        public bool debugDynamicGPUInstanceCounts;

        /// <summary>
        ///  All sculpts is an internal list of all sculpts the sculpt scene manager oversees. All SDF Sculpts call this function automatically at the start.
        /// </summary>
        /// <param name="brush">The brush that needs to added.</param>
        /// <param name="brushIndex">The renderer of the brush.</param>
        public void AddToAllSculpts (SDFSculpt sculpt, Renderer ren)
        {
            allSculpts.Add(sculpt);
            allSculptRenderers.Add(ren);
        }

        /// <summary>
        ///  Part of generating manager info: creates all render textures for assigning material properties to GPU instanced sculpts.
        /// </summary>
        public void GetAllMaterialPropertyRenderTextures ()
        {
            matTint.Clear();
            matProps.Clear();
            matProps2.Clear();

            dynamicMatTint.Clear();
            dynamicMatProps.Clear();
            dynamicMatProps2.Clear();

            for (int i = 0; i < myCameras.Count; i++)
            {
                matTint.Add(new List<List<RenderTexture>>());
                matProps.Add(new List<List<RenderTexture>>());
                matProps2.Add(new List<List<RenderTexture>>());

                dynamicMatTint.Add(new List<List<RenderTexture>>());
                dynamicMatProps.Add(new List<List<RenderTexture>>());
                dynamicMatProps2.Add(new List<List<RenderTexture>>());
            }

            for (int i = 0; i < myCameras.Count; i++)
            {
                for (int j = 0; j < parentSculpts.Count; j++)
                {
                    matTint[i].Add(new List<RenderTexture>());
                    matProps[i].Add(new List<RenderTexture>());
                    matProps2[i].Add(new List<RenderTexture>());

                    dynamicMatTint[i].Add(new List<RenderTexture>());
                    dynamicMatProps[i].Add(new List<RenderTexture>());
                    dynamicMatProps2[i].Add(new List<RenderTexture>());
                }
            }

            for (int i = 0; i < myCameras.Count; i++)
            {  
                for (int j = 0; j < parentSculpts.Count; j++)
                {      
                    for (int k = 0; k < parentSculpts[j].LODResolutions.Count + 1; k++)
                    {
                        RenderTexture tmpRT = new RenderTexture(32,32,1,RenderTextureFormat.Default);
                        tmpRT.filterMode = FilterMode.Point;
                        tmpRT.enableRandomWrite = true;
                        tmpRT.Create();
                        matTint[i][j].Add(tmpRT);

                        tmpRT = new RenderTexture(32,32,1,RenderTextureFormat.Default);
                        tmpRT.filterMode = FilterMode.Point;
                        tmpRT.enableRandomWrite = true;
                        tmpRT.Create();
                        matProps[i][j].Add(tmpRT);

                        tmpRT = new RenderTexture(32,32,1,RenderTextureFormat.Default);
                        tmpRT.filterMode = FilterMode.Point;
                        tmpRT.enableRandomWrite = true;
                        tmpRT.Create();
                        matProps2[i][j].Add(tmpRT);

                        tmpRT = new RenderTexture(32,32,1,RenderTextureFormat.Default);
                        tmpRT.filterMode = FilterMode.Point;
                        tmpRT.enableRandomWrite = true;
                        tmpRT.Create();
                        dynamicMatTint[i][j].Add(tmpRT);

                        tmpRT = new RenderTexture(32,32,1,RenderTextureFormat.Default);
                        tmpRT.filterMode = FilterMode.Point;
                        tmpRT.enableRandomWrite = true;
                        tmpRT.Create();
                        dynamicMatProps[i][j].Add(tmpRT);

                        tmpRT = new RenderTexture(32,32,1,RenderTextureFormat.Default);
                        tmpRT.filterMode = FilterMode.Point;
                        tmpRT.enableRandomWrite = true;
                        tmpRT.Create();
                        dynamicMatProps2[i][j].Add(tmpRT);
                    }
                }
            }
        }

        /// <summary>
        ///  Part of generating manager info: converts mesh data json files into meshes.
        /// </summary>
        public void BuildSculptsFromMeshData ()
        {
            for (int i = 0; i < allSculpts.Count; i++)
            {
                string path = Application.dataPath + allSculpts[i].myMeshPath + allSculpts[i].myName + "_MeshData.json";
                string pathLOD;

                if(allSculpts[i].customLODMeshes.Count > 0)
                {
                    if(usedRecipes.Contains(path))
                    {
                        allSculpts[i].meshFilter.mesh = allSculpts[firstUsageIndex[usedRecipes.IndexOf(path)]].meshFilter.mesh;
                        allSculpts[i].meshes = allSculpts[firstUsageIndex[usedRecipes.IndexOf(path)]].meshes;
                    }
                    else
                    {
                        allSculpts[i].meshFilter.mesh = allSculpts[i].customLODMeshes[0];
                        allSculpts[i].meshes = allSculpts[i].customLODMeshes;
                        usedRecipes.Add(path);
                        firstUsageIndex.Add(i);
                    }
                    continue;
                }

                if(File.Exists(path))
                {
                    if(usedRecipes.Contains(path))
                    {
                        allSculpts[i].meshFilter.mesh = allSculpts[firstUsageIndex[usedRecipes.IndexOf(path)]].meshFilter.mesh;
                        allSculpts[i].meshes = allSculpts[firstUsageIndex[usedRecipes.IndexOf(path)]].meshes;
                    }
                    else
                    {
                        Mesh myMesh = new Mesh();
                        myMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                        MeshGenerator.SculptMeshStruct meshStruct = new MeshGenerator.SculptMeshStruct();
                        allSculpts[i].meshes.Clear();

                        for (int j = 0; j < allSculpts[i].LODResolutions.Count + 1; j++)
                        {
                            string json;
                            if(j == 0)
                            {
                                json = File.ReadAllText(path);
                                meshStruct = JsonUtility.FromJson<MeshGenerator.SculptMeshStruct>(json);

                                myMesh.SetVertices(meshStruct.vertices);
                                myMesh.SetTriangles(meshStruct.triangles, 0 , false);
                                myMesh.SetUVs(0, meshStruct.uvs);
                                myMesh.SetColors(meshStruct.colors);
                                myMesh.SetNormals(meshStruct.normals);
                                myMesh.RecalculateBounds();
                                myMesh.UploadMeshData(false);
                                allSculpts[i].meshFilter.mesh = myMesh;
                                allSculpts[i].meshes.Add(myMesh);
                            }
                            else
                            {
                                pathLOD = Application.dataPath + allSculpts[i].myMeshPath + allSculpts[i].myName +  "_LOD" + j.ToString() + "MeshData.json";
                                if(File.Exists(pathLOD))
                                {
                                    json = File.ReadAllText(pathLOD);
                                    meshStruct = new MeshGenerator.SculptMeshStruct();
                                    meshStruct = JsonUtility.FromJson<MeshGenerator.SculptMeshStruct>(json);

                                    myMesh = new Mesh();
                                    myMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                                    myMesh.SetVertices(meshStruct.vertices);
                                    myMesh.SetTriangles(meshStruct.triangles, 0 , false);
                                    myMesh.SetUVs(0, meshStruct.uvs);
                                    myMesh.SetColors(meshStruct.colors);
                                    myMesh.SetNormals(meshStruct.normals);
                                    myMesh.RecalculateBounds();
                                    myMesh.UploadMeshData(false);
                                    
                                    allSculpts[i].meshes.Add(myMesh);
                                }
                            }
                        }

                        usedRecipes.Add(path);
                        firstUsageIndex.Add(i);
                    }

                    //Destroy(allSculpts[i].gameObject.GetComponent<BoxCollider>());
                    //allSculpts[i].gameObject.AddComponent<BoxCollider>();
                }
                else
                {
                    allSculpts[i].meshes.Clear();
                    allSculpts[i].meshes.Add(allSculpts[i].meshFilter.mesh);
                }
            }
        }

        public void BuildSingleSculptFromMeshData (SDFSculpt sculpt)
        {
            string path = Application.dataPath + sculpt.myMeshPath + sculpt.myName + "_MeshData.json";
            string json;
            Mesh myMesh = new Mesh();
            MeshGenerator.SculptMeshStruct meshStruct = new MeshGenerator.SculptMeshStruct();
            sculpt.meshes.Clear();

            int index = parentSculpts.IndexOf(sculpt);

            for (int i = 0; i < sculpt.LODResolutions.Count + 1; i++)
            {
                if(i > 0)
                    path = Application.dataPath + sculpt.myMeshPath + sculpt.myName +  "_LOD" + i.ToString() + "MeshData.json";
                
                if(File.Exists(path))
                {
                    json = File.ReadAllText(path);
                    meshStruct = new MeshGenerator.SculptMeshStruct();
                    meshStruct = JsonUtility.FromJson<MeshGenerator.SculptMeshStruct>(json);

                    myMesh = new Mesh();
                    myMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    myMesh.SetVertices(meshStruct.vertices);
                    myMesh.SetTriangles(meshStruct.triangles, 0 , false);
                    myMesh.SetUVs(0, meshStruct.uvs);
                    myMesh.SetColors(meshStruct.colors);
                    myMesh.SetNormals(meshStruct.normals);
                    myMesh.RecalculateBounds();
                    myMesh.UploadMeshData(false);
                    sculpt.meshes.Add(myMesh);
                    Debug.Log(i.ToString() + ": " + myMesh.vertexCount.ToString());
                    if(i == 0)
                        sculpt.meshFilter.mesh = myMesh;
                }
            }

            if(parentSculpts.Contains(sculpt))
            {
                if(enableGPUInstancing)
                {
                    if(sculpt.isSculptStatic)
                    {
                        for (int i = 0; i < childSculpts[index].Count; i++)
                        {
                            staticSculpts[childSculpts[index][i]].meshes = sculpt.meshes;
                            staticSculpts[childSculpts[index][i]].meshFilter.mesh = sculpt.meshFilter.mesh;
                        }
                    }
                    else
                    {
                        for (int i = 0; i < childSculpts[index].Count; i++)
                        {
                            dynamicSculpts[dynamicChildSculpts[index][i]].meshes = sculpt.meshes;
                            dynamicSculpts[dynamicChildSculpts[index][i]].meshFilter.mesh = sculpt.meshFilter.mesh;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < nonInstancedChildSculpts[index].Count; i++)
                    {
                        allSculpts[nonInstancedChildSculpts[index][i]].meshes = sculpt.meshes;
                        allSculpts[nonInstancedChildSculpts[index][i]].meshFilter.mesh = sculpt.meshFilter.mesh;
                    }
                }
            }
        }

        public SDFSculpt GetParentSculpt (SDFSculpt sculpt)
        {
            for (int i = 0; i < parentSculpts.Count; i++)
            {
                if(parentSculpts[i].myName == sculpt.myName &&
                parentSculpts[i].myRecipePath == sculpt.myRecipePath &&
                parentSculpts[i].myMeshPath == sculpt.myMeshPath)
                {
                    return parentSculpts[i];
                }
            }
            return null;
        }

        public void UpdateAllSculpts (SDFSculpt sculpt, Mesh mesh)
        {
            for (int i = 0; i < allSculpts.Count; i++)
            {
                if(allSculpts[i].myName == sculpt.myName &&
                allSculpts[i].myRecipePath == sculpt.myRecipePath &&
                allSculpts[i].myMeshPath == sculpt.myMeshPath)
                {
                    allSculpts[i].gameObject.GetComponent<MeshFilter>().mesh = mesh;
                    //Destroy(allSculpts[i].gameObject.GetComponent<BoxCollider>());
                    //allSculpts[i].gameObject.AddComponent<BoxCollider>();
                }
            }
        }

        /// <summary>
        ///  Part of generating manager info: separates all sculpts list into separate lists for dynamic and static sculpts. Important to know if GPU instancing.
        /// </summary>
        public void SplitDynamicAndStaticSculpts ()
        {
            staticSculptRenderers = new List<Renderer>();
            dynamicSculptRenderers = new List<Renderer>();
            staticSculpts = new List<SDFSculpt>();
            dynamicSculpts = new List<SDFSculpt>();

            for (int i = 0; i < allSculpts.Count; i++)
            {
                if(allSculpts[i].isSculptStatic)
                {
                    staticSculptRenderers.Add(allSculptRenderers[i]);
                    staticSculpts.Add(allSculpts[i]);
                    //Disable static sculpt renderers
                    if(enableGPUInstancing)
                    {
                        for (int j = 0; j < parentSculpts.Count; j++)
                        {
                            if(parentSculpts[j].myName == allSculpts[i].myName)
                            {

                                childSculpts[j].Add(staticSculpts.Count - 1);
                                allSculptRenderers[i].enabled = false;
                                break;
                            }
                        }
                    }
                    else
                    {
                        for (int j = 0; j < parentSculpts.Count; j++)
                        {
                            if(parentSculpts[j].myName == allSculpts[i].myName)
                            {
                                nonInstancedChildSculpts[j].Add(i);
                                break;
                            }
                        }
                    }
                }
                else
                {
                    dynamicSculptRenderers.Add(allSculptRenderers[i]);
                    dynamicSculpts.Add(allSculpts[i]);

                    if(enableGPUInstancing)
                    {
                        for (int j = 0; j < parentSculpts.Count; j++)
                        {
                            if(parentSculpts[j].myName == allSculpts[i].myName)
                            {
                                dynamicChildSculpts[j].Add(dynamicSculpts.Count - 1);
                                allSculptRenderers[i].enabled = false;
                                break;
                            }
                        }
                    }
                    else
                    {
                        for (int j = 0; j < parentSculpts.Count; j++)
                        {
                            if(parentSculpts[j].myName == allSculpts[i].myName)
                            {
                                nonInstancedChildSculpts[j].Add(i);
                                break;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        ///  Part of generating manager info: get bounding box info for all sculpts. Dynamic sculpts call this function every frame.
        /// </summary>
        /// <param name="staticBBs">True = recalculate all static bounding boxes. False = recalculate all dynamic bounding boxes.</param>
        public void GetBoundingBoxCorners (bool staticBBs)
        {
            if(staticBBs)
            {
                staticCullingResults = new List<int>();
                staticBoundingBoxes = new List<BBStruct>();
                for (int i = 0; i < staticSculptRenderers.Count; i++)
                {
                    BBStruct bb = new BBStruct();
                    Vector3 extends = staticSculptRenderers[i].bounds.extents;
                    bb.center = staticSculptRenderers[i].bounds.center;
                    bb.drb = staticSculptRenderers[i].bounds.center + new Vector3(extends.x, -extends.y, -extends.z);
                    bb.drf = staticSculptRenderers[i].bounds.center + new Vector3(extends.x, -extends.y, extends.z);
                    bb.dlb = staticSculptRenderers[i].bounds.center + new Vector3(-extends.x, -extends.y, -extends.z);
                    bb.dlf = staticSculptRenderers[i].bounds.center + new Vector3(-extends.x, -extends.y, extends.z);
                    bb.urb = staticSculptRenderers[i].bounds.center + new Vector3(extends.x, extends.y, -extends.z);
                    bb.urf = staticSculptRenderers[i].bounds.center + new Vector3(extends.x, extends.y, extends.z);
                    bb.ulb = staticSculptRenderers[i].bounds.center + new Vector3(-extends.x, extends.y, -extends.z);
                    bb.ulf = staticSculptRenderers[i].bounds.center + new Vector3(-extends.x, extends.y, extends.z);
                    bb.radius = Mathf.Max(Mathf.Max(extends.x, extends.y), extends.z);
                    staticLODTransitionScalars.Add(staticSculpts[i].lodTransitionScalar);
                    staticLODFirstThresholds.Add(staticSculpts[i].lodFirstTransitionThreshold);
                    staticBoundingBoxes.Add(bb);
                    staticCullingResults.Add(0);
                    staticSculpts[i].myTransform = Matrix4x4.TRS(staticSculpts[i].transform.position, staticSculpts[i].transform.rotation, staticSculpts[i].transform.lossyScale);
                }
            }
            else
            {
                dynamicCullingResults = new List<int>();
                dynamicBoundingBoxes = new List<BBStruct>();
                for (int i = 0; i < dynamicSculptRenderers.Count; i++)
                {
                    BBStruct bb = new BBStruct();
                    Vector3 extends = dynamicSculptRenderers[i].bounds.extents;
                    bb.center = staticSculptRenderers[i].bounds.center;
                    bb.drb = dynamicSculptRenderers[i].bounds.center + new Vector3(extends.x, -extends.y, -extends.z);
                    bb.drf = dynamicSculptRenderers[i].bounds.center + new Vector3(extends.x, -extends.y, extends.z);
                    bb.dlb = dynamicSculptRenderers[i].bounds.center + new Vector3(-extends.x, -extends.y, -extends.z);
                    bb.dlf = dynamicSculptRenderers[i].bounds.center + new Vector3(-extends.x, -extends.y, extends.z);
                    bb.urb = dynamicSculptRenderers[i].bounds.center + new Vector3(extends.x, extends.y, -extends.z);
                    bb.urf = dynamicSculptRenderers[i].bounds.center + new Vector3(extends.x, extends.y, extends.z);
                    bb.ulb = dynamicSculptRenderers[i].bounds.center + new Vector3(-extends.x, extends.y, -extends.z);
                    bb.ulf = dynamicSculptRenderers[i].bounds.center + new Vector3(-extends.x, extends.y, extends.z);
                    bb.radius = Mathf.Max(Mathf.Max(extends.x, extends.y), extends.z);
                    dynamicLODTransitionScalars.Add(dynamicSculpts[i].lodTransitionScalar);
                    dynamicLODFirstThresholds.Add(dynamicSculpts[i].lodFirstTransitionThreshold);
                    dynamicBoundingBoxes.Add(bb);
                    dynamicCullingResults.Add(0);
                    dynamicSculpts[i].myTransform = Matrix4x4.TRS(dynamicSculpts[i].transform.position, dynamicSculpts[i].transform.rotation, dynamicSculpts[i].transform.lossyScale);
                }
            }
        }

        /// <summary>
        ///  If not GPU instancing, the sculpt scene manager needs to know when the material properties have changed on a sculpt. Otherwise, this would only be called ounce.
        /// </summary>
        public void SetNonInstancedMaterialProperties ()
        {
            if(enableGPUInstancing == false)
            {
                for (int i = 0; i < staticSculptRenderers.Count; i++)
                {
                    staticSculptRenderers[i].material.SetColor("_TintColor", staticSculpts[i].tint);
                    staticSculptRenderers[i].material.SetFloat("_TintAmount", staticSculpts[i].tintAmount);
                    staticSculptRenderers[i].material.SetFloat("_Metallic", staticSculpts[i].metallic);
                    staticSculptRenderers[i].material.SetFloat("_Smoothness", staticSculpts[i].smoothness);
                    staticSculptRenderers[i].material.SetFloat("_Emission", staticSculpts[i].emission);
                    staticSculptRenderers[i].material.SetFloat("_CustomMaterialPropertyA", staticSculpts[i].customMaterialPropertyA);
                    staticSculptRenderers[i].material.SetFloat("_CustomMaterialPropertyB", staticSculpts[i].customMaterialPropertyB);
                    staticSculptRenderers[i].material.SetFloat("_CustomMaterialPropertyC", staticSculpts[i].customMaterialPropertyC);
                    staticSculptRenderers[i].material.SetFloat("_CustomMaterialPropertyD", staticSculpts[i].customMaterialPropertyD);
                }
                for (int i = 0; i < dynamicSculptRenderers.Count; i++)
                {
                    dynamicSculptRenderers[i].material.SetColor("_TintColor", dynamicSculpts[i].tint);
                    dynamicSculptRenderers[i].material.SetFloat("_TintAmount", dynamicSculpts[i].tintAmount);
                    dynamicSculptRenderers[i].material.SetFloat("_Metallic", dynamicSculpts[i].metallic);
                    dynamicSculptRenderers[i].material.SetFloat("_Smoothness", dynamicSculpts[i].smoothness);
                    dynamicSculptRenderers[i].material.SetFloat("_Emission", dynamicSculpts[i].emission);
                    dynamicSculptRenderers[i].material.SetFloat("_CustomMaterialPropertyA", dynamicSculpts[i].customMaterialPropertyA);
                    dynamicSculptRenderers[i].material.SetFloat("_CustomMaterialPropertyB", dynamicSculpts[i].customMaterialPropertyB);
                    dynamicSculptRenderers[i].material.SetFloat("_CustomMaterialPropertyC", dynamicSculpts[i].customMaterialPropertyC);
                    dynamicSculptRenderers[i].material.SetFloat("_CustomMaterialPropertyD", dynamicSculpts[i].customMaterialPropertyD);
                }
            }
        }

        //public void RecalculateAllInstancedSculptMaterialProperties ()
        //{
        //    We don't recalculte instanced materials since we have to stay up to date
        //}

        public void RecalculateAllNonInstancedSculptMaterialProperties ()
        {
            SetNonInstancedMaterialProperties();
        }

        public void RecalculateAllStaticSculptBoundingBoxes ()
        {
            GetBoundingBoxCorners(true);
        }

        /// <summary>
        ///  Call this function to let the sculpt scene manager know a sculpt has changed from being static or dynamic, since it will not know automatically.
        /// </summary>
        /// <param name="sculpt">The SDF Sculpt that is being updated.</param>
        public void SculptStaticDynamicChange (SDFSculpt sculpt)
        {
            int index = 0;
            Renderer ren = sculpt.gameObject.GetComponent<Renderer>();
            if(ren == null)
                return;
            BBStruct bb = new BBStruct();
            Vector3 extends = ren.bounds.extents;

            bb.center = ren.bounds.center;
            bb.drb = ren.bounds.center + new Vector3(extends.x, -extends.y, -extends.z);
            bb.drf = ren.bounds.center + new Vector3(extends.x, -extends.y, extends.z);
            bb.dlb = ren.bounds.center + new Vector3(-extends.x, -extends.y, -extends.z);
            bb.dlf = ren.bounds.center + new Vector3(-extends.x, -extends.y, extends.z);
            bb.urb = ren.bounds.center + new Vector3(extends.x, extends.y, -extends.z);
            bb.urf = ren.bounds.center + new Vector3(extends.x, extends.y, extends.z);
            bb.ulb = ren.bounds.center + new Vector3(-extends.x, extends.y, -extends.z);
            bb.ulf = ren.bounds.center + new Vector3(-extends.x, extends.y, extends.z);

            if(sculpt.isSculptStatic)
            {
                if(staticSculpts.Contains(sculpt))
                {
                    index = staticSculpts.IndexOf(sculpt);
                    staticSculptRenderers.RemoveAt(index);
                    staticCullingResults.RemoveAt(index);
                    staticBoundingBoxes.RemoveAt(index);
                    staticLODTransitionScalars.RemoveAt(index);
                    staticLODFirstThresholds.RemoveAt(index);

                    SDFSculpt p = GetParentSculpt(sculpt);
                    if(p != null)
                    {
                        index = parentSculpts.IndexOf(p);
                        for (int i = 0; i < childSculpts[index].Count; i++)
                        {
                            if(staticSculpts[childSculpts[index][i]] == sculpt)
                            {
                                dynamicChildSculpts[index].Add(childSculpts[index][i]);
                                childSculpts[index].RemoveAt(i);
                                break;
                            }
                        }
                    }

                    staticSculpts.Remove(sculpt);

                    if(enableGPUInstancing)
                    {
                        ren.enabled = true;
                    }
                    
                    dynamicSculpts.Add(sculpt);
                    dynamicSculptRenderers.Add(ren);
                    dynamicCullingResults.Add(0);
                    dynamicBoundingBoxes.Add(bb);
                    dynamicLODTransitionScalars.Add(sculpt.lodTransitionScalar);
                    dynamicLODFirstThresholds.Add(sculpt.lodFirstTransitionThreshold);
                }
            }
            else
            {
                if(dynamicSculpts.Contains(sculpt))
                {
                    index = dynamicSculpts.IndexOf(sculpt);
                    dynamicSculptRenderers.RemoveAt(index);
                    dynamicCullingResults.RemoveAt(index);
                    dynamicBoundingBoxes.RemoveAt(index);
                    dynamicLODTransitionScalars.RemoveAt(index);
                    dynamicLODFirstThresholds.RemoveAt(index);

                    SDFSculpt p = GetParentSculpt(sculpt);
                    if(p != null)
                    {
                        index = parentSculpts.IndexOf(p);
                        for (int i = 0; i < dynamicChildSculpts[index].Count; i++)
                        {
                            if(allSculpts[dynamicChildSculpts[index][i]] == sculpt)
                            {
                                childSculpts[index].Add(dynamicChildSculpts[index][i]);
                                dynamicChildSculpts[index].RemoveAt(i);
                                break;
                            }
                        }
                    }

                    dynamicSculpts.Remove(sculpt);

                    if(enableGPUInstancing)
                    {
                        ren.enabled = false;
                    }

                    staticSculpts.Add(sculpt);
                    staticSculptRenderers.Add(ren);
                    staticCullingResults.Add(0);
                    staticBoundingBoxes.Add(bb);
                    staticLODTransitionScalars.Add(sculpt.lodTransitionScalar);
                    staticLODFirstThresholds.Add(sculpt.lodFirstTransitionThreshold);
                }
            }
        }

        /// <summary>
        ///  Add a new sculpt to the sculpt scene manager after start function has already executed.
        /// </summary>
        /// <param name="sculpt">The sculpt to be added.</param>
        public void AddSculpt (SDFSculpt sculpt)
        {
            Renderer ren = sculpt.gameObject.GetComponent<Renderer>();
            if(ren == null)
                return;
            BBStruct bb = new BBStruct();
            Vector3 extends = ren.bounds.extents;

            bb.center = ren.bounds.center;
            bb.drb = ren.bounds.center + new Vector3(extends.x, -extends.y, -extends.z);
            bb.drf = ren.bounds.center + new Vector3(extends.x, -extends.y, extends.z);
            bb.dlb = ren.bounds.center + new Vector3(-extends.x, -extends.y, -extends.z);
            bb.dlf = ren.bounds.center + new Vector3(-extends.x, -extends.y, extends.z);
            bb.urb = ren.bounds.center + new Vector3(extends.x, extends.y, -extends.z);
            bb.urf = ren.bounds.center + new Vector3(extends.x, extends.y, extends.z);
            bb.ulb = ren.bounds.center + new Vector3(-extends.x, extends.y, -extends.z);
            bb.ulf = ren.bounds.center + new Vector3(-extends.x, extends.y, extends.z);
            
            if(sculpt.isSculptStatic == false)
            {
                if(dynamicSculpts.Contains(sculpt) == false && staticSculpts.Contains(sculpt) == false)
                {
                    allSculpts.Add(sculpt);
                    allSculptRenderers.Add(sculpt.gameObject.GetComponent<Renderer>());
                    dynamicSculpts.Add(sculpt);
                    dynamicSculptRenderers.Add(ren);
                    dynamicCullingResults.Add(0);
                    dynamicBoundingBoxes.Add(bb);
                    dynamicLODTransitionScalars.Add(sculpt.lodTransitionScalar);
                    dynamicLODFirstThresholds.Add(sculpt.lodFirstTransitionThreshold);

                    SDFSculpt p = GetParentSculpt(sculpt);
                    if(p != null)
                    {
                        nonInstancedChildSculpts[parentSculpts.IndexOf(p)].Add(allSculpts.Count - 1);
                    }
                }
            }
            else
            {
                if(dynamicSculpts.Contains(sculpt) == false &&staticSculpts.Contains(sculpt) == false)
                {
                    allSculpts.Add(sculpt);
                    allSculptRenderers.Add(sculpt.gameObject.GetComponent<Renderer>());
                    staticSculpts.Add(sculpt);
                    staticSculptRenderers.Add(ren);
                    staticCullingResults.Add(0);
                    staticBoundingBoxes.Add(bb);
                    staticLODTransitionScalars.Add(sculpt.lodTransitionScalar);
                    staticLODFirstThresholds.Add(sculpt.lodFirstTransitionThreshold);

                    SDFSculpt p = GetParentSculpt(sculpt);
                    if(p != null)
                    {
                        childSculpts[parentSculpts.IndexOf(p)].Add(staticSculpts.Count - 1);
                        if(enableGPUInstancing)
                        {
                            sculpt.gameObject.GetComponent<Renderer>().enabled = false;
                        }
                    }
                }
            }
        }

        /// <summary>
        ///  Remove a sculpt from the sculpt scene manager after start function has already executed.
        /// </summary>
        /// <param name="sculpt">The sculpt to be removed.</param>
        public void RemoveSculpt (SDFSculpt sculpt)
        {
            int index = 0;
            Renderer ren = sculpt.gameObject.GetComponent<Renderer>();
            if(ren == null)
                return;

            if(sculpt == null)
                return;

            if(sculpt.isSculptStatic)
            {
                if(staticSculpts.Contains(sculpt))
                {
                    index = staticSculpts.IndexOf(sculpt);
                    if(staticSculptRenderers.Count <= index)
                        return;
                    staticSculptRenderers.RemoveAt(index);
                    staticCullingResults.RemoveAt(index);
                    staticBoundingBoxes.RemoveAt(index);
                    staticLODTransitionScalars.RemoveAt(index);
                    staticLODFirstThresholds.RemoveAt(index);

                    SDFSculpt p = GetParentSculpt(sculpt);
                    if(p != null && sculpt != null)
                    {
                        index = parentSculpts.IndexOf(p);

                        if(enableGPUInstancing)
                        {
                            for (int i = 0; i < childSculpts[index].Count; i++)
                            {
                                if(childSculpts[index].Count <= i)
                                {
                                    if(staticSculpts[childSculpts[index][i]] == sculpt)
                                    {
                                        childSculpts[index].RemoveAt(i);
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            for (int i = 0; i < nonInstancedChildSculpts[index].Count; i++)
                            {
                                if(nonInstancedChildSculpts[index].Count <= i)
                                {
                                    if(allSculpts[nonInstancedChildSculpts[index][i]] == sculpt)
                                    {
                                        nonInstancedChildSculpts[index].RemoveAt(i);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    //if(p == sculpt)
                    //    Debug.Log("Deleting a parent sculpt can result in unexpected behavior. May have to set a new parent sculpt and regenerate manager info.(Ignore this message if you were just exiting Play Mode)");
                    if(enableGPUInstancing)
                        ren.enabled = true;
                    staticSculpts.Remove(sculpt);
                }
            }
            else
            {
                if(dynamicSculpts.Contains(sculpt))
                {
                    index = dynamicSculpts.IndexOf(sculpt);
                    if(dynamicSculptRenderers.Count <= index)
                        return;
                    dynamicSculptRenderers.RemoveAt(index);
                    dynamicCullingResults.RemoveAt(index);
                    dynamicBoundingBoxes.RemoveAt(index);
                    dynamicLODTransitionScalars.RemoveAt(index);
                    dynamicLODFirstThresholds.RemoveAt(index);

                    SDFSculpt p = GetParentSculpt(sculpt);
                    if(p != null)
                    {
                        index = parentSculpts.IndexOf(p);
                        if(enableGPUInstancing)
                        {
                            for (int i = 0; i < dynamicChildSculpts[index].Count; i++)
                            {
                                if(dynamicChildSculpts[index].Count <= i)
                                {
                                    if(dynamicSculpts[dynamicChildSculpts[index][i]] == sculpt)
                                    {
                                        dynamicChildSculpts[index].RemoveAt(i);
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            for (int i = 0; i < nonInstancedChildSculpts[index].Count; i++)
                            {
                                if(nonInstancedChildSculpts[index].Count <= i)
                                {
                                    if(allSculpts[nonInstancedChildSculpts[index][i]] == sculpt)
                                    {
                                        nonInstancedChildSculpts[index].RemoveAt(i);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    dynamicSculpts.Remove(sculpt);
                }
            }

            if(allSculpts.Contains(sculpt))
            {
                index = allSculpts.IndexOf(sculpt);
                allSculpts.Remove(sculpt);
                allSculptRenderers.RemoveAt(index);
            }
        }


        /// <summary>
        ///  If not GPU instancing, let the sculpt scene manager know when a single sculpt has changed material properties.
        /// </summary>
        /// <param name="sculpt">The sculpt that needs its material properties updated.</param>
        public void RecalculateSingleSculptMaterialProperties (SDFSculpt sculpt)
        {
            Renderer ren = null;

            if(staticSculpts.Contains(sculpt) && enableGPUInstancing == false)
                ren = staticSculptRenderers[staticSculpts.IndexOf(sculpt)];
            else if(dynamicSculpts.Contains(sculpt) && enableGPUInstancing == false)
                ren = dynamicSculptRenderers[dynamicSculpts.IndexOf(sculpt)];

            if(ren != null)
            {
                ren.material.SetColor("_TintColor", sculpt.tint);
                ren.material.SetFloat("_TintAmount", sculpt.tintAmount);
                ren.material.SetFloat("_Metallic", sculpt.metallic);
                ren.material.SetFloat("_Smoothness", sculpt.smoothness);
                ren.material.SetFloat("_Emission", sculpt.emission);
                ren.material.SetFloat("_CustomMaterialPropertyA", sculpt.customMaterialPropertyA);
                ren.material.SetFloat("_CustomMaterialPropertyB", sculpt.customMaterialPropertyB);
                ren.material.SetFloat("_CustomMaterialPropertyC", sculpt.customMaterialPropertyC);
                ren.material.SetFloat("_CustomMaterialPropertyD", sculpt.customMaterialPropertyD);
            }
        }

        /// <summary>
        ///  When a static sculpt has changed its transforms, this function will only update for the one sculpt.
        /// </summary>
        /// <param name="sculpt">The sculpt that needs its bounding box info updated.</param>
        public void RecalculateSingleStaticSculptBoundingBox (SDFSculpt sculpt)
        {
            BBStruct bb = new BBStruct();

            int index = staticSculpts.IndexOf(sculpt);
            Renderer ren = staticSculptRenderers[index];

            Vector3 extends = ren.bounds.extents;
            bb.center = ren.bounds.center;
            bb.drb = ren.bounds.center + new Vector3(extends.x, -extends.y, -extends.z);
            bb.drf = ren.bounds.center + new Vector3(extends.x, -extends.y, extends.z);
            bb.dlb = ren.bounds.center + new Vector3(-extends.x, -extends.y, -extends.z);
            bb.dlf = ren.bounds.center + new Vector3(-extends.x, -extends.y, extends.z);
            bb.urb = ren.bounds.center + new Vector3(extends.x, extends.y, -extends.z);
            bb.urf = ren.bounds.center + new Vector3(extends.x, extends.y, extends.z);
            bb.ulb = ren.bounds.center + new Vector3(-extends.x, extends.y, -extends.z);
            bb.ulf = ren.bounds.center + new Vector3(-extends.x, extends.y, extends.z);
            bb.radius = Mathf.Max(Mathf.Max(extends.x, extends.y), extends.z);
            staticBoundingBoxes[index] = bb;
            staticCullingResults[index] = 0;
        }

        /// <summary>
        ///  Update manager info when sculpts have been created, deleted, or changed. This recalculates all information for all sculpts.
        /// </summary>
        public void GenerateManagerInfo ()
        {
            childSculpts.Clear();
            nonInstancedChildSculpts.Clear();
            dynamicChildSculpts.Clear();
            for (int i = 0; i < parentSculpts.Count; i++)
            {
                childSculpts.Add(new List<int>());
                nonInstancedChildSculpts.Add(new List<int>());
                dynamicChildSculpts.Add(new List<int>());
            }
            GetAllMaterialPropertyRenderTextures();
            BuildSculptsFromMeshData();
            SplitDynamicAndStaticSculpts();
            GetBoundingBoxCorners(true);
            SetNonInstancedMaterialProperties();
            generatedManagerInfo = true;
        }

        //bool InFrustum(Matrix4x4 M, Vector3 p, float corner) 
        //{
        //    Vector4 Pclip = M * new Vector4(p.x, p.y, p.z, 1);
        //    Pclip /= Vector3.Distance(myCameras[0].transform.position, p);
        //    Pclip = new Vector4(Pclip.x * (((float)Screen.width)/((float)Screen.height)), Pclip.y, Pclip.z, Pclip.w);
        //    Debug.Log("X: " + Pclip.x.ToString() + " , Y: " + Pclip.y.ToString());
        //    return Mathf.Abs(Pclip.y) <= 0.5f && Pclip.x >= corner * -1 && Pclip.x < corner && Pclip.z < 0 && Vector3.Distance(myCameras[0].transform.position, p) < myCameras[0].farClipPlane;
        //}

        /// <summary>
        ///  Converts a regular mesh into a fuzzy mesh based on the given parameters and returns it. Uses the vertex color alpha channel to encode some of this information.
        /// </summary>
        /// <param name="m">The mesh that will be turned into a fuzzy mesh.</param>
        /// <param name="fuzzNoiseScale">How big is the noise pattern. </param>
        /// <param name="fuzzNoiseStrength">How strong is the effect of the noise.</param>
        /// <param name="fuzzScale">The size of each fuzz quad.</param>
        /// <param name="fuzzOffset">A uniform offset for the fuzz rotation.</param>
        /// <param name="recalcNorms">For each fuzz quad, make all normals match.</param>
        public Mesh GetFuzzyMesh (Mesh m, float fuzzNoiseScale, float fuzzNoiseStrength, float fuzzScale, float fuzzOffset, bool recalcNorms)
        {
            ComputeBuffer fVerts = new ComputeBuffer(m.vertices.Length, sizeof(float) * 3);
                fVerts.SetData(m.vertices);
                myFuzzyComputeShader.SetBuffer(myFuzzyComputeShader.FindKernel("CSMain"), "vertexBuffer", fVerts);
            ComputeBuffer fUVs = new ComputeBuffer(m.uv.Length, sizeof(float) * 2);
                fUVs.SetData(m.uv);
                myFuzzyComputeShader.SetBuffer(myFuzzyComputeShader.FindKernel("CSMain"), "uvBuffer", fUVs);
            ComputeBuffer fNorms = new ComputeBuffer(m.normals.Length, sizeof(float) * 3);
                fNorms.SetData(m.normals);
                myFuzzyComputeShader.SetBuffer(myFuzzyComputeShader.FindKernel("CSMain"), "normalBuffer", fNorms);
            ComputeBuffer fCols = new ComputeBuffer(m.colors.Length, sizeof(float) * 4);
                fCols.SetData(m.colors);
                myFuzzyComputeShader.SetBuffer(myFuzzyComputeShader.FindKernel("CSMain"), "colorBuffer", fCols);

            myFuzzyComputeShader.SetTexture(myFuzzyComputeShader.FindKernel("CSMain"), "noiseTexture", noise);
            myFuzzyComputeShader.SetFloat("fuzzRotationOffset", fuzzOffset);
            myFuzzyComputeShader.SetFloat("fuzzNoiseScale", fuzzNoiseScale);
            myFuzzyComputeShader.SetFloat("fuzzNoiseStrength", fuzzNoiseStrength);
            myFuzzyComputeShader.SetFloat("fuzzScale", fuzzScale);

            myFuzzyComputeShader.Dispatch(myFuzzyComputeShader.FindKernel("CSMain"),
            Mathf.Max(1,Mathf.CeilToInt(((float)m.vertices.Length)/64.0f)), 1, 1);

            Vector3[] verts = new Vector3[m.vertices.Length];
            Vector2[] uvs = new Vector2[m.uv.Length];
            Color[] colors = new Color[m.colors.Length];
            Vector3[] normals = new Vector3[m.normals.Length];

            //for (int i = 0; i < m.vertexCount / 6; i++)
            //{
            //    
            //}

            fVerts.GetData(verts);
            fUVs.GetData(uvs);
            fCols.GetData(colors);
            fNorms.GetData(normals);

            if(recalcNorms)
                m.SetNormals(normals);

            m.SetColors(colors);
            m.SetUVs(0, uvs);
            m.SetVertices(verts);

            m.RecalculateBounds();
            m.UploadMeshData(false);

            fVerts.Release();
            fUVs.Release();
            fNorms.Release();
            fCols.Release();

            m = RandomizeMeshAlpha (m, fuzzNoiseScale, fuzzNoiseStrength);

            return m;
        }

        /// <summary>
        ///  Flips / mirrors sculpt geometry over the x, y, or z axis. Does the same for LODs. This will overwrite the mesh data of the original.
        /// </summary>
        /// <param name="sculpt">The sculpt being flipped or mirrored.</param>
        /// <param name="xFlip">Flip mesh over x-axis.</param>
        /// <param name="yFlip">Flip mesh over y-axis.</param>
        /// <param name="zFlip">Flip mesh over z-axis.</param>
        public void FlipOverAxis (SDFSculpt sculpt, bool xFlip, bool yFlip, bool zFlip)
        {

            string path = Application.dataPath + sculpt.myMeshPath + sculpt.myName + "_MeshData.json";
            string json;
            Mesh myMesh = new Mesh();
            Mesh outputMesh = null;
            MeshGenerator.SculptMeshStruct meshStruct = new MeshGenerator.SculptMeshStruct();

            int index = parentSculpts.IndexOf(sculpt);

            for (int i = 0; i < sculpt.LODResolutions.Count + 1; i++)
            {
                if(i > 0)
                    path = Application.dataPath + sculpt.myMeshPath + sculpt.myName +  "_LOD" + i.ToString() + "MeshData.json";
                
                if(File.Exists(path))
                {
                    json = File.ReadAllText(path);
                    meshStruct = new MeshGenerator.SculptMeshStruct();
                    meshStruct = JsonUtility.FromJson<MeshGenerator.SculptMeshStruct>(json);

                    myMesh = new Mesh();
                    myMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    myMesh.SetVertices(meshStruct.vertices);
                    myMesh.SetTriangles(meshStruct.triangles, 0 , false);
                    myMesh.SetUVs(0, meshStruct.uvs);
                    myMesh.SetColors(meshStruct.colors);
                    myMesh.SetNormals(meshStruct.normals);
                    myMesh.RecalculateBounds();
                    myMesh.UploadMeshData(false);

                    ComputeBuffer fVerts = new ComputeBuffer(myMesh.vertices.Length, sizeof(float) * 3);
                        fVerts.SetData(myMesh.vertices);
                        myFuzzyComputeShader.SetBuffer(myFuzzyComputeShader.FindKernel("FlipOverAxis"), "vertexBuffer", fVerts);
                    ComputeBuffer fNorms = new ComputeBuffer(myMesh.normals.Length, sizeof(float) * 3);
                        fNorms.SetData(myMesh.normals);
                        myFuzzyComputeShader.SetBuffer(myFuzzyComputeShader.FindKernel("FlipOverAxis"), "normalBuffer", fNorms);
                    
                    myFuzzyComputeShader.SetInt("xAxis", 0);
                    myFuzzyComputeShader.SetInt("yAxis", 0);
                    myFuzzyComputeShader.SetInt("zAxis", 0);
                    if(xFlip)
                        myFuzzyComputeShader.SetInt("xAxis", 1);
                    if(yFlip)
                        myFuzzyComputeShader.SetInt("yAxis", 1);
                    if(zFlip)
                        myFuzzyComputeShader.SetInt("zAxis", 1);

                    myFuzzyComputeShader.Dispatch(myFuzzyComputeShader.FindKernel("FlipOverAxis"),
                    Mathf.Max(1, Mathf.CeilToInt(((float)myMesh.vertices.Length)/64.0f)), 1, 1);
                    
                    Vector3[] verts = new Vector3[myMesh.normals.Length];
                    Vector3[] normals = new Vector3[myMesh.normals.Length];

                    fVerts.GetData(verts);
                    fNorms.GetData(normals);

                    myMesh.SetTriangles(myMesh.triangles.Reverse().ToArray(), 0);
                    myMesh.SetVertices(verts);
                    myMesh.SetNormals(normals);

                    myMesh.RecalculateBounds();
                    myMesh.UploadMeshData(false);

                    fNorms.Release();
                    fVerts.Release();

                    if(i == 0)
                        outputMesh = myMesh;

                    meshStruct.vertices = new Vector3[myMesh.vertices.Length];
                    meshStruct.vertices = myMesh.vertices;
                    meshStruct.normals = new Vector3[myMesh.normals.Length];
                    meshStruct.normals = myMesh.normals;
                    meshStruct.triangles = new int[myMesh.triangles.Length];
                    meshStruct.triangles = myMesh.triangles;
                    meshStruct.uvs = new Vector2[myMesh.uv.Length];
                    meshStruct.uvs = myMesh.uv;
                    meshStruct.colors = new Color[myMesh.colors.Length];
                    meshStruct.colors = myMesh.colors;

                    json = JsonUtility.ToJson(meshStruct);
                    File.WriteAllText(path, json);

                }
            }

            sculpt.gameObject.GetComponent<MeshFilter>().sharedMesh = outputMesh;
        }

        /// <summary>
        ///  Flips / mirrors a mesh over the x, y, or z axis and returns it.
        /// </summary>
        /// <param name="m">The mesh being flipped or mirrored.</param>
        /// <param name="xFlip">Flip sculpt over x-axis.</param>
        /// <param name="yFlip">Flip sculpt over y-axis.</param>
        /// <param name="zFlip">Flip sculpt over z-axis.</param>

        public Mesh FlipOverAxisMesh (Mesh m, bool xFlip, bool yFlip, bool zFlip)
        {
            ComputeBuffer fVerts = new ComputeBuffer(m.vertices.Length, sizeof(float) * 3);
                fVerts.SetData(m.vertices);
                myFuzzyComputeShader.SetBuffer(myFuzzyComputeShader.FindKernel("FlipOverAxis"), "vertexBuffer", fVerts);
            ComputeBuffer fNorms = new ComputeBuffer(m.normals.Length, sizeof(float) * 3);
                fNorms.SetData(m.normals);
                myFuzzyComputeShader.SetBuffer(myFuzzyComputeShader.FindKernel("FlipOverAxis"), "normalBuffer", fNorms);
            
            myFuzzyComputeShader.SetInt("xAxis", 0);
            myFuzzyComputeShader.SetInt("yAxis", 0);
            myFuzzyComputeShader.SetInt("zAxis", 0);
            if(xFlip)
                myFuzzyComputeShader.SetInt("xAxis", 1);
            if(yFlip)
                myFuzzyComputeShader.SetInt("yAxis", 1);
            if(zFlip)
                myFuzzyComputeShader.SetInt("zAxis", 1);

            myFuzzyComputeShader.Dispatch(myFuzzyComputeShader.FindKernel("FlipOverAxis"),
            Mathf.Max(1, Mathf.CeilToInt(((float)m.vertices.Length)/64.0f)), 1, 1);
            Vector3[] verts = new Vector3[m.normals.Length];
            Vector3[] normals = new Vector3[m.normals.Length];

            fVerts.GetData(verts);
            fNorms.GetData(normals);

            m.SetTriangles(m.triangles.Reverse().ToArray(), 0);
            m.SetVertices(verts);
            m.SetNormals(normals);

            m.RecalculateBounds();
            m.UploadMeshData(false);

            fNorms.Release();
            fVerts.Release();

            return m;
        }

        /// <summary>
        ///  Converts a regular sculpt into a fuzzy sculpt. This includes the LODs. This will overwrite the mesh data of the original.
        /// </summary>
        /// <param name="sculpt">The sculpt being turned into a fuzzy sculpt.</param>
        public void ConvertToFuzzy (SDFSculpt sculpt)
        {
            //Mesh fuzzyMesh = GetFuzzyMesh(sculpt.gameObject.GetComponent<MeshFilter>().sharedMesh, sculpt.fuzzNoiseScale, sculpt.fuzzNoiseStrength, sculpt.fuzzScale, sculpt.fuzzRotationOffset, sculpt.realignFuzzNormals);
            //GetComponent<MeshFilter>().sharedMesh = fuzzyMesh;

            string path = Application.dataPath + sculpt.myMeshPath + sculpt.myName + "_MeshData.json";
            string json;
            Mesh myMesh = new Mesh();
            Mesh outputMesh = null;
            MeshGenerator.SculptMeshStruct meshStruct = new MeshGenerator.SculptMeshStruct();

            int index = parentSculpts.IndexOf(sculpt);

            for (int i = 0; i < sculpt.LODResolutions.Count + 1; i++)
            {
                if(i > 0)
                    path = Application.dataPath + sculpt.myMeshPath + sculpt.myName +  "_LOD" + i.ToString() + "MeshData.json";
                
                if(File.Exists(path))
                {
                    json = File.ReadAllText(path);
                    meshStruct = new MeshGenerator.SculptMeshStruct();
                    meshStruct = JsonUtility.FromJson<MeshGenerator.SculptMeshStruct>(json);

                    myMesh = new Mesh();
                    myMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    myMesh.SetVertices(meshStruct.vertices);
                    myMesh.SetTriangles(meshStruct.triangles, 0 , false);
                    myMesh.SetUVs(0, meshStruct.uvs);
                    myMesh.SetColors(meshStruct.colors);
                    myMesh.SetNormals(meshStruct.normals);
                    myMesh.RecalculateBounds();
                    myMesh.UploadMeshData(false);

                    myMesh = GetFuzzyMesh(myMesh, sculpt.fuzzNoiseScale, sculpt.fuzzNoiseStrength,
                        sculpt.fuzzScale, sculpt.fuzzRotationOffset, sculpt.realignFuzzNormals);

                    if(i == 0)
                        outputMesh = myMesh;

                    meshStruct.vertices = new Vector3[myMesh.vertices.Length];
                    meshStruct.vertices = myMesh.vertices;
                    meshStruct.normals = new Vector3[myMesh.normals.Length];
                    meshStruct.normals = myMesh.normals;
                    meshStruct.triangles = new int[myMesh.triangles.Length];
                    meshStruct.triangles = myMesh.triangles;
                    meshStruct.uvs = new Vector2[myMesh.uv.Length];
                    meshStruct.uvs = myMesh.uv;
                    meshStruct.colors = new Color[myMesh.colors.Length];
                    meshStruct.colors = myMesh.colors;

                    json = JsonUtility.ToJson(meshStruct);
                    File.WriteAllText(path, json);

                }
            }

            sculpt.gameObject.GetComponent<MeshFilter>().sharedMesh = outputMesh;
            
        }

        /// <summary>
        ///  Randomizes the vertex color alpha channel on the vertices  of a mesh. Called when generating all meshes and is used in the fuzzy SDF shader.
        /// </summary>
        /// <param name="m">The mesh being encoded with random values in its vertex color alpha channel.</param>
        /// <param name="fuzzNoiseScale">How big is the noise pattern.</param>
        /// <param name="fuzzNoiseStrength">How strong is the effect of the noise.</param>
        public Mesh RandomizeMeshAlpha (Mesh m, float fuzzNoiseScale, float fuzzNoiseStrength)
        {
            ComputeBuffer fVerts = new ComputeBuffer(m.vertices.Length, sizeof(float) * 3);
                fVerts.SetData(m.vertices);
                myFuzzyComputeShader.SetBuffer(myFuzzyComputeShader.FindKernel("RandomAlpha"), "vertexBuffer", fVerts);
            ComputeBuffer fCols = new ComputeBuffer(m.colors.Length, sizeof(float) * 4);
                fCols.SetData(m.colors);
                myFuzzyComputeShader.SetBuffer(myFuzzyComputeShader.FindKernel("RandomAlpha"), "colorBuffer", fCols);

            myFuzzyComputeShader.SetTexture(myFuzzyComputeShader.FindKernel("RandomAlpha"), "noiseTexture", noise);
            myFuzzyComputeShader.SetFloat("fuzzNoiseScale", fuzzNoiseScale);
            myFuzzyComputeShader.SetFloat("fuzzNoiseStrength", fuzzNoiseStrength);

            myFuzzyComputeShader.Dispatch(myFuzzyComputeShader.FindKernel("RandomAlpha"),
            Mathf.Max(1,Mathf.CeilToInt(((float)m.colors.Length)/64.0f)), 1, 1);
            Color[] colors = new Color[m.colors.Length];

            fCols.GetData(colors);
            m.SetColors(colors);

            m.RecalculateBounds();
            m.UploadMeshData(false);

            fCols.Release();
            fVerts.Release();

            return m;
        }

        float checkTime = 0;

        ComputeBuffer bbBuffer;
        ComputeBuffer meshIndexBuffer;
        ComputeBuffer fcResultBuffer;
        ComputeBuffer ocResultBuffer;
        ComputeBuffer lodTransitionBuffer;
        ComputeBuffer lodFirstThresholdBuffer;

        ComputeBuffer topLeftBuffer;
        ComputeBuffer bottomLeftBuffer;
        ComputeBuffer topRightBuffer;
        ComputeBuffer bottomRightBuffer;

        ComputeBuffer matPropBuffer;

        RenderTexture depthTexture;


        // Start is called before the first frame update
        void Start()
        {
            Camera.onPostRender += OnPostRenderCallback;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        // Update is called once per frame
        void Update()
        {
            if(generateManagerInfoWhenSceneBegins)
            {
                if(checkTime < 0.5f)
                {
                    checkTime += Time.deltaTime;
                    if(checkTime >= 0.5f)
                    {
                        GenerateManagerInfo ();
                    }
                }
            }
            if(generatedManagerInfo)
            {
                GetBoundingBoxCorners(false);
            }

            //if(enableGPUInstancing)
            //{
            //    mySDFShader.SetFloat("_SculptSceneManagerGPUInstancing", 1);
            //    myFuzzySDFShader.SetFloat("_SculptSceneManagerGPUInstancing", 1);
            //}
            //else
            //{
            //    mySDFShader.SetFloat("_SculptSceneManagerGPUInstancing", 0);
            //    myFuzzySDFShader.SetFloat("_SculptSceneManagerGPUInstancing", 0);
            //}
            
            //if(InFrustum(mainCam.worldToCameraMatrix, allSculpts[0].transform.position, corner))
            //    Debug.Log("I SEE");
            //else
            //    Debug.Log("I SEE NOT");

            int camerasToRender = myCameras.Count;
            if(enableGPUInstancing == false)
                camerasToRender = 1;

            int kid = myComputeShader.FindKernel("FrustumCulling");

            if(staticBoundingBoxes.Count > 0)
            {
                for (int camIndex = 0; camIndex < camerasToRender; camIndex++)
                {
                    if(myCameras[camIndex].enabled == false || myCameras[camIndex].gameObject.activeInHierarchy == false)
                        continue;

                    kid = myComputeShader.FindKernel("FrustumCulling");
                    depthTexture = trackedDepthTextures[camIndex];

                    bbBuffer = new ComputeBuffer(staticBoundingBoxes.Count, sizeof(float) * 3 * 9 + sizeof(float));
                    bbBuffer.SetData(staticBoundingBoxes);
                    myComputeShader.SetBuffer(kid, "bbs", bbBuffer);

                    meshIndexBuffer = new ComputeBuffer(staticCullingResults.Count, sizeof(int));
                    meshIndexBuffer.SetData(new int[staticCullingResults.Count]);
                    myComputeShader.SetBuffer(kid, "meshIndex", meshIndexBuffer);

                    fcResultBuffer = new ComputeBuffer(staticBoundingBoxes.Count, sizeof(float));
                    fcResultBuffer.SetData(staticCullingResults);
                    myComputeShader.SetBuffer(kid, "fCullResults", fcResultBuffer);

                    lodTransitionBuffer = new ComputeBuffer(staticLODTransitionScalars.Count, sizeof(float));
                    lodTransitionBuffer.SetData(staticLODTransitionScalars);
                    myComputeShader.SetBuffer(kid, "lodTransitionScalars", lodTransitionBuffer);

                    lodFirstThresholdBuffer = new ComputeBuffer(staticLODFirstThresholds.Count, sizeof(float));
                    lodFirstThresholdBuffer.SetData(staticLODFirstThresholds);
                    myComputeShader.SetBuffer(kid, "lodFirstTransitionThresholds", lodFirstThresholdBuffer);

                    topLeftBuffer = new ComputeBuffer(staticCullingResults.Count, sizeof(float) * 2);
                    topLeftBuffer.SetData(new Vector2[staticCullingResults.Count]);
                    myComputeShader.SetBuffer(kid, "topLeft", topLeftBuffer);
                    bottomLeftBuffer = new ComputeBuffer(staticCullingResults.Count, sizeof(float) * 2);
                    bottomLeftBuffer.SetData(new Vector2[staticCullingResults.Count]);
                    myComputeShader.SetBuffer(kid, "bottomLeft", bottomLeftBuffer);
                    topRightBuffer = new ComputeBuffer(staticCullingResults.Count, sizeof(float) * 2);
                    topRightBuffer.SetData(new Vector2[staticCullingResults.Count]);
                    myComputeShader.SetBuffer(kid, "topRight", topRightBuffer);
                    bottomRightBuffer = new ComputeBuffer(staticCullingResults.Count, sizeof(float) * 2);
                    bottomRightBuffer.SetData(new Vector2[staticCullingResults.Count]);
                    myComputeShader.SetBuffer(kid, "bottomRight", bottomRightBuffer);

                    Vector3 cPos = myCameras[camIndex].transform.position;
                    float corner = 0;

                    //Vector3 p = myCameras[camIndex].ScreenToWorldPoint(new Vector3(Screen.height * myCameras[camIndex].rect.height, 0,1));
                    //Vector4 Pclip = myCameras[camIndex].worldToCameraMatrix * new Vector4(p.x, p.y, p.z, 1);
                    //corner = Pclip.x * 10;
                    corner = 0.9f;
                    if(depthTexture.width > depthTexture.height)
                        corner += (Mathf.Abs(depthTexture.width - depthTexture.height)/(float)Mathf.Min(depthTexture.width, depthTexture.height))/2;
                    else
                    {
                        corner -= (Mathf.Abs(depthTexture.width - depthTexture.height)/(float)Mathf.Min(depthTexture.width, depthTexture.height))/2;
                    }
                    corner = ((float)depthTexture.width )/ ((float)depthTexture.height);
                    corner /= Mathf.Lerp(1.7f, 1.1f, ((float)depthTexture.width/(float)depthTexture.height)/3.6f);
                    //if(camIndex == 0)
                    //    Debug.Log(corner);
                    myComputeShader.SetInt("whichCam", camIndex);
                    myComputeShader.SetVector("mainCamPos", new Vector4(cPos.x, cPos.y, cPos.z, 1));
                    myComputeShader.SetFloat("screenW", Screen.width);
                    myComputeShader.SetFloat("screenH", Screen.height);
                    myComputeShader.SetFloat("depthTextureW", depthTexture.width);
                    myComputeShader.SetFloat("depthTextureH", depthTexture.height);
                    myComputeShader.SetFloat("screenCorner", corner);
                    myComputeShader.SetVector("camRect", new Vector4(myCameras[camIndex].rect.x,myCameras[camIndex].rect.y, myCameras[camIndex].rect.width, myCameras[camIndex].rect.height));
                    myComputeShader.SetFloat("farClipPlane", myCameras[camIndex].farClipPlane);
                    myComputeShader.SetFloat("nearClipPlane", myCameras[camIndex].nearClipPlane);
                    myComputeShader.SetMatrix("worldToCamMatrix", myCameras[camIndex].worldToCameraMatrix);
                    myComputeShader.SetMatrix("camToWorldMatrix", myCameras[camIndex].cameraToWorldMatrix);
                    myComputeShader.SetFloat("boundsError", boundsMarginOfError);
                    myComputeShader.SetFloat("depthError", depthMarginOfError);
                    myComputeShader.SetFloat("verticalError", verticalMarginOfError);

                    //if(camIndex == 0)
                    //    InFrustum(myCameras[camIndex].worldToCameraMatrix, test.transform.position, corner);

                    myComputeShader.Dispatch(kid, Mathf.Max(1,Mathf.CeilToInt(((float)staticBoundingBoxes.Count)/64.0f)), 1, 1);

                    int[] meshIndexArr = new int[meshIndexBuffer.count];
                    meshIndexBuffer.GetData(meshIndexArr);

                    if(enableOcclusionCulling == false)
                    {
                        float[] resultArr = new float[fcResultBuffer.count];
                        fcResultBuffer.GetData(resultArr);

                        //for (int i = 0; i < resultArr.Length; i++)
                        //{
                        //    Debug.Log(i.ToString() + "# static sculpt  -> " + resultArr[i].ToString());
                        //}
                        //Output = resultArr;

                        if(enableGPUInstancing)
                        {
                            GPUInstancing(camIndex, resultArr, meshIndexArr);
                        }
                        else
                        {
                            NoGPUInstancing (resultArr, meshIndexArr);
                        }
                    }
                    else
                    {
                        kid = myComputeShader.FindKernel("OcclusionCulling");

                        ocResultBuffer = new ComputeBuffer(staticCullingResults.Count, sizeof(float));
                        ocResultBuffer.SetData(staticCullingResults);
                        myComputeShader.SetBuffer(kid, "oCullResults", ocResultBuffer);

                        myComputeShader.SetBuffer(kid, "bbs", bbBuffer);
                        myComputeShader.SetBuffer(kid, "fCullResults", fcResultBuffer);
                        myComputeShader.SetBuffer(kid, "topLeft", topLeftBuffer);
                        myComputeShader.SetBuffer(kid, "bottomLeft", bottomLeftBuffer);
                        myComputeShader.SetBuffer(kid, "topRight", topRightBuffer);
                        myComputeShader.SetBuffer(kid, "bottomRight", bottomRightBuffer);

                        //RenderTexture depthTexture = new RenderTexture(Screen.width, Screen.height, 16, RenderTextureFormat.RFloat);
                        //depthTexture.enableRandomWrite = true;
                        //depthTexture.Create();

                        //Graphics.CopyTexture(Shader.GetGlobalTexture("_CameraDepthTexture"), depthTexture);

                        //Graphics.Blit(Shader.GetGlobalTexture("_CameraDepthTexture"), depthTexture);
                        
                        //test = depthTexture;
                        //depthTexture.enableRandomWrite = true;//Shader.GetGlobalTexture("_CameraDepthTexture");

                        myComputeShader.SetTexture(kid, "depthTexture", depthTexture);
                        
                        Vector3 ncp = myCameras[camIndex].ViewportToWorldPoint(new Vector3(0, 1, myCameras[camIndex].nearClipPlane));
                        myComputeShader.SetVector("ncpTopLeft", new Vector4(ncp.x, ncp.y, ncp.z, 0));
                        ncp = myCameras[camIndex].ViewportToWorldPoint(new Vector3(1, 1, myCameras[camIndex].nearClipPlane));
                        myComputeShader.SetVector("ncpTopRight", new Vector4(ncp.x, ncp.y, ncp.z, 0));
                        ncp = myCameras[camIndex].ViewportToWorldPoint(new Vector3(0, 0, myCameras[camIndex].nearClipPlane));
                        myComputeShader.SetVector("ncpBottomLeft", new Vector4(ncp.x, ncp.y, ncp.z, 0));
                        ncp = myCameras[camIndex].ViewportToWorldPoint(new Vector3(1, 0, myCameras[camIndex].nearClipPlane));
                        myComputeShader.SetVector("ncpBottomRight", new Vector4(ncp.x, ncp.y, ncp.z, 0));
                        
                        myComputeShader.Dispatch(kid, Mathf.CeilToInt(depthTexture.width/8.0f), Mathf.CeilToInt(depthTexture.height/8.0f), 1);

                        float[] resultArr = new float[ocResultBuffer.count];
                        ocResultBuffer.GetData(resultArr);

                        //int[] resultArr2 = new int[fcResultBuffer.count];
                        //fcResultBuffer.GetData(resultArr2);

                        if(enableGPUInstancing)
                        {
                            GPUInstancing(camIndex, resultArr, meshIndexArr);
                        }
                        else
                        {
                            NoGPUInstancing (resultArr, meshIndexArr);
                        }
                    }
                    StartCoroutine(Release(depthTexture));
                    topLeftBuffer.Release();
                    bottomLeftBuffer.Release();
                    topRightBuffer.Release();
                    bottomRightBuffer.Release();

                    bbBuffer.Release();
                    meshIndexBuffer.Release();
                    fcResultBuffer.Release();
                    lodTransitionBuffer.Release();
                    lodFirstThresholdBuffer.Release();
                    if(ocResultBuffer != null)
                        ocResultBuffer.Release();
                }
            }

            if(dynamicBoundingBoxes.Count > 0)
            {
                for (int camIndex = 0; camIndex < camerasToRender; camIndex++)
                {
                    if(myCameras[camIndex].enabled == false || myCameras[camIndex].gameObject.activeInHierarchy == false)
                        continue;

                    kid = myComputeShader.FindKernel("FrustumCullingDynamic");
                    depthTexture = trackedDepthTextures[camIndex];

                    bbBuffer = new ComputeBuffer(dynamicBoundingBoxes.Count, sizeof(float) * 3 * 9 + sizeof(float));
                    bbBuffer.SetData(dynamicBoundingBoxes);
                    myComputeShader.SetBuffer(kid, "bbsDynamic", bbBuffer);

                    meshIndexBuffer = new ComputeBuffer(dynamicCullingResults.Count, sizeof(int));
                    meshIndexBuffer.SetData(new int[dynamicCullingResults.Count]);
                    myComputeShader.SetBuffer(kid, "meshIndexDynamic", meshIndexBuffer);

                    fcResultBuffer = new ComputeBuffer(dynamicBoundingBoxes.Count, sizeof(float));
                    fcResultBuffer.SetData(dynamicCullingResults);
                    myComputeShader.SetBuffer(kid, "fCullResultsDynamic", fcResultBuffer);

                    lodTransitionBuffer = new ComputeBuffer(dynamicLODTransitionScalars.Count, sizeof(float));
                    lodTransitionBuffer.SetData(dynamicLODTransitionScalars);
                    myComputeShader.SetBuffer(kid, "lodTransitionScalarsDynamic", lodTransitionBuffer);

                    lodFirstThresholdBuffer = new ComputeBuffer(dynamicLODFirstThresholds.Count, sizeof(float));
                    lodFirstThresholdBuffer.SetData(dynamicLODFirstThresholds);
                    myComputeShader.SetBuffer(kid, "lodFirstTransitionThresholdsDynamic", lodFirstThresholdBuffer);

                    topLeftBuffer = new ComputeBuffer(dynamicCullingResults.Count, sizeof(float) * 2);
                    topLeftBuffer.SetData(new Vector2[dynamicCullingResults.Count]);
                    myComputeShader.SetBuffer(kid, "topLeftDynamic", topLeftBuffer);
                    bottomLeftBuffer = new ComputeBuffer(dynamicCullingResults.Count, sizeof(float) * 2);
                    bottomLeftBuffer.SetData(new Vector2[dynamicCullingResults.Count]);
                    myComputeShader.SetBuffer(kid, "bottomLeftDynamic", bottomLeftBuffer);
                    topRightBuffer = new ComputeBuffer(dynamicCullingResults.Count, sizeof(float) * 2);
                    topRightBuffer.SetData(new Vector2[dynamicCullingResults.Count]);
                    myComputeShader.SetBuffer(kid, "topRightDynamic", topRightBuffer);
                    bottomRightBuffer = new ComputeBuffer(dynamicCullingResults.Count, sizeof(float) * 2);
                    bottomRightBuffer.SetData(new Vector2[dynamicCullingResults.Count]);
                    myComputeShader.SetBuffer(kid, "bottomRightDynamic", bottomRightBuffer);

                    Vector3 cPos = myCameras[camIndex].transform.position;
                    float corner = 0;

                    corner = 0.9f;
                    if(depthTexture.width > depthTexture.height)
                        corner += (Mathf.Abs(depthTexture.width - depthTexture.height)/(float)Mathf.Min(depthTexture.width, depthTexture.height))/2;
                    else
                    {
                        corner -= (Mathf.Abs(depthTexture.width - depthTexture.height)/(float)Mathf.Min(depthTexture.width, depthTexture.height))/2;
                    }
                    corner = ((float)depthTexture.width )/ ((float)depthTexture.height);
                    corner /= Mathf.Lerp(1.7f, 1.1f, ((float)depthTexture.width/(float)depthTexture.height)/3.6f);
                    //if(camIndex == 0)
                    //    Debug.Log(corner);
                    myComputeShader.SetInt("whichCam", camIndex);
                    myComputeShader.SetVector("mainCamPos", new Vector4(cPos.x, cPos.y, cPos.z, 1));
                    myComputeShader.SetFloat("screenW", Screen.width);
                    myComputeShader.SetFloat("screenH", Screen.height);
                    myComputeShader.SetFloat("depthTextureW", depthTexture.width);
                    myComputeShader.SetFloat("depthTextureH", depthTexture.height);
                    myComputeShader.SetFloat("screenCorner", corner);
                    myComputeShader.SetVector("camRect", new Vector4(myCameras[camIndex].rect.x,myCameras[camIndex].rect.y, myCameras[camIndex].rect.width, myCameras[camIndex].rect.height));
                    myComputeShader.SetFloat("farClipPlane", myCameras[camIndex].farClipPlane);
                    myComputeShader.SetFloat("nearClipPlane", myCameras[camIndex].nearClipPlane);
                    myComputeShader.SetMatrix("worldToCamMatrix", myCameras[camIndex].worldToCameraMatrix);
                    myComputeShader.SetMatrix("camToWorldMatrix", myCameras[camIndex].cameraToWorldMatrix);
                    myComputeShader.SetFloat("boundsError", boundsMarginOfError);
                    myComputeShader.SetFloat("depthError", depthMarginOfError);
                    myComputeShader.SetFloat("verticalError", verticalMarginOfError);

                    //if(camIndex == 0)
                    //    InFrustum(myCameras[camIndex].worldToCameraMatrix, test.transform.position, corner);

                    myComputeShader.Dispatch(kid, Mathf.Max(1,Mathf.CeilToInt(((float)dynamicBoundingBoxes.Count)/64.0f)), 1, 1);

                    int[] meshIndexArr = new int[meshIndexBuffer.count];
                    meshIndexBuffer.GetData(meshIndexArr);

                    if(enableOcclusionCulling == false)
                    {
                        float[] resultArr = new float[fcResultBuffer.count];
                        fcResultBuffer.GetData(resultArr);

                        //Debug.Log(meshIndexArr[0]);
                        if(enableGPUInstancing)
                        {
                            GPUInstancingDynamic(camIndex, resultArr, meshIndexArr);
                        }
                        else
                        {
                            NoGPUInstancingDynamic(resultArr, meshIndexArr);
                        }
                        //NoGPUInstancingDynamic (resultArr, meshIndexArr);
                    }
                    else
                    {
                        kid = myComputeShader.FindKernel("OcclusionCullingDynamic");

                        ocResultBuffer = new ComputeBuffer(dynamicCullingResults.Count, sizeof(float));
                        ocResultBuffer.SetData(dynamicCullingResults);
                        myComputeShader.SetBuffer(kid, "oCullResultsDynamic", ocResultBuffer);

                        myComputeShader.SetBuffer(kid, "bbsDynamic", bbBuffer);
                        myComputeShader.SetBuffer(kid, "fCullResultsDynamic", fcResultBuffer);
                        myComputeShader.SetBuffer(kid, "topLeftDynamic", topLeftBuffer);
                        myComputeShader.SetBuffer(kid, "bottomLeftDynamic", bottomLeftBuffer);
                        myComputeShader.SetBuffer(kid, "topRightDynamic", topRightBuffer);
                        myComputeShader.SetBuffer(kid, "bottomRightDynamic", bottomRightBuffer);

                        myComputeShader.SetTexture(kid, "depthTexture", depthTexture);
                        
                        Vector3 ncp = myCameras[camIndex].ViewportToWorldPoint(new Vector3(0, 1, myCameras[camIndex].nearClipPlane));
                        myComputeShader.SetVector("ncpTopLeft", new Vector4(ncp.x, ncp.y, ncp.z, 0));
                        ncp = myCameras[camIndex].ViewportToWorldPoint(new Vector3(1, 1, myCameras[camIndex].nearClipPlane));
                        myComputeShader.SetVector("ncpTopRight", new Vector4(ncp.x, ncp.y, ncp.z, 0));
                        ncp = myCameras[camIndex].ViewportToWorldPoint(new Vector3(0, 0, myCameras[camIndex].nearClipPlane));
                        myComputeShader.SetVector("ncpBottomLeft", new Vector4(ncp.x, ncp.y, ncp.z, 0));
                        ncp = myCameras[camIndex].ViewportToWorldPoint(new Vector3(1, 0, myCameras[camIndex].nearClipPlane));
                        myComputeShader.SetVector("ncpBottomRight", new Vector4(ncp.x, ncp.y, ncp.z, 0));
                        
                        myComputeShader.Dispatch(kid, Mathf.CeilToInt(depthTexture.width/8.0f), Mathf.CeilToInt(depthTexture.height/8.0f), 1);

                        float[] resultArr = new float[ocResultBuffer.count];
                        ocResultBuffer.GetData(resultArr);

                        if(enableGPUInstancing)
                        {
                            GPUInstancingDynamic(camIndex, resultArr, meshIndexArr);
                        }
                        else
                        {
                            NoGPUInstancingDynamic (resultArr, meshIndexArr);
                        }
                    }
                    StartCoroutine(Release(depthTexture));
                    topLeftBuffer.Release();
                    bottomLeftBuffer.Release();
                    topRightBuffer.Release();
                    bottomRightBuffer.Release();

                    bbBuffer.Release();
                    meshIndexBuffer.Release();
                    fcResultBuffer.Release();
                    lodTransitionBuffer.Release();
                    lodFirstThresholdBuffer.Release();
                    if(ocResultBuffer != null)
                        ocResultBuffer.Release();
                }
            }
        }

        List<List<List<MyInstanceData>>> instDataStatic = new List<List<List<MyInstanceData>>>();
        List<List<List<MyInstanceData>>> instDataStaticOnlyShadow = new List<List<List<MyInstanceData>>>();
        List<List<List<MyInstanceData>>> instDataDynamic = new List<List<List<MyInstanceData>>>();
        List<List<List<MyInstanceData>>> instDataDynamicOnlyShadow = new List<List<List<MyInstanceData>>>();

        struct GPUInstancingDataStruct
        {
            public int camIndex;
            public float[] resultArr;
            public int[] meshIndexArr;
            public int parentIndex;

            public GPUInstancingDataStruct(int camIndex, float[] resultArr, int[] meshIndexArr, int parentIndex)
            {
                this.camIndex = camIndex;
                this.resultArr = resultArr;
                this.meshIndexArr = meshIndexArr;
                this.parentIndex = parentIndex;
            }
        };

        void GPUInstancing (int camIndex, float[] resultArr, int[] meshIndexArr)
        {
            Thread t = new Thread (GPUInstancingSorting);
            t.Start(new GPUInstancingDataStruct(camIndex, resultArr, meshIndexArr, 0));

            for (int i = 0; i < parentSculpts.Count; i++)
            {
                //Debug.Log(i.ToString() + ": " + childSculpts[i].Count.ToString());
                mySDFShader = parentSculpts[i].gameObject.GetComponent<Renderer>().material;
                mySDFShader.SetFloat("_SculptSceneManagerGPUInstancing", 1);
                //farAwaySDFFuzzyShader.SetFloat("_SculptSceneManagerGPUInstancing", 1);
                RenderParams rp;
                //if(parentSculpts[i].isFuzzy)
                //    rp = new RenderParams(myFuzzySDFShader);
                //else
                    rp = new RenderParams(mySDFShader);//parentSculpts[i].GetComponent<Renderer>().material);
                rp.camera = myCameras[camIndex];
                rp.shadowCastingMode = ShadowCastingMode.On;
                rp.matProps = new MaterialPropertyBlock();
                RenderParams rpShadowOnly = rp;
                rpShadowOnly.shadowCastingMode = ShadowCastingMode.ShadowsOnly;

                List<List<MyInstanceData>> instData = new List<List<MyInstanceData>>();
                List<List<MyInstanceData>> instDataOnlyShadow = new List<List<MyInstanceData>>();
                
                /*MyInstanceData newInstData;
                //ComputeBuffer cols;
                for (int j = 0; j < parentSculpts[i].meshes.Count; j++)
                {
                    instData.Add(new List<MyInstanceData>());
                    instDataOnlyShadow.Add(new List<MyInstanceData>());
                }
                for (int j = 0; j < childSculpts[i].Count; j++)
                {
                    newInstData = new MyInstanceData();
                    newInstData.tint = staticSculpts[childSculpts[i][j]].tint;
                    newInstData.tintAmount = staticSculpts[childSculpts[i][j]].tintAmount;
                    newInstData.metallic = staticSculpts[childSculpts[i][j]].metallic;
                    newInstData.smoothness = staticSculpts[childSculpts[i][j]].smoothness;
                    newInstData.emission = staticSculpts[childSculpts[i][j]].emission;
                    if(staticSculpts[childSculpts[i][j]] == editedSculpt)
                        newInstData.objectToWorld = Matrix4x4.TRS(Vector3.one * 99999, Quaternion.identity, Vector3.zero);
                    else
                        newInstData.objectToWorld = Matrix4x4.TRS(staticSculpts[childSculpts[i][j]].transform.position, staticSculpts[childSculpts[i][j]].transform.transform.rotation, staticSculpts[childSculpts[i][j]].transform.lossyScale);//staticSculpts[childSculpts[i][j]].transform.localToWorldMatrix;

                    if(resultArr[childSculpts[i][j]] > 0 || staticSculpts[childSculpts[i][j]].cullType != SDFSculpt.CullType.Disappear)
                    {
                        //if(staticSculpts[childSculpts[j][i]].cullType == SDFSculpt.CullType.LowestLOD ||
                        //staticSculpts[childSculpts[j][i]].cullType == SDFSculpt.CullType.OnlyShadowAndLowestLOD)
                        //{
                        //    if(resultArr[childSculpts[j][i]] < 1 && instDataOnlyShadow[parentSculpts[i].meshes.Count-1].Count < 1022)
                        //    {
                        //        instDataOnlyShadow[parentSculpts[i].meshes.Count-1].Add(staticSculpts[childSculpts[j][i]].transform.localToWorldMatrix);
                        //    }
                        //    else if(instData[parentSculpts[i].meshes.Count-1].Count < 1022)
                        //    {
                        //        instData[parentSculpts[i].meshes.Count-1].Add(staticSculpts[childSculpts[j][i]].transform.localToWorldMatrix);
                        //    }
                        //}
                        //else
                        //{
                        //    for (int k = 0; k < parentSculpts[i].meshes.Count; k++)
                        //    {
                        //        if(resultArr[childSculpts[j][i]] < 1 && instDataOnlyShadow[k].Count < 1022)
                        //        {
                        //            instDataOnlyShadow[k].Add(staticSculpts[childSculpts[j][i]].transform.localToWorldMatrix);
                        //        }
                        //        else if(meshIndexArr[childSculpts[j][i]] == k && instData[k].Count < 1022)
                        //        {
                        //            instData[k].Add(staticSculpts[childSculpts[j][i]].transform.localToWorldMatrix);
                        //            break;
                        //        }
                        //    }
                        //}
                        if(resultArr[childSculpts[i][j]] > 0)
                        {
                            for (int k = 0; k < parentSculpts[i].meshes.Count; k++)
                            {
                                if(meshIndexArr[childSculpts[i][j]] == k && instData[k].Count < 1022)
                                {
                                    instData[k].Add(newInstData);
                                    break;
                                }
                            }
                        }
                        else if(staticSculpts[childSculpts[i][j]].cullType == SDFSculpt.CullType.LowestLOD)
                        {
                            if(instData[parentSculpts[i].meshes.Count-1].Count < 1022)
                            {
                                instData[parentSculpts[i].meshes.Count-1].Add(newInstData);
                            }
                        }
                        else if(staticSculpts[childSculpts[i][j]].cullType == SDFSculpt.CullType.OnlyShadowAndLowestLOD)
                        {
                            if(instDataOnlyShadow[parentSculpts[i].meshes.Count-1].Count < 1022)
                            {
                                instDataOnlyShadow[parentSculpts[i].meshes.Count-1].Add(newInstData);
                            }
                        }
                        else if(staticSculpts[childSculpts[i][j]].cullType == SDFSculpt.CullType.OnlyShadow)
                        {
                            for (int k = 0; k < parentSculpts[i].meshes.Count; k++)
                            {
                                if(meshIndexArr[childSculpts[i][j]] == k && instDataOnlyShadow[k].Count < 1022)
                                {
                                    instDataOnlyShadow[k].Add(newInstData);
                                    break;
                                }
                            }
                        }
                    }
                }*/

                if(instDataStatic.Count > i)
                    instData = instDataStatic[i];
                if(instDataStaticOnlyShadow.Count > i)
                    instDataOnlyShadow = instDataStaticOnlyShadow[i];

                for (int j = 0; j < parentSculpts[i].meshes.Count; j++)
                {

                    int matPropBufferSize = 0;
                    
                    if(instDataOnlyShadow.Count > 0)
                    {
                        if(instDataOnlyShadow[j].Count > 0)
                            Graphics.RenderMeshInstanced(rpShadowOnly, parentSculpts[i].meshes[j], 0, instDataOnlyShadow[j]);
                    }
                    if(instData.Count > 0)
                    {
                    if(instData[j].Count > 0)
                    {
                        int kid = myComputeShader.FindKernel("SetMaterialProperties");

                        matPropBufferSize = instData[j].Count;
                        matPropBuffer = new ComputeBuffer(matPropBufferSize, sizeof(float) * 4 * 7);
                        matPropBuffer.SetData(instData[j]);
                        myComputeShader.SetBuffer(kid, "instanceData", matPropBuffer);

                        myComputeShader.SetTexture(kid, "tint", matTint[camIndex][i][j]);
                        myComputeShader.SetTexture(kid, "props", matProps[camIndex][i][j]);
                        myComputeShader.SetTexture(kid, "props2", matProps2[camIndex][i][j]);
                        myComputeShader.SetInt("propCount", matPropBufferSize);

                        myComputeShader.Dispatch(kid, 4, 4, 1);

                        //Debug.Log(matTint[i].Count);
                        rp.matProps.SetTexture("_MatTint", matTint[camIndex][i][j]);
                        rp.matProps.SetTexture("_MatProps", matProps[camIndex][i][j]);
                        rp.matProps.SetTexture("_MatProps2", matProps2[camIndex][i][j]);
                        //if(i == 0 && j == 1)
                        //    test2.mainTexture = matTint[i][j];
                        //rp.matProps.SetFloat("_TintAmount", 1);
                        //if(j >= parentSculpts[i].meshes.Count - 1)
                        //    rp.material = farAwaySDFFuzzyShader;
                        Graphics.RenderMeshInstanced(rp, parentSculpts[i].meshes[j], 0, instData[j]);
                        if(matPropBuffer != null)
                            matPropBuffer.Release();
                    }
                    }
                    if(debugStaticGPUInstanceCounts)
                    {
                        Debug.Log("\"" + parentSculpts[i].myName + " LOD" + j.ToString() +
                            "\" GPU Instance Count: " + matPropBufferSize.ToString());
                    }
                }
            }
                //matTint.Release();
                //matProps.Release();
        }

        void GPUInstancingSorting (object data)
        {
            int camIndex = ((GPUInstancingDataStruct)(data)).camIndex;
            float[] resultArr = ((GPUInstancingDataStruct)(data)).resultArr;
            int[] meshIndexArr = ((GPUInstancingDataStruct)(data)).meshIndexArr;
            int nothing = ((GPUInstancingDataStruct)(data)).parentIndex;

            List<List<List<MyInstanceData>>> instData = new List<List<List<MyInstanceData>>>();
            List<List<List<MyInstanceData>>> instDataOnlyShadow = new List<List<List<MyInstanceData>>>();

            MyInstanceData newInstData;
            //ComputeBuffer cols;
            for (int i = 0; i < parentSculpts.Count; i++)
            {
                instData.Add(new List<List<MyInstanceData>>());
                instDataOnlyShadow.Add(new List<List<MyInstanceData>>());

                for (int j = 0; j < parentSculpts[i].meshes.Count; j++)
                {
                    instData[i].Add(new List<MyInstanceData>());
                    instDataOnlyShadow[i].Add(new List<MyInstanceData>());
                }
                for (int j = 0; j < childSculpts[i].Count; j++)
                {
                    newInstData = new MyInstanceData();
                    newInstData.tint = staticSculpts[childSculpts[i][j]].tint;
                    newInstData.tintAmount = staticSculpts[childSculpts[i][j]].tintAmount;
                    newInstData.metallic = staticSculpts[childSculpts[i][j]].metallic;
                    newInstData.smoothness = staticSculpts[childSculpts[i][j]].smoothness;
                    newInstData.emission = staticSculpts[childSculpts[i][j]].emission;
                    if(staticSculpts[childSculpts[i][j]] == editedSculpt)
                        newInstData.objectToWorld = Matrix4x4.TRS(Vector3.one * 99999, Quaternion.identity, Vector3.zero);
                    else
                        newInstData.objectToWorld = staticSculpts[childSculpts[i][j]].myTransform;//Matrix4x4.TRS(staticSculpts[childSculpts[i][j]].transform.position, staticSculpts[childSculpts[i][j]].transform.transform.rotation, staticSculpts[childSculpts[i][j]].transform.lossyScale);//staticSculpts[childSculpts[i][j]].transform.localToWorldMatrix;

                    if(resultArr[childSculpts[i][j]] > 0 || staticSculpts[childSculpts[i][j]].cullType != SDFSculpt.CullType.Disappear)
                    {
                        if(resultArr[childSculpts[i][j]] > 0)
                        {
                            for (int k = 0; k < parentSculpts[i].meshes.Count; k++)
                            {
                                if(meshIndexArr[childSculpts[i][j]] == k && instData[i][k].Count < 1022)
                                {
                                    instData[i][k].Add(newInstData);
                                    break;
                                }
                            }
                        }
                        else if(staticSculpts[childSculpts[i][j]].cullType == SDFSculpt.CullType.LowestLOD)
                        {
                            if(instData[i][parentSculpts[i].meshes.Count-1].Count < 1022)
                            {
                                instData[i][parentSculpts[i].meshes.Count-1].Add(newInstData);
                            }
                        }
                        else if(staticSculpts[childSculpts[i][j]].cullType == SDFSculpt.CullType.OnlyShadowAndLowestLOD)
                        {
                            if(instDataOnlyShadow[i][parentSculpts[i].meshes.Count-1].Count < 1022)
                            {
                                instDataOnlyShadow[i][parentSculpts[i].meshes.Count-1].Add(newInstData);
                            }
                        }
                        else if(staticSculpts[childSculpts[i][j]].cullType == SDFSculpt.CullType.OnlyShadow)
                        {
                            for (int k = 0; k < parentSculpts[i].meshes.Count; k++)
                            {
                                if(meshIndexArr[childSculpts[i][j]] == k && instDataOnlyShadow[i][k].Count < 1022)
                                {
                                    instDataOnlyShadow[i][k].Add(newInstData);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            instDataStatic = instData;
            instDataStaticOnlyShadow = instDataOnlyShadow;
        }

        void GPUInstancingDynamicSorting (object data)
        {
            int camIndex = ((GPUInstancingDataStruct)(data)).camIndex;
            float[] resultArr = ((GPUInstancingDataStruct)(data)).resultArr;
            int[] meshIndexArr = ((GPUInstancingDataStruct)(data)).meshIndexArr;
            int nothing = ((GPUInstancingDataStruct)(data)).parentIndex;

            List<List<List<MyInstanceData>>> instData = new List<List<List<MyInstanceData>>>();
            List<List<List<MyInstanceData>>> instDataOnlyShadow = new List<List<List<MyInstanceData>>>();

            MyInstanceData newInstData;
            for (int i = 0; i < parentSculpts.Count; i++)
            {
                instData.Add(new List<List<MyInstanceData>>());
                instDataOnlyShadow.Add(new List<List<MyInstanceData>>());
            for (int j = 0; j < parentSculpts[i].meshes.Count; j++)
            {
                instData[i].Add(new List<MyInstanceData>());
                instDataOnlyShadow[i].Add(new List<MyInstanceData>());
            }
            for (int j = 0; j < dynamicChildSculpts[i].Count; j++)
            {
                newInstData = new MyInstanceData();
                newInstData.tint = dynamicSculpts[dynamicChildSculpts[i][j]].tint;
                newInstData.tintAmount = dynamicSculpts[dynamicChildSculpts[i][j]].tintAmount;
                newInstData.metallic = dynamicSculpts[dynamicChildSculpts[i][j]].metallic;
                newInstData.smoothness = dynamicSculpts[dynamicChildSculpts[i][j]].smoothness;
                newInstData.emission = dynamicSculpts[dynamicChildSculpts[i][j]].emission;
                if(dynamicSculpts[dynamicChildSculpts[i][j]] == editedSculpt)
                    newInstData.objectToWorld = Matrix4x4.TRS(Vector3.one * 99999, Quaternion.identity, Vector3.zero);
                else
                    newInstData.objectToWorld = dynamicSculpts[dynamicChildSculpts[i][j]].myTransform;//newInstData.objectToWorld = Matrix4x4.TRS(dynamicSculpts[dynamicChildSculpts[i][j]].transform.position, dynamicSculpts[dynamicChildSculpts[i][j]].transform.transform.rotation, dynamicSculpts[dynamicChildSculpts[i][j]].transform.lossyScale);//dynamicSculpts[dynamicChildSculpts[i][j]].transform.localToWorldMatrix;

                if(resultArr[dynamicChildSculpts[i][j]] > 0 || dynamicSculpts[dynamicChildSculpts[i][j]].cullType != SDFSculpt.CullType.Disappear)
                {
                    if(resultArr[dynamicChildSculpts[i][j]] > 0)
                    {
                        for (int k = 0; k < parentSculpts[i].meshes.Count; k++)
                        {
                            if(meshIndexArr[dynamicChildSculpts[i][j]] == k && instData[i][k].Count < 1022)
                            {
                                instData[i][k].Add(newInstData);
                                break;
                            }
                        }
                    }
                    else if(dynamicSculpts[dynamicChildSculpts[i][j]].cullType == SDFSculpt.CullType.LowestLOD)
                    {
                        if(instData[i][parentSculpts[i].meshes.Count-1].Count < 1022)
                        {
                            instData[i][parentSculpts[i].meshes.Count-1].Add(newInstData);
                        }
                    }
                    else if(dynamicSculpts[dynamicChildSculpts[i][j]].cullType == SDFSculpt.CullType.OnlyShadowAndLowestLOD)
                    {
                        if(instDataOnlyShadow[i][parentSculpts[i].meshes.Count-1].Count < 1022)
                        {
                            instDataOnlyShadow[i][parentSculpts[i].meshes.Count-1].Add(newInstData);
                        }
                    }
                    else if(dynamicSculpts[dynamicChildSculpts[i][j]].cullType == SDFSculpt.CullType.OnlyShadow)
                    {
                        for (int k = 0; k < parentSculpts[i].meshes.Count; k++)
                        {
                            if(meshIndexArr[dynamicChildSculpts[i][j]] == k && instDataOnlyShadow[i][k].Count < 1022)
                            {
                                instDataOnlyShadow[i][k].Add(newInstData);
                                break;
                            }
                        }
                    }
                }
            }
            }
            instDataDynamic = instData;
            instDataDynamicOnlyShadow = instDataOnlyShadow;
        }

        void GPUInstancingDynamic (int camIndex, float[] resultArr, int[] meshIndexArr)
        {
            Thread t = new Thread (GPUInstancingDynamicSorting);
            t.Start(new GPUInstancingDataStruct(camIndex, resultArr, meshIndexArr, 0));

            for (int i = 0; i < parentSculpts.Count; i++)
            {
                mySDFShader = parentSculpts[i].gameObject.GetComponent<Renderer>().material;
                mySDFShader.SetFloat("_SculptSceneManagerGPUInstancing", 1);
                RenderParams rp;
                //if(parentSculpts[i].isFuzzy)
                //    rp = new RenderParams(myFuzzySDFShader);
                //else
                    rp = new RenderParams(mySDFShader);//parentSculpts[i].GetComponent<Renderer>().material);
                rp.camera = myCameras[camIndex];
                rp.shadowCastingMode = ShadowCastingMode.On;
                rp.matProps = new MaterialPropertyBlock();
                RenderParams rpShadowOnly = rp;
                rpShadowOnly.shadowCastingMode = ShadowCastingMode.ShadowsOnly;

                List<List<MyInstanceData>> instData = new List<List<MyInstanceData>>();
                List<List<MyInstanceData>> instDataOnlyShadow = new List<List<MyInstanceData>>();
                /*MyInstanceData newInstData;
                //ComputeBuffer cols;
                for (int j = 0; j < parentSculpts[i].meshes.Count; j++)
                {
                    instData.Add(new List<MyInstanceData>());
                    instDataOnlyShadow.Add(new List<MyInstanceData>());
                }
                for (int j = 0; j < dynamicChildSculpts[i].Count; j++)
                {
                    newInstData = new MyInstanceData();
                    newInstData.tint = dynamicSculpts[dynamicChildSculpts[i][j]].tint;
                    newInstData.tintAmount = dynamicSculpts[dynamicChildSculpts[i][j]].tintAmount;
                    newInstData.metallic = dynamicSculpts[dynamicChildSculpts[i][j]].metallic;
                    newInstData.smoothness = dynamicSculpts[dynamicChildSculpts[i][j]].smoothness;
                    newInstData.emission = dynamicSculpts[dynamicChildSculpts[i][j]].emission;
                    if(dynamicSculpts[dynamicChildSculpts[i][j]] == editedSculpt)
                        newInstData.objectToWorld = Matrix4x4.TRS(Vector3.one * 99999, Quaternion.identity, Vector3.zero);
                    else
                        newInstData.objectToWorld = newInstData.objectToWorld = Matrix4x4.TRS(dynamicSculpts[dynamicChildSculpts[i][j]].transform.position, dynamicSculpts[dynamicChildSculpts[i][j]].transform.transform.rotation, dynamicSculpts[dynamicChildSculpts[i][j]].transform.lossyScale);//dynamicSculpts[dynamicChildSculpts[i][j]].transform.localToWorldMatrix;

                    if(resultArr[dynamicChildSculpts[i][j]] > 0 || dynamicSculpts[dynamicChildSculpts[i][j]].cullType != SDFSculpt.CullType.Disappear)
                    {
                        if(resultArr[dynamicChildSculpts[i][j]] > 0)
                        {
                            for (int k = 0; k < parentSculpts[i].meshes.Count; k++)
                            {
                                if(meshIndexArr[dynamicChildSculpts[i][j]] == k && instData[k].Count < 1022)
                                {
                                    instData[k].Add(newInstData);
                                    break;
                                }
                            }
                        }
                        else if(dynamicSculpts[dynamicChildSculpts[i][j]].cullType == SDFSculpt.CullType.LowestLOD)
                        {
                            if(instData[parentSculpts[i].meshes.Count-1].Count < 1022)
                            {
                                instData[parentSculpts[i].meshes.Count-1].Add(newInstData);
                            }
                        }
                        else if(dynamicSculpts[dynamicChildSculpts[i][j]].cullType == SDFSculpt.CullType.OnlyShadowAndLowestLOD)
                        {
                            if(instDataOnlyShadow[parentSculpts[i].meshes.Count-1].Count < 1022)
                            {
                                instDataOnlyShadow[parentSculpts[i].meshes.Count-1].Add(newInstData);
                            }
                        }
                        else if(dynamicSculpts[dynamicChildSculpts[i][j]].cullType == SDFSculpt.CullType.OnlyShadow)
                        {
                            for (int k = 0; k < parentSculpts[i].meshes.Count; k++)
                            {
                                if(meshIndexArr[dynamicChildSculpts[i][j]] == k && instDataOnlyShadow[k].Count < 1022)
                                {
                                    instDataOnlyShadow[k].Add(newInstData);
                                    break;
                                }
                            }
                        }
                    }
                }*/
                
                if(instDataDynamic.Count > i)
                    instData = instDataDynamic[i];
                if(instDataDynamicOnlyShadow.Count > i)
                    instDataOnlyShadow = instDataDynamicOnlyShadow[i];

                for (int j = 0; j < parentSculpts[i].meshes.Count; j++)
                {

                    int matPropBufferSize = 0;
                    if(instDataOnlyShadow.Count > j)
                    {
                        if(instDataOnlyShadow[j].Count > 0)
                            Graphics.RenderMeshInstanced(rpShadowOnly, parentSculpts[i].meshes[j], 0, instDataOnlyShadow[j]);
                    }

                    if(instData.Count > j)
                    {
                    if(instData[j].Count > 0)
                    {
                        int kid = myComputeShader.FindKernel("SetMaterialProperties");

                        matPropBufferSize = instData[j].Count;
                        matPropBuffer = new ComputeBuffer(matPropBufferSize, sizeof(float) * 4 * 7);
                        matPropBuffer.SetData(instData[j]);
                        myComputeShader.SetBuffer(kid, "instanceData", matPropBuffer);

                        myComputeShader.SetTexture(kid, "tint", dynamicMatTint[camIndex][i][j]);
                        myComputeShader.SetTexture(kid, "props", dynamicMatProps[camIndex][i][j]);
                        myComputeShader.SetTexture(kid, "props2", dynamicMatProps2[camIndex][i][j]);
                        myComputeShader.SetInt("propCount", matPropBufferSize);

                        myComputeShader.Dispatch(kid, 4, 4, 1);

                        //Debug.Log(matTint[i].Count);
                        rp.matProps.SetTexture("_MatTint", dynamicMatTint[camIndex][i][j]);
                        rp.matProps.SetTexture("_MatProps", dynamicMatProps[camIndex][i][j]);
                        rp.matProps.SetTexture("_MatProps2", dynamicMatProps2[camIndex][i][j]);
                        //if(i == 0 && j == 1)
                        //    test2.mainTexture = matTint[i][j];
                        //rp.matProps.SetFloat("_TintAmount", 1);
                        Graphics.RenderMeshInstanced(rp, parentSculpts[i].meshes[j], 0, instData[j]);
                        if(matPropBuffer != null)
                            matPropBuffer.Release();
                    }
                    }
                    if(debugDynamicGPUInstanceCounts)
                    {
                        Debug.Log("\"" + parentSculpts[i].myName + " LOD" + j.ToString() +
                            "\" GPU Instance Count: " + matPropBufferSize.ToString());
                    }
                }
            }
                //matTint.Release();
                //matProps.Release();
        }

        void NoGPUInstancing (float[] resultArr, int[] meshIndexArr)
        {
            for (int i = 0; i < staticSculptRenderers.Count; i++)
            {
                mySDFShader = staticSculptRenderers[i].material;
                mySDFShader.SetFloat("_SculptSceneManagerGPUInstancing", 0);
                //Debug.Log(i.ToString() + " sculpt -> " + resultArr[i].ToString());
                if(staticSculpts[i] == editedSculpt)
                {
                    staticSculptRenderers[i].enabled = false;
                    continue;
                }

                if(resultArr[i] > 0)
                {
                    staticSculptRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    staticSculptRenderers[i].enabled = true;
                    staticSculpts[i].meshFilter.mesh = staticSculpts[i].meshes[(int)Mathf.Min(meshIndexArr[i], staticSculpts[i].meshes.Count-1)];
                }
                else
                {
                    if(staticSculpts[i].cullType == SDFSculpt.CullType.Disappear)
                    {
                        staticSculptRenderers[i].enabled = false;
                    }
                    if(staticSculpts[i].cullType == SDFSculpt.CullType.LowestLOD)
                    {
                        staticSculptRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                        staticSculptRenderers[i].enabled = true;
                        staticSculpts[i].meshFilter.mesh = staticSculpts[i].meshes[staticSculpts[i].meshes.Count-1];
                    }
                    if(staticSculpts[i].cullType == SDFSculpt.CullType.OnlyShadow)
                    {
                        staticSculptRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                        staticSculptRenderers[i].enabled = true;
                        staticSculpts[i].meshFilter.mesh = staticSculpts[i].meshes[(int)Mathf.Min(meshIndexArr[i], staticSculpts[i].meshes.Count-1)];
                    }
                    if(staticSculpts[i].cullType == SDFSculpt.CullType.OnlyShadowAndLowestLOD)
                    {
                        staticSculptRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                        staticSculptRenderers[i].enabled = true;
                        staticSculpts[i].meshFilter.mesh = staticSculpts[i].meshes[staticSculpts[i].meshes.Count-1];
                    }
                }

                /*if(loadLOD)
                {
                    float interval = 0.6f;
                    int meshIndex = 0;
                    for (int j = 0; j < staticSculpts[i].meshes.Count; j++)
                    {
                        if(screenCoverageArr[i] < interval){
                            interval /= 25;
                            meshIndex += 1;
                        }
                        else
                            break;
                    }
                    //Debug.Log("Mesh:  " + meshIndex);
                    staticSculpts[i].meshFilter.mesh = staticSculpts[i].meshes[meshIndex];
                }*/
            }
        }

        void NoGPUInstancingDynamic (float[] resultArr, int[] meshIndexArr)
        {
            for (int i = 0; i < dynamicSculptRenderers.Count; i++)
            {
                mySDFShader = dynamicSculptRenderers[i].material;
                mySDFShader.SetFloat("_SculptSceneManagerGPUInstancing", 0);
                //Debug.Log(i.ToString() + " sculpt -> " + resultArr[i].ToString());
                if(dynamicSculpts[i] == editedSculpt)
                {
                    dynamicSculptRenderers[i].enabled = false;
                    continue;
                }

                if(resultArr[i] > 0)
                {
                    dynamicSculptRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    dynamicSculptRenderers[i].enabled = true;
                    dynamicSculpts[i].meshFilter.mesh = dynamicSculpts[i].meshes[(int)Mathf.Min(meshIndexArr[i], dynamicSculpts[i].meshes.Count-1)];
                }
                else
                {
                    if(dynamicSculpts[i].cullType == SDFSculpt.CullType.Disappear)
                    {
                        dynamicSculptRenderers[i].enabled = false;
                    }
                    if(dynamicSculpts[i].cullType == SDFSculpt.CullType.LowestLOD)
                    {
                        dynamicSculptRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                        dynamicSculptRenderers[i].enabled = true;
                        dynamicSculpts[i].meshFilter.mesh = dynamicSculpts[i].meshes[dynamicSculpts[i].meshes.Count-1];
                    }
                    if(dynamicSculpts[i].cullType == SDFSculpt.CullType.OnlyShadow)
                    {
                        dynamicSculptRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                        dynamicSculptRenderers[i].enabled = true;
                        dynamicSculpts[i].meshFilter.mesh = dynamicSculpts[i].meshes[(int)Mathf.Min(meshIndexArr[i], dynamicSculpts[i].meshes.Count-1)];
                    }
                    if(dynamicSculpts[i].cullType == SDFSculpt.CullType.OnlyShadowAndLowestLOD)
                    {
                        dynamicSculptRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                        dynamicSculptRenderers[i].enabled = true;
                        dynamicSculpts[i].meshFilter.mesh = dynamicSculpts[i].meshes[dynamicSculpts[i].meshes.Count-1];
                    }
                }
            }
        }

        // Unity calls the methods in this delegate's invocation list before rendering any camera
        void OnPostRenderCallback(Camera cam)
        {
            RenderTexture depthTexture = new RenderTexture(cam.scaledPixelWidth, cam.pixelHeight, 16, RenderTextureFormat.RFloat);
            depthTexture.enableRandomWrite = true;
            depthTexture.Create();
            Graphics.Blit(Shader.GetGlobalTexture("_CameraDepthTexture"), depthTexture);
            if(myCameras.Contains(cam))
            {
                if(camerasToTrack.Contains(cam))
                {
                    trackedDepthTextures[camerasToTrack.IndexOf(cam)] = depthTexture;
                }
                else
                {
                    camerasToTrack.Add(cam);
                    trackedDepthTextures.Add(depthTexture);
                }
            }
        }

        void OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            //Texture t = Shader.GetGlobalTexture("_CameraDepthTexture");
            RenderTexture depthTexture = new RenderTexture(cam.scaledPixelWidth, cam.pixelHeight, 16, RenderTextureFormat.RFloat);
            depthTexture.enableRandomWrite = true;
            depthTexture.Create();
            Graphics.Blit(Shader.GetGlobalTexture("_CameraDepthTexture"), depthTexture);

            if(myCameras.Contains(cam))
            {
                if(camerasToTrack.Contains(cam))
                {
                    trackedDepthTextures[camerasToTrack.IndexOf(cam)] = depthTexture;
                }
                else
                {
                    camerasToTrack.Add(cam);
                    trackedDepthTextures.Add(depthTexture);
                }
            }
        }

        void OnDestroy()
        {
            Camera.onPostRender -= OnPostRenderCallback;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        }

        IEnumerator Release (RenderTexture rt)
        {
            yield return new WaitForEndOfFrame();
            rt.Release();
        }
    }
}