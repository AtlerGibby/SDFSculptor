using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


namespace SDFSculptor
{
    [CustomEditor(typeof(SculptSceneManager))]
    public class SculptSceneManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            SculptSceneManager myScript = (SculptSceneManager)target;
            if(GUILayout.Button(new GUIContent("Generate Manager Info", EditorGUIUtility.FindTexture( "Audio Mixer" ), "In Play Mode, update manager info when sculpts have been created, deleted, or changed. This recalculates all information for all sculpts.")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    myScript.GenerateManagerInfo();
                }
                else
                {
                    Debug.Log("Must be in Play mode to activate this.");
                }
            }

            if(GUILayout.Button(new GUIContent("Recalculate All Non-Instanced Sculpt Material Properties", EditorGUIUtility.FindTexture( "d_TreeEditor.Material" ), "In Play Mode, recalculate all non-instanced sculpt material properties if they have changed. Instanced sculpt material properties are updated every frame.")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    myScript.RecalculateAllNonInstancedSculptMaterialProperties();
                }
                else
                {
                    Debug.Log("Must be in Play mode to activate this.");
                }
            }

            if(GUILayout.Button(new GUIContent("Recalculate All Static Sculpt Bounding Boxes", EditorGUIUtility.FindTexture( "d_Grid.BoxTool" ), "In Play Mode, recalculate all static sculpt bounding boxes for frustum/occlusion culling.")))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    myScript.RecalculateAllStaticSculptBoundingBoxes();
                }
                else
                {
                    Debug.Log("Must be in Play mode to activate this.");
                }
            }
        }
    }
}
