using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SDFSculptor
{
    [CustomEditor(typeof(MeshGenerator))]
    public class MeshGeneratorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            MeshGenerator myScript = (MeshGenerator)target;
            if(GUILayout.Button(new GUIContent("Reorder Brushes", EditorGUIUtility.FindTexture( "d_Refresh" ), "In Play Mode, if the order of brushes has been changed / sibling index has changed, notify the Mesh Generator.")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    myScript.ReorderBrushes();
                }
                else
                {
                    Debug.Log("Must be in Play mode to activate this.");
                }
            }
        }
    }
}
