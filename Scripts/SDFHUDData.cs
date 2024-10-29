using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SDFSculptor
{
    [CreateAssetMenu(fileName = "SDF_HUD_Data", menuName = "ScriptableObjects/SignedDistanceFieldHUDData", order = 1)]
    public class SDFHUDData : ScriptableObject
    {
        [Header("Icons")]
        public Texture crossHairIcon;
        public Texture grabIcon;
        public Texture rotateIcon;
        public Texture scaleIcon;
        public Texture blendIcon;
        public Texture roundIcon;
        public Texture warpStrengthIcon;
        public Texture warpScaleIcon;
        public Texture warpPatternIcon;
        public Texture colorIcon;
        public Texture brushIcon;
        public Texture addIcon;
        public Texture subtractIcon;
        public Texture intersectionIcon;
        public Texture inverseIntersectionIcon;
        public Texture paintIcon;
        public Texture duplicateIcon;
        public Texture deleteIcon;

        [Header("Materials")]
        public Material highlightMaterial;
        public Material selectMaterial;

        [Header("Prefabs")]
        public GameObject boxBrushPrefab;
        public GameObject sphereBrushPrefab;
        public GameObject ellipsoidBrushPrefab;
        public GameObject torusBrushPrefab;
        public GameObject linkBrushPrefab;
        public GameObject curveBrushPrefab;

        [Header("Basic Keybindings")]
        [Tooltip("Move forward.")]
        public KeyCode moveForwardKey;
        [Tooltip("Move backward.")]
        public KeyCode moveBackwardKey;
        [Tooltip("Move left.")]
        public KeyCode moveLeftKey;
        [Tooltip("Move right.")]
        public KeyCode moveRightKey;
        [Tooltip("Move up.")]
        public KeyCode moveUpKey;
        [Tooltip("Move down.")]
        public KeyCode moveDownKey;
        [Tooltip("Rotation snapping toggle when using rotate tool.")]
        public KeyCode rotationSnappingKey;
        [Tooltip("Increasing or decreasing selection depth with scroll wheel.")]
        public KeyCode selectionDepthKey;
        [Tooltip("Speed up moving / speed up certain tools.")]
        public KeyCode speedKey;
        [Tooltip("Look at all tools.")]
        public KeyCode viewToolsKey;
        [Tooltip("Confirm an edit to a brush.")]
        public KeyCode confirmKey;

        public KeyCode xAxisKey;
        public KeyCode yAxisKey;
        public KeyCode zAxisKey;


        [Header("More Keybindings")]
        public KeyCode useBrushKey;
        public KeyCode regenerateMesh;
        public KeyCode grabShortcut;
        public KeyCode rotateShortcut;
        public KeyCode scaleShortcut;
        public KeyCode blendShortcut;
        public KeyCode roundShortcut;
        public KeyCode warpStrengthShortcut;
        public KeyCode warpScaleShortcut;
        public KeyCode warpPatternShortcut;
        public KeyCode colorShortcut;
        public KeyCode brushShortcut;
        public KeyCode addShortcut;
        public KeyCode subtractShortcut;
        public KeyCode intersectionShortcut;
        public KeyCode inverseIntersectionShortcut;
        public KeyCode paintShortcut;
        public KeyCode duplicateShortcut;
        public KeyCode deleteShortcut;
        public KeyCode undoShortcut;
        public KeyCode redoShortcut;

        public KeyCode spectatorShortcut;
    }
}
