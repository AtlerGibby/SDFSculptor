using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SDFSculptor
{
    public class SurfaceSpawner : MonoBehaviour
    {
        [Tooltip("Compute shader for generating random points.")]
        public ComputeShader myComputeShader;
        [Tooltip("Noise Texture used by compute shader.")]
        public Texture noise;

        [Header("Spawning Options")]
        [Tooltip("How many objects to spawn per triangle on a mesh's surface.")]
        [Range(1,100)]
        public int spawnDensity = 1;
        [Tooltip("Scale of the objects to spawn.")]
        public Vector3 spawnScale = Vector3.one;
        [Tooltip("The object to spawn.")]
        public GameObject spawnObject;
        [Tooltip("The object to spawn on (Must have a MeshFiler/Mesh)(Mesh needs Read/Write enabled).")]
        public GameObject surfaceObject;
        [Tooltip("Only spawn objects inside the bounds of this Surface Spawner.")]
        public bool onlyInBounds;

        Mesh surface;
        Vector3 lastPos;

        [Header("Spawn Direction")]
        [Tooltip("Only spawn objects on parts of the mesh that face a certain direction.")]
        public bool onlyInOneDirection;
        [Tooltip("The direction a triangle needs to face for objects to spawn on it.")]
        public Vector3 direction = Vector3.up;
        [Tooltip("In degrees, how different a triangle's normal can be and still have objects spawn on it.")]
        public float degreeError = 45;


        //[Tooltip("How the brush affects the sculpt.")]
        //public SpawnType brushType;

        [HideInInspector]
        public List<Matrix4x4> spawnTransforms = new List<Matrix4x4>();


        // Start is called before the first frame update
        //void Start()
        //{
        //    
        //}

        // Update is called once per frame
        //void Update()
        //{
        //    
        //}

        /// <summary>
        /// Spawns "spawn objects" on the "surface object" (assuming you have set these variables) using the surface spawner's parameters.
        /// </summary>
        /// <param name="randomOffset">Pass a random float to get randomized placement This can be the current time for example.</param>
        public GameObject[] Spawn(float randomOffset)
        {
            if(spawnObject != null && surfaceObject != null)
            {
                surface = surfaceObject.GetComponent<MeshFilter>().sharedMesh;
                CalculateSpawnTransforms(randomOffset);
                GameObject[] output = SpawnSculptsAfterCalculatons();
                return output;
            }
            else
            {
                return null;
            }
        }

        void CalculateSpawnTransforms(float randomOffset)
        {
            int triCount = Mathf.CeilToInt(surface.triangles.Length * spawnDensity * (1.0f/3.0f));

            ComputeBuffer fTris = new ComputeBuffer(surface.triangles.Length, sizeof(int));
                fTris.SetData(surface.triangles);
                myComputeShader.SetBuffer(myComputeShader.FindKernel("OnMeshSurface"), "triangleBuffer", fTris);
            ComputeBuffer fVerts = new ComputeBuffer(surface.vertices.Length, sizeof(float) * 3);
                fVerts.SetData(surface.vertices);
                myComputeShader.SetBuffer(myComputeShader.FindKernel("OnMeshSurface"), "vertexBuffer", fVerts);
            ComputeBuffer fNorms = new ComputeBuffer(surface.normals.Length, sizeof(float) * 3);
                fNorms.SetData(surface.normals);
                myComputeShader.SetBuffer(myComputeShader.FindKernel("OnMeshSurface"), "normalBuffer", fNorms);

            ComputeBuffer sPos = new ComputeBuffer(triCount, sizeof(float) * 3);
                sPos.SetData(new Vector3[triCount]);
                myComputeShader.SetBuffer(myComputeShader.FindKernel("OnMeshSurface"), "spawnPosBuffer", sPos);
            ComputeBuffer sNorms = new ComputeBuffer(triCount, sizeof(float) * 3);
                sNorms.SetData(new Vector3[triCount]);
                myComputeShader.SetBuffer(myComputeShader.FindKernel("OnMeshSurface"), "spawnNormBuffer", sNorms);
            ComputeBuffer boundsCheck = new ComputeBuffer(triCount, sizeof(int));
                boundsCheck.SetData(new int[triCount]);
                myComputeShader.SetBuffer(myComputeShader.FindKernel("OnMeshSurface"), "boundsCheckBuffer", boundsCheck);

            myComputeShader.SetTexture(myComputeShader.FindKernel("OnMeshSurface"), "noiseTexture", noise);

            Vector3 center = transform.position;
            Vector3 front = transform.forward * transform.lossyScale.z * 0.5f + transform.position;
            Vector3 back = -transform.forward * transform.lossyScale.z  * 0.5f+ transform.position;
            Vector3 left = -transform.right * transform.lossyScale.x  * 0.5f+ transform.position;
            Vector3 right = transform.right * transform.lossyScale.x  * 0.5f+ transform.position;
            Vector3 top = transform.up * transform.lossyScale.y * 0.5f + transform.position;
            Vector3 bottom = -transform.up * transform.lossyScale.y * 0.5f+ transform.position;

            myComputeShader.SetVector("boundsCenter", new Vector4(center.x, center.y, center.z,1));
            myComputeShader.SetVector("boundsFront", new Vector4(front.x, front.y, front.z,1));
            myComputeShader.SetVector("boundsBack", new Vector4(back.x, back.y, back.z,1));
            myComputeShader.SetVector("boundsLeft", new Vector4(left.x, left.y, left.z,1));
            myComputeShader.SetVector("boundsRight", new Vector4(right.x, right.y, right.z,1));
            myComputeShader.SetVector("boundsTop", new Vector4(top.x, top.y, top.z,1));
            myComputeShader.SetVector("boundsBottom", new Vector4(bottom.x, bottom.y, bottom.z,1));
            myComputeShader.SetVector("surfacePosition", new Vector4(surfaceObject.transform.position.x, surfaceObject.transform.position.y, surfaceObject.transform.position.z,1));

            myComputeShader.SetFloat("density", spawnDensity);
            myComputeShader.SetFloat("timeOffset", randomOffset);
            lastPos = surfaceObject.transform.position;
            surfaceObject.transform.position = Vector3.zero;
            myComputeShader.SetMatrix("objToWorld", surfaceObject.transform.worldToLocalMatrix);

            myComputeShader.Dispatch(myComputeShader.FindKernel("OnMeshSurface"),
                Mathf.Max(1,Mathf.CeilToInt(triCount/64.0f)), 1, 1);

            //Vector3[] verts = new Vector3[surface.vertices.Length];
            //Vector3[] normals = new Vector3[surface.normals.Length];
            Vector3[] spawnPos = new Vector3[triCount];
            Vector3[] spawnNormals = new Vector3[triCount];
            int[] spawnInBounds = new int[triCount];

            sPos.GetData(spawnPos);
            sNorms.GetData(spawnNormals);
            boundsCheck.GetData(spawnInBounds);
            //fVerts.GetData(verts);
            //fNorms.GetData(normals);

            Vector3 scale = Vector3.one;
            Matrix4x4 tmp = new Matrix4x4();
            spawnTransforms.Clear();

            for (int i = 0; i < Mathf.CeilToInt(surface.triangles.Length * spawnDensity * (1.0f/3.0f)); i++)
            {
                if(onlyInBounds)
                {
                    if(spawnInBounds[i] == 0)
                        continue;
                }

                tmp = Matrix4x4.TRS(spawnPos[i], Quaternion.LookRotation(spawnNormals[i]), scale);
                if(onlyInOneDirection)
                {
                    if(Vector3.Angle(spawnNormals[i], direction) < degreeError)
                        spawnTransforms.Add(tmp);
                }
                else
                {
                    spawnTransforms.Add(tmp);
                }
            }

            fTris.Release();
            fVerts.Release();
            fNorms.Release();

            sPos.Release();
            sNorms.Release();
            boundsCheck.Release();

            surfaceObject.transform.position = lastPos;

        }

        GameObject[] SpawnSculptsAfterCalculatons()
        {
            GameObject[] output = new GameObject[spawnTransforms.Count + 1];
            GameObject goParent = new GameObject(spawnObject.name + "_Parent");
            goParent.transform.parent = surfaceObject.transform;
            goParent.transform.localPosition = Vector3.zero;
            goParent.transform.localRotation = Quaternion.identity;
            goParent.transform.localScale = new Vector3(1/surfaceObject.transform.localScale.x, 1/surfaceObject.transform.localScale.y, 1/surfaceObject.transform.localScale.z); 

            for (int i = 0; i < spawnTransforms.Count; i++)
            {
                GameObject go = GameObject.Instantiate(spawnObject, spawnTransforms[i].GetPosition() + lastPos, spawnTransforms[i].rotation);// surfaceObject.transform);
                go.transform.localScale = spawnScale;
                go.transform.parent = goParent.transform;
                //go.transform.localScale = new Vector3(1/surfaceObject.transform.localScale.x, 1/surfaceObject.transform.localScale.y, 1/surfaceObject.transform.localScale.z);
                output[i] = go;
            }
            output[output.Length - 1] = goParent;
            return output;
        }
    }
}