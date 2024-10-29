using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SDFSculptor
{
    public class SDFBrush : MonoBehaviour
    {
        public enum SDFBrushType
        {
            Add,
            Subtract,
            InvertedIntersection,
            Intersection,
            Paint
        };

        public enum SDFBrushShape
        {
            Sphere,
            Box,
            Cylinder,
            Cone,
            Torus,
            Ellipsoid,
            RoundedBox,
            RoundedCylinder,
            RoundedCone,
            TriangularPrism,
            HexagonalPrism,
            Pyramid,
            Link,
            BezierCurve
        };

        [Tooltip("How the brush affects the sculpt.")]
        public SDFBrushType brushType;
        [Tooltip("The shape of the brush.")]
        public SDFBrushShape brushShape;
        [Tooltip("Color of the brush.")]
        public Color color = Color.white;


        [Tooltip("Roundness of the brush (not all brush shapes use this).")]
        public float roundA;
        [HideInInspector]
        public float roundB;

        [Tooltip("The scale of the warp effect.")]
        public float warpScale;
        [Tooltip("The strength of the warp effect.")]
        public float warpStrength;
        [Tooltip("Is the warp pattern determined by a texture or a math function (texture is more random looking).")]
        public bool textureWarp;

        [Tooltip("Blend of the brush.")]
        [Range(0f, 10f)]
        public float blend;
        
        // Start is called before the first frame update
        void Start()
        {
            GetComponent<MeshRenderer>().enabled = false;
            for (int i = 0; i < transform.childCount; i++)
            {
                if(transform.GetChild(i).gameObject.GetComponent<MeshRenderer>().enabled)
                    transform.GetChild(i).gameObject.GetComponent<MeshRenderer>().enabled = false;
            }
        }

        // Update is called once per frame
        //void Update()
        //{
        //    
        //}

        void OnEnable ()
        {
            //if(gameObject.GetComponent<BoxCollider>())
            //{
            //    brushShape = SDFBrushShape.Box;
            //}
            //else if(gameObject.GetComponent<SphereCollider>())
            //{
            //    brushShape = SDFBrushShape.Sphere;
            //}
            if(transform.parent.gameObject.GetComponent<MeshGenerator>())
                transform.parent.gameObject.GetComponent<MeshGenerator>().AddBrush(this, transform.GetSiblingIndex());
        }

        void OnDisable ()
        {
            if(transform.parent.gameObject.GetComponent<MeshGenerator>())
                transform.parent.gameObject.GetComponent<MeshGenerator>().RemoveBrush(this);//, transform.GetSiblingIndex());
        }
    }
}
