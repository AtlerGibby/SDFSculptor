using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

namespace SDFSculptor
{
    public class SDFSculpt : MonoBehaviour
    {
        [Header("Global SDF Sculpt Properties")]
        [Tooltip("Name of the sculpt, used when saving recipe and mesh files.")]
        public string myName = "MySculpt";
        [Tooltip("Application.dataPath + \"/ Insert Recipe Path /\". Make sure to include slashes at start and end.")]
        public string myRecipePath = "/SDFSculptor/SculptBrushData/";
        [Tooltip("Application.dataPath + \"/ Insert Mesh Path /\". Make sure to include slashes at start and end.")]
        public string myMeshPath = "/SDFSculptor/SculptMeshData/";
        [Tooltip("Resolution of Sculpt. Best to keep everything in powers of 2: 16, 32, 64, 128, 256. Resolution of LOD0.")]
        [Range(8, 256)]
        public int meshResolution = 64;
        [Tooltip("Scale of the sculpt in terms of the raw geometry. The mesh can be scaled using the transform like normal after it is generated.")]
        [Range(0.1f, 100)]
        public float scale = 20;
        [Tooltip("Make the bottom center of the sculpting space the origin. By default, the middle is the origin.")]
        public bool originAtBottom;

        [Tooltip("When generating a mesh, this will make it fuzzy.")]
        public bool isFuzzy;
        [Tooltip("How big is the noise pattern.")]
        public float fuzzNoiseScale = 0.5f;
        [Tooltip("How strong is the effect of the noise.")]
        public float fuzzNoiseStrength = 0.5f;
        [Tooltip("The size of each fuzz quad.")]
        public float fuzzScale = 2;
        [Tooltip("A uniform offset for the fuzz rotation.")]
        [Range(0, 1)]
        public float fuzzRotationOffset = 0;
        [Tooltip("For each fuzz quad, make all normals match.")]
        public bool realignFuzzNormals;

        [Tooltip("Increase or decrease the screen coverage threshold before the first LOD transition. 1 = never uses first LOD, 0 = never transitions.")]
        [Range(0, 1)]
        public float lodFirstTransitionThreshold = 0.6f;
        [Tooltip("Increase or decrease the distance before LOD transitions are triggered.")]
        [Range(2, 500)]
        public float lodTransitionScalar = 25;
        [Tooltip("Resolution of LODs in descending order. [0] = LOD1's resolution, [1] = LOD2's resolution, and so forth.")]
        public List<int> LODResolutions = new List<int>() {32,16,8};

        [Tooltip("If this list contains any meshes, it will be used as a substitute for the sculpt / sculpt LODs. [0] = LOD0, [1] = LOD1, and so forth.")]
        public List<Mesh> customLODMeshes;

        [Tooltip("When entering edit mode, the rotation of this GameObject will rotate all brushes.")]
        public bool rotationAffectsSculpt;
        [Tooltip("Static sculpts can be culled more easily since they never move and their bounding box information stays the same.")]
        public bool isSculptStatic;

        public enum CullType
        {
            Disappear,
            LowestLOD,
            OnlyShadow,
            OnlyShadowAndLowestLOD,
        }
        [Tooltip("How to cull this sculpt. Disappear = don't render, LowestLOD = swap to lowest LOD, OnlyShadow = don't render mesh but render shadow, OnlyShadowAndLowestLOD = combine OnlyShadow and LowestLOD.")]
        public CullType cullType;

        [Header("Individual SDF Sculpt Properties")]
        [Tooltip("Color to tint the sculpt.")]
        public Color tint = Color.white;
        [Tooltip("How much to tint the sculpt.")]
        public float tintAmount = 0;
        [Tooltip("How metallic is this sculpt.")]
        public float metallic = 0.1f;
        [Tooltip("How smooth or rough the sculpt is.")]
        public float smoothness = 0.2f;
        [Tooltip("How emissive is the sculpt.")]
        public float emission = 0;

        [Tooltip("Custom Material Property A: Decide how to use this value in the SDFShader shader graph.")]
        public float customMaterialPropertyA = 0;
        [Tooltip("Custom Material Property B: Decide how to use this value in the SDFShader shader graph.")]
        public float customMaterialPropertyB = 0;
        [Tooltip("Custom Material Property C: Decide how to use this value in the SDFShader shader graph.")]
        public float customMaterialPropertyC = 0;
        [Tooltip("Custom Material Property D: Decide how to use this value in the SDFShader shader graph.")]
        public float customMaterialPropertyD = 0;
        
        [HideInInspector]
        public Matrix4x4 myTransform;
        

        [HideInInspector]
        public MeshFilter meshFilter;
        [HideInInspector]
        public List<Mesh> meshes;

        SculptSceneManager ssm;

        void Awake()
        {
            myTransform = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        }

        void Start()
        {
            meshFilter = GetComponent<MeshFilter>();
            /*if(File.Exists(Application.dataPath + myMeshPath + myName + "_MeshData.json"))
            {
                Mesh myMesh = new Mesh();
                MeshGenerator.SculptMeshStruct meshStruct = new MeshGenerator.SculptMeshStruct();

                string json = File.ReadAllText(Application.dataPath + myMeshPath + myName + "_MeshData.json");
                meshStruct = JsonUtility.FromJson<MeshGenerator.SculptMeshStruct>(json);

                myMesh.SetVertices(meshStruct.vertices);
                myMesh.SetTriangles(meshStruct.triangles, 0 , false);
                myMesh.SetUVs(0, meshStruct.uvs);
                myMesh.SetColors(meshStruct.colors);
                myMesh.SetNormals(meshStruct.normals);
                myMesh.RecalculateBounds();
                myMesh.UploadMeshData(false);
                meshFilter.mesh = myMesh;

                Destroy(GetComponent<BoxCollider>());
                gameObject.AddComponent<BoxCollider>();
            }*/
            ssm = GameObject.FindObjectOfType<SculptSceneManager>();
            ssm.AddToAllSculpts(this, GetComponent<Renderer>());
        }

        public void UpdateIsStatic ()
        {
            ssm.SculptStaticDynamicChange(this);
        }

        public void UpdateMaterialProperties ()
        {
            ssm.RecalculateSingleSculptMaterialProperties(this);
        }

        public void UpdateBoundingBox ()
        {
            ssm.RecalculateSingleStaticSculptBoundingBox(this);
        }

        public void AddToSceneManager ()
        {
            ssm.AddSculpt(this);
        }

        public void FuzzFunction ()
        {
            Mesh fuzzyMesh = GameObject.FindObjectOfType<SculptSceneManager>().GetFuzzyMesh(GetComponent<MeshFilter>().sharedMesh, fuzzNoiseScale, fuzzNoiseStrength, fuzzScale, fuzzRotationOffset, realignFuzzNormals);
            GetComponent<MeshFilter>().sharedMesh = fuzzyMesh;
        }

        public void ConvertToFuzzy ()
        {
            GameObject.FindObjectOfType<SculptSceneManager>().ConvertToFuzzy(this);
        }

        public void FlipOverAxis ( bool xFlip, bool yFlip, bool zFlip)
        {
            GameObject.FindObjectOfType<SculptSceneManager>().FlipOverAxis(this, xFlip, yFlip, zFlip);
        }


        void OnDestroy ()
        {
            if(ssm != null)
                ssm.RemoveSculpt(this);
        }

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
    }
}
