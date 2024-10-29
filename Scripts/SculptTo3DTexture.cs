using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine;
using Unity.Collections;
using UnityEditor;

namespace SDFSculptor
{
    public class SculptTo3DTexture : MonoBehaviour
    {
        public enum SDFInfoType
        {
            Color,
            Normals,
            Distance,
        };

        [Tooltip("The Compute Shader that converts a 3D texture into a 2D texture.")]
        public ComputeShader myComputeShader;
        [Tooltip("Application.dataPath + \"/ Insert Texture Export Path /\". Make sure to include slashes at start and end.")]
        public string textureExportPath = "/SDFSculptor/Sculpt3DTexture/";
        [Tooltip("The 3D texture used to generate an SDF sculpt. Can be previewed here.")]
        public RenderTexture my3DTexture;

        [Tooltip("Blurs the generated signed distance shape in the 3D texture.")]
        [Range(0.001f, 1)]
        public float blur = 0.001f;

        [Tooltip("The type of information to get from the SDF Sculpt.")]
        public SDFInfoType infoType;


        public RenderTexture Convert3DTo2D (RenderTexture renderTexture)
        {
            int res = renderTexture.width;
            float width = Mathf.Round(Mathf.Sqrt(res));
            float height = Mathf.Ceil(res / width);
            float leftOver = (height*width) - res;

            RenderTexture newTex = new RenderTexture((int)(res * width), (int)(res * height), 0, RenderTextureFormat.ARGB32);
            newTex.enableRandomWrite = true;
            newTex.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
            newTex.Create();

            
            myComputeShader.SetFloat("Width", width);
            myComputeShader.SetFloat("Height", height);
            myComputeShader.SetFloat("LeftOver", leftOver);
            myComputeShader.SetTexture(myComputeShader.FindKernel("CSMain"), "Volume", renderTexture);
            myComputeShader.SetTexture(myComputeShader.FindKernel("CSMain"), "Result", newTex);
            myComputeShader.SetFloat("Resolution", res);

            myComputeShader.Dispatch(myComputeShader.FindKernel("CSMain"), ((int)(res * width))/8, ((int)(res * height))/8, 1);
            return newTex;
        }

        public void Update3DTexture (RenderTexture renderTexture)
        {
            my3DTexture = renderTexture;
        }
    }
}