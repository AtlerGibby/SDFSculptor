using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

using System;
using System.Threading;
//using Unity.Mathematics;
using System.IO;


namespace SDFSculptor
{
    public class MeshGenerator : MonoBehaviour
    {
        [Tooltip("The texture used when texture warp is enabled on an SDF Brush.")]
        public Texture noise;
        [Tooltip("The SDF Compute Shader that is responsible for defining a Signed Distance Field and building a mesh from it.")]
        public ComputeShader myComputeShader;
        [Tooltip("The material used to preview how a sculpt will look like when editing a sculpt.")]
        public Material previewMaterial;
        [Tooltip("The material used when previewing how a sculpt would look like if it was fuzzy.")]
        public Material previewFuzzyMaterial;
        [HideInInspector]
        public SDFSculpt mySculpt;
        [Tooltip("The default mesh to assign to a sculpt when there is no sculpt information.")]
        public Mesh defaultMesh;
        [HideInInspector]
        public List<SDFBrush> myBrushes = new List<SDFBrush>();
        [Tooltip("Smooth the normals of the mesh. Will get a blocky result if disabled (more performant if disabled since we skip dual-contouring).")]
        public bool shareVerticies;
        //public bool automaticallyAddChildBrushes;

        //[Tooltip("The player controller editing the sculpt.")]
        SDFSculptorController SDFPlayer;
        GameObject myCam;

        [Header("Optimizations")]
        [Tooltip("Only render geometry that faces the player's camera.")]
        public bool onlyRenderIfFacingCamera;
        [Tooltip("Only do a single smoothing pass on the mesh. Will get a slightly blocky result around the blended regions of the mesh.")]
        public bool lowQualityDualContouring;
        [Tooltip("Calculate vertex colors.")]
        public bool renderColors;

        [Tooltip("Trigger the \"OnlyRenderIfFacingCamera\" optimization when this face count limit is surpassed. May want to reduce sculpt complexity at this point.")]
        public int faceCountToTiggerOptimization = 1000000;
        [Tooltip("Stop rendering faces if this limit is surpassed. When a sculpt gets too complex it can take a long time to update, so this is the final safe guard.")]
        public int maximumNumberOfFaces = 500000;

        //public bool originAtBottom;
        //public Vector3 origin;
        
        SculptSceneManager ssm;

        List<BoxBrush> boxes = new List<BoxBrush>();
        List<SphereBrush> spheres = new List<SphereBrush>();

        public List<Brush> brushes = new List<Brush>();

        List<int> orderOfOperations = new List<int>();

        GameObject lod1 = null;
        GameObject lod2 = null;

        //int resolution = 128;
        [Header("Error / Hole Fixing")]
        [Tooltip("Expand the area around a brush to look for overlapping brushes (Bigger => Less Holes, Smaller => More Performance).")]
        public float baseMarginOfError;
        [Tooltip("Expand the area around a brush multiplied by the blend of the brush to look for overlapping brushes (Bigger => Less Holes, Smaller => More Performance).")]
        public float blendMarginOfError;
        int meshResolution = 256;
        int resolutionLOD0 = 128;
        int resolutionLOD1 = 64;
        int resolutionLOD2 = 32;
        float scale = 5;


        List<Collider> mySculptsColliders = new List<Collider>();

        //MeshData meshData = new MeshData();
        MeshData meshDataLOD0 = new MeshData();
        MeshData meshDataLOD1 = new MeshData();
        MeshData meshDataLOD2 = new MeshData();

        //Dictionary<Vector3, Voxel> data;
        Dictionary<Vector3, Voxel> dataLOD0;
        Dictionary<Vector3, Voxel> dataLOD1;
        Dictionary<Vector3, Voxel> dataLOD2;

        int[] myData;

        RenderTexture sdf16;
        RenderTexture sdf64;
        RenderTexture sdf256;
        RenderTexture sdfTo3DTex;
        RenderTexture sdfOverlap;
        RenderTexture sdfOverlapFull;

        int kid1;
        int kid2;
        //int kid3;

        int[] kids = new int[8];

        bool updateMesh;
        bool queueUpdate;
        bool errorUpdate;

        float startTimeDelay = 0;
        bool liveUpdate;
        float liveUpdateDelay;
        bool createLODs;
        int lodsCreated;

        SDFSculpt mySculptParent;
        bool foundSculptParent;
        Renderer boundingBox;
        Renderer gridXY;
        Renderer gridXZ;
        Renderer gridYZ;
        [Header("Other")]
        [Tooltip("Get prefabs for brushes from SDF HUD Data.")]
        public SDFHUDData hudData;
        [Tooltip("Show face and voxel information to see how complex the mesh is.")]
        public bool debugMeshData;

        /// <summary>
        /// Check if the Mesh Generator is busy generating a mesh.
        /// </summary>
        public bool IsUpdating ()
        {
            if(!liveUpdate && !updateMesh && !queueUpdate)
                return false;
            else
                return true;
        }


        static readonly Vector3[] voxelVerticies = new Vector3[8]
        {
            new Vector3(0,0,0),
            new Vector3(1,0,0),
            new Vector3(0,1,0),
            new Vector3(1,1,0),

            new Vector3(0,0,1),
            new Vector3(1,0,1),
            new Vector3(0,1,1),
            new Vector3(1,1,1)
        };

        static readonly Vector3[] voxelFaceChecks = new Vector3[6]
        {
            new Vector3(0,0,-1),
            new Vector3(0,0,1),
            new Vector3(-1,0,0),
            new Vector3(1,0,0),
            new Vector3(0,-1,0),
            new Vector3(0,1,0)
        };

        static readonly int[,] voxelVertexIndex = new int[6,4]
        {
            {0,1,2,3},
            {4,5,6,7},
            {4,0,6,2},
            {5,1,7,3},
            {0,1,4,5},
            {2,3,6,7}
        };

        static readonly Vector2[] voxelUVs = new Vector2[4]
        {
            new Vector2(0,0),
            new Vector2(0,1),
            new Vector2(1,0),
            new Vector2(1,1)
        };

        static readonly int[,] voxelTris = new int[6,6]
        {
            {0,2,3,0,3,1},
            {0,1,2,1,3,2},
            {0,2,3,0,3,1},
            {0,1,2,1,3,2},
            {0,1,2,1,3,2},
            {0,2,3,0,3,1}
        };

        public struct Voxel
        {
            public int Id;

            public bool isSolid
            {
                get
                {
                    return Id != 0;
                }
            }
        }

        public struct BoxBrush
        {
            public Matrix4x4 transform;
            public Vector3 scale;
            public Color color;
            public float blend;
            public int brushType;
        }
        public struct SphereBrush
        {
            public Vector3 position;
            public float scale;
            public Color color;
            public float blend;
            public int brushType;
        }


        public struct Brush
        {
            public Matrix4x4 transform;
            public Vector3 position;
            public Vector3 scale;
            public Color color;
            public float roundA;
            public float roundB;
            public float noiseAmount;
            public float noiseStrength;
            public int textureNoise;
            public float blend;
            public Vector3 curveA;
            public Vector3 curveB;
            public Vector3 bounds;
            public int brushType;
            public int brushShape;
        }

        public struct MeshData
        {
            public Mesh mesh;
            public List<Vector3> verticies;
            public List<Vector3> normals;
            public List<int> triangles;
            public List<Vector2> uvs;
            public List<Color> colors;
            public bool initialized;

            public void ClearMesh ()
            {
                if(!initialized)
                {
                    mesh = new Mesh();
                    verticies = new List<Vector3>();
                    triangles = new List<int>();
                    uvs = new List<Vector2>();
                    normals = new List<Vector3>();
                    colors = new List<Color>();
                    initialized = true;
                }
                else
                {
                    mesh.Clear();
                    verticies.Clear();
                    triangles.Clear();
                    uvs.Clear();
                    normals.Clear();
                    colors.Clear();
                }
            }

            public void UploadMesh (bool shareVerticies)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

                mesh.SetVertices(verticies);
                mesh.SetTriangles(triangles, 0 , false);
                mesh.SetUVs(0, uvs);
                mesh.SetColors(colors);

                //mesh.Optimize();

                if(!shareVerticies)
                {
                    mesh.RecalculateNormals();
                    mesh.Optimize();
                }
                else
                {
                    mesh.SetNormals(normals);
                    mesh.Optimize();
                }
                mesh.RecalculateBounds();
                mesh.UploadMeshData(false);
            }
        }

        //void ClearData ()
        //{
            //data.Clear();
            //data = new Voxel[resolution * resolution * resolution];
            //for (int x = 0; x < resolution; x++)
            //{
            //    for (int y = 0; y < resolution; y++)
            //    {
            //        for (int z = 0; z < resolution; z++)
            //        {
            //            data[IndexFromCoord(new Vector3(x,y,z))].Id = 0;
            //        }
            //    }
            //}
        //}

        //int IndexFromCoord (Vector3 idx, int resolution)
        //{
        //    return Mathf.RoundToInt(idx.x) + (Mathf.RoundToInt(idx.y) * resolution) + (Mathf.RoundToInt(idx.z) * (resolution * resolution));
        //}

        //public Voxel this[Vector3 index]
        //{
        //    get
        //    {
        //        if(data.ContainsKey(index))
        //            return data[index];
        //        else
        //            return new Voxel() {Id = 0};
        //    }
        //    set
        //    {
        //        if(data.ContainsKey(index))
        //            data[index] = value;
        //        else
        //            data.Add(index, value);
        //    }
        //}


        ComputeBuffer vertexBuffer;
        ComputeBuffer triangleBuffer;
        ComputeBuffer uvBuffer;
        ComputeBuffer normalBuffer;
        ComputeBuffer colorBuffer;
        //ComputeBuffer ooBuffer;
        //ComputeBuffer boxBuffer;
        //ComputeBuffer sphereBuffer;
        ComputeBuffer brushBuffer;
        ComputeBuffer counter;
        ComputeBuffer faceCounter;
        int leftOver;
        int laps;
        int maxFaces;

        void GenerateMesh(MeshData meshData, int[] data, int resolution) //Dictionary<Vector3, Voxel> data
        {
            //Vector3 blockPos;
            //Voxel block = new Voxel() {Id = 1};
            //int block;
            //int counter = 0;
            dispatchCompleted = 0;
            myComputeShader.SetInt("Resolution", meshResolution);

            Vector3[] faceVerticies = new Vector3[4];
            Vector2[] faceUVs = new Vector2[4];

            //int kid = myComputeShader.FindKernel("CSFaceCount");

            //ooBuffer = new ComputeBuffer(orderOfOperations.Count, sizeof(int));
            //ooBuffer.SetData(orderOfOperations);
            //boxBuffer = new ComputeBuffer(boxes.Count, sizeof(float) * 4 * 4 + sizeof(float) * 8 + sizeof(int));
            //boxBuffer.SetData(boxes);
            //sphereBuffer = new ComputeBuffer(spheres.Count, sizeof(float) * 9 + sizeof(int));
            //sphereBuffer.SetData(spheres);
            brushBuffer = new ComputeBuffer(brushes.Count, sizeof(float) * 4 * 4 + sizeof(float) * 24 + sizeof(int) * 3);
            brushBuffer.SetData(brushes);

            //myComputeShader.SetBuffer(kid2, "orderOfOperations", ooBuffer);
            //myComputeShader.SetBuffer(kid2, "boxes", boxBuffer);
            //myComputeShader.SetBuffer(kid2, "spheres", sphereBuffer);
            myComputeShader.SetBuffer(kid2, "edits", brushBuffer);

            counter = new ComputeBuffer(2,4, ComputeBufferType.Counter);
            faceCounter = new ComputeBuffer(1, 4, ComputeBufferType.Counter);
            //ComputeBuffer iterCounter = new ComputeBuffer(1, 4, ComputeBufferType.Counter);
            faceCounter.SetCounterValue(0);
            counter.SetCounterValue(0);
            //iterCounter.SetCounterValue(0);
            counter.SetData(new uint[] { 0, 0});
            faceCounter.SetData(new uint[] {0});
            //iterCounter.SetData(new uint[] {0});
            myComputeShader.SetBuffer(kid2, "faceCounter", faceCounter);

            //myComputeShader.SetTexture(kid, "Result256", sdf256);

            myComputeShader.Dispatch(kid2, meshResolution/8, meshResolution/8, meshResolution/8);

            System.Array faceCountOutputArray = System.Array.CreateInstance(typeof(uint), faceCounter.count);
            faceCounter.GetData(faceCountOutputArray);
            int faceCountOutput = (int)((UInt32)(faceCountOutputArray.GetValue(0)));

            if(faceCountOutput == 0)
            {
                Debug.Log("EMPTY COMPUTE BUFFER: Canceling Mesh Generation. Make sure to have one additive brush inside. [" + hudData.regenerateMesh.ToString() + "] to try again.");
                faceCounter.Release();
                counter.Release();
                brushBuffer.Release();

                queueUpdate = false;
                updateMesh = false;
                errorUpdate = true;
                sdf256.Release();
                sdfOverlap.Release();
                sdfOverlapFull.Release();

                return;
            }
            if(debugMeshData)
                Debug.Log("Faces: " + faceCountOutput.ToString());
            
            leftOver = (faceCountOutput) % 6000;
            laps = (faceCountOutput - leftOver) / 6000;
            if(leftOver > 0)
                laps += 1;
            //faceCountOutput = Mathf.Min(10000, faceCountOutput);
            

            //kid = myComputeShader.FindKernel("CSGenerateMesh");

            //myComputeShader.SetBuffer(kid3, "orderOfOperations", ooBuffer);
            //myComputeShader.SetBuffer(kid3, "boxes", boxBuffer);
            //myComputeShader.SetBuffer(kid3, "spheres", sphereBuffer);

            //myComputeShader.SetBuffer(kid3, "edits", brushBuffer);

            vertexBuffer = new ComputeBuffer(faceCountOutput * 6, sizeof(float) * 3);
            vertexBuffer.SetData(new Vector3[faceCountOutput * 6]);
            triangleBuffer = new ComputeBuffer(faceCountOutput * 6, sizeof(int));
            triangleBuffer.SetData(new int[faceCountOutput * 6]);
            uvBuffer = new ComputeBuffer(faceCountOutput * 6, sizeof(float) * 2);
            uvBuffer.SetData(new Vector2[faceCountOutput * 6]);
            normalBuffer = new ComputeBuffer(faceCountOutput * 6, sizeof(float) * 3);
            normalBuffer.SetData(new Vector3[faceCountOutput * 6]);
            colorBuffer = new ComputeBuffer(faceCountOutput * 6, sizeof(float) * 4);
            colorBuffer.SetData(new Color[faceCountOutput * 6]);


            //ComputeBuffer testCounter = new ComputeBuffer(1, 4, ComputeBufferType.Counter);
            //testCounter.SetData(new uint[] {0});
            //myComputeShader.SetBuffer(kid3, "testCounter", testCounter);

            //myComputeShader.SetBuffer(kid3, "iterCounter", iterCounter);


            /*myComputeShader.SetBuffer(kid3, "counter", counter);
            myComputeShader.SetBuffer(kid3, "vertexBuffer", vertexBuffer);
            myComputeShader.SetBuffer(kid3, "triangleBuffer", triangleBuffer);
            myComputeShader.SetBuffer(kid3, "uvBuffer", uvBuffer);
            myComputeShader.SetBuffer(kid3, "normalBuffer", normalBuffer);
            myComputeShader.SetBuffer(kid3, "colorBuffer", colorBuffer);
            */

            for (int i = 0; i < kids.Length; i++)
            {
                if(i == 0)
                {
                //myComputeShader.SetBuffer(kids[i], "orderOfOperations", ooBuffer);
                //myComputeShader.SetBuffer(kids[i], "boxes", boxBuffer);
                //myComputeShader.SetBuffer(kids[i], "spheres", sphereBuffer);
                myComputeShader.SetBuffer(kids[i], "edits", brushBuffer);
                myComputeShader.SetBuffer(kids[i], "counter", counter);
                myComputeShader.SetBuffer(kids[i], "vertexBuffer", vertexBuffer);
                myComputeShader.SetBuffer(kids[i], "triangleBuffer", triangleBuffer);
                myComputeShader.SetBuffer(kids[i], "uvBuffer", uvBuffer);
                myComputeShader.SetBuffer(kids[i], "normalBuffer", normalBuffer);
                myComputeShader.SetBuffer(kids[i], "colorBuffer", colorBuffer);
                }
            }

            //myComputeShader.SetTexture(kid, "Result256", sdf256);
            maxFaces = faceCountOutput*6;


            for (int i = 0; i < 1; i++)
            {
                //counter.SetCounterValue(0);
                //iterCounter.SetCounterValue(0);
                //myComputeShader.SetFloat("minMeshGenData", 6000 * (i));
                //myComputeShader.SetFloat("maxMeshGenData", (int)Mathf.Min(6000 * (i + 1), faceCountOutput*6));
                
                //myComputeShader.Dispatch(kid3, 32, 32, 32);

                /*faceCountOutputArray = System.Array.CreateInstance(typeof(uint), iterCounter.count);
                iterCounter.GetData(faceCountOutputArray);
                faceCountOutput = (int)((UInt32)(faceCountOutputArray.GetValue(0)));
                Debug.Log("Test COUNT ");
                Debug.Log(faceCountOutput);
                Debug.Log( 6000 * (i));*/
            }

            //var argBuffer = new ComputeBuffer(3, sizeof(uint), ComputeBufferType.IndirectArguments);
            //ComputeBuffer.CopyCount(voxelCounter, argBuffer, 0);

            //System.Array test = System.Array.CreateInstance(typeof(uint), faceCounter.count);
            //faceCounter.GetData(test);
            //for (int i = 0; i < test.Length; i++)
            //{
            //    Debug.Log(test.GetValue(i));
            //}

            /*Vector3[] vertArr = new Vector3[vertexBuffer.count];
            Vector2[] uvArr = new Vector2[vertexBuffer.count];
            int[] triArr = new int[vertexBuffer.count];
            Vector3[] normArr = new Vector3[vertexBuffer.count];
            Color[] colArr = new Color[vertexBuffer.count];
            
            vertexBuffer.GetData(vertArr);
            uvBuffer.GetData(uvArr);
            triangleBuffer.GetData(triArr);
            normalBuffer.GetData(normArr);
            colorBuffer.GetData(colArr);


            meshData.mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            meshData.mesh.SetVertices(vertArr);
            meshData.mesh.SetTriangles(triArr, 0 , false);
            meshData.mesh.SetUVs(0, uvArr);
            meshData.mesh.SetColors(colArr);

            meshData.mesh.SetNormals(normArr);
            //if(!shareVerticies)
            //{
            //    meshData.mesh.RecalculateNormals();
            //    meshData.mesh.Optimize();
            //}
            //else
            //{
            //    meshData.mesh.SetNormals(normArr);
            //    meshData.mesh.Optimize();
            //}
            meshData.mesh.RecalculateBounds();
            //meshData.mesh.RecalculateNormals();
            meshData.mesh.UploadMeshData(false);
            GetComponent<MeshFilter>().mesh = meshData.mesh;*/

            /*AsyncGPUReadback.Request(vertexBuffer, grabVertArr);
            AsyncGPUReadback.Request(uvBuffer, grabUVArr);
            AsyncGPUReadback.Request(triangleBuffer, grabTriArr);
            AsyncGPUReadback.Request(normalBuffer, grabNormArr);
            AsyncGPUReadback.Request(colorBuffer, grabColArr);


            ooBuffer.Release();
            boxBuffer.Release();
            sphereBuffer.Release();

            counter.Release();
            faceCounter.Release();

            vertexBuffer.Release();
            triangleBuffer.Release();
            uvBuffer.Release();
            normalBuffer.Release();
            colorBuffer.Release();*/

            //float x = 0; //float y  = 0; float z = 0;
            //for (int i = 0; i < vertArr.Length; i++)
            //{
                //if(vertArr[i] == Vector3.zero)
                //    x += 1;
                //if(vertArr[i].y > y)
                //    y = vertArr[i].y;
                //if(vertArr[i].z > z)
                //    z = vertArr[i].z;

                //if(vertArr[i].x > 18 || vertArr[i].y > 18 || vertArr[i].z > 18)
                //    Debug.Log(vertArr[i]);
            //}
            //Debug.Log("ZEROs: ");
            //Debug.Log(x);
            //Debug.Log(y);
            //Debug.Log(z);
            

            /*for (int x = 0; x < resolution; x++)
            {
                for (int y = 0; y < resolution; y++)
                {
                    for (int z = 0; z < resolution; z++)
                    {
                        blockPos = new Vector3(x,y,z);

                        block = data[IndexFromCoord(blockPos, resolution)];

                        if(block == 0)
                            continue;

                        for (int i = 0; i < 6; i++)
                        {
                            
                            Vector3 checkPos = blockPos + (voxelFaceChecks[i]);
                            if(checkPos.x >= resolution || checkPos.y >= resolution || checkPos.z >= resolution ||
                            checkPos.x < 0 || checkPos.y < 0 || checkPos.z < 0)
                                continue;

                            if(data[IndexFromCoord(checkPos, resolution)] == 1)
                                continue;

                            for (int j = 0; j < 4; j++)
                            {
                                faceVerticies[j] = voxelVerticies[voxelVertexIndex[i,j]];

                                faceVerticies[j] /= resolution;
                                faceVerticies[j] *= scale;
                                faceVerticies[j] += blockPos / resolution * scale;

                                faceUVs[j] = voxelUVs[j];
                            }
                            for (int j = 0; j < 6; j++)
                            {
                                meshData.verticies.Add(faceVerticies[voxelTris[i,j]]);
                                meshData.uvs.Add(faceUVs[voxelTris[i,j]]);
                                meshData.triangles.Add(counter++);

                                if(shareVerticies)
                                {
                                    meshData.normals.Add(Vector3.zero);
                                    meshData.colors.Add(Color.clear);
                                }
                                else
                                {
                                    meshData.colors.Add(Color.clear);
                                }*/

                                /*Vector3 gradient;
                                int curVert = meshData.verticies.Count - 1;
                                gradient = GetSDFGradient(meshData.verticies[curVert]);
                                meshData.verticies[curVert] -= gradient * GetSDFDistance(meshData.verticies[curVert]);
                                if(shareVerticies)
                                {
                                    meshData.normals.Add(gradient);
                                    meshData.colors.Add(GetSDFColor(meshData.verticies[curVert])); //new Color(gradient.x, gradient.y, gradient.z, 1));
                                }
                                else
                                {
                                    meshData.colors.Add(GetSDFColor(meshData.verticies[curVert]));
                                }*/
                        /* }
                        }
                        //generateMeshProgress += 1;
                    }
                }
            }*/

            /*foreach (KeyValuePair<Vector3, Voxel> kvp in data)
            {
                if(kvp.Value.Id == 0)
                    continue;

                blockPos = kvp.Key;
                block = kvp.Value;

                for (int i = 0; i < 6; i++)
                {
                    if(data.ContainsKey(blockPos + (voxelFaceChecks[i]/resolution * scale)))
                        if(data[blockPos + (voxelFaceChecks[i]/resolution * scale)].isSolid)
                            continue;

                    for (int j = 0; j < 4; j++)
                    {
                        faceVerticies[j] = voxelVerticies[voxelVertexIndex[i,j]];

                        faceVerticies[j] /= resolution;
                        faceVerticies[j] *= scale;
                        faceVerticies[j] += blockPos;

                        faceUVs[j] = voxelUVs[j];
                    }
                    for (int j = 0; j < 6; j++)
                    {
                        meshData.verticies.Add(faceVerticies[voxelTris[i,j]]);
                        meshData.uvs.Add(faceUVs[voxelTris[i,j]]);
                        meshData.triangles.Add(counter++);

                        // Dual Contouring and Normals
                        //meshData.verticies[meshData.verticies.Count - 1] -= 
                        //Vector3.ClampMagnitude(GetSDFGradient(meshData.verticies[meshData.verticies.Count - 1]) * 9999999, 1) *
                        //GetSDFDistance(meshData.verticies[meshData.verticies.Count - 1]); // * 2;// * scale / resolution;
                    }
                }
            }*/

            /*for (int i = 0; i < 512; i++)
            {
                Thread t = new Thread (AddToMeshData);
                t.Start(new AddToMeshDataStruct(resolution, i, meshData));
            }*/

            StartCoroutine(GenMeshLoop());
            startReadingMeshBuffers = 1;

            //Debug.Log("GenMesh DONE");

            //ClearVoxelArray(voxelBuffer);
            //voxelBuffer.Release();
        }

        /*void ReadMeshBuffers()
        {
            // Dispatch the compute shader
            //myComputeShader.Dispatch(kid3, 32, 32, 32);

            // Request the data from the GPU to the CPU
            AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(vertexBuffer);
            AsyncGPUReadbackRequest request1 = AsyncGPUReadback.Request(uvBuffer);
            AsyncGPUReadbackRequest request2 = AsyncGPUReadback.Request(triangleBuffer);
            AsyncGPUReadbackRequest request3 = AsyncGPUReadback.Request(normalBuffer);
            AsyncGPUReadbackRequest request4 = AsyncGPUReadback.Request(colorBuffer);

        // Debug.Log("Waiting");

            // Wait for the request to complete
            while (!request.done || !request1.done || !request2.done || !request3.done || !request4.done)
            {
                return;//yield return null;
            }

            if (request.hasError || request1.hasError || request2.hasError || request3.hasError || request4.hasError)
            {
                Debug.Log("GPU readback error detected.");
            }

            if (request.done && request1.done && request2.done && request3.done && request4.done && startReadingMeshBuffers == 1)
            {
                delygit();
            }
        }*/

        int dispatchCompleted = 0;
        int startReadingMeshBuffers = 0;

        Vector3[] vertArr;
        Vector2[] uvArr;
        int[] triArr;
        Vector3[] normArr;
        Color[] colArr;

        //int vertI;
        //int uvI;
        //int triI;
        //int normI;
        //int colI;

        int kidIter = 0;
        IEnumerator GenMeshLoop ()
        {
            yield return new WaitForEndOfFrame();
            //print(dispatchCompleted);
            //myComputeShader.SetInt("minMeshGenData", 6000 * (dispatchCompleted/5));
            myComputeShader.SetInt("maxMeshGenData", maximumNumberOfFaces);
            myCam = SDFPlayer.transform.GetChild(0).gameObject;
            myComputeShader.SetVector("CamForward", new Vector4(myCam.transform.forward.x, myCam.transform.forward.y, myCam.transform.forward.z, 1));
            myComputeShader.SetFloat("baseMarginOfError", baseMarginOfError);
            myComputeShader.SetFloat("blendMarginOfError", blendMarginOfError);
            if(onlyRenderIfFacingCamera || (maxFaces > faceCountToTiggerOptimization))
            {
                //Debug.Log("TOO MANY");
                myComputeShader.SetInt("onlyFacingCam", 1);
            }
            else
                myComputeShader.SetInt("onlyFacingCam", 0);
            if(lowQualityDualContouring)
                myComputeShader.SetInt("lowQualityDualContouring", 1);
            else
                myComputeShader.SetInt("lowQualityDualContouring", 0);

            if(shareVerticies)
                myComputeShader.SetInt("shareVerticies", 1);
            else
                myComputeShader.SetInt("shareVerticies", 0);

            if(renderColors)
                myComputeShader.SetInt("renderColors", 1);
            else
                myComputeShader.SetInt("renderColors", 0);
            myComputeShader.Dispatch(kids[kidIter], meshResolution/4, meshResolution/4, meshResolution/4);
            


            //counter.SetCounterValue(0);
            /*System.Array tmpArr = System.Array.CreateInstance(typeof(uint), counter.count);
            counter.GetData(tmpArr);
            int faceCountOutput = (int)((UInt32)(tmpArr.GetValue(0)));
            Debug.Log("Faces Before Rendering: ");
            Debug.Log(faceCountOutput);

            for (int i = 0; i < 1; i++)
            {
                //counter.SetCounterValue(0);
                //iterCounter.SetCounterValue(0);
                myComputeShader.SetInt("minMeshGenData", 6000 * (kidIter));
                myComputeShader.SetInt("maxMeshGenData", (int)Mathf.Min(6000 * (kidIter + 1), maxFaces));
            
                myComputeShader.Dispatch(kids[0], 32, 32, 32);
                myComputeShader.Dispatch(myComputeShader.FindKernel("Test"), 1, 1, 1);

                //tmpArr = System.Array.CreateInstance(typeof(uint), counter.count);
                //counter.GetData(tmpArr);
                //faceCountOutput = (int)((UInt32)(tmpArr.GetValue(0)));
                //Debug.Log("Faces: ");
                //Debug.Log(faceCountOutput);
            }*/

            AsyncGPUReadback.Request(vertexBuffer, grabVertArr);
            AsyncGPUReadback.Request(uvBuffer, grabUVArr);
            AsyncGPUReadback.Request(triangleBuffer, grabTriArr);
            AsyncGPUReadback.Request(normalBuffer, grabNormArr);
            AsyncGPUReadback.Request(colorBuffer, grabColArr);
        }

        void grabVertArr (AsyncGPUReadbackRequest request)
        {
            vertArr = new Vector3[vertexBuffer.count];

            vertArr = request.GetData<Vector3>().ToArray();
            //vertI++;
            if(request.done)
                StartCoroutine(checkAllData());
        }
        void grabUVArr (AsyncGPUReadbackRequest request)
        {
            uvArr = new Vector2[vertexBuffer.count];
            uvArr = request.GetData<Vector2>().ToArray();
            if(request.done)
                StartCoroutine(checkAllData());
        }
        void grabTriArr (AsyncGPUReadbackRequest request)
        {
            triArr = new int[vertexBuffer.count];
            triArr = request.GetData<int>().ToArray();
            if(request.done)
                StartCoroutine(checkAllData());
        }
        void grabNormArr (AsyncGPUReadbackRequest request)
        {
            normArr = new Vector3[vertexBuffer.count];
            normArr = request.GetData<Vector3>().ToArray();
            if(request.done)
                StartCoroutine(checkAllData());
        }
        void grabColArr (AsyncGPUReadbackRequest request)
        {
            colArr = new Color[vertexBuffer.count];
            colArr = request.GetData<Color>().ToArray();

            if(request.done)
                StartCoroutine(checkAllData());
        }
        IEnumerator checkAllData()
        {
            yield return new WaitForEndOfFrame();
            dispatchCompleted += 1;
            if(dispatchCompleted == 5)
            {
                dispatchCompleted = 0;
                delygit();
            }
            //if(dispatchCompleted % 5 == 0)
            //{
            //    if(dispatchCompleted/5 >= laps)
            //    {
            //        delygit();
            //    }
            //    else
            //    {
            //        StartCoroutine(GenMeshLoop());
            //    }
            //}
        }

        CombineInstance[] meshes = new CombineInstance[8];
        void delygit ()
        {
            //Debug.Log("dellygit");
            //Debug.Log(kidIter);
            //dispatchCompleted += 1;

            //if(dispatchCompleted > 4)
            //{

                vertexBuffer.GetData(vertArr);
                uvBuffer.GetData(uvArr);
                triangleBuffer.GetData(triArr);
                normalBuffer.GetData(normArr);
                colorBuffer.GetData(colArr);


                Mesh mesh = new Mesh();
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(vertArr);
                mesh.SetTriangles(triArr, 0 , false);
                mesh.SetUVs(0, uvArr);
                mesh.SetColors(colArr);

                if(shareVerticies)
                    mesh.SetNormals(normArr);
                else
                    mesh.RecalculateNormals();
                    
                mesh.RecalculateBounds();
                mesh.UploadMeshData(false);
                //if(kidIter < 8)
                //{
                //    meshes[kidIter].mesh = mesh;
                //    meshes[kidIter].transform = transform.localToWorldMatrix;
                //}
                GetComponent<Renderer>().material = previewMaterial;
                GetComponent<MeshFilter>().mesh = mesh;

                if(!queueUpdate)
                {
                    if(mySculpt.gameObject.GetComponent<SculptTo3DTexture>())
                    {
                        sdfTo3DTex = new RenderTexture(meshResolution, meshResolution, 0, RenderTextureFormat.ARGB32);
                        sdfTo3DTex.enableRandomWrite = true;
                        sdfTo3DTex.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
                        sdfTo3DTex.volumeDepth = meshResolution;
                        sdfTo3DTex.Create();

                        myComputeShader.SetInt("infoType", (int)(mySculpt.gameObject.GetComponent<SculptTo3DTexture>().infoType));
                        myComputeShader.SetFloat("sculptToTextureBlur", mySculpt.gameObject.GetComponent<SculptTo3DTexture>().blur * -100);
                        
                        myComputeShader.SetTexture(myComputeShader.FindKernel("CSGetVolume"), "Result256", sdf256);
                        myComputeShader.SetTexture(myComputeShader.FindKernel("CSGetVolume"), "ResultColor", sdfTo3DTex);
                        myComputeShader.SetTexture(myComputeShader.FindKernel("CSGetVolume"), "overlap", sdfOverlapFull);
                        myComputeShader.SetTexture(myComputeShader.FindKernel("CSGetVolume"), "noiseTexture", noise);
                        myComputeShader.SetBuffer(myComputeShader.FindKernel("CSGetVolume"), "edits", brushBuffer);

                        myComputeShader.Dispatch(myComputeShader.FindKernel("CSGetVolume"),
                            meshResolution/4, meshResolution/4, meshResolution/4);

                        mySculpt.gameObject.GetComponent<SculptTo3DTexture>().Update3DTexture(sdfTo3DTex);
                        //sdfTo3DTex.Release();
                    }
                }

                //kidIter += 1;

                brushBuffer.Release();

                counter.Release();
                faceCounter.Release();

                vertexBuffer.Release();
                triangleBuffer.Release();
                uvBuffer.Release();
                normalBuffer.Release();
                colorBuffer.Release();

                /*if(kidIter == 8)
                {
                    //ooBuffer.Release();
                    //boxBuffer.Release();
                    //sphereBuffer.Release();

                    mesh = new Mesh();
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    mesh.CombineMeshes(meshes);
                    GetComponent<MeshFilter>().mesh = mesh;
                    Debug.Log("DONE");
                }
                else*/
                //{
                    //StartCoroutine(GenMeshLoop());
                //}
                
                startReadingMeshBuffers = 0;
            //}

            if(queueUpdate)
            {
                DefineSDF();
                queueUpdate = false;
            }
            else
            {
                queueUpdate = false;
                updateMesh = false;

                sdf256.Release();
                sdfOverlap.Release();
                sdfOverlapFull.Release();
            }
            if(lodsCreated > 0)
            {
                CreateLODS();
            }
        }

        /*struct AddToMeshDataStruct
        {
            public int resolution;
            public int index;
            public MeshData meshData;

            public AddToMeshDataStruct(int resolution, int index, MeshData meshData)
            {
                this.resolution = resolution;
                this.index = index;
                this.meshData = meshData;
            }
        };

        public void AddToMeshData (object data)
        {
            int resolution = ((AddToMeshDataStruct)(data)).resolution;
            int index = ((AddToMeshDataStruct)(data)).index;
            MeshData meshData = ((AddToMeshDataStruct)(data)).meshData;

            int vertCount = meshData.verticies.Count;
            int vertsPerThread = Mathf.FloorToInt((float)vertCount / 512f);
            int vertsLeftOver = vertCount % 512;
            int totalTasks = vertsPerThread;
            if(index < vertsLeftOver)
                totalTasks += 1;


            Vector3 gradient;
            for (int i = 0; i < totalTasks; i++)
            {
                gradient = GetSDFGradient(meshData.verticies[i * 512 + index]);
                meshData.verticies[i * 512 + index] -= gradient * GetSDFDistance(meshData.verticies[i * 512 + index]);
                meshData.verticies[i * 512 + index] -= gradient * GetSDFDistance(meshData.verticies[i * 512 + index]);
                meshData.verticies[i * 512 + index] -= gradient * GetSDFDistance(meshData.verticies[i * 512 + index]);
                gradient = GetSDFGradient(meshData.verticies[i * 512 + index]);

                if(shareVerticies)
                {
                    meshData.normals[i * 512 + index] = (gradient);
                    meshData.colors[i * 512 + index] = (GetSDFColor(meshData.verticies[i * 512 + index])); //new Color(gradient.x, gradient.y, gradient.z, 1));
                }
                else
                {
                    meshData.colors[i * 512 + index] = (GetSDFColor(meshData.verticies[i * 512 + index]));
                }
            }

            generateMeshProgress += 1;
        }*/

        /*void ClearVoxelArray(ComputeBuffer voxelBuffer)
        {
            int kid = myComputeShader.FindKernel("Clear");

            myComputeShader.SetBuffer(kid, "voxelArray", voxelBuffer);
            myComputeShader.SetInt("Resolution", resolution);
            myComputeShader.Dispatch(kid, resolution / 8, resolution / 8, resolution / 8);
        }*/

        /*void UploadMesh (int LOD)
        {
            if(LOD == 0)
            {
                //Debug.Log("LOD0");
                meshDataLOD0.UploadMesh(shareVerticies);
                GetComponent<MeshFilter>().mesh = meshDataLOD0.mesh;
            //GetComponent<MeshCollider>().sharedMesh = meshDataLOD0.mesh;
            }
            if(LOD == 1)
            {
                meshDataLOD1.UploadMesh(shareVerticies);
                //lod1 = new GameObject("LOD1");
                lod1.transform.parent = transform;
                lod1.transform.position = transform.position;
                lod1.transform.rotation = transform.rotation;
                lod1.transform.localScale = Vector3.one;
                lod1.AddComponent<MeshFilter>();
                lod1.AddComponent<MeshRenderer>();
                lod1.GetComponent<MeshFilter>().mesh = meshDataLOD1.mesh;
                lod1.GetComponent<MeshRenderer>().material = GetComponent<MeshRenderer>().material;
                lod1.SetActive(false);
            }
            if(LOD == 2)
            {
                meshDataLOD2.UploadMesh(shareVerticies);
                //lod2 = new GameObject("LOD2");
                lod2.transform.parent = transform;
                lod2.transform.position = transform.position;
                lod2.transform.rotation = transform.rotation;
                lod2.transform.localScale = Vector3.one;
                lod2.AddComponent<MeshFilter>();
                lod2.AddComponent<MeshRenderer>();
                lod2.GetComponent<MeshFilter>().mesh = meshDataLOD2.mesh;
                lod2.GetComponent<MeshRenderer>().material = GetComponent<MeshRenderer>().material;
                lod2.SetActive(false);
            }

            if(lod1 != null && lod2 != null && GetComponent<MeshFilter>().mesh != null)
            {
                lod1.SetActive(true);
                lod2.SetActive(true);
                LOD[] lods = new LOD[3];
                lods[0].screenRelativeTransitionHeight = 0.6f;
                lods[1].screenRelativeTransitionHeight = 0.2f;
                lods[2].screenRelativeTransitionHeight = 0.05f;
                lods[0].renderers = new Renderer[1] {GetComponent<MeshRenderer>()};
                lods[1].renderers = new Renderer[1] {lod1.GetComponent<MeshRenderer>()};
                lods[2].renderers = new Renderer[1] {lod2.GetComponent<MeshRenderer>()};
                //GetComponent<LODGroup>().SetLODs(lods);
                //GetComponent<LODGroup>().RecalculateBounds();
            }
        }*/

        /// <summary>
        /// Called to rotate all brushes around an axis that runs through the center of the bounding box 180 degrees.
        /// </summary>
        /// <param name="axis">The axis to rotate all brushes around.</param>
        public void FlipOverAxis (Vector3 axis)
        {
            for (int i = 0; i < myBrushes.Count; i++)
            {
                myBrushes[i].transform.RotateAround(boundingBox.transform.position, axis, 180);
            }
        }

        /// <summary>
        /// Called to make the mesh generator's mesh a fuzzy mesh for. Will also temporarily change the material to the preview fuzzy material.
        /// </summary>
        public void PreviewFuzzy()
        {
            GetComponent<MeshFilter>().mesh = ssm.GetFuzzyMesh(GetComponent<MeshFilter>().mesh, mySculpt.fuzzNoiseScale,
                mySculpt.fuzzNoiseStrength, mySculpt.fuzzScale, mySculpt.fuzzRotationOffset, mySculpt.realignFuzzNormals);
            GetComponent<Renderer>().material = previewFuzzyMaterial;
        }

        string recipeFile;
        string meshFile;

        public struct sculptRecipeStruct
        {
            public Vector3 sculptOffsetP;
            public Quaternion sculptOffsetR;
            public Vector3 sculptOffsetS;
            public Vector3[] transformP;
            public Quaternion[] transformR;
            public Vector3[] transformS;
            public Vector3[] curveAP;
            public Quaternion[] curveAR;
            public Vector3[] curveAS;
            public Vector3[] curveBP;
            public Quaternion[] curveBR;
            public Vector3[] curveBS;
            public Color[] color;
            public float[] roundA;
            public float[] noiseAmount;
            public float[] noiseStrength;
            public bool[] textureNoise;
            public float[] blend;
            public int[] brushType;
            public int[] brushShape;
        }

        public struct SculptMeshStruct
        {
            public Vector3[] vertices;
            public Vector3[] normals;
            public int[] triangles;
            public Vector2[] uvs;
            public Color[] colors;
        }

        sculptRecipeStruct sculptRecipe;
        SculptMeshStruct sculptMesh;

        //public void readFile()
        //{
        //    // Does the file exist?
        //    if (File.Exists(recipeFile))
        //    {
        //        // Read the entire file and save its contents.
        //        string fileContents = File.ReadAllText(recipeFile);
        //        // Deserialize the JSON data 
        //        //  into a pattern matching the GameData class.
        //        sculptRecipe = JsonUtility.FromJson<sculptRecipeStruct>(fileContents);
        //    }
        //}

        //public void writeFile()
        //{
        //    // Serialize the object into JSON and save string.
        //    string jsonString = JsonUtility.ToJson(sculptRecipe);
        //    // Write JSON to file.
        //    File.WriteAllText(recipeFile, jsonString);
        //}

        void WipeAllData ()
        {
            List<SDFBrush> tmp = myBrushes;
            myBrushes = new List<SDFBrush>();
            for (int i = 0; i < tmp.Count; i++)
            {
                GameObject.Destroy(tmp[i].gameObject);
            }
            brushes = new List<Brush>();
        }

        /// <summary>
        /// Called when done editing a sculpt and exiting edit mode as the SDF Player.
        /// </summary>
        public void SaveRecipe()
        {
            ssm.editedSculpt = null;
            boundingBox.enabled = false;
            gridXY.enabled = false;
            gridXZ.enabled = false;
            gridYZ.enabled = false;

            if(!Directory.Exists(Application.dataPath + mySculptParent.myRecipePath))
                Directory.CreateDirectory(Application.dataPath + mySculptParent.myRecipePath);
            if(!Directory.Exists(Application.dataPath + mySculptParent.myMeshPath))
                Directory.CreateDirectory(Application.dataPath + mySculptParent.myMeshPath);
            
            Mesh myMesh = GetComponent<MeshFilter>().mesh;
            Vector3[] adjustedVerts =  myMesh.vertices;
            for (int i = 0; i < adjustedVerts.Length; i++)
            {
                adjustedVerts[i] -= mySculpt.transform.position;
            }
            myMesh.SetVertices(adjustedVerts);
            myMesh.RecalculateBounds();
            myMesh.UploadMeshData(false);

            if(mySculpt.isFuzzy)
            {
                myMesh = ssm.GetFuzzyMesh(myMesh, mySculpt.fuzzNoiseScale, mySculpt.fuzzNoiseStrength, mySculpt.fuzzScale,
                    mySculpt.fuzzRotationOffset, mySculpt.realignFuzzNormals);
            }
            else
            {
                myMesh = ssm.RandomizeMeshAlpha(myMesh, mySculpt.fuzzNoiseScale, mySculpt.fuzzNoiseStrength);
            }
            
            //mySculpt.gameObject.GetComponent<MeshFilter>().mesh = myMesh;
            //ssm.UpdateAllSculpts(mySculpt, myMesh);

            //Destroy(mySculpt.gameObject.GetComponent<BoxCollider>());
            //mySculpt.gameObject.AddComponent<BoxCollider>();

            for (int i = 0; i < mySculptsColliders.Count; i++)
            {
                mySculptsColliders[i].enabled = true;
            }

            if(mySculpt.GetComponent<MeshFilter>().mesh == null)
                mySculpt.gameObject.GetComponent<MeshFilter>().mesh = defaultMesh;
            else if(mySculpt.GetComponent<MeshFilter>().mesh.vertexCount <= 0)
                mySculpt.gameObject.GetComponent<MeshFilter>().mesh = defaultMesh;

            sculptRecipe = new sculptRecipeStruct();

            sculptRecipe.sculptOffsetP = mySculpt.transform.position;
            sculptRecipe.sculptOffsetR = mySculpt.transform.rotation;
            sculptRecipe.sculptOffsetS = mySculpt.transform.lossyScale;
            sculptRecipe.transformP = new Vector3[myBrushes.Count];
            sculptRecipe.transformR = new Quaternion[myBrushes.Count];
            sculptRecipe.transformS = new Vector3[myBrushes.Count];
            sculptRecipe.curveAP = new Vector3[myBrushes.Count];
            sculptRecipe.curveAR = new Quaternion[myBrushes.Count];
            sculptRecipe.curveAS = new Vector3[myBrushes.Count];
            sculptRecipe.curveBP = new Vector3[myBrushes.Count];
            sculptRecipe.curveBR = new Quaternion[myBrushes.Count];
            sculptRecipe.curveBS = new Vector3[myBrushes.Count];

            sculptRecipe.color = new Color[myBrushes.Count];
            sculptRecipe.roundA = new float[myBrushes.Count];
            sculptRecipe.noiseAmount = new float[myBrushes.Count];
            sculptRecipe.noiseStrength = new float[myBrushes.Count];
            sculptRecipe.textureNoise = new bool[myBrushes.Count];
            sculptRecipe.blend = new float[myBrushes.Count];
            sculptRecipe.brushType = new int[myBrushes.Count];
            sculptRecipe.brushShape = new int[myBrushes.Count];

            for (int i = 0; i < myBrushes.Count; i++)
            {
                // Fill sculpt recipe
                sculptRecipe.transformP[i] = myBrushes[i].transform.position;
                sculptRecipe.transformR[i] = myBrushes[i].transform.rotation;
                sculptRecipe.transformS[i] = myBrushes[i].transform.localScale;
                if(myBrushes[i].transform.childCount > 0)
                {
                    sculptRecipe.curveAP[i] = myBrushes[i].transform.GetChild(0).position;
                    sculptRecipe.curveAR[i] = myBrushes[i].transform.GetChild(0).rotation;
                    sculptRecipe.curveAS[i] = myBrushes[i].transform.GetChild(0).localScale;
                    sculptRecipe.curveBP[i] = myBrushes[i].transform.GetChild(1).position;
                    sculptRecipe.curveBR[i] = myBrushes[i].transform.GetChild(1).rotation;
                    sculptRecipe.curveBS[i] = myBrushes[i].transform.GetChild(1).localScale;
                }
                sculptRecipe.color[i] = myBrushes[i].color;
                sculptRecipe.roundA[i] = myBrushes[i].roundA;
                sculptRecipe.noiseAmount[i] = myBrushes[i].warpScale;
                sculptRecipe.noiseStrength[i] = myBrushes[i].warpStrength;
                sculptRecipe.textureNoise[i] = myBrushes[i].textureWarp;
                sculptRecipe.blend[i] = myBrushes[i].blend;
                sculptRecipe.brushType[i] = (int)myBrushes[i].brushType;
                sculptRecipe.brushShape[i] = (int) myBrushes[i].brushShape;
            }
            string json = JsonUtility.ToJson(sculptRecipe);
            File.WriteAllText(recipeFile, json);

            sculptMesh.vertices = new Vector3[myMesh.vertices.Length];
            sculptMesh.vertices = myMesh.vertices;
            sculptMesh.normals = new Vector3[myMesh.normals.Length];
            sculptMesh.normals = myMesh.normals;
            sculptMesh.triangles = new int[myMesh.triangles.Length];
            sculptMesh.triangles = myMesh.triangles;
            sculptMesh.uvs = new Vector2[myMesh.uv.Length];
            sculptMesh.uvs = myMesh.uv;
            sculptMesh.colors = new Color[myMesh.colors.Length];
            sculptMesh.colors = myMesh.colors;

            json = JsonUtility.ToJson(sculptMesh);
            File.WriteAllText(meshFile, json);
            
            if(mySculptParent.LODResolutions.Count > 0)
            {
                createLODs = true;
                lodsCreated = mySculptParent.LODResolutions.Count + 1;
                meshResolution = mySculptParent.LODResolutions[0];
            }
            else
            {
                ssm.BuildSingleSculptFromMeshData(mySculptParent);
                if(mySculpt.isSculptStatic)
                    ssm.RecalculateSingleStaticSculptBoundingBox(mySculpt);
                WipeAllData();
            }
        }

        void CreateLODS()
        {  
            Mesh myMesh = GetComponent<MeshFilter>().mesh;
            Vector3[] adjustedVerts =  myMesh.vertices;
            for (int i = 0; i < adjustedVerts.Length; i++)
            {
                adjustedVerts[i] -= mySculpt.transform.position;
            }
            myMesh.SetVertices(adjustedVerts);
            myMesh.RecalculateBounds();
            myMesh.UploadMeshData(false);

            if(mySculpt.isFuzzy)
            {
                myMesh = ssm.GetFuzzyMesh(myMesh, mySculpt.fuzzNoiseScale, mySculpt.fuzzNoiseStrength,
                    mySculpt.fuzzScale * (mySculpt.meshResolution/meshResolution), mySculpt.fuzzRotationOffset, mySculpt.realignFuzzNormals);
            }
            else
            {
                myMesh = ssm.RandomizeMeshAlpha(myMesh, mySculpt.fuzzNoiseScale, mySculpt.fuzzNoiseStrength);
            }

            sculptMesh.vertices = new Vector3[myMesh.vertices.Length];
            sculptMesh.vertices = myMesh.vertices;
            sculptMesh.normals = new Vector3[myMesh.normals.Length];
            sculptMesh.normals = myMesh.normals;
            sculptMesh.triangles = new int[myMesh.triangles.Length];
            sculptMesh.triangles = myMesh.triangles;
            sculptMesh.uvs = new Vector2[myMesh.uv.Length];
            sculptMesh.uvs = myMesh.uv;
            sculptMesh.colors = new Color[myMesh.colors.Length];
            sculptMesh.colors = myMesh.colors;

            string json = JsonUtility.ToJson(sculptMesh);
            File.WriteAllText(Application.dataPath + mySculptParent.myMeshPath + mySculptParent.myName +
                "_LOD" + (mySculptParent.LODResolutions.Count - (lodsCreated-1)).ToString() + "MeshData.json", json);

            if(lodsCreated == 1)
                lodsCreated = 0;

            if(lodsCreated > 0)
            {
                meshResolution = mySculptParent.LODResolutions[(mySculptParent.LODResolutions.Count - lodsCreated) + 1];
                createLODs = true;
            }
            else
            {
                ssm.BuildSingleSculptFromMeshData(mySculptParent);
                if(mySculpt.isSculptStatic)
                    ssm.RecalculateSingleStaticSculptBoundingBox(mySculpt);
                WipeAllData();
            }
        }

        /// <summary>
        /// Called when beginning to edit a sculpt and entering edit mode as the SDF Player.
        /// </summary>
        public void LoadRecipe()
        {
            GetComponent<Renderer>().material = previewMaterial;
            ssm.editedSculpt = mySculpt;
            mySculptParent = ssm.GetParentSculpt(mySculpt);
            if(mySculptParent == null)
                mySculptParent = mySculpt;

            if(!Directory.Exists(Application.dataPath + mySculptParent.myRecipePath))
                Directory.CreateDirectory(Application.dataPath + mySculptParent.myRecipePath);
            if(!Directory.Exists(Application.dataPath + mySculptParent.myMeshPath))
                Directory.CreateDirectory(Application.dataPath + mySculptParent.myMeshPath);

            recipeFile = Application.dataPath + mySculptParent.myRecipePath + mySculptParent.myName + "_BrushData.json";
            meshFile = Application.dataPath + mySculptParent.myMeshPath + mySculptParent.myName + "_MeshData.json";
            
            mySculpt.GetComponent<MeshFilter>().mesh = null;
            boundingBox.enabled = true;
            boundingBox.transform.position = mySculpt.transform.position;
            boundingBox.transform.localScale = Vector3.one * mySculpt.scale;

            //gridXY.enabled = true;
            gridXZ.enabled = true;
            //gridYZ.enabled = true;
            gridXY.transform.position = mySculpt.transform.position;
            gridXZ.transform.position = mySculpt.transform.position;
            gridYZ.transform.position = mySculpt.transform.position;
            gridXY.transform.localScale = Vector3.one * mySculpt.scale;
            gridXZ.transform.localScale = Vector3.one * mySculpt.scale;
            gridYZ.transform.localScale = Vector3.one * mySculpt.scale;

            if(mySculpt.originAtBottom)
            {
                boundingBox.transform.position += mySculpt.transform.up * mySculpt.scale * 0.5f * Mathf.Min(transform.localScale.x, transform.localScale.y, transform.localScale.z);
                gridXY.transform.position += mySculpt.transform.up * mySculpt.scale * 0.5f * Mathf.Min(transform.localScale.x, transform.localScale.y, transform.localScale.z);
                gridYZ.transform.position += mySculpt.transform.up * mySculpt.scale * 0.5f * Mathf.Min(transform.localScale.x, transform.localScale.y, transform.localScale.z);
            }
            scale = mySculpt.scale;
            meshResolution = mySculpt.meshResolution;
            if (File.Exists(recipeFile))
            {
                GetComponent<MeshFilter>().mesh = mySculpt.gameObject.GetComponent<MeshFilter>().mesh;
                mySculptsColliders  = new List<Collider>(mySculpt.gameObject.GetComponents<Collider>());
                for (int i = 0; i < mySculptsColliders.Count; i++)
                {
                    mySculptsColliders[i].enabled = false;
                }
                mySculpt.gameObject.GetComponent<Collider>().enabled = false;
                string json = File.ReadAllText(recipeFile);
                sculptRecipe = JsonUtility.FromJson<sculptRecipeStruct>(json);
                //transform.eulerAngles = mySculpt.transform.eulerAngles - sculptRecipe.sculptOffsetR.eulerAngles;

                for (int i = 0; i < sculptRecipe.transformP.Length; i++)
                {
                    // Create brushes from sculpt recipe
                    SDFBrush newBrush = null;
                    if(sculptRecipe.brushShape[i] == 0){
                        newBrush = GameObject.Instantiate(hudData.sphereBrushPrefab,transform.position,
                        Quaternion.identity, transform).GetComponent<SDFBrush>();
                    }
                    if(sculptRecipe.brushShape[i] == 5){
                        newBrush = GameObject.Instantiate(hudData.ellipsoidBrushPrefab,transform.position,
                        Quaternion.identity, transform).GetComponent<SDFBrush>();
                    }
                    if(sculptRecipe.brushShape[i] == 1 || sculptRecipe.brushShape[i] == 2 ||
                    sculptRecipe.brushShape[i] == 3 || sculptRecipe.brushShape[i] == 6 ||
                    sculptRecipe.brushShape[i] == 7 || sculptRecipe.brushShape[i] == 8 ||
                    sculptRecipe.brushShape[i] == 9 || sculptRecipe.brushShape[i] == 10 ||
                    sculptRecipe.brushShape[i] == 11){
                        newBrush = GameObject.Instantiate(hudData.boxBrushPrefab,transform.position,
                            Quaternion.identity, transform).GetComponent<SDFBrush>();
                    }
                    if(sculptRecipe.brushShape[i] == 4)
                    {
                        newBrush = GameObject.Instantiate(hudData.torusBrushPrefab,transform.position,
                            Quaternion.identity, transform).GetComponent<SDFBrush>();
                    }
                    if(sculptRecipe.brushShape[i] == 12)
                    {
                        newBrush = GameObject.Instantiate(hudData.linkBrushPrefab,transform.position,
                            Quaternion.identity, transform).GetComponent<SDFBrush>();
                    }
                    if(sculptRecipe.brushShape[i] == 13)
                    {
                        newBrush = GameObject.Instantiate(hudData.curveBrushPrefab,transform.position,
                            Quaternion.identity, transform).GetComponent<SDFBrush>();
                    }

                    switch (sculptRecipe.brushShape[i])
                    {
                        case 0:
                            newBrush.name = "Sphere"; break;
                        case 1:
                            newBrush.name = "Box"; break;
                        case 2:
                            newBrush.name = "Cylinder"; break;
                        case 3:
                            newBrush.name = "Cone"; break;
                        case 4:
                            newBrush.name = "Torus"; break;
                        case 5:
                            newBrush.name = "Ellipsoid"; break;
                        case 6:
                            newBrush.name = "SBox"; break;
                        case 7:
                            newBrush.name = "SCylinder"; break;
                        case 8:
                            newBrush.name = "SCone"; break;
                        case 9:
                            newBrush.name = "TriPrism"; break;
                        case 10:
                            newBrush.name = "HexPrism"; break;
                        case 11:
                            newBrush.name = "Pyramid"; break;
                        case 12:
                            newBrush.name = "Link"; break;
                        case 13:
                            newBrush.name = "Curve"; break;
                        default:
                            newBrush.name = newBrush.name; break;
                    } 
                    newBrush.transform.SetSiblingIndex(transform.childCount-1);

                    newBrush.color = sculptRecipe.color[i];
                    newBrush.roundA = sculptRecipe.roundA[i];
                    newBrush.warpScale = sculptRecipe.noiseAmount[i];
                    newBrush.warpStrength = sculptRecipe.noiseStrength[i];
                    newBrush.textureWarp = sculptRecipe.textureNoise[i];
                    newBrush.blend = sculptRecipe.blend[i];
                    newBrush.brushType = (SDFBrush.SDFBrushType)sculptRecipe.brushType[i];
                    newBrush.brushShape = (SDFBrush.SDFBrushShape)sculptRecipe.brushShape[i];

                    Vector3 posOffset = mySculpt.transform.position - sculptRecipe.sculptOffsetP;
                    Vector3 rotOffset = mySculpt.transform.eulerAngles - sculptRecipe.sculptOffsetR.eulerAngles;
                    if(mySculpt.rotationAffectsSculpt == false)
                        rotOffset = Vector3.zero;
                    Vector3 sizeOffset = mySculpt.transform.lossyScale - sculptRecipe.sculptOffsetS;
                    newBrush.transform.position = sculptRecipe.transformP[i] + posOffset;
                    newBrush.transform.RotateAround(mySculpt.transform.position, Vector3.up, rotOffset.y);
                    newBrush.transform.RotateAround(mySculpt.transform.position, Vector3.right, rotOffset.x);
                    newBrush.transform.RotateAround(mySculpt.transform.position, Vector3.forward, rotOffset.z);
                    newBrush.transform.eulerAngles = sculptRecipe.transformR[i].eulerAngles + rotOffset;
                    newBrush.transform.localScale = sculptRecipe.transformS[i];
                    if(newBrush.transform.childCount > 0)
                    {
                        newBrush.transform.GetChild(0).position = sculptRecipe.curveAP[i] + posOffset;
                        newBrush.transform.GetChild(0).eulerAngles = sculptRecipe.curveAR[i].eulerAngles + rotOffset;
                        newBrush.transform.GetChild(0).localScale = sculptRecipe.curveAS[i];
                        newBrush.transform.GetChild(1).position = sculptRecipe.curveBP[i] + posOffset;
                        newBrush.transform.GetChild(1).eulerAngles = sculptRecipe.curveBR[i].eulerAngles + rotOffset;
                        newBrush.transform.GetChild(1).localScale = sculptRecipe.curveBS[i];
                    }
                    newBrush.enabled = true;
                }
            }
            else
            {
                transform.rotation = Quaternion.identity;
            }
        }

        void Awake ()
        {
            transform.position = Vector3.zero;
        }
        

        // Start is called before the first frame update
        void Start()
        {

            SDFPlayer = GameObject.FindObjectOfType<SDFSculptorController>();
            sculptRecipe = new sculptRecipeStruct();
            sculptMesh = new SculptMeshStruct();

            ssm = GameObject.FindObjectOfType<SculptSceneManager>();
            //Debug.Log(Application.dataPath + "/SDFSculptor/SculptData/gamedata.json");

            if(mySculpt)
            {
                recipeFile = Application.dataPath + mySculpt.myRecipePath + mySculpt.myName + "_BrushData.json";
                meshFile = Application.dataPath + mySculpt.myMeshPath + mySculpt.myName + "_MeshData.json";
            }
            boundingBox = transform.Find("BoundingBox").gameObject.GetComponent<Renderer>();
            gridXY = transform.Find("GridXY").gameObject.GetComponent<Renderer>();
            gridXZ = transform.Find("GridXZ").gameObject.GetComponent<Renderer>();
            gridYZ = transform.Find("GridYZ").gameObject.GetComponent<Renderer>();

            //sdf16 = new RenderTexture(16, 16, 0);
            //sdf64 = new RenderTexture(64, 64, 0);
            //sdf256 = new RenderTexture(256, 256, 0);

            kid1 = myComputeShader.FindKernel("CSDefineSDF");
            kid2 = myComputeShader.FindKernel("CSFaceCount");
            //kid3 = myComputeShader.FindKernel("CSGenerateMesh");

            for (int i = 0; i < kids.Length; i++)
            {
                if(i == 0)
                    kids[i] = myComputeShader.FindKernel("CSGenerateMesh" + (i+1).ToString());
            }

            //data = new Dictionary<Vector3, Voxel>();
            dataLOD0 = new Dictionary<Vector3, Voxel>();
            dataLOD1 = new Dictionary<Vector3, Voxel>();
            dataLOD2 = new Dictionary<Vector3, Voxel>();
            myData = new int[resolutionLOD0 * resolutionLOD0 * resolutionLOD0];
            //ClearData();

            /*if(automaticallyAddChildBrushes)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    //if(myBrushes.Count <= i)
                    //{
                        if(transform.GetChild(i).gameObject.activeInHierarchy)
                        {
                            if(transform.GetChild(i).gameObject.GetComponent<SDFBrush>())
                            {
                                myBrushes.Add(transform.GetChild(i).gameObject.GetComponent<SDFBrush>());
                            }
                        }
                    //}
                }
                for (int i = 0; i < myBrushes.Count; i++)
                {
                    brushes.Add(CreateBrush(myBrushes[i]));
                }
            }*/

        }

        /// <summary>
        /// Toggle the grid plane facing front and back.
        /// </summary>
        public void ToggleGridXY ()
        {
            gridXY.enabled = !gridXY.enabled;
        }
        /// <summary>
        /// Toggle the grid plane facing up and down.
        /// </summary>
        public void ToggleGridXZ ()
        {
            gridXZ.enabled = !gridXZ.enabled;
        }
        /// <summary>
        /// Toggle the grid plane facing left and right.
        /// </summary>
        public void ToggleGridYZ ()
        {
            gridYZ.enabled = !gridYZ.enabled;
        }

        void GetBrushes ()
        {
            //
            //    for (int i = 0; i < transform.childCount; i++)
            //    {
            //        if(myBrushes.Count <= i)
            //        {
            //            if(transform.GetChild(i).gameObject.activeInHierarchy)
            //            {
            //                if(transform.GetChild(i).gameObject.GetComponent<SDFBrush>())
            //                {
            //                    myBrushes.Add(transform.GetChild(i).gameObject.GetComponent<SDFBrush>());
            //                }
            //            }
            //        }
            //    }
            //}

            // Define the SDF from box and sphere colliders
            //for (int i = 0; i < myBrushes.Count; i++)
            //{
                /*if(myBrushes[i].GetComponent<BoxCollider>())
                {
                    BoxBrush bb = new BoxBrush();
                    bb.transform = Matrix4x4.TRS(myBrushes[i].transform.position, myBrushes[i].transform.rotation, Vector3.one);
                    bb.scale = myBrushes[i].transform.lossyScale;
                    bb.color = myBrushes[i].color;
                    bb.blend = myBrushes[i].blend;
                    bb.brushType = (int)myBrushes[i].brushType;

                    boxes.Add(bb);
                    orderOfOperations.Add(0);
                }
                if(myBrushes[i].GetComponent<SphereCollider>())
                {
                    SphereBrush sb = new SphereBrush();
                    sb.position = myBrushes[i].transform.position;
                    sb.scale = myBrushes[i].transform.lossyScale.x;
                    sb.color = myBrushes[i].color;
                    sb.blend = myBrushes[i].blend;
                    sb.brushType = (int)myBrushes[i].brushType;

                    spheres.Add(sb);
                    orderOfOperations.Add(1);
                }*/

                //brushes.Add(CreateBrush(myBrushes[i]));
            //}

            for (int i = 0; i < myBrushes.Count; i++)
            {
                if((int)myBrushes[i].brushType != brushes[i].brushType || 
                (int)myBrushes[i].brushShape != brushes[i].brushShape || 
                myBrushes[i].color != brushes[i].color ||
                myBrushes[i].blend != brushes[i].blend ||
                myBrushes[i].roundA != brushes[i].roundA ||
                myBrushes[i].roundB != brushes[i].roundB ||
                myBrushes[i].warpScale != brushes[i].noiseAmount ||
                myBrushes[i].warpStrength != brushes[i].noiseStrength ||
                (myBrushes[i].textureWarp ? 1 : 0) != brushes[i].textureNoise)
                    UpdateBrush(myBrushes[i]);  
            }

            // Generate Blocky Voxel Mesh

            //meshData.ClearMesh();

            meshDataLOD0.ClearMesh();
            meshDataLOD1.ClearMesh();
            meshDataLOD2.ClearMesh();

            //myThread0 = new Thread (DefineSDF);
            //myThread0.Name = "LOD0";
            //myThread0.Start();
            done0 = false;
            //DefineSDF();

            //myThreadLOD1 = new Thread (DefineSDF);
            //myThreadLOD1.Start();
            //myThreadLOD1.Name = "LOD1";
            //LOD1done = false;
    //
            //myThreadLOD2 = new Thread (DefineSDF);
            //myThreadLOD2.Start();
            //myThreadLOD2.Name = "LOD2";
            //LOD2done = false;
            
            //done2 = true;

            //UploadMesh();

            // Create LODs?
        }

        Brush CreateBrush (SDFBrush sdfb)
        {
            Brush b = new Brush();

            b.transform = Matrix4x4.TRS(sdfb.transform.position, sdfb.transform.rotation, Vector3.one);
            b.position = sdfb.transform.position;
            b.scale = sdfb.transform.lossyScale;
            b.color = sdfb.color;
            b.roundA = sdfb.roundA;
            b.roundB = sdfb.roundB;
            b.noiseAmount = sdfb.warpScale;
            b.noiseStrength = sdfb.warpStrength;
            b.textureNoise = sdfb.textureWarp ? 1 : 0;
            b.blend = sdfb.blend;
            b.brushType = (int)sdfb.brushType;
            //b.brushShape = 0;

            b.brushShape = (int)sdfb.brushShape;

            if(sdfb.brushShape == SDFBrush.SDFBrushShape.Cylinder)
                b.scale = new Vector3(Mathf.Min(sdfb.transform.lossyScale.z, sdfb.transform.lossyScale.x)/2, sdfb.transform.lossyScale.y, 1);
            if(sdfb.brushShape == SDFBrush.SDFBrushShape.Cone)
                b.scale = new Vector3(Mathf.Min(sdfb.transform.lossyScale.x, sdfb.transform.lossyScale.z)/sdfb.transform.lossyScale.y, sdfb.transform.lossyScale.y, 1);

            if(sdfb.brushShape == SDFBrush.SDFBrushShape.Torus)
            {
                b.roundB = Mathf.Min(Mathf.Min(sdfb.transform.lossyScale.x, sdfb.transform.lossyScale.y), sdfb.transform.lossyScale.z)/2; // Ring Thickness
                b.roundA = Mathf.Min(sdfb.transform.lossyScale.x/2, sdfb.transform.lossyScale.z/2) - b.roundB; // Ring Size
            }

            if(sdfb.brushShape == SDFBrush.SDFBrushShape.Link)
            {

                b.roundB = Mathf.Min(sdfb.transform.lossyScale.x/2, sdfb.transform.lossyScale.y/2) - sdfb.transform.lossyScale.z/2; // Link Length
                b.roundA = sdfb.transform.lossyScale.y/2 - sdfb.transform.lossyScale.x/2 - Mathf.Min(b.roundB, sdfb.transform.lossyScale.z/2); // Link Width/Thickness
            }

            if(sdfb.brushShape == SDFBrush.SDFBrushShape.RoundedCone)
            {
                b.position -= sdfb.transform.up * (sdfb.transform.lossyScale.y/4 + Mathf.Min(b.scale.x, b.scale.z)/4);
                b.transform = Matrix4x4.TRS(b.position, sdfb.transform.rotation, Vector3.one);
                b.roundA = Mathf.Lerp(0, Mathf.Min(Mathf.Min(b.scale.x, b.scale.z), b.scale.y)/2, sdfb.roundA);
                b.roundB = Mathf.Lerp(Mathf.Min(Mathf.Min(b.scale.x, b.scale.z), b.scale.y)/2, 0, sdfb.roundA);
            }

            if(sdfb.brushShape == SDFBrush.SDFBrushShape.RoundedCylinder)
            {
                b.roundB = Mathf.Min(b.scale.x, b.scale.z)/4;//Mathf.Lerp(Mathf.Min(b.scale.x, b.scale.z)/2, 0, sdfb.roundA);
            }

            if(sdfb.transform.childCount > 0 && sdfb.brushShape == SDFBrush.SDFBrushShape.BezierCurve)
            {
                b.curveA = sdfb.transform.GetChild(0).transform.position;
                b.curveB = sdfb.transform.GetChild(1).transform.position;

                b.roundA = sdfb.transform.GetChild(0).transform.localScale.x * 0.5f;
                b.roundB = sdfb.transform.GetChild(1).transform.localScale.x * -0.5f + b.roundA;
                
                Bounds bounds = gameObject.GetComponent<Renderer>().bounds;
                bounds.Encapsulate (sdfb.transform.GetChild(0).gameObject.GetComponent<Renderer>().bounds);
                bounds.Encapsulate (sdfb.transform.GetChild(1).gameObject.GetComponent<Renderer>().bounds);
                b.bounds = bounds.extents;
            }
            else
            {
                b.bounds = sdfb.gameObject.GetComponent<Renderer>().bounds.extents;
            }

            return b;
        }

        /// <summary>
        /// If the order of the brushes has changed, it won't be automatically known, so call this function. 
        /// </summary>
        public void ReorderBrushes ()
        {
            if(boundingBox.enabled == false)
            {
                Debug.Log("Can Only reorder brushes if editing a sculpt and brushes are present.");
                return;
            }

            myBrushes.Clear();
            brushes.Clear();

            for (int i = 0; i < transform.childCount; i++)
            {
                if(transform.GetChild(i).gameObject.activeInHierarchy)
                {
                    if(transform.GetChild(i).gameObject.GetComponent<SDFBrush>())
                    {
                        myBrushes.Add(transform.GetChild(i).gameObject.GetComponent<SDFBrush>());
                        brushes.Add(CreateBrush(transform.GetChild(i).gameObject.GetComponent<SDFBrush>()));
                    }
                }

                if(brushes.Count >= 2)
                {
                    if(myBrushes[0].transform.GetSiblingIndex() > myBrushes[1].transform.GetSiblingIndex())
                    {
                        SDFBrush tmp = myBrushes[0];
                        myBrushes[0] = myBrushes[1];
                        myBrushes[1] = tmp;
                    }
                }
            }

            ForceRegenMesh();
        }

        /// <summary>
        /// Add a new brush to the mesh generator / sculpt.
        /// </summary>
        /// <param name="brush">The brush to be added.</param>
        /// <param name="brushIndex">The sibling index of the brush so the mesh generator knows the brush order.</param>
        public void AddBrush (SDFBrush brush, int brushIndex)
        {
            if(startTimeDelay < 1)
                return;
            if(myBrushes.Count == 0)
            {
                myBrushes.Add(brush);
                brushes.Add(CreateBrush(brush));
                return;
            }
            for (int i = brushIndex - 1; i >= 0; i--)
            {
                if(transform.GetChild(i).gameObject.activeInHierarchy)
                {
                    if(transform.GetChild(i).gameObject.GetComponent<SDFBrush>())
                    {
                        int bI = myBrushes.IndexOf(transform.GetChild(i).gameObject.GetComponent<SDFBrush>());
                        if(bI > 0)
                        {
                            if(bI < myBrushes.Count)
                            {
                                myBrushes.Insert(bI + 1, brush);
                                brushes.Insert(bI + 1, CreateBrush(brush));
                            }
                            else
                            {
                                myBrushes.Add(brush);
                                brushes.Add(CreateBrush(brush));
                            }
                            break;
                        }
                    }
                }
                if(i == 0)
                {
                    myBrushes.Insert(0, brush);
                    brushes.Insert(0, CreateBrush(brush));
                }
            }

            if(brushes.Count >= 2)
            {
                if(myBrushes[0].transform.GetSiblingIndex() > myBrushes[1].transform.GetSiblingIndex())
                {
                    SDFBrush tmp = myBrushes[0];
                    myBrushes[0] = myBrushes[1];
                    myBrushes[1] = tmp;
                }
            }
            
        }

        /// <summary>
        /// Remove a brush from the mesh generator / sculpt. 
        /// </summary>
        /// <param name="brush">The brush to be removed.</param>
        public void RemoveBrush (SDFBrush brush)
        {
            if(startTimeDelay < 1)
                return;
            if(myBrushes.Contains(brush) == false)
                return;
            int brushIndex = myBrushes.IndexOf(brush);
            if(myBrushes.Count > brushIndex)
            {
                if(myBrushes[brushIndex] == brush)
                {
                    myBrushes.RemoveAt(brushIndex);
                    brushes.RemoveAt(brushIndex);
                }
            }

        }

        /// <summary>
        /// Called after a brush’s properties have been changed and the sculpt needs to be updated. 
        /// </summary>
        /// <param name="brush">The brush that needs to be updated.</param>
        public void UpdateBrush (SDFBrush brush)
        {
            if(startTimeDelay < 1)
                return;
            //Debug.Log("UPDATE");
            brushes[myBrushes.IndexOf(brush)] = CreateBrush(brush);
        }

        /// <summary>
        /// Called after remove brush, add brush, update brush, and after cancelling an edit.
        /// </summary>
        public void TriggerMeshUpdate ()
        {
            liveUpdate = true;
        }

        //Thread myThread1;
        Thread myThread0;

        Thread myThreadLOD1;
        Thread myThreadLOD2;
        bool LOD1done;
        bool LOD2done;

        //bool done1;
        bool done0;
        bool done512 = false;

        int defineSDFProgress = 0;
        int generateMeshProgress = 0;

        void DefineSDF ()
        {
            //GetComponent<MeshFilter>().mesh = null;
            if(brushes.Count <= 0)
            {
                //queueUpdate = false;
                //updateMesh = false;
                //liveUpdateDelay = 0;
                //liveUpdate = false;
                //GetComponent<MeshFilter>().mesh = null;
                return;
            }

            int resolution = 0;
            if(Thread.CurrentThread == myThread0)
            {
                resolution = resolutionLOD0;
            }
            if(Thread.CurrentThread == myThreadLOD1)
            {
                resolution = resolutionLOD1;
            }
            if(Thread.CurrentThread == myThreadLOD2)
            {
                resolution = resolutionLOD2;
            }

            /*sdf16 = new RenderTexture(16, 16, 0);
            sdf16.enableRandomWrite = true;
            sdf16.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            sdf16.volumeDepth = 16;
            sdf16.Create();

            sdf64 = new RenderTexture(64, 64, 0);
            sdf64.enableRandomWrite = true;
            sdf64.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            sdf64.volumeDepth = 64;
            sdf64.Create();*/

            sdf256 = new RenderTexture(meshResolution, meshResolution, 0, RenderTextureFormat.RGHalf);
            sdf256.enableRandomWrite = true;
            sdf256.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            sdf256.volumeDepth = meshResolution;
            sdf256.Create();

            sdfOverlap = new RenderTexture(brushes.Count, brushes.Count, 0);
            sdfOverlap.enableRandomWrite = true;
            sdfOverlap.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
            sdfOverlap.Create();

            sdfOverlapFull = new RenderTexture(brushes.Count, brushes.Count, 0);
            sdfOverlapFull.enableRandomWrite = true;
            sdfOverlapFull.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
            sdfOverlapFull.Create();


            //ComputeBuffer ooBuffer = new ComputeBuffer(orderOfOperations.Count, sizeof(int));
            //ooBuffer.SetData(orderOfOperations);
    //
            //ComputeBuffer boxBuffer = new ComputeBuffer(boxes.Count, sizeof(float) * 4 * 4 + sizeof(float) * 8 + sizeof(int));
            //boxBuffer.SetData(boxes);
    //
            //ComputeBuffer sphereBuffer = new ComputeBuffer(spheres.Count, sizeof(float) * 9 + sizeof(int));
            //sphereBuffer.SetData(spheres);

            ComputeBuffer brushBuffer = new ComputeBuffer(brushes.Count, sizeof(float) * 4 * 4 + sizeof(float) * 24 + sizeof(int) * 3);
            brushBuffer.SetData(brushes);

            //myComputeShader.SetBuffer(kid1, "orderOfOperations", ooBuffer);
            //myComputeShader.SetBuffer(kid1, "boxes", boxBuffer);
            //myComputeShader.SetBuffer(kid1, "spheres", sphereBuffer);
            myComputeShader.SetBuffer(kid1, "edits", brushBuffer);

            //System.Array test = System.Array.CreateInstance(typeof(Matrix4x4), 1);
            //tb.GetData(test);
            //for (int i = 0; i < test.Length; i++)
            //{
            //    Debug.Log(test.GetValue(i));
            //}

            myComputeShader.SetInt("Resolution", 16);
            float adjustedScale = scale;
            //adjustedScale *= Mathf.Min(mySculpt.transform.localScale.x, mySculpt.transform.localScale.y, mySculpt.transform.localScale.z);
            myComputeShader.SetFloat("scale", adjustedScale);
            
            //if(originAtBottom)
            //    myComputeShader.SetVector("origin", new Vector4(origin.x + transform.position.x, origin.y + (adjustedScale/2) + transform.position.y, origin.z + transform.position.z, 0));
            //else
            //    myComputeShader.SetVector("origin", new Vector4(origin.x + transform.position.x, origin.y + transform.position.y, origin.z + transform.position.z, 0));

            if(mySculpt.originAtBottom)
            {
                myComputeShader.SetVector("origin", new Vector4(mySculpt.transform.position.x,
                    (adjustedScale/2) + mySculpt.transform.position.y,
                    mySculpt.transform.position.z, 0));
            }
            else
            {
                myComputeShader.SetVector("origin", new Vector4(mySculpt.transform.position.x,
                    mySculpt.transform.position.y,
                    mySculpt.transform.position.z, 0));
            }
            //myComputeShader.SetVector("origin", new Vector4(transform.position.x - adjustedScale/2,
            //    transform.position.y - adjustedScale/2,
            //    transform.position.z - adjustedScale/2, 0));

            //myComputeShader.SetTexture(kid1, "Result16", sdf16);
            //myComputeShader.SetTexture(kid1, "Result64", sdf64);
            myComputeShader.SetTexture(kid1, "Result256", sdf256);

            myComputeShader.SetTexture(kid2, "Result256", sdf256);
            //myComputeShader.SetTexture(kid3, "Result256", sdf256);

            myComputeShader.SetTexture(kid1, "overlap", sdfOverlap);
            myComputeShader.SetTexture(kid2, "overlap", sdfOverlap);
            //myComputeShader.SetTexture(kid3, "overlap", sdfOverlap);

            myComputeShader.SetTexture(kid1, "noiseTexture", noise);
            myComputeShader.SetTexture(kid2, "noiseTexture", noise);
            //myComputeShader.SetTexture(kid3, "noiseTexture", noise);

            for (int i = 0; i < kids.Length; i++)
            {
                if(i == 0)
                {
                    myComputeShader.SetTexture(kids[i], "Result256", sdf256);
                    myComputeShader.SetTexture(kids[i], "overlap", sdfOverlap);
                    myComputeShader.SetTexture(kids[i], "noiseTexture", noise);
                }
            }

            myComputeShader.SetBuffer(myComputeShader.FindKernel("CSBrushOverlap"), "edits", brushBuffer);
            myComputeShader.SetTexture(myComputeShader.FindKernel("CSBrushOverlap"), "overlap", sdfOverlap);
            if(brushes.Count < 8)
            {
                myComputeShader.Dispatch(myComputeShader.FindKernel("CSBrushOverlap"),brushes.Count,1,1);
            }
            else
            {
                myComputeShader.Dispatch(myComputeShader.FindKernel("CSBrushOverlap"),
                Mathf.CeilToInt(brushes.Count/8) + 1, Mathf.CeilToInt(brushes.Count/8) + 1,1);
            }

            ComputeBuffer counterLocal = new ComputeBuffer(1,4, ComputeBufferType.Counter);
            counterLocal.SetCounterValue(0);
            counterLocal.SetData(new uint[] {0});
            myComputeShader.SetBuffer(kid1, "counter", counterLocal);

            //myComputeShader.Dispatch(kid1,2,2,2);

            //myComputeShader.SetInt("Resolution", 64);
            //myComputeShader.Dispatch(kid1,8,8,8);

            myComputeShader.SetInt("Resolution", meshResolution);
            myComputeShader.Dispatch(kid1,meshResolution/8,meshResolution/8,meshResolution/8);

            System.Array faceCountOutputArray = System.Array.CreateInstance(typeof(uint), counterLocal.count);
            counterLocal.GetData(faceCountOutputArray);
            int faceCountOutput = (int)((UInt32)(faceCountOutputArray.GetValue(0)));
            if(debugMeshData)
                Debug.Log("Voxels: " + faceCountOutput.ToString());

            counterLocal.Release();
            

            //cmd.SetGlobalTexture("_GIVolumeFront", giVolumeTextureFront);
            //cmd.SetGlobalTexture("_GIVolumeBack", giVolumeTextureBack);

            //ooBuffer.Release();
            //boxBuffer.Release();
            //sphereBuffer.Release();
            brushBuffer.Release();


            PreGenerateMesh();

            /*for (int i = 0; i < 512; i++)
            {
                Thread t = new Thread (AddToData);
                t.Start(new AddToDataStruct(resolution, i));
            }*/

            /*for (int i = 0; i < resolution; i++)
            {
                for (int j = 0; j < resolution; j++)
                {
                    for (int k = 0; k < resolution; k++)
                    {

                        if(GetSDFDistance(new Vector3(i,j,k) / resolution * scale) <= 0)
                        {
                            if(Thread.CurrentThread == myThread0)
                            {
                                if(dataLOD0.ContainsKey(new Vector3(i,j,k) / resolution * scale) == false)
                                {
                                    dataLOD0.Add(new Vector3(i,j,k) / resolution * scale, new Voxel() {Id = 1});
                                }
                            }
                            if(Thread.CurrentThread == myThreadLOD1)
                            {
                                if(dataLOD1.ContainsKey(new Vector3(i,j,k) / resolution * scale) == false)
                                {
                                    dataLOD1.Add(new Vector3(i,j,k) / resolution * scale, new Voxel() {Id = 1});
                                }
                            }
                            if(Thread.CurrentThread == myThreadLOD2)
                            {
                                if(dataLOD2.ContainsKey(new Vector3(i,j,k) / resolution * scale) == false)
                                {
                                    dataLOD2.Add(new Vector3(i,j,k) / resolution * scale, new Voxel() {Id = 1});
                                }
                            }
                        }
                        defineSDFProgress += 1;
                    }
                }
            }*/

        }

        void PreGenerateMesh ()
        {
            int resolution = resolutionLOD0;
            if(Thread.CurrentThread == myThread0)
            {
                resolution = resolutionLOD0;
            }
            if(Thread.CurrentThread == myThreadLOD1)
            {
                resolution = resolutionLOD1;
            }
            if(Thread.CurrentThread == myThreadLOD2)
            {
                resolution = resolutionLOD2;
            }

            //if(Thread.CurrentThread == myThread0)
            //{
            GenerateMesh(meshDataLOD0, myData, resolution);
            //}

            //if(Thread.CurrentThread == myThreadLOD1)
            //    GenerateMesh(meshDataLOD1, dataLOD1, resolution);
    //
            //if(Thread.CurrentThread == myThreadLOD2)
            //    GenerateMesh(meshDataLOD2, dataLOD2, resolution);
        }

        /*struct AddToDataStruct
        {
            public int resolution;
            public int index;

            public AddToDataStruct(int resolution, int index)
            {
                this.resolution = resolution;
                this.index = index;
            }
        };

        public void AddToData (object data)
        {
            int resolution = ((AddToDataStruct)(data)).resolution;
            int index = ((AddToDataStruct)(data)).index;

            int xOffset = 0;
            int yOffset = 0;
            int zOffset = 0;

            if(index >= 8)
            {
                if(index >= 64)
                {
                    xOffset = index % 8;
                    yOffset = Mathf.FloorToInt((float)index / 8f) % (8);
                    zOffset = Mathf.FloorToInt((float)index / 8f / 8f);
                }
                else
                {
                    xOffset = index % 8;
                    yOffset = Mathf.FloorToInt((float)index / 8f);
                }
            }
            else
            {
                xOffset = index;
            }

            xOffset *= Mathf.FloorToInt(resolution/8);
            yOffset *= Mathf.FloorToInt(resolution/8);
            zOffset *= Mathf.FloorToInt(resolution/8);

            //if(yOffset > 512)
            //    Debug.Log(yOffset);

            for (int i = 0; i < Mathf.FloorToInt(resolution / 8); i++)
            {
                for (int j = 0; j < Mathf.FloorToInt(resolution / 8); j++)
                {
                    for (int k = 0; k < Mathf.FloorToInt(resolution / 8); k++)
                    {
                        //Debug.Log(new Vector3(i + xOffset,j + yOffset,k + zOffset));
                        if(GetSDFDistance(new Vector3(i + xOffset,j + yOffset,k + zOffset) / resolution * scale) <= 0)
                        {
                            //if(dataLOD0.ContainsKey(new Vector3(i + xOffset,j + yOffset,k + zOffset) / resolution * scale) == false)
                            //{
                            myData[IndexFromCoord(new Vector3(i + xOffset,j + yOffset,k + zOffset), resolution)] = 1;
                            //}
                        }
                    }
                }
            }

            defineSDFProgress += 1;
        }

        
        public float GetSDFDistance (Vector3 querryPos)
        {
            float output = 0;
            int bIndex = 0;
            int sIndex = 0;
            //int test = 0;
            for (int i = 0; i < orderOfOperations.Count; i++)
            {
                if(orderOfOperations[i] == 0)
                {
                    if(i == 0)
                        output = SDFBox(querryPos, boxes[bIndex].transform, boxes[bIndex].scale);
                    else
                        output = Union(output, SDFBox(querryPos, boxes[bIndex].transform, boxes[bIndex].scale), boxes[bIndex].blend);
                    bIndex += 1;
                }
                else if(orderOfOperations[i] == 1)
                {
                    if(i == 0)
                        output = SDFSphere(querryPos, spheres[sIndex].position, spheres[sIndex].scale);
                    else
                        output = Union(output, SDFSphere(querryPos, spheres[sIndex].position, spheres[sIndex].scale), spheres[sIndex].blend);
                    //if(Union(output, SDFSphere(querryPos, spheres[sIndex].position, spheres[sIndex].scale), spheres[sIndex].blend) <= 0)
                    //    test += 1;
                    sIndex += 1;
                }
            }

            return output;
        }

        public Color GetSDFColor (Vector3 querryPos)
        {
            float output = 9999999;
            Color outputColor = Color.black;
            int bIndex = 0;
            int sIndex = 0;
            float sdfOut = 0;
            for (int i = 0; i < orderOfOperations.Count; i++)
            {
                if(orderOfOperations[i] == 0)
                {
                    sdfOut = SDFBox(querryPos, boxes[bIndex].transform, boxes[bIndex].scale);
                    output = Union(output, sdfOut, boxes[bIndex].blend);
                    outputColor = Color.Lerp(boxes[bIndex].color, outputColor, sdfOut);
                    bIndex += 1;
                }
                else if(orderOfOperations[i] == 1)
                {
                    sdfOut = SDFSphere(querryPos, spheres[sIndex].position, spheres[sIndex].scale);
                    output = Union(output, sdfOut, spheres[sIndex].blend);
                    outputColor = Color.Lerp(spheres[sIndex].color, outputColor, sdfOut);
                    sIndex += 1;
                }
            }
            return outputColor;
        }

        public Vector3 GetSDFGradient (Vector3 querryPos)
        {
            const float eps = 0.01f;
            return Vector3.Normalize (new Vector3(   
                GetSDFDistance(querryPos + new Vector3(eps, 0, 0)) - GetSDFDistance(querryPos - new Vector3(eps, 0, 0)),
                GetSDFDistance(querryPos + new Vector3(0, eps, 0)) - GetSDFDistance(querryPos - new Vector3(0, eps, 0)),
                GetSDFDistance(querryPos + new Vector3(0, 0, eps)) - GetSDFDistance(querryPos - new Vector3(0, 0, eps))
            ));
        }

        public float SDFBox (Vector3 wp, Matrix4x4 t, Vector3 scale)
        {
            Vector3 p = t.inverse.MultiplyPoint3x4(wp);
            Vector3 q = new Vector3(Mathf.Abs(p.x), Mathf.Abs(p.y), Mathf.Abs(p.z)) - scale/2;
            return Vector3.Magnitude(Vector3.Max(q, Vector3.zero)) + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y,q.z)), 0);
        }

        public float SDFSphere (Vector3 wp, Vector3 t, float scale)
        {
            return Vector3.Distance(t, wp) - scale/2;
        }

        float Union( float valueA, float valueB, float lerp ) 
        {
            //float h = Mathf.Max(lerp - Mathf.Abs(valueA - valueB), 0.0f ) / lerp;
            //return Mathf.Min(valueA, valueB) - h * h * lerp * (1.0f / 4.0f);

            float h = Mathf.Clamp( 0.5f + 0.5f * (valueB - valueA) / lerp, 0.0f, 1.0f );
            return Mathf.Lerp( valueB, valueA, h ) - lerp * h * (1.0f - h);
            //return Mathf.Min(valueA, valueB);

            //float res = Mathf.Exp(-lerp*valueA) + Mathf.Exp(-lerp*valueB);
            //return -Mathf.Log(Mathf.Max(0.0001f,res)) / lerp;
        }

        float Subtraction( float valueA, float valueB, float lerp ) 
        {
            float h = Mathf.Clamp( 0.5f - 0.5f * (valueB+valueA)/lerp, 0.0f, 1.0f );
            return Mathf.Lerp( valueB, -valueA, h ) + lerp * h * (1.0f - h); 
        }

        float Intersection( float valueA, float valueB, float lerp ) 
        {
            float h = Mathf.Clamp( 0.5f - 0.5f * (valueB-valueA) / lerp, 0.0f, 1.0f );
            return Mathf.Lerp( valueB, valueA, h ) + lerp * h * (1.0f - h); 
        }*/

        // Update is called once per frame
        //void Update()
        //{
        //    
        //}
        void Update ()
        {
            //Debug.Log(defineSDFProgress);
            if(myThread0 != null)
            {
                if(defineSDFProgress >= 512 && myThread0.IsAlive == false && done0 == false && done512 == false)
                {
                    //Debug.Log("MID");
                    myThread0 = new Thread(PreGenerateMesh);
                    myThread0.Start();
                    done512 = true;
                }

                //if(generateMeshProgress >= 512 && myThread0.IsAlive == false && done0 == false && done512 == true)
                //{
                //    UploadMesh(0);
                //    done0 = true;
                //}
            }

            //if(myThreadLOD1 != null)
            //{
            //    if(myThreadLOD1.IsAlive == false && LOD1done == false)
            //    {
            //        UploadMesh(1);
            //        LOD1done = true;
            //    }
            //}

            //if(myThreadLOD2 != null)
            //{
            //    if(myThreadLOD2.IsAlive == false && LOD2done == false)
            //    {
            //        UploadMesh(2);
            //        LOD2done = true;
            //    }
            //}

            if(liveUpdateDelay > 0.1f && brushes.Count > 0)
            {
                if(updateMesh == false)
                {
                    GetBrushes();
                    DefineSDF();
                    updateMesh = true;
                }
                else
                {
                    GetBrushes();
                    queueUpdate = true;
                }
                liveUpdateDelay = 0;
                liveUpdate = false;
            }
            else if(brushes.Count <= 0)
            {
                GetComponent<MeshFilter>().mesh = null;
                liveUpdateDelay = 0;
                liveUpdate = false;
            }

            if(startTimeDelay < 1)
            {
                startTimeDelay += Time.deltaTime;
            }
            else
            {
                for (int i = 0; i < myBrushes.Count; i++)
                {
                    if(myBrushes[i].transform.hasChanged)
                    {
                        if(myBrushes[i].brushShape == SDFBrush.SDFBrushShape.Sphere)
                            myBrushes[i].transform.localScale = Vector3.one * myBrushes[i].transform.localScale.x;
                        
                        UpdateBrush(myBrushes[i]);
                        myBrushes[i].transform.hasChanged = false;
                        liveUpdate = true;
                    }
                    else if(myBrushes[i].transform.childCount > 0)
                    {
                        for (int j = 0; j < myBrushes[i].transform.childCount; j++)
                        {
                            if(myBrushes[i].transform.GetChild(j).hasChanged)
                            {
                                myBrushes[i].transform.GetChild(j).localScale = Vector3.one * myBrushes[i].transform.GetChild(j).localScale.x;
                                //myBrushes[i].transform.GetChild(j).GetComponent<SphereCollider>().radius = myBrushes[i].transform.GetChild(j).localScale.x;
                                UpdateBrush(myBrushes[i]);
                                myBrushes[i].transform.GetChild(j).hasChanged = false;
                                liveUpdate = true;
                                break;
                            }
                        }
                    }

                    if(liveUpdate && myBrushes[i].transform.childCount > 0)
                    {
                        //Update Box Collider of Curve
                        Vector3 endPoint = (myBrushes[i].transform.position + myBrushes[i].transform.GetChild(0).transform.position +
                        myBrushes[i].transform.GetChild(1).transform.position) / 3;

                        myBrushes[i].gameObject.GetComponents<BoxCollider>()[1].center = Vector3.Lerp(
                            myBrushes[i].transform.position, endPoint, 0.2f) - myBrushes[i].transform.position;
                        myBrushes[i].gameObject.GetComponents<BoxCollider>()[2].center = Vector3.Lerp(
                            myBrushes[i].transform.position, endPoint, 0.45f) - myBrushes[i].transform.position;
                        myBrushes[i].gameObject.GetComponents<BoxCollider>()[3].center = Vector3.Lerp(
                            myBrushes[i].transform.position, endPoint, 0.7f) - myBrushes[i].transform.position;

                        myBrushes[i].gameObject.GetComponents<BoxCollider>()[1].center /= myBrushes[i].transform.localScale.x;
                        myBrushes[i].gameObject.GetComponents<BoxCollider>()[2].center/= myBrushes[i].transform.localScale.x;
                        myBrushes[i].gameObject.GetComponents<BoxCollider>()[3].center /= myBrushes[i].transform.localScale.x;
                    }
                }
            }
                
            if(liveUpdate)
                liveUpdateDelay += Time.deltaTime;

            //if(startReadingMeshBuffers == 1)
            //{
            //    //StartCoroutine(ReadMeshBuffers());
            //    //ReadMeshBuffers();
            //}

            if(createLODs && lodsCreated > 0)
            {
                lodsCreated -= 1;
                createLODs = false;
                DefineSDF();
            }
        }

        /// <summary>
        /// Called when you press the regenerate mesh button and internally in the reorder brush. Has more error checking than TriggerMeshUpdate().  
        /// </summary>
        public void ForceRegenMesh ()
        {
            if(errorUpdate)
            {
                updateMesh = false;
                queueUpdate = false;
                errorUpdate = false;
            }
            
            if(brushes.Count <= 0)
            {
                GetComponent<MeshFilter>().mesh = null;
                liveUpdateDelay = 0;
                liveUpdate = false;
            }

            if(updateMesh == false)
            {
                GetBrushes();
                DefineSDF();
                updateMesh = true;
            }
            else
            {
                GetBrushes();
                queueUpdate = true;
            }
            liveUpdateDelay = 0;
            liveUpdate = false;
        }

        void OnDestroy ()
        {
            if(vertexBuffer != null)
                vertexBuffer.Release();
            if(triangleBuffer != null)
                triangleBuffer.Release();
            if(uvBuffer != null)
                uvBuffer.Release();
            if(normalBuffer != null)
                normalBuffer.Release();
            if(colorBuffer != null)
                colorBuffer.Release();
            if(brushBuffer != null)
                brushBuffer.Release();
            if(counter != null)
                counter.Release();
            if(faceCounter != null)
                faceCounter.Release();
        }
    }
}
