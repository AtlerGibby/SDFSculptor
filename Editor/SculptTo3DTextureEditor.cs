using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine;
using Unity.Collections;
using UnityEditor;
using System.IO;

namespace SDFSculptor
{
    [CustomEditor(typeof(SculptTo3DTexture)), CanEditMultipleObjects]
    public class SculptTo3DTextureEditor : Editor
    {
        //public string textureExportPath = "/SDFSculptor/Sculpt3DTexture/";
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

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            SculptTo3DTexture myScript = (SculptTo3DTexture)target;
            if(GUILayout.Button(new GUIContent("Export 3D Texture", EditorGUIUtility.FindTexture( "SaveAs" ), "In Play Mode, after editing a sculpt, this componenet saves the 3D texture generated to allow you to export it as an asset.")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    if(myScript.gameObject.GetComponent<SDFSculpt>())
                    {
                        if(myScript.my3DTexture != null)
                        {
                            ExportTexture3D(myScript.my3DTexture);
                        }
                        else
                        {
                            Debug.Log("No 3D texture to export, start editing the sculpt to generate this data.");
                        }
                    }
                    else
                    {
                        Debug.Log("No SDFSculpt component attached to this GameObject.");
                    }
                }
                else
                {
                    Debug.Log("Must be in Play mode to activate this.");
                }
            }

            if(GUILayout.Button(new GUIContent("Export PNG Texture", EditorGUIUtility.FindTexture( "SaveAs" ), "In Play Mode, after editing a sculpt, this componenet saves the 3D texture generated as a PNG texture.")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    if(myScript.gameObject.GetComponent<SDFSculpt>())
                    {
                        if(myScript.my3DTexture != null)
                        {
                            ExportTexturePNG(myScript.my3DTexture);
                        }
                        else
                        {
                            Debug.Log("No 3D texture to export, start editing the sculpt to generate this data.");
                        }
                    }
                    else
                    {
                        Debug.Log("No SDFSculpt component attached to this GameObject.");
                    }
                }
                else
                {
                    Debug.Log("Must be in Play mode to activate this.");
                }
            }

        }

        void ExportTexture3D (RenderTexture renderTexture)
        {
            SculptTo3DTexture myScript = (SculptTo3DTexture)target;
            int width = renderTexture.width;
            int height = renderTexture.height;
            int depth = renderTexture.volumeDepth;
            var a = new NativeArray<Color32>(width * height * depth, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); //change if format is not 8 bits (i was using R8_UNorm) (create a struct with 4 bytes etc)
            string sculptName = myScript.gameObject.GetComponent<SDFSculpt>().myName;
            AsyncGPUReadback.RequestIntoNativeArray(ref a, renderTexture, 0, (_) =>
            {
                Texture3D output = new Texture3D(width, height, depth,
                    UnityEngine.Experimental.Rendering.DefaultFormat.LDR,
                    UnityEngine.Experimental.Rendering.TextureCreationFlags.None);
                output.SetPixelData(a, 0);
                output.Apply(false, false);
                AssetDatabase.CreateAsset(output, $"Assets" + myScript.textureExportPath + sculptName + ".asset");
                AssetDatabase.SaveAssetIfDirty(output);
                a.Dispose();
                renderTexture.Release();
            });
        }

        void ExportTexturePNG (RenderTexture renderTexture)
        {
            SculptTo3DTexture myScript = (SculptTo3DTexture)target;
            RenderTexture newTex = myScript.Convert3DTo2D(renderTexture);
            Texture2D tex;

            string sculptName = myScript.gameObject.GetComponent<SDFSculpt>().myName;
            tex = new Texture2D(newTex.width, newTex.height, TextureFormat.ARGB32, false, false);
            Rect rect = new Rect(0, 0, newTex.width, newTex.height);
            RenderTexture.active = newTex;
            tex.ReadPixels(rect, 0, 0, false);

            File.WriteAllBytes($"Assets" + myScript.textureExportPath + sculptName + ".png", tex.EncodeToPNG());
            RenderTexture.active = null;
            newTex.Release();
            renderTexture.Release();
        }
    }
}