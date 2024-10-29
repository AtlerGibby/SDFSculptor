using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SDFSculptor
{
    [CustomEditor(typeof(SurfaceSpawner))]
    public class SurfaceSpawnerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            SurfaceSpawner myScript = (SurfaceSpawner)target;
            if(GUILayout.Button(new GUIContent("Spawn Meshes On Surface", EditorGUIUtility.FindTexture( "CreateAddNew" ), "In Edit Mode, spawn sculpts on the surface of a mesh.")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode == false)
                {
                    GameObject[] output = myScript.Spawn((float)(EditorApplication.timeSinceStartup));
                    //Debug.Log((float)(EditorApplication.timeSinceStartup));
                    int undoID = Undo.GetCurrentGroup();
                    for (int i = 0; i < output.Length; i++)
                    {
                        Undo.RegisterCreatedObjectUndo(output[i], "Spawned Many Sculpts");
                        Undo.CollapseUndoOperations(undoID);
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
