using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;


namespace SDFSculptor
{
    [CustomEditor(typeof(SDFSculpt)), CanEditMultipleObjects]
    public class SDFSculptEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            SDFSculpt myScript = (SDFSculpt)target;
            if(GUILayout.Button(new GUIContent("Update Is Static Value", EditorGUIUtility.FindTexture( "Audio Mixer" ), "In Play Mode, update Sculpt Scene Manager that \"IsSculptStatic\" changed.")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    myScript.UpdateIsStatic();
                }
                else
                {
                    Debug.Log("Must be in Play mode to activate this.");
                }
            }

            if(GUILayout.Button(new GUIContent("Update Material Properties", EditorGUIUtility.FindTexture( "d_TreeEditor.Material" ), "In Play Mode, update Sculpt Scene Manager that this sculpt's material properties changed (Only necessary if this sculpt isn't being Instanced).")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    myScript.UpdateMaterialProperties();
                }
                else
                {
                    Debug.Log("Must be in Play mode to activate this.");
                }
            }

            if(GUILayout.Button(new GUIContent("Update Bounding Box", EditorGUIUtility.FindTexture( "d_Grid.BoxTool" ), "In Play Mode, update Sculpt Scene Manager that the transform or shape of this sculpt has changed.")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    myScript.UpdateBoundingBox();
                }
                else
                {
                    Debug.Log("Must be in Play mode to activate this.");
                }
            }

            if(GUILayout.Button(new GUIContent("Add to Sculpt Scene Manager", EditorGUIUtility.FindTexture( "d_Toolbar Plus" ), "In Play Mode, add this sculpt to the sculpt scene manager if it is a new sculpt. The manager doesn't automatically know if a new sculpt is made so you have to notify it.")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    myScript.AddToSceneManager();
                }
                else
                {
                    Debug.Log("Must be in Play mode to activate this.");
                }
            }

            if(GUILayout.Button(new GUIContent("Save Mesh Asset", EditorGUIUtility.FindTexture( "d_TreeEditor.Geometry" ), "Save the mesh in this GameObject's MeshFilter as an asset file.")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    if(myScript.gameObject.GetComponent<MeshFilter>())
                    {
                        if(myScript.gameObject.GetComponent<MeshFilter>().mesh)
                        {
                            string path = EditorUtility.SaveFilePanel("Save Mesh Asset", "Assets/", name, "asset");
                            if (string.IsNullOrEmpty(path))
                                return;
                            path = FileUtil.GetProjectRelativePath(path);

                            AssetDatabase.CreateAsset(myScript.gameObject.GetComponent<MeshFilter>().mesh, path);
                            AssetDatabase.SaveAssets();
                        }
                    }
                }
                else
                {
                    Debug.Log("Must be in Play mode to activate this.");
                }
            }
            if(GUILayout.Button(new GUIContent("Flip Over X-Axis", EditorGUIUtility.FindTexture( "RotateTool On" ), "In Edit Mode, flip the SDF sculpt in this GameObject's MeshFilter over the x-axis. This will overwrite the mesh data of the original.")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode == false)
                {
                    Undo.RegisterCompleteObjectUndo(myScript.gameObject.GetComponent<MeshFilter>(), "Flip Mesh");
                    myScript.FlipOverAxis(true, false, false);
                }
                else
                {
                    Debug.Log("Must be in Edit mode to activate this.");
                }
            }
            if(GUILayout.Button(new GUIContent("Flip Over Y-Axis", EditorGUIUtility.FindTexture( "RotateTool On" ), "In Edit Mode, flip the SDF sculpt in this GameObject's MeshFilter over the y-axis. This will overwrite the mesh data of the original.")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode == false)
                {
                    Undo.RegisterCompleteObjectUndo(myScript.gameObject.GetComponent<MeshFilter>(), "Flip Mesh");
                    myScript.FlipOverAxis(false, true, false);
                }
                else
                {
                    Debug.Log("Must be in Edit mode to activate this.");
                }
            }
            if(GUILayout.Button(new GUIContent("Flip Over Z-Axis", EditorGUIUtility.FindTexture( "RotateTool On" ), "In Edit Mode, flip the mesh in this GameObject's MeshFilter over the z-axis. This will overwrite the mesh data of the original.")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode == false)
                {
                    Undo.RegisterCompleteObjectUndo(myScript.gameObject.GetComponent<MeshFilter>(), "Flip Mesh");
                    myScript.FlipOverAxis(false, false, true);
                }
                else
                {
                    Debug.Log("Must be in Edit mode to activate this.");
                }
            }
            if(GUILayout.Button(new GUIContent("Preview Fuzzy Mesh", EditorGUIUtility.FindTexture( "ViewToolZoom" ), "In Edit Mode, preview the SDF sculpt in the mesh filter as a fuzzy mesh.")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode == false)
                {
                    MeshFilter meshFilter = myScript.gameObject.GetComponent<MeshFilter>();
                    if(File.Exists(Application.dataPath + myScript.myMeshPath + myScript.myName + "_MeshData.json"))
                    {
                        Undo.RegisterCompleteObjectUndo(myScript.gameObject.GetComponent<MeshFilter>(), "Setting a Mesh");

                        if(myScript.isFuzzy)
                        {
                            Debug.Log("This sculpt is already fuzzy so this is unnecessary. Use the other Preview Mesh options.");
                        }
                        else
                        {
                            myScript.FuzzFunction();
                        }
                    }
                    else
                    {
                        Debug.Log("Could not find " + Application.dataPath + myScript.myMeshPath + myScript.myName + "_MeshData.json.");
                    }
                }
                else
                {
                    Debug.Log("Must be in Edit mode to activate this.");
                }
            }

            if(GUILayout.Button(new GUIContent("Convert to Fuzzy Mesh", EditorGUIUtility.FindTexture( "d_Refresh" ), "In Edit Mode, convert a non fuzzy sculpt into a fuzzy sculpt. This will overwrite the mesh data of the original.")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode == false)
                {
                    MeshFilter meshFilter = myScript.gameObject.GetComponent<MeshFilter>();
                    if(File.Exists(Application.dataPath + myScript.myMeshPath + myScript.myName + "_MeshData.json"))
                    {
                        Undo.RegisterCompleteObjectUndo(myScript.gameObject.GetComponent<MeshFilter>(), "Setting a Mesh");

                        if(myScript.isFuzzy)
                        {
                            Debug.Log("This sculpt is already fuzzy so this is unnecessary.");
                        }
                        else
                        {
                            myScript.ConvertToFuzzy();
                            myScript.isFuzzy = true;
                        }
                    }
                    else
                    {
                        Debug.Log("Could not find " + Application.dataPath + myScript.myMeshPath + myScript.myName + "_MeshData.json.");
                    }
                }
                else
                {
                    Debug.Log("Must be in Edit mode to activate this.");
                }
            }

            if(GUILayout.Button(new GUIContent("Preview Mesh From My Mesh Path", EditorGUIUtility.FindTexture( "ViewToolZoom" ), "In Edit Mode, preview the mesh if a mesh file has been created.")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode == false)
                {
                    MeshFilter meshFilter = myScript.gameObject.GetComponent<MeshFilter>();
                    if(File.Exists(Application.dataPath + myScript.myMeshPath + myScript.myName + "_MeshData.json"))
                    {
                        Undo.RegisterCompleteObjectUndo(myScript.gameObject.GetComponent<MeshFilter>(), "Setting a Mesh");

                        Mesh myMesh = new Mesh();
                        myMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                        MeshGenerator.SculptMeshStruct meshStruct = new MeshGenerator.SculptMeshStruct();

                        string json = File.ReadAllText(Application.dataPath + myScript.myMeshPath + myScript.myName + "_MeshData.json");
                        meshStruct = JsonUtility.FromJson<MeshGenerator.SculptMeshStruct>(json);

                        myMesh.SetVertices(meshStruct.vertices);
                        myMesh.SetTriangles(meshStruct.triangles, 0 , false);
                        myMesh.SetUVs(0, meshStruct.uvs);
                        myMesh.SetColors(meshStruct.colors);
                        myMesh.SetNormals(meshStruct.normals);
                        myMesh.RecalculateBounds();
                        myMesh.UploadMeshData(false);
                        meshFilter.mesh = myMesh;
                    }
                    else
                    {
                        Debug.Log("Could not find " + Application.dataPath + myScript.myMeshPath + myScript.myName + "_MeshData.json.");
                    }
                }
                else
                {
                    Debug.Log("Must be in Edit mode to activate this.");
                }
            }

            if(GUILayout.Button(new GUIContent("Preview LOD 1 Mesh From My Mesh Path", EditorGUIUtility.FindTexture( "ViewToolZoom" ), "In Edit Mode, preview the first LOD of the sculpt if a mesh file has been created.")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode == false)
                {
                    MeshFilter meshFilter = myScript.gameObject.GetComponent<MeshFilter>();
                    if(File.Exists(Application.dataPath + myScript.myMeshPath + myScript.myName + "_LOD1MeshData.json"))
                    {
                        Undo.RegisterCompleteObjectUndo(myScript.gameObject.GetComponent<MeshFilter>(), "Setting a Mesh");

                        Mesh myMesh = new Mesh();
                        myMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                        MeshGenerator.SculptMeshStruct meshStruct = new MeshGenerator.SculptMeshStruct();

                        string json = File.ReadAllText(Application.dataPath + myScript.myMeshPath + myScript.myName + "_LOD1MeshData.json");
                        meshStruct = JsonUtility.FromJson<MeshGenerator.SculptMeshStruct>(json);

                        myMesh.SetVertices(meshStruct.vertices);
                        myMesh.SetTriangles(meshStruct.triangles, 0 , false);
                        myMesh.SetUVs(0, meshStruct.uvs);
                        myMesh.SetColors(meshStruct.colors);
                        myMesh.SetNormals(meshStruct.normals);
                        myMesh.RecalculateBounds();
                        myMesh.UploadMeshData(false);
                        meshFilter.mesh = myMesh;
                    }
                    else
                    {
                        Debug.Log("Could not find " + Application.dataPath + myScript.myMeshPath + myScript.myName + "_LOD1MeshData.json.");
                    }
                }
                else
                {
                    Debug.Log("Must be in Edit mode to activate this.");
                }
            }

            if(GUILayout.Button(new GUIContent("Preview LOD 2 Mesh From My Mesh Path", EditorGUIUtility.FindTexture( "ViewToolZoom" ), "In Edit Mode, preview the second LOD of the sculpt if a mesh file has been created.")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode == false)
                {
                    MeshFilter meshFilter = myScript.gameObject.GetComponent<MeshFilter>();
                    if(File.Exists(Application.dataPath + myScript.myMeshPath + myScript.myName + "_LOD2MeshData.json"))
                    {
                        Undo.RegisterCompleteObjectUndo(myScript.gameObject.GetComponent<MeshFilter>(), "Setting a Mesh");

                        Mesh myMesh = new Mesh();
                        myMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                        MeshGenerator.SculptMeshStruct meshStruct = new MeshGenerator.SculptMeshStruct();

                        string json = File.ReadAllText(Application.dataPath + myScript.myMeshPath + myScript.myName + "_LOD2MeshData.json");
                        meshStruct = JsonUtility.FromJson<MeshGenerator.SculptMeshStruct>(json);

                        myMesh.SetVertices(meshStruct.vertices);
                        myMesh.SetTriangles(meshStruct.triangles, 0 , false);
                        myMesh.SetUVs(0, meshStruct.uvs);
                        myMesh.SetColors(meshStruct.colors);
                        myMesh.SetNormals(meshStruct.normals);
                        myMesh.RecalculateBounds();
                        myMesh.UploadMeshData(false);
                        meshFilter.mesh = myMesh;
                    }
                    else
                    {
                        Debug.Log("Could not find " + Application.dataPath + myScript.myMeshPath + myScript.myName + "_LOD2MeshData.json.");
                    }
                }
                else
                {
                    Debug.Log("Must be in Edit mode to activate this.");
                }
            }

            if(GUILayout.Button(new GUIContent("Preview LOD 3 Mesh From My Mesh Path", EditorGUIUtility.FindTexture( "ViewToolZoom" ), "In Edit Mode, preview the third LOD of the sculpt if a mesh file has been created.")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode == false)
                {
                    MeshFilter meshFilter = myScript.gameObject.GetComponent<MeshFilter>();
                    if(File.Exists(Application.dataPath + myScript.myMeshPath + myScript.myName + "_LOD3MeshData.json"))
                    {
                        Undo.RegisterCompleteObjectUndo(myScript.gameObject.GetComponent<MeshFilter>(), "Setting a Mesh");

                        Mesh myMesh = new Mesh();
                        myMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                        MeshGenerator.SculptMeshStruct meshStruct = new MeshGenerator.SculptMeshStruct();

                        string json = File.ReadAllText(Application.dataPath + myScript.myMeshPath + myScript.myName + "_LOD3MeshData.json");
                        meshStruct = JsonUtility.FromJson<MeshGenerator.SculptMeshStruct>(json);

                        myMesh.SetVertices(meshStruct.vertices);
                        myMesh.SetTriangles(meshStruct.triangles, 0 , false);
                        myMesh.SetUVs(0, meshStruct.uvs);
                        myMesh.SetColors(meshStruct.colors);
                        myMesh.SetNormals(meshStruct.normals);
                        myMesh.RecalculateBounds();
                        myMesh.UploadMeshData(false);
                        meshFilter.mesh = myMesh;
                    }
                    else
                    {
                        Debug.Log("Could not find " + Application.dataPath + myScript.myMeshPath + myScript.myName + "_LOD3MeshData.json.");
                    }
                }
                else
                {
                    Debug.Log("Must be in Edit mode to activate this.");
                }
            }

            if(GUILayout.Button(new GUIContent("Duplicate Sculpt Data", EditorGUIUtility.FindTexture( "d_TreeEditor.Geometry" ), "In Edit Mode, duplicate the current sculpt data to work on it without changing the original.")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode == false)
                {
                    if(File.Exists(Application.dataPath + myScript.myMeshPath + myScript.myName + "_MeshData.json") && File.Exists(Application.dataPath + myScript.myRecipePath + myScript.myName + "_BrushData.json"))
                    {
                        string path = EditorUtility.SaveFilePanel("Duplicate Sculpt Data", Application.dataPath + myScript.myRecipePath,  myScript.myName + "_Duplicate_BrushData", "json");
                        if (string.IsNullOrEmpty(path))
                            return;
                        path = FileUtil.GetProjectRelativePath(path);
                        string newName = path.Substring(FileUtil.GetProjectRelativePath(Application.dataPath + myScript.myRecipePath).Length);
                        newName = newName.Substring(0, newName.Length - 15);

                        if(newName == myScript.myName)
                        {
                            Debug.Log("Must change the name of the sculpt you are duplicating.");
                        }
                        else
                        {
                            path = Application.dataPath + myScript.myRecipePath + newName + "_BrushData.json";
                            if(File.Exists(path))
                                File.Delete(path);
                            FileUtil.CopyFileOrDirectory(Application.dataPath + myScript.myRecipePath + myScript.myName + "_BrushData.json", path);
                            path = Application.dataPath + myScript.myMeshPath + newName + "_MeshData.json";
                            if(File.Exists(path))
                                File.Delete(path);
                            FileUtil.CopyFileOrDirectory(Application.dataPath + myScript.myMeshPath + myScript.myName + "_MeshData.json", path);
                            for (int i = 0; i < myScript.LODResolutions.Count; i++)
                            {
                                path = Application.dataPath + myScript.myMeshPath + newName + "_LOD" + (i+1).ToString() + "MeshData.json";
                                if(File.Exists(path))
                                    File.Delete(path);
                                FileUtil.CopyFileOrDirectory(Application.dataPath + myScript.myMeshPath + myScript.myName + "_LOD" + (i+1).ToString() + "MeshData.json", path);
                            }
                        }
                    }
                    else
                    {
                        Debug.Log("No Sculpt Data to duplicate.");
                    }
                }
                else
                {
                    Debug.Log("Must be in Edit mode to activate this.");
                }
            }
        }
    }
}
