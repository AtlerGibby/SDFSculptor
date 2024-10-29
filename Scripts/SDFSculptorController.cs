using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SDFSculptor
{
    public class SDFSculptorController : MonoBehaviour
    {
        [Tooltip("Default movement speed.")]
        public float moveSpeed = 5;

        [Tooltip("Default edit speed (Move/Rotate/Scale).")]
        public float editSpeed = 5;

        [Tooltip("Speed multiplier.")]
        public float speedBoostAmount = 2;
        float speedBoost = 1;

        //GICameraProperties myCam;
        bool directLightDebugOn;
        bool lightMapDebugOn;
        bool HUDOn = true;

        [Tooltip("Disable ability to edit / all HUD elements.")]
        public bool spectatorMode;

        [HideInInspector]
        public bool editMode;

        bool mouseLookToggle = true;
        //public Texture2D cpt;

        int mode = 0;

        [Tooltip("Reference to the text in the title bar.")]
        public TextMesh currentText;
        [Tooltip("Reference to the text in the description bar.")]
        public TextMesh descriptionText;

        Camera myCam;
        [Tooltip("You can leave this alone unless you want to change the physics layers that brushes exist on.")]
        public LayerMask mask;
        RaycastHit hit;
        RaycastHit hit2;

        Renderer gtHighlighter;
        GameObject selectedObject;
        GameObject lookedAtObject;
        SDFBrush selectedBrush;

        GameObject tools;
        GameObject shapes;
        GameObject colors;
        Renderer crossHair;
        Material undoBtn;
        Material redoBtn;

        GameObject colorHandle;
        GameObject hueHandle;
        GameObject saturationHandle;
        GameObject valueHandle;

        Material colorOutput;
        Material eyeDropper;
        Collider colorPicker;
        GameObject hueSlider;
        GameObject saturationSlider;
        GameObject valueSlider;

        GameObject titleText;
        GameObject titleBackground;

        Renderer xAxis;
        Renderer yAxis;
        Renderer zAxis;

        Vector3 lastPos;
        Quaternion lastRot;
        Vector3 lastScale;
        Color lastColor;
        float lastBlend;
        float lastRound;
        float lastWarpScale;
        float lastWarpStrength;
        bool lastWarpPattern;
        int colorSelected;
        int selectionDepth;
        bool cantRound;
        float distanceSpeed;
        bool duplicatePause;
        bool colorPicking;
        bool newBrush;
        bool pressedTheUndoBtn;
        bool pressedTheRedoBtn;

        struct historyAction 
        {
            public string name;
            public int indexOfAction;
            public int actionType;
            public Vector3 transformP;
            public Quaternion transformR;
            public Vector3 transformS;

            public Vector3 curveAP;
            public Quaternion curveAR;
            public Vector3 curveAS;

            public Vector3 curveBP;
            public Quaternion curveBR;
            public Vector3 curveBS;

            public Color color;
            public float roundA;
            public float noiseAmount;
            public float noiseStrength;
            public bool textureNoise;
            public float blend;
            public int brushType;
            public int brushShape;

            public Vector3 transformPPrev;
            public Quaternion transformRPrev;
            public Vector3 transformSPrev;

            public Vector3 curveAPPrev;
            public Quaternion curveARPrev;
            public Vector3 curveASPrev;

            public Vector3 curveBPPrev;
            public Quaternion curveBRPrev;
            public Vector3 curveBSPrev;

            public Color colorPrev;
            public float roundAPrev;
            public float noiseAmountPrev;
            public float noiseStrengthPrev;
            public bool textureNoisePrev;
            public float blendPrev;
            public int brushTypePrev;
            public int brushShapePrev;
        }

        historyAction tmpPrevAction = new historyAction();
        List<historyAction> history = new List<historyAction>();
        int currentHistoryAction = 0;

        //[Tooltip("A prefab that can be spawned in, expected to have a rigidbody.")]
        //public GameObject cubePrimitive;
        //List<GameObject> primitives = new List<GameObject>();

        //AudioSource lightAudio;
        //AudioSource spawnAudio;

        MeshGenerator meshGenerator;
        [Tooltip("Some basic information needed by the SDF Sculptor Controller.")]
        public SDFHUDData hudData;

        [Tooltip("Debug when edit is made.")]
        public bool debugHistory;

        public AudioClip addSFX;
        public AudioClip removeSFX;
        public AudioClip paintSFX;

        AudioSource audioSource;


        // Start is called before the first frame update
        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            mouseLookToggle = true;

            gtHighlighter = transform.Find("SDFBrush_GT_Highlighter").gameObject.GetComponent<Renderer>();
            meshGenerator = GameObject.FindObjectOfType<MeshGenerator>();
            myCam = transform.GetComponentInChildren<Camera>();
            xAxis = transform.Find("XAxis").gameObject.GetComponent<Renderer>();
            yAxis = transform.Find("YAxis").gameObject.GetComponent<Renderer>();
            zAxis = transform.Find("ZAxis").gameObject.GetComponent<Renderer>();
            crossHair = transform.Find("Main Camera/CrossHair").gameObject.GetComponent<Renderer>();
            titleBackground = transform.Find("Main Camera/TitleBackground").gameObject;
            titleText = transform.Find("Main Camera/TitleText").gameObject;
            tools = transform.Find("Main Camera/Tools").gameObject;
            shapes = transform.Find("Main Camera/Shapes").gameObject;
            colors = transform.Find("Main Camera/Colors").gameObject;

            colorHandle = transform.Find("Main Camera/Colors/ColorPickerHandle").gameObject;
            hueHandle = transform.Find("Main Camera/Colors/HueSliderHandle").gameObject;
            saturationHandle = transform.Find("Main Camera/Colors/SaturationSliderHandle").gameObject;
            valueHandle = transform.Find("Main Camera/Colors/ValueSliderHandle").gameObject;
            colorPicker = transform.Find("Main Camera/Colors/ColorPicker").gameObject.GetComponent<Collider>();
            hueSlider = transform.Find("Main Camera/Colors/HueSlider").gameObject;
            saturationSlider = transform.Find("Main Camera/Colors/SaturationSlider").gameObject;
            valueSlider = transform.Find("Main Camera/Colors/ValueSlider").gameObject;

            eyeDropper = transform.Find("Main Camera/Colors/EyeDropper").gameObject.GetComponent<Renderer>().material;
            colorOutput = transform.Find("Main Camera/Colors/ColorOutput").gameObject.GetComponent<Renderer>().material;
            undoBtn = transform.Find("Main Camera/Tools/Undo").gameObject.GetComponent<Renderer>().material;
            redoBtn = transform.Find("Main Camera/Tools/Redo").gameObject.GetComponent<Renderer>().material;

            audioSource = transform.Find("Main Camera/Audio Source").gameObject.GetComponent<AudioSource>();

            SetColorPickerColor(Color.red);

            if(spectatorMode)
            {
                titleText.SetActive(false);
                titleBackground.SetActive(false);
                crossHair.gameObject.SetActive(false);
            }
        }

        // Update is called once per frame
        void Update()
        {
            if(Input.GetKeyDown(hudData.spectatorShortcut))
            {
                spectatorMode = !spectatorMode;
            }
            
            if(!editMode)
            {
                NavigateFunctions();
            }
            else
            {
                EditFunctions();
            }

            if(crossHair.gameObject.activeInHierarchy == false && spectatorMode == false)
            {
                titleText.SetActive(true);
                titleBackground.SetActive(true);
                crossHair.gameObject.SetActive(true);
            }
        }

        void PlaySFX (AudioClip audioClip)
        {
            audioSource.clip = audioClip;
            audioSource.Play();
        }

        void RestoreLast ()
        {
            if(selectedBrush)
            {
                selectedObject.transform.position = lastPos;
                selectedObject.transform.rotation = lastRot;
                selectedObject.transform.localScale = lastScale;
                selectedBrush.color = lastColor;
                selectedBrush.blend = lastBlend;
                selectedBrush.roundA = lastRound;
                selectedBrush.warpScale = lastWarpScale;
                selectedBrush.warpStrength = lastWarpStrength;
                meshGenerator.TriggerMeshUpdate();

                //if(currentHistoryAction > 1 && !newBrush)
                //{
                //    if(history[currentHistoryAction - 2].name != history[currentHistoryAction - 1].name)
                //    {
                //        history.RemoveAt(history.Count - 1);
                //        currentHistoryAction -= 1;
                //    }
                //}
                //if(newBrush)
                //    SaveHistory(selectedBrush, false, false);
            }
        }

        void RememberLast ()
        {
            if(selectedBrush)
            {
                lastPos = selectedObject.transform.position;
                lastRot = selectedObject.transform.rotation;
                lastScale = selectedObject.transform.localScale;
                lastColor = selectedBrush.color;
                lastBlend = selectedBrush.blend;
                lastRound = selectedBrush.roundA;
                lastWarpScale = selectedBrush.warpScale;
                lastWarpStrength = selectedBrush.warpStrength;

                tmpPrevAction.transformPPrev = selectedObject.transform.position;
                tmpPrevAction.transformRPrev = selectedObject.transform.rotation;
                tmpPrevAction.transformSPrev = selectedObject.transform.localScale;
                if(selectedBrush.transform.childCount > 0)
                {
                    tmpPrevAction.curveAPPrev = selectedBrush.transform.GetChild(0).position;
                    tmpPrevAction.curveARPrev = selectedBrush.transform.GetChild(0).rotation;
                    tmpPrevAction.curveASPrev = selectedBrush.transform.GetChild(0).localScale;
                    tmpPrevAction.curveBPPrev = selectedBrush.transform.GetChild(1).position;
                    tmpPrevAction.curveBRPrev = selectedBrush.transform.GetChild(1).rotation;
                    tmpPrevAction.curveBSPrev = selectedBrush.transform.GetChild(1).localScale;
                }
                else
                {
                    tmpPrevAction.curveAPPrev = Vector3.zero;
                    tmpPrevAction.curveARPrev = Quaternion.identity;
                    tmpPrevAction.curveASPrev = Vector3.zero;
                    tmpPrevAction.curveBPPrev = Vector3.zero;
                    tmpPrevAction.curveBRPrev = Quaternion.identity;
                    tmpPrevAction.curveBSPrev = Vector3.zero;
                }
                tmpPrevAction.colorPrev = selectedBrush.color;
                tmpPrevAction.roundAPrev = selectedBrush.roundA;
                tmpPrevAction.noiseAmountPrev = selectedBrush.warpScale;
                tmpPrevAction.noiseStrengthPrev = selectedBrush.warpStrength;
                tmpPrevAction.textureNoisePrev = selectedBrush.textureWarp;
                tmpPrevAction.blendPrev = selectedBrush.blend;
                tmpPrevAction.brushTypePrev = (int)selectedBrush.brushType;
                tmpPrevAction.brushShapePrev = (int)selectedBrush.brushShape;

                //if(currentHistoryAction > 0)
                //{
                //    if(history[currentHistoryAction - 1].name != selectedBrush.gameObject.name)
                //        SaveHistory(selectedBrush, newBrush, false);
                //}
                //else if(newBrush)
                //    SaveHistory(selectedBrush, true, false);
            }
        }

        void ColorSliders ()
        {
            float hue = 0;
            float saturation = 0;
            float value = 0;
            Vector3 hitPoint = hit.point + hit.normal * 0.001f;
            //Vector3 colorPickerBounds = colorPicker.bounds.extents;
            Vector3 right = colorPicker.transform.right * colorPicker.transform.localScale.x;
            Vector3 top = colorPicker.transform.up * colorPicker.transform.localScale.y;
            if(hit.collider.gameObject.name == "ColorPicker")
            {
                colorHandle.transform.position = hitPoint;
                Vector3 bl = (colorPicker.transform.position - top) - right; //Vector3.Distance(hit.point, (hit.transform.position - top) - right);
                Vector3 br = (colorPicker.transform.position - top) + right; //Vector3.Distance(hit.point, (hit.transform.position - top) + right);
                Vector3 tl = (colorPicker.transform.position + top) - right; //Vector3.Distance(hit.point, (hit.transform.position + top) - right);
                Vector3 tr = (colorPicker.transform.position + top) + right; //Vector3.Distance(hit.point, (hit.transform.position + top) + right);

                float w = Vector3.Distance(bl, br);
                float h = Vector3.Distance(bl, tl);

                Vector3 newTop = Vector3.Project(hit.point - tl, tr - tl) + tl;

                Vector3 newRight = Vector3.Project(hit.point - bl, tl - bl) + bl;
                
                saturation = ((Vector3.Distance(newTop, tl) / w) - 0.25f) * 2;
                value = ((Vector3.Distance(newRight, bl) / h) - 0.25f) * 2;

                //Debug.Log(saturation.ToString() + " / " + value.ToString());
                saturationHandle.transform.position = saturationSlider.transform.position + (hit.normal * 0.001f) + right * saturation - right/2;
                valueHandle.transform.position = valueSlider.transform.position + (hit.normal * 0.001f) + right * value - right/2;

                hue = Mathf.Lerp(1.01f, -0.01f, Vector3.Distance(hueHandle.transform.position, hueSlider.transform.position - right/2)/(w/2)); //Vector3.Distance(hueHandle.transform.position, hueSlider.transform.position - right/2)/(w/2);
                colorOutput.color = Color.HSVToRGB(hue,saturation,value);
                colorPicker.GetComponent<Renderer>().material.SetColor("_Color", Color.HSVToRGB(hue,1,1));
                valueSlider.GetComponent<Renderer>().material.SetColor("_Color", Color.HSVToRGB(hue,1,1));
                saturationSlider.GetComponent<Renderer>().material.SetColor("_Color", Color.HSVToRGB(hue,1,1));
            }
            if(hit.collider.gameObject.name == "HueSlider")
            {
                Vector3 bl = (colorPicker.transform.position - top) - right;
                Vector3 br = (colorPicker.transform.position - top) + right;
                float w = Vector3.Distance(bl, br) / 2;

                hueHandle.transform.position = new Vector3(hitPoint.x, hit.collider.transform.position.y, hitPoint.z) + (hit.normal * 0.001f);
                hue =  Mathf.Lerp(1.01f, -0.01f, Vector3.Distance(hueHandle.transform.position, hueSlider.transform.position - right/2)/w);
                saturation = Vector3.Distance(saturationHandle.transform.position, saturationSlider.transform.position - right/2)/w;
                value = Vector3.Distance(valueHandle.transform.position, valueSlider.transform.position - right/2)/w;
                
                //Debug.Log(hue);

                colorOutput.color = Color.HSVToRGB(hue,saturation,value);
                colorPicker.GetComponent<Renderer>().material.SetColor("_Color", Color.HSVToRGB(hue,1,1));
                valueSlider.GetComponent<Renderer>().material.SetColor("_Color", Color.HSVToRGB(hue,1,1));
                saturationSlider.GetComponent<Renderer>().material.SetColor("_Color", Color.HSVToRGB(hue,1,1));
            }
            if(hit.collider.gameObject.name == "SaturationSlider")
            {
                Vector3 bl = (colorPicker.transform.position - top) - right;
                Vector3 br = (colorPicker.transform.position - top) + right;
                float w = Vector3.Distance(bl, br) / 2;

                saturationHandle.transform.position = new Vector3(hitPoint.x, hit.collider.transform.position.y, hitPoint.z) + (hit.normal * 0.001f);
                hue = Mathf.Lerp(1.01f, -0.01f, Vector3.Distance(hueHandle.transform.position, hueSlider.transform.position - right/2)/w);
                saturation = Vector3.Distance(saturationHandle.transform.position, saturationSlider.transform.position - right/2)/w;
                value = Vector3.Distance(valueHandle.transform.position, valueSlider.transform.position - right/2)/w;

                colorHandle.transform.position = bl + (saturation + 0.5f) * right + (value+ 0.5f) * top + (hit.normal * 0.001f);

                colorOutput.color = Color.HSVToRGB(hue,saturation,value);
                colorPicker.GetComponent<Renderer>().material.SetColor("_Color", Color.HSVToRGB(hue,1,1));
                valueSlider.GetComponent<Renderer>().material.SetColor("_Color", Color.HSVToRGB(hue,1,1));
                saturationSlider.GetComponent<Renderer>().material.SetColor("_Color", Color.HSVToRGB(hue,1,1));
            }
            if(hit.collider.gameObject.name == "ValueSlider")
            {
                Vector3 bl = (colorPicker.transform.position - top) - right;
                Vector3 br = (colorPicker.transform.position - top) + right;
                float w = Vector3.Distance(bl, br) / 2;

                valueHandle.transform.position = new Vector3(hitPoint.x, hit.collider.transform.position.y, hitPoint.z) + (hit.normal * 0.001f);
                hue = Mathf.Lerp(1.01f, -0.01f, Vector3.Distance(hueHandle.transform.position, hueSlider.transform.position - right/2)/w);
                saturation = Vector3.Distance(saturationHandle.transform.position, saturationSlider.transform.position - right/2)/w;
                value = Vector3.Distance(valueHandle.transform.position, valueSlider.transform.position - right/2)/w;

                colorHandle.transform.position = bl + (saturation + 0.5f) * right + (value + 0.5f) * top + (hit.normal * 0.001f);

                colorOutput.color = Color.HSVToRGB(hue,saturation,value);
                colorPicker.GetComponent<Renderer>().material.SetColor("_Color", Color.HSVToRGB(hue,1,1));
                valueSlider.GetComponent<Renderer>().material.SetColor("_Color", Color.HSVToRGB(hue,1,1));
                saturationSlider.GetComponent<Renderer>().material.SetColor("_Color", Color.HSVToRGB(hue,1,1));
            }
        }

        void SetColorPickerColor (Color initialColor)
        {
            colorOutput.color = initialColor;
            float hue;
            float saturation;
            float value;
            Color.RGBToHSV(colorOutput.color, out hue, out saturation, out value);

            colorPicker.GetComponent<Renderer>().material.SetColor("_Color", Color.HSVToRGB(hue,1,1));
            valueSlider.GetComponent<Renderer>().material.SetColor("_Color", Color.HSVToRGB(hue,1,1));
            saturationSlider.GetComponent<Renderer>().material.SetColor("_Color", Color.HSVToRGB(hue,1,1));
            colorPicking = false;
            eyeDropper.color = Color.white;

            Vector3 right = colorPicker.transform.right * colorPicker.transform.localScale.x;
            Vector3 top = colorPicker.transform.up * colorPicker.transform.localScale.y;

            Vector3 bl = (colorPicker.transform.position - top) - right;
            colorHandle.transform.position = bl + (saturation + 0.5f) * right + (value + 0.5f) * top + (transform.forward * -0.001f);

            bl = hueSlider.transform.position - right;
            hueHandle.transform.position = bl + ((1-hue) + 0.5f) * right + (transform.forward * -0.001f);
            bl = valueSlider.transform.position - right;
            valueHandle.transform.position = bl + (value + 0.5f) * right + (transform.forward * -0.001f);
            bl = saturationSlider.transform.position - right;
            saturationHandle.transform.position = bl + (saturation + 0.5f) * right + (transform.forward * -0.001f);
        }

        //IEnumerator ColorPicker ()
        //{
        //    yield return new WaitForEndOfFrame();
        //    Texture cot = Shader.GetGlobalTexture("_CameraOpaqueTexture");
        //    float cotScaleW = (float)cot.width/(float)Screen.width;
        //    float cotScaleH = (float)cot.height/(float)Screen.height;
        //    RenderTexture mid = new RenderTexture((int)cot.width, (int)cot.height, 24);
        //    Graphics.Blit(cot, mid);
        //    Texture2D tex = new Texture2D((int)cot.width, (int)cot.height, TextureFormat.RGBA32, false);
        //    
        //    yield return new WaitForEndOfFrame();
        //    Graphics.CopyTexture(mid, tex);
        //    mid.Release();
    //
        //    yield return new WaitForEndOfFrame();
        //    Debug.Log(tex.GetPixel(478, 266) == tex.GetPixel(245, 80));
        //    SetColorPickerColor(tex.GetPixel(Mathf.RoundToInt(Input.mousePosition.x * cotScaleW), Mathf.RoundToInt(Input.mousePosition.y * cotScaleH)));
        //}

        IEnumerator DeleteBrush(GameObject brush)
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            Destroy(brush);
        }

        void SaveHistory (SDFBrush current, bool isNewBrush, bool removingBrush)
        {
            //Avoid duplicate deletes
            if(currentHistoryAction > 0)
            {
                if(history[currentHistoryAction - 1].name == current.gameObject.name && history[currentHistoryAction - 1].actionType == 2 && removingBrush)
                    return;
            }

            if(debugHistory)
                Debug.Log("Edited " + current.name + ", Edit #" + currentHistoryAction);

            // Overwrite history
            if(currentHistoryAction - 1 < history.Count - 1)
            {
                if(debugHistory)
                    Debug.Log("Overwrite history");
                history.RemoveRange(currentHistoryAction, history.Count - (currentHistoryAction));
            }

            historyAction newAction = new historyAction();
            newAction.indexOfAction = current.transform.GetSiblingIndex();
            newAction.actionType = 1;
            newAction.name = current.gameObject.name;
            if(isNewBrush)
                newAction.actionType = 0;
            if(removingBrush)
                newAction.actionType = 2;

            newAction.transformP = selectedObject.transform.position;
            newAction.transformR = selectedObject.transform.rotation;
            newAction.transformS = selectedObject.transform.localScale;
            if(selectedBrush.transform.childCount > 0)
            {
                newAction.curveAP = selectedBrush.transform.GetChild(0).position;
                newAction.curveAR = selectedBrush.transform.GetChild(0).rotation;
                newAction.curveAS = selectedBrush.transform.GetChild(0).localScale;
                newAction.curveBP = selectedBrush.transform.GetChild(1).position;
                newAction.curveBR = selectedBrush.transform.GetChild(1).rotation;
                newAction.curveBSPrev = selectedBrush.transform.GetChild(1).localScale;
            }
            else
            {
                newAction.curveAP = Vector3.zero;
                newAction.curveAR = Quaternion.identity;
                newAction.curveAS = Vector3.zero;
                newAction.curveBP = Vector3.zero;
                newAction.curveBR = Quaternion.identity;
                newAction.curveBS = Vector3.zero;
            }

            newAction.color = current.color;
            newAction.roundA = current.roundA;
            newAction.noiseAmount = current.warpScale;
            newAction.noiseStrength = current.warpStrength;
            newAction.textureNoise = current.textureWarp;
            newAction.blend = current.blend;
            newAction.brushType = (int)current.brushType;
            newAction.brushShape = (int)current.brushShape;

            //if(newAction.actionType == 1 || newAction.actionType == 2)
            //{
                newAction.transformPPrev = tmpPrevAction.transformPPrev;
                newAction.transformRPrev = tmpPrevAction.transformRPrev;
                newAction.transformSPrev = tmpPrevAction.transformSPrev;
                newAction.curveAPPrev = tmpPrevAction.curveAPPrev;
                newAction.curveARPrev = tmpPrevAction.curveARPrev;
                newAction.curveASPrev = tmpPrevAction.curveASPrev;
                newAction.curveBPPrev = tmpPrevAction.curveBPPrev;
                newAction.curveBRPrev = tmpPrevAction.curveBRPrev;
                newAction.curveBSPrev = tmpPrevAction.curveBSPrev;
                newAction.colorPrev = tmpPrevAction.colorPrev;
                newAction.roundAPrev = tmpPrevAction.roundAPrev;
                newAction.noiseAmountPrev = tmpPrevAction.noiseAmountPrev;
                newAction.noiseStrengthPrev = tmpPrevAction.noiseStrengthPrev;
                newAction.textureNoisePrev = tmpPrevAction.textureNoisePrev;
                newAction.blendPrev = tmpPrevAction.blendPrev;
                newAction.brushTypePrev = tmpPrevAction.brushTypePrev;
                newAction.brushShapePrev = tmpPrevAction.brushShapePrev;
            //}

            history.Add(newAction);
            currentHistoryAction += 1;
        }

        void NavigateFunctions ()
        {
            if(titleText.activeInHierarchy && spectatorMode)
                titleText.SetActive(false);
            if(titleBackground.activeInHierarchy && spectatorMode)
                titleBackground.SetActive(false);
            if(crossHair.gameObject.activeInHierarchy && spectatorMode)
                crossHair.gameObject.SetActive(false);

            if(tools.activeInHierarchy)
                tools.SetActive(false);
            if(shapes.activeInHierarchy)
                shapes.SetActive(false);
            if(colors.activeInHierarchy)
                colors.SetActive(false);

            // Rotate
            float inputRotateAxisX = 0;
            float inputRotateAxisY = 0;

            // Left, Right, Front, and Back
            float inputVertical = 0;
            float inputHorizontal = 0;
            // Up and Down
            float inputYAxis = 0;

            for(int i = 0; i < Input.touchCount; i++)
            {
                if(Input.touches[i].position.x < Screen.width / 2)
                {
                    inputHorizontal += (Input.touches[i].position.x - Screen.width / 4) / (Screen.width/4) * 3;
                    inputVertical += (Input.touches[i].position.y - Screen.height / 2) / (Screen.height/2) * 3;
                }
            }

            // Movement
            if(Input.GetKey(hudData.moveForwardKey))
                inputVertical += 1;
            if(Input.GetKey(hudData.moveBackwardKey))
                inputVertical -= 1;
            if(Input.GetKey(hudData.moveLeftKey))
                inputHorizontal -= 1;
            if(Input.GetKey(hudData.moveRightKey))
                inputHorizontal += 1;
            if(Input.GetKey(hudData.moveUpKey))
                inputYAxis += 1;
            if(Input.GetKey(hudData.moveDownKey))
                inputYAxis -= 1;
            if(Input.GetKey(hudData.speedKey))
                speedBoost = speedBoostAmount;
            else
                speedBoost = 1;

            Physics.Raycast(myCam.ScreenPointToRay(new Vector2(Screen.width/2, Screen.height/2)), out hit, 1000, mask, QueryTriggerInteraction.Ignore);
            if(hit.collider != null)
            {
                if(hit.collider.gameObject.GetComponent<SDFSculpt>())
                {
                    currentText.text = "[" + hudData.useBrushKey.ToString() + "]-Edit: "
                    + hit.collider.gameObject.GetComponent<SDFSculpt>().myName;
                }
                else
                {
                    currentText.text = "Find a sculpture to edit...";
                }
            }
            else
            {
                currentText.text = "Find a sculpture to edit...";
            }
            
            if(Input.GetKeyDown(hudData.useBrushKey))
            {
                if(selectedObject == null)
                {
                    Physics.Raycast(myCam.ScreenPointToRay(new Vector2(Screen.width/2, Screen.height/2)), out hit, 1000, mask, QueryTriggerInteraction.Ignore);
                    if(hit.collider != null)
                    {
                        if(hit.collider.gameObject.GetComponent<SDFSculpt>())
                        {
                            editMode = true;
                            currentText.text = "[" + hudData.viewToolsKey.ToString() + "]-Tools";
                            meshGenerator.mySculpt = hit.collider.gameObject.GetComponent<SDFSculpt>();
                            //meshGenerator.transform.position = hit.collider.transform.position;
                            meshGenerator.LoadRecipe();
                        }
                    }
                }
            }

            if(Input.GetKeyDown(hudData.viewToolsKey))
            {
                mouseLookToggle = !mouseLookToggle;
                if(!mouseLookToggle)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }

            // looking around
            if(mouseLookToggle && selectedObject == null)
            {
                inputRotateAxisX = Input.GetAxisRaw("Mouse X") * 5;
                inputRotateAxisY = Input.GetAxisRaw("Mouse Y") * 5;

                for(int i = 0; i < Input.touchCount; i++)
                {
                    if(Input.touches[i].position.x > Screen.width / 2)
                    {
                        inputRotateAxisX += (Input.touches[i].position.x - (Screen.width / 4 + Screen.width / 2)) / (Screen.width/4);
                        inputRotateAxisY += (Input.touches[i].position.y - Screen.height / 2) / (Screen.height/2);

                        inputRotateAxisX *= 3;
                        inputRotateAxisY *= 3;
                    }
                }
            }

            float rotationX = transform.localEulerAngles.x;
            float newRotationY = transform.localEulerAngles.y + inputRotateAxisX;

            float newRotationX = (rotationX - inputRotateAxisY);
            if (rotationX <= 90.0f && newRotationX >= 0.0f)
                newRotationX = Mathf.Clamp(newRotationX, 0.0f, 90.0f);
            if (rotationX >= 270.0f)
                newRotationX = Mathf.Clamp(newRotationX, 270.0f, 360.0f);

            transform.localRotation = Quaternion.Euler(newRotationX, newRotationY, transform.localEulerAngles.z);

            // moving around
            float moveSpeedUnscaled = Time.unscaledDeltaTime * moveSpeed * speedBoost;

            transform.position += transform.forward * moveSpeedUnscaled * inputVertical;
            transform.position += transform.right * moveSpeedUnscaled * inputHorizontal;
            transform.position += Vector3.up * moveSpeedUnscaled * inputYAxis;
        }

        void EditFunctions ()
        {

            if(spectatorMode)
            {
                titleText.SetActive(false);
                titleBackground.SetActive(false);
                crossHair.gameObject.SetActive(false);
                editMode = false;
            }

            // Rotate
            float inputRotateAxisX = 0;
            float inputRotateAxisY = 0;

            // Left, Right, Front, and Back
            float inputVertical = 0;
            float inputHorizontal = 0;
            // Up and Down
            float inputYAxis = 0;

            for(int i = 0; i < Input.touchCount; i++)
            {
                if(Input.touches[i].position.x < Screen.width / 2)
                {
                    inputHorizontal += (Input.touches[i].position.x - Screen.width / 4) / (Screen.width/4) * 3;
                    inputVertical += (Input.touches[i].position.y - Screen.height / 2) / (Screen.height/2) * 3;
                }
            }

            if(Input.GetKeyDown(hudData.viewToolsKey))
            {
                mouseLookToggle = !mouseLookToggle;
                if(!mouseLookToggle)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }

                if(selectedObject)
                {
                    if(lookedAtObject != selectedObject)
                        selectedObject.GetComponent<Renderer>().enabled = false;
                }

                if(mode != 0)
                    RestoreLast();
                if(newBrush)
                {
                    meshGenerator.RemoveBrush(selectedBrush);
                    meshGenerator.TriggerMeshUpdate();
                    StartCoroutine(DeleteBrush(selectedObject));
                }
                mode = 0;
                selectedObject = null;
                selectedBrush = null;
                crossHair.material.mainTexture = hudData.crossHairIcon;
                duplicatePause = false;
                newBrush = false;
                colorPicking = false;
                eyeDropper.color = Color.white;
                currentText.text = "[" + hudData.viewToolsKey.ToString() + "]-Tools";
            }

            if(Input.GetKeyDown(KeyCode.Alpha2))
            {
                selectionDepth += 1;
            }
            if(Input.GetKeyDown(KeyCode.Alpha1))
            {
                selectionDepth -= 1;
                if(selectionDepth < 0)
                    selectionDepth = 0;
            }

            if(Input.mouseScrollDelta.y != 0 && Input.GetKey(hudData.selectionDepthKey) && selectedObject == null)
            {
                if(Input.mouseScrollDelta.y > 0)
                {
                    selectionDepth += 1;
                }
                else if(Input.mouseScrollDelta.y < 0)
                {
                    selectionDepth -= 1;
                    if(selectionDepth < 0)
                        selectionDepth = 0;
                }
            }

            //if(Input.GetMouseButtonDown(0))
            //{
            //    Cursor.visible = !Cursor.visible;
            //    if(Cursor.visible)
            //        Cursor.lockState = CursorLockMode.None;
            //    else
            //        Cursor.lockState = CursorLockMode.Locked;
            //}

            Physics.Raycast(myCam.ScreenPointToRay(new Vector2(Screen.width/2, Screen.height/2)), out hit, 1000, mask, QueryTriggerInteraction.Ignore);
            if(hit.collider != null && mode != 0 && mode != 9)
            {
                for (int i = 0; i < selectionDepth; i++)
                {
                    if(Physics.Raycast(hit.point + myCam.transform.forward * 0.01f, myCam.transform.forward, 1000, mask, QueryTriggerInteraction.Ignore))
                    {
                        Physics.Raycast(hit.point + myCam.transform.forward * 0.01f, myCam.transform.forward, out hit, 1000, mask, QueryTriggerInteraction.Ignore);
                    }
                    else
                    {
                        selectionDepth = i;
                        break;
                    }
                }

                if(hit.collider.gameObject != selectedObject)
                {
                    if(hit.collider.gameObject.GetComponent<SDFBrush>())
                    {
                        if(lookedAtObject)
                            lookedAtObject.GetComponent<Renderer>().enabled = false;
                        lookedAtObject = hit.collider.gameObject;
                        if(selectedObject == null)
                        {
                            hudData.highlightMaterial.SetColor("_Color", hit.collider.gameObject.GetComponent<SDFBrush>().color);
                            lookedAtObject.GetComponent<Renderer>().enabled = true;
                            lookedAtObject.GetComponent<Renderer>().material = hudData.highlightMaterial;
                        }
                    }
                    else if(hit.collider.transform.parent)
                    {
                        if(hit.collider.transform.parent.gameObject.GetComponent<SDFBrush>())
                        {
                            if(lookedAtObject)
                                lookedAtObject.GetComponent<Renderer>().enabled = false;
                            lookedAtObject = hit.collider.gameObject;
                            if(selectedObject == null)
                            {
                                hudData.highlightMaterial.SetColor("_Color", hit.collider.transform.parent.gameObject.GetComponent<SDFBrush>().color);
                                lookedAtObject.GetComponent<Renderer>().enabled = true;
                                lookedAtObject.GetComponent<Renderer>().material = hudData.highlightMaterial;
                            }
                        }
                        else if(lookedAtObject)
                        {
                            lookedAtObject.GetComponent<Renderer>().enabled = false;
                            lookedAtObject = null;
                        }
                    }
                    else if(lookedAtObject)
                    {
                        lookedAtObject.GetComponent<Renderer>().enabled = false;
                        lookedAtObject = null;
                    }
                }
            }
            else if(lookedAtObject)
            {
                lookedAtObject.GetComponent<Renderer>().material = hudData.highlightMaterial;
                lookedAtObject.GetComponent<Renderer>().enabled = false;
                lookedAtObject = null;
                selectionDepth = 0;
            }

            if(lookedAtObject && selectedObject == null && mode != 0 && mode != 9)
            {
                gtHighlighter.enabled = true;
                gtHighlighter.transform.position = lookedAtObject.transform.position;
                gtHighlighter.transform.rotation = lookedAtObject.transform.rotation;
                gtHighlighter.transform.localScale = lookedAtObject.transform.lossyScale;

            }
            else
            {
                gtHighlighter.enabled = false;
            }

            /*if((Input.GetKeyDown(KeyCode.G) && !Input.GetKey(KeyCode.LeftShift)) ||
            Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.T) || Input.GetKeyDown(KeyCode.B) ||
            Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.V) || Input.GetKeyDown(KeyCode.M) ||
            Input.GetKeyDown(KeyCode.N) ||
            Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.Alpha0) ||
            Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.H))//Input.GetMouseButtonDown(0))
            */
            if(Input.GetKeyDown(hudData.useBrushKey) && mode != 0)
            {
                if(selectedObject == null)
                {
                    Physics.Raycast(myCam.ScreenPointToRay(new Vector2(Screen.width/2, Screen.height/2)), out hit, 1000, mask, QueryTriggerInteraction.Ignore);
                    if(hit.collider != null)
                    {
                        for (int i = 0; i < selectionDepth; i++)
                        {
                            Physics.Raycast(hit.point + myCam.transform.forward * 0.01f, myCam.transform.forward, out hit, 1000, mask, QueryTriggerInteraction.Ignore);
                        }

                        if(hit.collider.gameObject.GetComponent<SDFBrush>())
                        {
                            selectedObject = hit.collider.gameObject;
                            if(lookedAtObject == selectedObject)
                            {
                                lookedAtObject.GetComponent<Renderer>().material = hudData.selectMaterial;
                                lookedAtObject = null;
                            }

                            selectedBrush = selectedObject.GetComponent<SDFBrush>();
                            RememberLast();
                        }
                        else if(hit.collider.transform.parent != null)
                        {
                            if(hit.collider.transform.parent.gameObject.GetComponent<SDFBrush>() && (mode == 1 || mode == 3))
                            {
                                selectedObject = hit.collider.gameObject;
                                if(lookedAtObject == selectedObject)
                                {
                                    lookedAtObject.GetComponent<Renderer>().material = hudData.selectMaterial;
                                    lookedAtObject = null;
                                }

                                selectedBrush = hit.collider.transform.parent.gameObject.GetComponent<SDFBrush>();
                                RememberLast();
                            }
                        }
                    }
                }
                if(selectedObject)
                {
                    if(selectedBrush.brushShape == SDFBrush.SDFBrushShape.Box ||
                    selectedBrush.brushShape == SDFBrush.SDFBrushShape.Sphere ||
                    selectedBrush.brushShape == SDFBrush.SDFBrushShape.BezierCurve ||
                    selectedBrush.brushShape == SDFBrush.SDFBrushShape.Link ||
                    selectedBrush.brushShape == SDFBrush.SDFBrushShape.Ellipsoid ||
                    selectedBrush.brushShape == SDFBrush.SDFBrushShape.Torus)
                        cantRound = true;
                    else
                        cantRound = false;
        
                    if(mode == 8)
                        SetColorPickerColor(selectedBrush.color);
                }
            }

            if(selectedObject != null && (mode == 1 || mode == 2 || mode == 3))
            {
                xAxis.transform.position = selectedObject.transform.position;
                yAxis.transform.position = selectedObject.transform.position;
                zAxis.transform.position = selectedObject.transform.position;
                if(Input.GetKey(hudData.xAxisKey))
                    xAxis.enabled = true;
                else
                    xAxis.enabled = false;
                if(Input.GetKey(hudData.yAxisKey))
                    yAxis.enabled = true;
                else
                    yAxis.enabled = false;
                if(Input.GetKey(hudData.zAxisKey))
                    zAxis.enabled = true;
                else
                    zAxis.enabled = false;
            }
            else if(xAxis.enabled || yAxis.enabled || zAxis.enabled)
            {
                xAxis.enabled = false;
                yAxis.enabled = false;
                zAxis.enabled = false;
            }

            if(mode == 17 && selectedObject)
            {
                //Physics.Raycast(myCam.ScreenPointToRay(new Vector2(Screen.width/2, Screen.height/2)), out hit, 1000, mask, QueryTriggerInteraction.Ignore);
                if(selectedObject != null)
                {
                    SaveHistory(selectedObject.GetComponent<SDFBrush>(), false, true);
                    meshGenerator.RemoveBrush(selectedObject.GetComponent<SDFBrush>());
                    meshGenerator.TriggerMeshUpdate();
                    StartCoroutine(DeleteBrush(selectedObject));
                    //GameObject.Destroy(hit.collider.gameObject);
                }
            }

            if(mode == 16 && selectedObject && duplicatePause == false)
            {
                //Physics.Raycast(myCam.ScreenPointToRay(new Vector2(Screen.width/2, Screen.height/2)), out hit, 1000, mask, QueryTriggerInteraction.Ignore);
                //if(hit.collider != null)
                //{
                //    selectedObject = GameObject.Instantiate(hit.collider.gameObject, hit.collider.gameObject.transform.position,
                //    hit.collider.gameObject.transform.rotation, meshGenerator.transform);
                //    selectedObject.transform.localScale = hit.collider.gameObject.transform.localScale;
                //    selectedBrush = selectedObject.GetComponent<SDFBrush>();
                //}

                selectedObject = GameObject.Instantiate(hit.collider.gameObject, hit.collider.gameObject.transform.position,
                hit.collider.gameObject.transform.rotation, meshGenerator.transform);
                selectedObject.transform.localScale = hit.collider.gameObject.transform.localScale;
                selectedBrush = selectedObject.GetComponent<SDFBrush>();

                //meshGenerator.AddBrush(selectedObject.GetComponent<SDFBrush>(), selectedObject.transform.GetSiblingIndex());
                //meshGenerator.TriggerMeshUpdate();
                newBrush = true;
                duplicatePause = true;
            }

            if(Input.GetMouseButtonDown(1))
            {
                if(mode != 0)// && !newBrush)
                    RestoreLast();
                if(newBrush)
                {
                    meshGenerator.RemoveBrush(selectedBrush);
                    meshGenerator.TriggerMeshUpdate();
                    StartCoroutine(DeleteBrush(selectedObject));
                }
                if(selectedObject)
                {
                    if(lookedAtObject != selectedObject)
                        selectedObject.GetComponent<Renderer>().enabled = false;
                }
                if(mode == 8)
                    colors.SetActive(false);

                mode = 0;
                selectedObject = null;
                selectedBrush = null;
                duplicatePause = false;
                newBrush = false;
                colorPicking = false;
                eyeDropper.color = Color.white;
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
                mouseLookToggle = true;
                currentText.text = "[" + hudData.viewToolsKey.ToString() + "]-Tools";
            }
            if((((Input.GetMouseButtonDown(0) && mode != 9) || Input.GetKeyDown(hudData.confirmKey)) && mouseLookToggle) || 
            (Input.GetKeyDown(hudData.confirmKey) && mode == 8 && mouseLookToggle == false) || (editMode == false && mode != 0))
            {
                //mode = 0;
                //SaveHistory(selectedBrush, )
                if(selectedBrush != null)
                {
                    //SaveHistory(selectedBrush, false, false);
                    SaveHistory(selectedBrush, newBrush, false);
                    //if(newBrush)
                    //    SaveHistory(selectedBrush, false, false);
                }

                if(selectedObject)
                {
                    if(lookedAtObject != selectedObject)
                        selectedObject.GetComponent<Renderer>().enabled = false;
                }

                if(mode == 8)
                    colors.SetActive(false);

                if(mode > 0 && mode < 12 && mode != 8)
                {
                    if(selectedBrush != null)
                    {
                        if(selectedBrush.brushType == SDFBrush.SDFBrushType.Add)
                            PlaySFX(addSFX);
                        if(selectedBrush.brushType == SDFBrush.SDFBrushType.Subtract ||
                        selectedBrush.brushType == SDFBrush.SDFBrushType.Intersection ||
                        selectedBrush.brushType == SDFBrush.SDFBrushType.InvertedIntersection)
                            PlaySFX(removeSFX);
                        if(selectedBrush.brushType == SDFBrush.SDFBrushType.Paint)
                            PlaySFX(paintSFX);
                    }
                }
                if(mode >= 12 && mode < 15)
                    PlaySFX(removeSFX);
                if(mode == 8)
                    PlaySFX(paintSFX);

                selectedObject = null;
                selectedBrush = null;
                duplicatePause = false;
                newBrush = false;
                colorPicking = false;
                eyeDropper.color = Color.white;
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
                mouseLookToggle = true;
                //Cursor.visible = false;
                //Cursor.lockState = CursorLockMode.Locked;
                //currentText.text = "Current: Navigate";
            }
            if(Input.GetKeyDown(hudData.regenerateMesh))
            {
                Debug.Log("REGEN");
                meshGenerator.ForceRegenMesh();
            }
            if(Input.GetKeyDown(hudData.grabShortcut) && selectedObject == null)
            {
                //if(mode != 0)
                //    RestoreLast();
                //else
                //{
                    RememberLast();
                    mode = 1;
                    currentText.text = "Grab";
                //}
            }
            if(Input.GetKeyDown(hudData.rotateShortcut) && selectedObject == null)
            {
                //if(mode != 0)
                //    RestoreLast();
                //else
                //{
                    RememberLast();
                    mode = 2;
                    currentText.text = "Rotate";
                //}
            }
            if(Input.GetKeyDown(hudData.scaleShortcut) && selectedObject == null)
            {
                //if(mode != 0)
                //    RestoreLast();
                //else
                //{
                    RememberLast();
                    mode = 3;
                    currentText.text = "Scale";
            // }
            }
            if(Input.GetKeyDown(hudData.blendShortcut) && selectedObject == null)
            {
                //if(mode != 0)
                //    RestoreLast();
                //else
                //{
                    RememberLast();
                    mode = 4;
                    currentText.text = "Blend";
                //}
            }
            if(Input.GetKeyDown(hudData.roundShortcut) && selectedObject == null)
            {
                //if(mode != 0)
                //    RestoreLast();
                //else
                //{
                    RememberLast();
                    mode = 5;
                    currentText.text = "Roundness";
                //}
            }
            if(Input.GetKeyDown(hudData.warpStrengthShortcut) && selectedObject == null)
            {
                //if(mode != 0)
                //    RestoreLast();
                //else
                //{
                    RememberLast();
                    mode = 6;
                    currentText.text = "WarpStrength";
                //}
            }
            if(Input.GetKeyDown(hudData.warpScaleShortcut) && selectedObject == null)
            {
                //if(mode != 0)
                //    RestoreLast();
                //else
                //{
                    RememberLast();
                    mode = 7;
                    currentText.text = "WarpScale";
                //}
            }
            if(Input.GetKeyDown(hudData.colorShortcut) && selectedObject == null)
            {
                //if(mode != 0)
                //    RestoreLast();
                //else
                //{
                    RememberLast();
                    mode = 8;
                    currentText.text = "Color";// [X]-Red [Y]-Green [Z]-Blue";
                    crossHair.material.mainTexture = hudData.colorIcon;
                //}
            }
            if(Input.GetKeyDown(hudData.brushShortcut) && selectedObject == null)
            {
                mode = 9;
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                mouseLookToggle = false;
                myCam.transform.Find("Shapes").gameObject.SetActive(true);
                myCam.transform.Find("Colors").gameObject.SetActive(true);
                currentText.text = "Brush";
                crossHair.material.mainTexture = hudData.brushIcon;
            }
            if(Input.GetKeyDown(hudData.warpPatternShortcut) && selectedObject == null)
            {
                mode = 10;
                currentText.text = "WarpPattern";
            }
            if(Input.GetKeyDown(hudData.addShortcut) && selectedObject == null)
            {
                mode = 11;
                currentText.text = "Add";
            }
            if(Input.GetKeyDown(hudData.subtractShortcut) && selectedObject == null)
            {
                mode = 12;
                currentText.text = "Subtract";
            }
            if(Input.GetKeyDown(hudData.intersectionShortcut) && selectedObject == null)
            {
                mode = 13;
                currentText.text = "Intersection";
            }
            if(Input.GetKeyDown(hudData.inverseIntersectionShortcut) && selectedObject == null)
            {
                mode = 14;
                currentText.text = "InverseIntersection";
            }
            if(Input.GetKeyDown(hudData.paintShortcut) && selectedObject == null)
            {
                mode = 15;
                currentText.text = "Paint";
            }
            if(Input.GetKeyDown(hudData.duplicateShortcut) && selectedObject == null)
            {
                mode = 16;
                currentText.text = "Duplicate";
            }
            if(Input.GetKeyDown(hudData.deleteShortcut) && selectedObject == null)
            {
                mode = 17;
                currentText.text = "Delete";
            }

            if(mode == 10 && selectedObject)
            {
                RememberLast();
                selectedObject.GetComponent<SDFBrush>().textureWarp = !(selectedObject.GetComponent<SDFBrush>().textureWarp);
                SaveHistory(selectedObject.GetComponent<SDFBrush>(), false, false);
            }
            if(mode == 11 && selectedObject)
            {
                RememberLast();
                selectedObject.GetComponent<SDFBrush>().brushType = SDFBrush.SDFBrushType.Add;
                SaveHistory(selectedObject.GetComponent<SDFBrush>(), false, false);
                PlaySFX(addSFX);
            }
            if(mode == 12 && selectedObject)
            {
                RememberLast();
                selectedObject.GetComponent<SDFBrush>().brushType = SDFBrush.SDFBrushType.Subtract;
                SaveHistory(selectedObject.GetComponent<SDFBrush>(), false, false);
                PlaySFX(removeSFX);
            }
            if(mode == 13 && selectedObject)
            {
                RememberLast();
                selectedObject.GetComponent<SDFBrush>().brushType = SDFBrush.SDFBrushType.Intersection;
                SaveHistory(selectedObject.GetComponent<SDFBrush>(), false, false);
                PlaySFX(removeSFX);
            }
            if(mode == 14 && selectedObject)
            {
                RememberLast();
                selectedObject.GetComponent<SDFBrush>().brushType = SDFBrush.SDFBrushType.InvertedIntersection;
                SaveHistory(selectedObject.GetComponent<SDFBrush>(), false, false);
                PlaySFX(removeSFX);
            }
            if(mode == 15 && selectedObject)
            {
                RememberLast();
                selectedObject.GetComponent<SDFBrush>().brushType = SDFBrush.SDFBrushType.Paint;
                SaveHistory(selectedObject.GetComponent<SDFBrush>(), false, false);
                PlaySFX(paintSFX);
            }
            if(mode >= 10 && mode <= 15 && selectedObject)
            {
                meshGenerator.UpdateBrush(selectedObject.GetComponent<SDFBrush>());
                meshGenerator.TriggerMeshUpdate();
                selectedObject = null;
                selectedBrush = null;
            }
            if(mode != 16 && duplicatePause)
            {
                duplicatePause = false;
                newBrush = false;
            }

            if(mode == 9)
            {
                if(tools.activeInHierarchy)
                    tools.SetActive(false);
                if(!shapes.activeInHierarchy)
                    shapes.SetActive(true);
                if(!colors.activeInHierarchy)
                    colors.SetActive(true);

                Physics.Raycast(myCam.ScreenPointToRay(Input.mousePosition + new Vector3(-2,-2, 0)), out hit, 1, mask, QueryTriggerInteraction.Ignore);
                if(hit.collider != null)
                {
                    currentText.text = hit.collider.gameObject.name;
                }
                if(Input.GetMouseButtonDown(0))
                {
                    bool SelectSomething = false;
                    if(!colorPicking)
                    {
                        if(hit.collider != null)
                        {
                            if(hit.collider.gameObject.name == "Sphere")
                            {
                                selectedObject = GameObject.Instantiate(hudData.sphereBrushPrefab,
                                    transform.position + transform.forward * 5,
                                    Quaternion.LookRotation(transform.forward, Vector3.up), meshGenerator.transform);
                            }

                            if(hit.collider.gameObject.name == "Ellipsoid")
                            {
                                selectedObject = GameObject.Instantiate(hudData.ellipsoidBrushPrefab,
                                    transform.position + transform.forward * 5,
                                    Quaternion.LookRotation(transform.forward, Vector3.up), meshGenerator.transform);
                            }

                            if(hit.collider.gameObject.name == "Box" || hit.collider.gameObject.name == "Cylinder" ||
                            hit.collider.gameObject.name == "Cone" || hit.collider.gameObject.name == "Pyramid" ||
                            hit.collider.gameObject.name == "HexPrism" || hit.collider.gameObject.name == "TriPrism" ||
                            hit.collider.gameObject.name == "SBox" || hit.collider.gameObject.name == "SCone" ||
                            hit.collider.gameObject.name == "SCylinder")
                            {
                                selectedObject = GameObject.Instantiate(hudData.boxBrushPrefab,
                                    transform.position + transform.forward * 5,
                                    Quaternion.LookRotation(transform.forward, Vector3.up), meshGenerator.transform);
                            }

                            if(hit.collider.gameObject.name == "Torus")
                            {
                                selectedObject = GameObject.Instantiate(hudData.torusBrushPrefab,
                                    transform.position + transform.forward * 5,
                                    Quaternion.LookRotation(transform.forward, Vector3.up), meshGenerator.transform);
                            }

                            if(hit.collider.gameObject.name == "Link")
                            {
                                selectedObject = GameObject.Instantiate(hudData.linkBrushPrefab,
                                    transform.position + transform.forward * 5,
                                    Quaternion.LookRotation(transform.forward, Vector3.up), meshGenerator.transform);
                            }

                            if(hit.collider.gameObject.name == "Curve")
                            {
                                selectedObject = GameObject.Instantiate(hudData.curveBrushPrefab,
                                    transform.position + transform.forward * 5,
                                    Quaternion.LookRotation(transform.forward, Vector3.up), meshGenerator.transform);
                            }

                            if(hit.collider.gameObject.name == "Box"){
                                selectedObject.GetComponent<SDFBrush>().brushShape = SDFBrush.SDFBrushShape.Box;
                                SelectSomething = true;}
                            if(hit.collider.gameObject.name == "Sphere"){
                                selectedObject.GetComponent<SDFBrush>().brushShape = SDFBrush.SDFBrushShape.Sphere;
                                SelectSomething = true;}
                            if(hit.collider.gameObject.name == "Ellipsoid"){
                                selectedObject.GetComponent<SDFBrush>().brushShape = SDFBrush.SDFBrushShape.Ellipsoid;
                                SelectSomething = true;}
                            if(hit.collider.gameObject.name == "Cylinder"){
                                selectedObject.GetComponent<SDFBrush>().brushShape = SDFBrush.SDFBrushShape.Cylinder;
                                SelectSomething = true;}
                            if(hit.collider.gameObject.name == "Cone"){
                                selectedObject.GetComponent<SDFBrush>().brushShape = SDFBrush.SDFBrushShape.Cone;
                                SelectSomething = true;}
                            if(hit.collider.gameObject.name == "Pyramid"){
                                selectedObject.GetComponent<SDFBrush>().brushShape = SDFBrush.SDFBrushShape.Pyramid;
                                SelectSomething = true;}
                            if(hit.collider.gameObject.name == "HexPrism"){
                                selectedObject.GetComponent<SDFBrush>().brushShape = SDFBrush.SDFBrushShape.HexagonalPrism;
                                SelectSomething = true;}
                            if(hit.collider.gameObject.name == "TriPrism"){
                                selectedObject.GetComponent<SDFBrush>().brushShape = SDFBrush.SDFBrushShape.TriangularPrism;
                                SelectSomething = true;}
                            if(hit.collider.gameObject.name == "SBox"){
                                selectedObject.GetComponent<SDFBrush>().brushShape = SDFBrush.SDFBrushShape.RoundedBox;
                                SelectSomething = true;}
                            if(hit.collider.gameObject.name == "SCone"){
                                selectedObject.GetComponent<SDFBrush>().brushShape = SDFBrush.SDFBrushShape.RoundedCone;
                                SelectSomething = true;}
                            if(hit.collider.gameObject.name == "SCylinder"){
                                selectedObject.GetComponent<SDFBrush>().brushShape = SDFBrush.SDFBrushShape.RoundedCylinder;
                                SelectSomething = true;}
                            if(hit.collider.gameObject.name == "Torus"){
                                selectedObject.GetComponent<SDFBrush>().brushShape = SDFBrush.SDFBrushShape.Torus;
                                SelectSomething = true;}
                            if(hit.collider.gameObject.name == "Link"){
                                selectedObject.GetComponent<SDFBrush>().brushShape = SDFBrush.SDFBrushShape.Link;
                                SelectSomething = true;}
                            if(hit.collider.gameObject.name == "Curve"){
                                selectedObject.GetComponent<SDFBrush>().brushShape = SDFBrush.SDFBrushShape.BezierCurve;
                                SelectSomething = true;}

                            if(hit.collider.gameObject.name == "EyeDropper")
                            {
                                colorPicking = true;
                                eyeDropper.color = Color.gray;
                            }
                            if(selectedObject)
                            {
                                selectedObject.transform.SetSiblingIndex(meshGenerator.transform.childCount - 1);
                                selectedObject.GetComponent<SDFBrush>().enabled = true;
                                selectedObject.name = hit.collider.gameObject.name;
                            }
                        }
                    }
                    else
                    {
                        //StartCoroutine(ColorPicker());
                        Physics.Raycast(myCam.ScreenPointToRay(Input.mousePosition + new Vector3(-2,-2, 0)), out hit, 1000, mask, QueryTriggerInteraction.Ignore);
                        if(hit.collider != null)
                        {
                            if(hit.collider.gameObject.GetComponent<SDFBrush>())
                            {
                                SetColorPickerColor(hit.collider.gameObject.GetComponent<SDFBrush>().color);
                            }
                        }
                    }

                    if(SelectSomething)
                    {
                        mode = 1;
                        newBrush = true;
                        mouseLookToggle = true;
                        Cursor.visible = false;
                        Cursor.lockState = CursorLockMode.Locked;
                        selectedBrush = selectedObject.GetComponent<SDFBrush>();
                        selectedBrush.color = colorOutput.color;
                        currentText.text = "Grab";
                        //meshGenerator.AddBrush(selectedObject.GetComponent<SDFBrush>(), selectedObject.transform.GetSiblingIndex());
                        meshGenerator.TriggerMeshUpdate();
                        RememberLast();
                    }
                }
                if(Input.GetMouseButton(0) && mode != 1)
                {
                    if(hit.collider != null)
                    {
                        ColorSliders(); 
                    }
                }
            }
            if(mode == 8 && selectedObject != null)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                mouseLookToggle = false;
                if(!colors.activeInHierarchy)
                    colors.SetActive(true);
                if(tools.activeInHierarchy)
                    tools.SetActive(false);
                if(shapes.activeInHierarchy)
                    shapes.SetActive(false);

                Physics.Raycast(myCam.ScreenPointToRay(Input.mousePosition + new Vector3(-2,-2, 0)), out hit, 1, mask, QueryTriggerInteraction.Ignore);
                if(hit.collider != null)
                {
                    currentText.text = hit.collider.gameObject.name;
                }
                if(Input.GetMouseButtonDown(0))
                {
                    if(!colorPicking)
                    {
                        if(hit.collider != null)
                        {
                            if(hit.collider.gameObject.name == "EyeDropper")
                            {
                                colorPicking = true;
                                eyeDropper.color = Color.gray;
                            }
                        }
                    }
                    else
                    {
                        //StartCoroutine(ColorPicker());
                        Physics.Raycast(myCam.ScreenPointToRay(Input.mousePosition + new Vector3(-2,-2, 0)), out hit, 1000, mask, QueryTriggerInteraction.Ignore);
                        if(hit.collider != null)
                        {
                            if(hit.collider.gameObject.GetComponent<SDFBrush>())
                            {
                                SetColorPickerColor(hit.collider.gameObject.GetComponent<SDFBrush>().color);
                            }
                        }
                    }
                }
                if(Input.GetMouseButton(0) && mode != 1)
                {
                    if(hit.collider != null)
                    {
                        ColorSliders(); 
                    }
                }
                if(Input.GetMouseButtonUp(0))
                {
                    selectedObject.GetComponent<SDFBrush>().color = colorOutput.color;
                    meshGenerator.TriggerMeshUpdate();
                }

                /*Physics.Raycast(myCam.ScreenPointToRay(Input.mousePosition), out hit, 1, mask, QueryTriggerInteraction.Ignore);
                if(hit.collider != null)
                {
                    currentText.text = hit.collider.gameObject.name;
                }

                float shift = 0.025f;
                if(Input.GetKey(hudData.speedKey))
                    shift = 0.05f;

                Color col = selectedObject.GetComponent<SDFBrush>().color;
                float r = col.r;
                float g = col.g;
                float b = col.b;
                if(Input.GetKey(hudData.xAxisKey))
                {
                    r += (Input.GetAxisRaw("Mouse X") + Input.GetAxisRaw("Mouse Y")) * shift;
                    currentText.text = "Color Red: " + r * 255;
                }
                if(Input.GetKey(hudData.yAxisKey))
                {
                    g += (Input.GetAxisRaw("Mouse X") + Input.GetAxisRaw("Mouse Y")) * shift;
                    currentText.text = " Color Green: " + g * 255;
                }
                if(Input.GetKey(hudData.zAxisKey))
                {
                    b += (Input.GetAxisRaw("Mouse X") + Input.GetAxisRaw("Mouse Y")) * shift;
                    currentText.text = "Color Blue: " + b * 255;
                }

                if((Input.GetKey(hudData.xAxisKey) || Input.GetKey(hudData.yAxisKey) || Input.GetKey(hudData.zAxisKey)))
                {
                    selectedObject.GetComponent<SDFBrush>().color = new Color(Mathf.Clamp(r,0,1), Mathf.Clamp(g,0,1), Mathf.Clamp(b,0,1), 1);
                    meshGenerator.TriggerMeshUpdate();
                }
                else
                {
                    currentText.text = "Color [X]-Red [Y]-Green [Z]-Blue";
                }*/
            }
            else if(mode == 8 && selectedObject == null)
            {
                currentText.text = "Color";
            }

            if(Input.mouseScrollDelta.y != 0 && (Input.GetKey(hudData.speedKey) == false || selectedObject != null))
                inputVertical += Input.mouseScrollDelta.y * 10;


            Vector3 adjustedForward = transform.forward;
            Vector3 adjustedRight = transform.right;
            Vector3 adjustedUp = transform.up;

            if(selectedObject)
            {
                adjustedForward = (selectedObject.transform.position - transform.position).normalized;
                distanceSpeed = Vector3.Magnitude(selectedObject.transform.position - transform.position) / 20;
            }

            if(Input.GetKey(hudData.xAxisKey) || Input.GetKey(hudData.yAxisKey) || Input.GetKey(hudData.zAxisKey))
            {
                adjustedForward = Vector3.forward;
                adjustedRight = Vector3.right;
                adjustedUp = Vector3.up;

                //adjustedForward = new Vector3(Mathf.Round(transform.forward.x), Mathf.Round(transform.forward.y),Mathf.Round(transform.forward.z));
                //adjustedRight = new Vector3(Mathf.Round(transform.right.x), Mathf.Round(transform.right.y),Mathf.Round(transform.right.z));
                //adjustedUp = new Vector3(Mathf.Round(transform.up.x), Mathf.Round(transform.up.y),Mathf.Round(transform.up.z));
            }

            
            if(selectedObject == null) // Navigation
            {
                if(Input.GetKey(hudData.moveForwardKey))
                    inputVertical += 1;
                if(Input.GetKey(hudData.moveBackwardKey))
                    inputVertical -= 1;
                if(Input.GetKey(hudData.moveLeftKey))
                    inputHorizontal -= 1;
                if(Input.GetKey(hudData.moveRightKey))
                    inputHorizontal += 1;
                if(Input.GetKey(hudData.moveUpKey))
                    inputYAxis += 1;
                if(Input.GetKey(hudData.moveDownKey))
                    inputYAxis -= 1;
                if(Input.GetKey(hudData.speedKey))
                    speedBoost = speedBoostAmount;
                else
                    speedBoost = 1;

                if(mode == 4)
                    currentText.text = "Blend";
                if(mode == 5)
                    currentText.text = "Roundness";
                if(mode == 6)
                    currentText.text = "WarpStrength";
                if(mode == 7)
                    currentText.text = "WarpScale";

            }
            else if((mode == 1 || mode == 16) && selectedObject != null) // Grab
            {
                xAxis.transform.rotation = Quaternion.identity;
                yAxis.transform.rotation = Quaternion.identity;
                zAxis.transform.rotation = Quaternion.identity;
                if((!Input.GetKey(hudData.xAxisKey) && !Input.GetKey(hudData.yAxisKey) && !Input.GetKey(hudData.zAxisKey)))
                {
                    if(Input.GetKey(hudData.moveForwardKey))
                        selectedObject.transform.position += Vector3.ClampMagnitude((adjustedForward * Time.deltaTime * editSpeed * distanceSpeed),1);
                    if(Input.GetKey(hudData.moveBackwardKey))
                        selectedObject.transform.position += Vector3.ClampMagnitude((adjustedForward * -1 * Time.deltaTime * editSpeed * distanceSpeed),1);
                }
                else if(Input.GetKey(hudData.zAxisKey))
                {
                    float XInf = Vector3.Dot(transform.right, Vector3.forward);
                    float YInf = Vector3.Dot(transform.up, Vector3.forward);

                    selectedObject.transform.position += (adjustedForward * 2 * Time.deltaTime * editSpeed * distanceSpeed *
                    (Input.GetAxisRaw("Mouse X") * XInf +  Input.GetAxisRaw("Mouse Y") * YInf));
                }

                if((!Input.GetKey(hudData.xAxisKey) && !Input.GetKey(hudData.yAxisKey) && !Input.GetKey(hudData.zAxisKey)))
                {
                    selectedObject.transform.position += Vector3.ClampMagnitude(((adjustedRight * Input.GetAxisRaw("Mouse X") * 2)
                    * Time.deltaTime * editSpeed * distanceSpeed),1);
                }
                else if(Input.GetKey(hudData.xAxisKey))
                {
                    float XInf = Vector3.Dot(transform.right, Vector3.right);
                    float YInf = Vector3.Dot(transform.up, Vector3.right);
                    
                    selectedObject.transform.position += (adjustedRight * 2 * Time.deltaTime * editSpeed * distanceSpeed *
                    (Input.GetAxisRaw("Mouse X") * XInf + Input.GetAxisRaw("Mouse Y") * YInf));
                }

                if((!Input.GetKey(hudData.xAxisKey) && !Input.GetKey(hudData.yAxisKey) && !Input.GetKey(hudData.zAxisKey)))
                {
                    selectedObject.transform.position += Vector3.ClampMagnitude(((adjustedUp * Input.GetAxisRaw("Mouse Y") * 2)
                    * Time.deltaTime * editSpeed * distanceSpeed), 1);
                }
                else if(Input.GetKey(hudData.yAxisKey))
                {
                    float XInf = 0;
                    float YInf = 1;
                    
                    selectedObject.transform.position += (adjustedUp * 2 * Time.deltaTime * editSpeed * distanceSpeed *
                    (Input.GetAxisRaw("Mouse X") * XInf +  Input.GetAxisRaw("Mouse Y") * YInf));
                }
            }
            else if(mode == 2 && selectedObject != null) // Rotate
            {
                xAxis.transform.rotation = selectedObject.transform.rotation; //Quaternion.identity;
                yAxis.transform.rotation = selectedObject.transform.rotation; //Quaternion.identity;
                zAxis.transform.rotation = selectedObject.transform.rotation; //Quaternion.identity;
                int shift = 2;
                //if(Input.GetKey(KeyCode.LeftControl))
                //    shift = 1;
                if(Input.GetKey(hudData.speedKey))
                    shift = 4;

                if(Input.GetKey(hudData.rotationSnappingKey))
                    shift *= 15; 

                if((!Input.GetKey(hudData.xAxisKey) && !Input.GetKey(hudData.yAxisKey) && !Input.GetKey(hudData.zAxisKey)))
                {
                    selectedObject.transform.RotateAround(selectedObject.transform.position, transform.up, Mathf.Clamp(
                        shift * 20 * Time.deltaTime * editSpeed * Input.GetAxisRaw("Mouse X"), -5, 5));
                    selectedObject.transform.RotateAround(selectedObject.transform.position, transform.right, Mathf.Clamp(
                        shift * 20 * Time.deltaTime * editSpeed * Input.GetAxisRaw("Mouse Y"), -5, 5));
                }
                else if(Input.GetKey(hudData.xAxisKey))
                {
                    float XInf = Vector3.Dot(transform.right, Vector3.forward);
                    float YInf = Vector3.Dot(transform.up, Vector3.forward);
                    selectedObject.transform.RotateAround(selectedObject.transform.position, selectedObject.transform.right, shift * 20 * Time.deltaTime * editSpeed
                    * (Input.GetAxisRaw("Mouse X") * XInf + Input.GetAxisRaw("Mouse Y") * YInf));
                }
                else if(Input.GetKey(hudData.yAxisKey))
                {
                    float XInf = 0;
                    float YInf = 1; 
                    selectedObject.transform.RotateAround(selectedObject.transform.position, selectedObject.transform.up, shift * 20 * Time.deltaTime * editSpeed
                    * (Input.GetAxisRaw("Mouse X") * XInf + Input.GetAxisRaw("Mouse Y") * YInf));
                }
                else if(Input.GetKey(hudData.zAxisKey))
                {
                    float XInf = Vector3.Dot(transform.right, Vector3.right);
                    float YInf = Vector3.Dot(transform.up, Vector3.right);
                    selectedObject.transform.RotateAround(selectedObject.transform.position, selectedObject.transform.forward, shift * 20 * Time.deltaTime * editSpeed
                    * (Input.GetAxisRaw("Mouse X") * XInf + Input.GetAxisRaw("Mouse Y") * YInf));
                }

                if(Input.GetKey(hudData.rotationSnappingKey))
                {
                    selectedObject.transform.rotation = Quaternion.Euler(Mathf.Round(selectedObject.transform.eulerAngles.x/10) * 10, 
                    Mathf.Round(selectedObject.transform.eulerAngles.y/45) * 45,
                    Mathf.Round(selectedObject.transform.eulerAngles.z/45) * 45);
                }
            }
            else if(mode == 3 && selectedObject != null) // Scale
            {
                xAxis.transform.rotation = selectedObject.transform.rotation;
                yAxis.transform.rotation = selectedObject.transform.rotation;
                zAxis.transform.rotation = selectedObject.transform.rotation;
                float shift = 0.5f;
                //if(Input.GetKey(KeyCode.LeftControl))
                //    shift = 0.25f;
                if(Input.GetKey(hudData.speedKey))
                    shift = 1;

                if((!Input.GetKey(hudData.xAxisKey) && !Input.GetKey(hudData.yAxisKey) && !Input.GetKey(hudData.zAxisKey)))
                {
                    selectedObject.transform.localScale += Vector3.one * ((Input.GetAxisRaw("Mouse X") + Input.GetAxisRaw("Mouse Y")) * shift);
                }
                else if(Input.GetKey(hudData.xAxisKey))
                {
                    selectedObject.transform.localScale += Vector3.right * ((Input.GetAxisRaw("Mouse X") + Input.GetAxisRaw("Mouse Y")) * shift);
                }
                else if(Input.GetKey(hudData.yAxisKey))
                {
                    selectedObject.transform.localScale += Vector3.up * ((Input.GetAxisRaw("Mouse X") + Input.GetAxisRaw("Mouse Y")) * shift);
                }
                else if(Input.GetKey(hudData.zAxisKey))
                {
                    selectedObject.transform.localScale += Vector3.forward * ((Input.GetAxisRaw("Mouse X") + Input.GetAxisRaw("Mouse Y")) * shift);
                }
            }
            else if(mode == 4 && selectedObject != null) // Blend
            {
                float shift = 0.02f;
                if(Input.GetKey(hudData.speedKey))
                    shift = 0.05f;

                selectedBrush.blend += (Input.GetAxisRaw("Mouse X") + Input.GetAxisRaw("Mouse Y"))* shift;
                selectedBrush.blend = Mathf.Clamp(selectedBrush.blend, 0, 10);
                meshGenerator.UpdateBrush(selectedBrush);
                meshGenerator.TriggerMeshUpdate();
                currentText.text = "Blend: " + selectedBrush.blend;

            }
            else if(mode == 5 && selectedObject != null) // Roundness
            {
                float shift = 0.01f;
                if(Input.GetKey(hudData.speedKey))
                    shift = 0.05f;

                selectedBrush.roundA += (Input.GetAxisRaw("Mouse X") + Input.GetAxisRaw("Mouse Y")) * shift;
                selectedBrush.roundA = Mathf.Clamp(selectedBrush.roundA, 0, 10);
                meshGenerator.UpdateBrush(selectedBrush);
                meshGenerator.TriggerMeshUpdate();
                if(cantRound)
                    currentText.text = "No Rounding For This Shape";
                else
                    currentText.text = "Roundness: " + selectedBrush.roundA;
            }
            else if(mode == 6 && selectedObject != null) // Warp Strength
            {
                float shift = 0.01f;
                if(Input.GetKey(hudData.speedKey))
                    shift = 0.05f;

                selectedBrush.warpStrength += (Input.GetAxisRaw("Mouse X") + Input.GetAxisRaw("Mouse Y")) * shift;
                selectedBrush.warpStrength = Mathf.Clamp(selectedBrush.warpStrength, 0, 1);
                meshGenerator.UpdateBrush(selectedBrush);
                meshGenerator.TriggerMeshUpdate();
                currentText.text = "WarpStrength: " + selectedBrush.warpStrength;

            }
            else if(mode == 7 && selectedObject != null) // Warp Scale
            {
                float shift = 0.01f;
                if(Input.GetKey(hudData.speedKey))
                    shift = 0.05f;

                selectedBrush.warpScale += (Input.GetAxisRaw("Mouse X") + Input.GetAxisRaw("Mouse Y")) * shift;
                selectedBrush.warpScale = Mathf.Clamp(selectedBrush.warpScale, 0, 1);
                meshGenerator.UpdateBrush(selectedBrush);
                meshGenerator.TriggerMeshUpdate();
                currentText.text = "WarpScale: " + selectedBrush.warpScale;

            }

            if(mouseLookToggle == false && mode != 9 && mode != 8)
            {
                if(!tools.activeInHierarchy)
                    tools.SetActive(true);
                //if(!shapes.activeInHierarchy)
                //    shapes.SetActive(true);
                    
                Physics.Raycast(myCam.ScreenPointToRay(Input.mousePosition), out hit2, 1, mask, QueryTriggerInteraction.Ignore);
                if(hit2.collider != null)
                {
                    currentText.text = hit2.collider.gameObject.name;
                    if(currentText.text == "Grab")
                        descriptionText.text = "[" + hudData.grabShortcut.ToString() + "]-Move a brush, [" + hudData.moveForwardKey.ToString() + "]/[" + hudData.moveBackwardKey.ToString() + "] to move back and forth, [" + hudData.xAxisKey.ToString() + "]/[" + hudData.yAxisKey.ToString() + "]/[" + hudData.zAxisKey.ToString() + "] to lock axis.";
                    else if(currentText.text == "Rotate")
                        descriptionText.text = "[" + hudData.rotateShortcut.ToString() + "]-Rotate a brush, [" + hudData.rotationSnappingKey.ToString() + "] to snap angles, [" + hudData.xAxisKey.ToString() + "]/[" + hudData.yAxisKey.ToString() + "]/[" + hudData.zAxisKey.ToString() + "] to lock axis.";
                    else if(currentText.text == "Scale")
                        descriptionText.text = "[" + hudData.scaleShortcut.ToString() + "]-Scale a brush, [" + hudData.xAxisKey.ToString() + "]/[" + hudData.yAxisKey.ToString() + "]/[" + hudData.zAxisKey.ToString() + "] to scale on a local axis.";
                    else if(currentText.text == "Blend")
                        descriptionText.text = "[" + hudData.blendShortcut.ToString() + "]-Blend brush with other brushes. [" + hudData.speedKey.ToString() + "] for speed.";
                    else if(currentText.text == "Roundness")
                        descriptionText.text = "[" + hudData.roundShortcut.ToString() + "]-Roundness of a brush (Only some shapes). [" + hudData.speedKey.ToString() + "] for speed.";
                    else if(currentText.text == "WarpStrength")
                        descriptionText.text = "[" + hudData.warpStrengthShortcut.ToString() + "]-Warp strength of a brush. [" + hudData.speedKey.ToString() + "] for speed.";
                    else if(currentText.text == "WarpScale")
                        descriptionText.text = "[" + hudData.warpScaleShortcut.ToString() + "]-Warp scale of a brush. [" + hudData.speedKey.ToString() + "] for speed.";
                    else if(currentText.text == "Color")
                        descriptionText.text = "[" + hudData.colorShortcut.ToString() + "]-Color of a brush. [" + hudData.speedKey.ToString() + "] for speed.";
                    else if(currentText.text == "Brush")
                        descriptionText.text = "[" + hudData.brushShortcut.ToString() + "]-Pick a brush to add to the sculpt.";
                    else if(currentText.text == "WarpPattern")
                        descriptionText.text = "[" + hudData.warpPatternShortcut.ToString() + "]-Swap between warp patterns, there are 2 patterns.";
                    else if(currentText.text == "Add")
                        descriptionText.text = "[" + hudData.addShortcut.ToString() + "]-Set any brush to \"ADD\" mode.";
                    else if(currentText.text == "Subtract")
                        descriptionText.text = "[" + hudData.subtractShortcut.ToString() + "]-Set any brush to \"SUBTRACT\" mode.";
                    else if(currentText.text == "Intersect")
                        descriptionText.text = "[" + hudData.intersectionShortcut.ToString() + "]-Set any brush to \"INTERSECTION\" mode.";
                    else if(currentText.text == "InverseIntersect")
                        descriptionText.text = "[" + hudData.inverseIntersectionShortcut.ToString() + "]-Set any brush to \"INVERSE INTERSECTION\" mode.";
                    else if(currentText.text == "Paint")
                        descriptionText.text = "[" + hudData.paintShortcut.ToString() + "]-Set any brush to \"PAINT\" mode.";
                    else if(currentText.text == "Duplicate")
                        descriptionText.text = "[" + hudData.duplicateShortcut.ToString() + "]-Duplicate a brush.";
                    else if(currentText.text == "Delete")
                        descriptionText.text = "[" + hudData.deleteShortcut.ToString() + "]-Delete a brush.";
                    else if(currentText.text == "Done")
                        descriptionText.text = "[No Shortcut]-Exit sculpt mode.";
                    else if(currentText.text == "Undo")
                        descriptionText.text = "[" + hudData.undoShortcut.ToString() + "]-Undo the last edit.";
                    else if(currentText.text == "Redo")
                        descriptionText.text = "[" + hudData.redoShortcut.ToString() + "]-Redo the last undone edit.";
                    else if(currentText.text == "RoateAroundX")
                        descriptionText.text = "[No Shortcut]-Rotate Sculpt Around X Axis by 180 degrees.";
                    else if(currentText.text == "RoateAroundY")
                        descriptionText.text = "[No Shortcut]-Rotate Sculpt Around Y Axis by 180 degrees.";
                    else if(currentText.text == "RoateAroundZ")
                        descriptionText.text = "[No Shortcut]-Rotate Sculpt Around Z Axis by 180 degrees.";
                    else if(currentText.text == "GridX")
                        descriptionText.text = "[No Shortcut]-Toggle Grid facing Left/Right or on Y,Z plane.";
                    else if(currentText.text == "GridY")
                        descriptionText.text = "[No Shortcut]-Toggle Grid facing Up/Down or on X,Z plane.";
                    else if(currentText.text == "GridZ")
                        descriptionText.text = "[No Shortcut]-Toggle Grid facing Front/Back or on X,Y plane.";
                    else if(currentText.text == "FuzzyPreview")
                        descriptionText.text = "[No Shortcut]-Preview what this sculpt looks like with fuzz settings.";
                }
                else
                {
                    currentText.text = "Select A Tool";
                    descriptionText.text = "...";
                }
                if(Input.GetMouseButtonDown(0))
                {
                    //Debug.Log(currentText.text);
                    if(currentText.text == "Grab")
                    {
                        mode = 1;
                    }
                    else if(currentText.text == "Rotate")
                    {
                        mode = 2;
                    }
                    else if(currentText.text == "Scale")
                    {
                        mode = 3;
                    }
                    else if(currentText.text == "Blend")
                    {
                        mode = 4;
                    }
                    else if(currentText.text == "Roundness")
                    {
                        mode = 5;
                    }
                    else if(currentText.text == "WarpStrength")
                    {
                        mode = 6;
                    }
                    else if(currentText.text == "WarpScale")
                    {
                        mode = 7;
                    }
                    else if(currentText.text == "Color")
                    {
                        mode = 8;
                        if(tools.activeInHierarchy)
                            tools.SetActive(false);
                        if(shapes.activeInHierarchy)
                            shapes.SetActive(false);
                        crossHair.material.mainTexture = hudData.colorIcon;
                    }
                    else if(currentText.text == "Brush")
                    {
                        mode = 9;
                        crossHair.material.mainTexture = hudData.brushIcon;
                    }
                    else if(currentText.text == "WarpPattern")
                    {
                        mode = 10;
                    }
                    else if(currentText.text == "Add")
                    {
                        mode = 11;
                    }
                    else if(currentText.text == "Subtract")
                    {
                        mode = 12;
                    }
                    else if(currentText.text == "Intersection")
                    {
                        mode = 13;
                    }
                    else if(currentText.text == "InverseIntersection")
                    {
                        mode = 14;
                    }
                    else if(currentText.text == "Paint")
                    {
                        mode = 15;
                    }
                    else if(currentText.text == "Duplicate")
                    {
                        mode = 16;
                    }
                    else if(currentText.text == "Delete")
                    {
                        mode = 17;
                    }
                    else if(currentText.text == "Done")
                    {
                        meshGenerator.SaveRecipe();
                        editMode = false;
                        crossHair.material.mainTexture = hudData.crossHairIcon;
                        if(debugHistory)
                            Debug.Log("Wiping History");
                        history.Clear();
                        currentHistoryAction = 0;
                        gtHighlighter.enabled = false;
                        mouseLookToggle = true;
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                    }
                    else if(currentText.text == "Undo")
                    {
                        pressedTheUndoBtn = true;
                    }
                    else if(currentText.text == "Redo")
                    {
                        pressedTheRedoBtn = true;
                    }
                    else if(currentText.text == "RoateAroundX")
                    {
                        meshGenerator.FlipOverAxis(Vector3.right);
                    }
                    else if(currentText.text == "RoateAroundY")
                    {
                        meshGenerator.FlipOverAxis(Vector3.up);
                    }
                    else if(currentText.text == "RoateAroundZ")
                    {
                        meshGenerator.FlipOverAxis(Vector3.forward);
                    }
                    else if(currentText.text == "GridX")
                    {
                        meshGenerator.ToggleGridYZ();
                    }
                    else if(currentText.text == "GridY")
                    {
                        meshGenerator.ToggleGridXZ();
                    }
                    else if(currentText.text == "GridZ")
                    {
                        meshGenerator.ToggleGridXY();
                    }
                    else if(currentText.text == "FuzzyPreview")
                    {
                        meshGenerator.PreviewFuzzy();
                    }
                    if(mode != 9 && currentText.text != "Select A Tool")
                    {
                        mouseLookToggle = true;
                        selectedObject = null;
                        selectedBrush = null;
                        Cursor.visible = false;
                        Cursor.lockState = CursorLockMode.Locked;
                    }
                }
            }
            else if(mode != 9 && mode != 8)
            {
                if(tools.activeInHierarchy)
                    tools.SetActive(false);
                if(shapes.activeInHierarchy)
                    shapes.SetActive(false);
                if(colors.activeInHierarchy)
                    colors.SetActive(false);

                if(mode == 0)
                    crossHair.material.mainTexture = hudData.crossHairIcon;
                if(mode == 1)
                    crossHair.material.mainTexture = hudData.grabIcon;
                if(mode == 2)
                    crossHair.material.mainTexture = hudData.rotateIcon;
                if(mode == 3)
                    crossHair.material.mainTexture = hudData.scaleIcon;
                if(mode == 4)
                    crossHair.material.mainTexture = hudData.blendIcon;
                if(mode == 5)
                    crossHair.material.mainTexture = hudData.roundIcon;
                if(mode == 6)
                    crossHair.material.mainTexture = hudData.warpStrengthIcon;
                if(mode == 7)
                    crossHair.material.mainTexture = hudData.warpScaleIcon;
                if(mode == 8)
                    crossHair.material.mainTexture = hudData.colorIcon;
                if(mode == 9)
                    crossHair.material.mainTexture = hudData.brushIcon;

                if(mode == 10)
                    crossHair.material.mainTexture = hudData.warpPatternIcon;
                if(mode == 11)
                    crossHair.material.mainTexture = hudData.addIcon;
                if(mode == 12)
                    crossHair.material.mainTexture = hudData.subtractIcon;
                if(mode == 13)
                    crossHair.material.mainTexture = hudData.intersectionIcon;
                if(mode == 14)
                    crossHair.material.mainTexture = hudData.inverseIntersectionIcon;
                if(mode == 15)
                    crossHair.material.mainTexture = hudData.paintIcon;
                if(mode == 16)
                    crossHair.material.mainTexture = hudData.duplicateIcon;
                if(mode == 17)
                    crossHair.material.mainTexture = hudData.deleteIcon;
            }

            if((((Input.GetKeyDown(hudData.undoShortcut) || pressedTheUndoBtn) && currentHistoryAction > 0) || 
            ((Input.GetKeyDown(hudData.redoShortcut) || pressedTheRedoBtn) && currentHistoryAction < history.Count))
            && !selectedObject && !duplicatePause && !colorPicking && !meshGenerator.IsUpdating() &&
            !(!mouseLookToggle && mode == 9) && !(!mouseLookToggle && mode == 8))
            {
                int undoRedo = 1;
                if(Input.GetKeyDown(hudData.undoShortcut) || pressedTheUndoBtn)
                    undoRedo = -1;

                //print(undoRedo);

                //bool spawnBrush = false;
                //bool despawnBrush = false;
                int prevHistoryIndex = currentHistoryAction - 1;

                if(undoRedo == -1)
                {
                    //prevHistoryIndex = Mathf.Min(prevHistoryIndex, history.Count-1);
                    //if(currentHistoryAction >= 0)
                    //{
                    //    if(history[prevHistoryIndex].actionType == 0)
                    //        despawnBrush = true;
                    //}
                    //currentHistoryAction -= 1;
                    //if(history[currentHistoryAction].actionType == 2)
                    //    spawnBrush = true;
                    currentHistoryAction -= 1;
                }
                if(undoRedo == 1)
                {
                    //prevHistoryIndex = Mathf.Max(prevHistoryIndex, 0);
                    //if(history[prevHistoryIndex].actionType == 0)
                    //    spawnBrush = true;
                    //currentHistoryAction += 1;
                    //if(currentHistoryAction < history.Count)
                    //{
                    //    if(history[currentHistoryAction].actionType == 1)
                    //        despawnBrush = true;
                    //}
                }

                if(debugHistory)
                {
                    if(undoRedo == -1)
                        Debug.Log("Undo Edit #" + currentHistoryAction);
                    if(undoRedo == 1)
                        Debug.Log("Redo Edit #" + currentHistoryAction);
                }
                //Debug.Log(undoRedo.ToString() + " -- history: "+ history.Count.ToString() + ", cur: " + (currentHistoryAction).ToString());
                //Debug.Log(undoRedo.ToString() +"name: " + history[currentHistoryAction - 1].name + " -- action: " + history[currentHistoryAction - 1].actionType.ToString());
                //Debug.Log(prevHistoryIndex);
                SDFBrush historyBrush = null;
                int historyIndex = currentHistoryAction;
                    
                //if(undoRedo == -1)
                //    currentHistoryAction += 1;

                if((history[historyIndex].actionType == 0 && undoRedo == 1) ||
                (history[historyIndex].actionType == 2 && undoRedo == -1))
                {
                    if(history[historyIndex].brushShape == 0)
                    {
                        historyBrush = GameObject.Instantiate(hudData.sphereBrushPrefab,transform.position + transform.forward * 5,
                            Quaternion.LookRotation(transform.forward, Vector3.up), meshGenerator.transform).GetComponent<SDFBrush>();
                    }
                    if(history[historyIndex].brushShape == 5)
                    {
                        historyBrush = GameObject.Instantiate(hudData.ellipsoidBrushPrefab,transform.position + transform.forward * 5,
                            Quaternion.LookRotation(transform.forward, Vector3.up), meshGenerator.transform).GetComponent<SDFBrush>();
                    }
                    if(history[historyIndex].brushShape == 1 || history[historyIndex].brushShape == 2 ||
                    history[historyIndex].brushShape == 3 || history[historyIndex].brushShape == 6 ||
                    history[historyIndex].brushShape == 7 || history[historyIndex].brushShape == 8 ||
                    history[historyIndex].brushShape == 9 || history[historyIndex].brushShape == 10 ||
                    history[historyIndex].brushShape == 11)
                    {
                        historyBrush = GameObject.Instantiate(hudData.boxBrushPrefab,transform.position + transform.forward * 5,
                            Quaternion.LookRotation(transform.forward, Vector3.up), meshGenerator.transform).GetComponent<SDFBrush>();
                    }
                    if(history[historyIndex].brushShape == 4)
                    {
                        historyBrush = GameObject.Instantiate(hudData.torusBrushPrefab,transform.position + transform.forward * 5,
                            Quaternion.LookRotation(transform.forward, Vector3.up), meshGenerator.transform).GetComponent<SDFBrush>();
                    }
                    if(history[historyIndex].brushShape == 12)
                    {
                        historyBrush = GameObject.Instantiate(hudData.linkBrushPrefab,transform.position + transform.forward * 5,
                            Quaternion.LookRotation(transform.forward, Vector3.up), meshGenerator.transform).GetComponent<SDFBrush>();
                    }
                    if(history[historyIndex].brushShape == 13)
                    {
                        historyBrush = GameObject.Instantiate(hudData.curveBrushPrefab,transform.position + transform.forward * 5,
                            Quaternion.LookRotation(transform.forward, Vector3.up), meshGenerator.transform).GetComponent<SDFBrush>();
                    }
                    historyBrush.transform.SetSiblingIndex(history[historyIndex].indexOfAction);
                    historyBrush.enabled = true;
                }

                if((history[historyIndex].actionType == 0 && undoRedo == 1) ||
                (history[historyIndex].actionType == 2 && undoRedo == -1) || history[historyIndex].actionType == 1)
                //if(spawnBrush && !despawnBrush)
                {
                    if(historyBrush == null)
                    {
                        historyBrush = meshGenerator.transform.GetChild(history[historyIndex].indexOfAction).gameObject.GetComponent<SDFBrush>();
                    }

                    //if(history[historyIndex].actionType == 1)
                    //{
                        //historyBrush = meshGenerator.transform.GetChild(history[prevHistoryIndex].indexOfAction).gameObject.GetComponent<SDFBrush>();

                    if(undoRedo == -1)
                    {
                        historyBrush.color = history[historyIndex].colorPrev;
                        historyBrush.roundA = history[historyIndex].roundAPrev;
                        historyBrush.warpScale = history[historyIndex].noiseAmountPrev;
                        historyBrush.warpStrength = history[historyIndex].noiseStrengthPrev;
                        historyBrush.textureWarp = history[historyIndex].textureNoisePrev;
                        historyBrush.blend = history[historyIndex].blendPrev;
                        historyBrush.brushType = (SDFBrush.SDFBrushType)history[historyIndex].brushTypePrev;
                        historyBrush.brushShape = (SDFBrush.SDFBrushShape)history[historyIndex].brushShapePrev;

                        historyBrush.transform.position = history[historyIndex].transformPPrev;
                        historyBrush.transform.rotation = history[historyIndex].transformRPrev;
                        historyBrush.transform.localScale = history[historyIndex].transformSPrev;
                        if(historyBrush.transform.childCount > 0)
                        {
                            historyBrush.transform.GetChild(0).position = history[historyIndex].curveAPPrev;
                            historyBrush.transform.GetChild(0).rotation = history[historyIndex].curveARPrev;
                            historyBrush.transform.GetChild(0).localScale = history[historyIndex].curveASPrev;
                            historyBrush.transform.GetChild(1).position = history[historyIndex].curveBPPrev;
                            historyBrush.transform.GetChild(1).rotation = history[historyIndex].curveBRPrev;
                            historyBrush.transform.GetChild(1).localScale = history[historyIndex].curveBSPrev;
                        }
                    }
                    else
                    {

                    //}

                        historyBrush.color = history[historyIndex].color;
                        historyBrush.roundA = history[historyIndex].roundA;
                        historyBrush.warpScale = history[historyIndex].noiseAmount;
                        historyBrush.warpStrength = history[historyIndex].noiseStrength;
                        historyBrush.textureWarp = history[historyIndex].textureNoise;
                        historyBrush.blend = history[historyIndex].blend;
                        historyBrush.brushType = (SDFBrush.SDFBrushType)history[historyIndex].brushType;
                        historyBrush.brushShape = (SDFBrush.SDFBrushShape)history[historyIndex].brushShape;

                        historyBrush.transform.position = history[historyIndex].transformP;
                        historyBrush.transform.rotation = history[historyIndex].transformR;
                        historyBrush.transform.localScale = history[historyIndex].transformS;
                        if(historyBrush.transform.childCount > 0)
                        {
                            historyBrush.transform.GetChild(0).position = history[historyIndex].curveAP;
                            historyBrush.transform.GetChild(0).rotation = history[historyIndex].curveAR;
                            historyBrush.transform.GetChild(0).localScale = history[historyIndex].curveAS;
                            historyBrush.transform.GetChild(1).position = history[historyIndex].curveBP;
                            historyBrush.transform.GetChild(1).rotation = history[historyIndex].curveBR;
                            historyBrush.transform.GetChild(1).localScale = history[historyIndex].curveBS;
                        }
                    }
                }

                if((history[historyIndex].actionType == 2 && undoRedo == 1) ||
                (history[historyIndex].actionType == 0 && undoRedo == -1))
                //if(despawnBrush)
                {
                    //if(historyIndex < 0)
                    //    historyIndex = 0;
                    historyBrush = meshGenerator.transform.GetChild(history[historyIndex].indexOfAction).gameObject.GetComponent<SDFBrush>();
                    //meshGenerator.RemoveBrush(historyBrush);
                    //meshGenerator.TriggerMeshUpdate();
                    StartCoroutine(DeleteBrush(historyBrush.gameObject));
                }
                    

                if(undoRedo == 1)
                {
                    currentHistoryAction += 1;
                }
                meshGenerator.TriggerMeshUpdate();
            }
            pressedTheRedoBtn = false;
            pressedTheUndoBtn = false;
            if(currentHistoryAction > 0)
                undoBtn.color = Color.white;
            else
                undoBtn.color = Color.gray;
            if(currentHistoryAction < history.Count)
                redoBtn.color = Color.white;
            else
                redoBtn.color = Color.gray;
            
            // looking around
            if(mouseLookToggle && selectedObject == null)
            {
                inputRotateAxisX = Input.GetAxisRaw("Mouse X") * 5;
                inputRotateAxisY = Input.GetAxisRaw("Mouse Y") * 5;

                for(int i = 0; i < Input.touchCount; i++)
                {
                    if(Input.touches[i].position.x > Screen.width / 2)
                    {
                        inputRotateAxisX += (Input.touches[i].position.x - (Screen.width / 4 + Screen.width / 2)) / (Screen.width/4);
                        inputRotateAxisY += (Input.touches[i].position.y - Screen.height / 2) / (Screen.height/2);

                        inputRotateAxisX *= 3;
                        inputRotateAxisY *= 3;
                    }
                }
            }

            float rotationX = transform.localEulerAngles.x;
            float newRotationY = transform.localEulerAngles.y + inputRotateAxisX;

            float newRotationX = (rotationX - inputRotateAxisY);
            if (rotationX <= 90.0f && newRotationX >= 0.0f)
                newRotationX = Mathf.Clamp(newRotationX, 0.0f, 90.0f);
            if (rotationX >= 270.0f)
                newRotationX = Mathf.Clamp(newRotationX, 270.0f, 360.0f);

            transform.localRotation = Quaternion.Euler(newRotationX, newRotationY, transform.localEulerAngles.z);

            // moving around
            float moveSpeedUnscaled = Time.unscaledDeltaTime * moveSpeed * speedBoost;

            transform.position += transform.forward * moveSpeedUnscaled * inputVertical;
            transform.position += transform.right * moveSpeedUnscaled * inputHorizontal;
            transform.position += Vector3.up * moveSpeedUnscaled * inputYAxis;
        }
    }
}