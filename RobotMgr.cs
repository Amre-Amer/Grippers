using System;
using System.Collections.Generic;
using UnityEngine;

public class RobotMgr : MonoBehaviour
{
    // have findPackage work with Update, instead of estimating.
    // will take longer, but won't need scale factors, more resilient (!)
    
    public float angleTorso;
    public float angleArmUpper;
    public float angleArmLower;
    public float angleGripperUpper;
    public float angleGripperLower;
    private float anglePackage;

    public GameObject torso;
    public GameObject armUpper;
    public GameObject armLower;
    public GameObject grippers;
    public GameObject gripperUpper;
    public GameObject gripperLower;
    public GameObject package;
    public GameObject target0;
    public GameObject target1;
    public GameObject targetUpperLeft;
    public GameObject targetUpperRight;
    public Camera cam;
    public GameObject display;
    public GameObject display2;
    public GameObject displayBorder;
    public Renderer displayBorderRenderer;
    public GameObject target;
    private GameObject targetPrev;

    private float angleTorsoNew = 0;
    private float angleArmUpperNew = 0;
    private float angleArmLowerNew = 0;
    private float angleGripperUpperNew = 0;
    private float angleGripperLowerNew = 0;
    private Vector3 posHandle;
    private Vector3 eulHandle;

    public float lengthArms;
    public float smooth;
    public float smoothFast;
    public float smoothSlow;
    
    private Texture2D texGrabbed;
    public float totalRedPixels;
    public ScriptItem currentScriptItem;
    private Color colorGripperUpper;
    private Color colorGripperLower;
    private ScriptItem lastScriptItem;

    private int fpsCount;
    
    public AudioSource audioSource;
    public AudioClip clipPressureOn;
    public AudioClip clipPressureOff;

    public RenderTexture renderTexture;
    private RenderTexture currentActiveRT;
    private Color[] pixels;
    private int maxRedX;
    private int maxRedY;
    private float maxRed;
    public bool ynBorderRed;
    float redThreshold;
    private int texGrabbedWidth;
    private int texGrabbedHeight;
    public int totalRedPixelsTarget;
    public float scaleXY;
    public float scaleZ;
    private float distdxdydz;
    private float minRobotY;
    private bool ynDistDxDyDz;    
    bool ynMinRobotY;
    private bool ynError;
    private string txtError;
    private float timeLastFindPackage;
    public bool ynManual;
    public bool ynDemo;
    private float angDemo;
    private List<ScriptItem> scriptList = new();
    private int nCurrentScriptList;
    private float tolerance;
    private float timeFindPackage;
    private float durationFindPackage;
    private int countFindPackage;
    
    void Start()
    {
        targetPrev = new GameObject("targetPrev");
        ynManual = false;
        ynDemo = false;
        redThreshold = .95f;
        smoothFast = .125f;
        smoothSlow = .025f;
        smooth = 0;
        lengthArms = .75f;
        
        tolerance = .01f;
        durationFindPackage = 4;
        
        minRobotY = .1f;

        totalRedPixelsTarget = 96; // 16; //30; 
        
        scaleXY = .0005f;
        scaleZ = .0016f;

        texGrabbedWidth = 128;
        texGrabbedHeight = 128;
        
        renderTexture.width = texGrabbedWidth;
        renderTexture.height = texGrabbedHeight;

        colorGripperUpper = GetColor(gripperUpper);
        colorGripperLower = GetColor(gripperLower);

        RunScript();
        
        InvokeRepeating(nameof(Fps), 1, 1);
    }

    void Update()
    {
        GrabImage();
        FindHandle();
        CheckError();

        if (currentScriptItem == ScriptItem.FindPackage)
        {
            AdjustTargetXY();
            AdjustTargetZ();
        }        

        if (!ynError || currentScriptItem != ScriptItem.FindPackage)
        {
            RotateTorso();
            SetAngleArms();
            Smooth();
            PositionActuators();
        }
        if (ynDemo) Demo();
        
        CheckGripperOpenClose();
        CheckGoto0();
        CheckGoto1();
        CheckGotoUpperLeft();
        CheckGotoUpperRight();
        CheckFindPackage();
        
        lastScriptItem = currentScriptItem;
        fpsCount++;
    }

    void PositionActuators()
    {
        torso.transform.localEulerAngles = new (0, angleTorso, 0);        
        armUpper.transform.localEulerAngles = new(angleArmUpper, 0, 0);        
        armLower.transform.localEulerAngles = new(angleArmLower, 0, 0);        
        LevelGrippers();
        gripperUpper.transform.localEulerAngles = new(angleGripperUpper, 0, 0);
        gripperLower.transform.localEulerAngles = new(angleGripperLower, 0, 0);
    }
    
    void Demo()
    {
        float x = 1.25f * Mathf.Cos(angDemo * Mathf.Deg2Rad);
        float y = 1.25f + .5f * Mathf.Sin(angDemo * Mathf.Deg2Rad);
        float z = 1.475f;
        package.transform.position = new(x, y, z);
        package.transform.eulerAngles = Vector3.zero;
        angDemo += .5f;
    }

    void RunScript()
    {
        scriptList.Clear();
        
        scriptList.Add(ScriptItem.GripperOpen);
        
        scriptList.Add(ScriptItem.Goto0);
        scriptList.Add(ScriptItem.FindPackage);
        //if (ynManual) return;
        scriptList.Add(ScriptItem.GripperClose);
        
        scriptList.Add(ScriptItem.GotoUpperLeft);
        scriptList.Add(ScriptItem.GotoUpperRight);

        scriptList.Add(ScriptItem.Goto1);

        scriptList.Add(ScriptItem.GripperOpen);
        scriptList.Add(ScriptItem.FindPackage);
        scriptList.Add(ScriptItem.GripperClose);
        
        scriptList.Add(ScriptItem.GotoUpperRight);
        
        scriptList.Add(ScriptItem.Goto0);

        nCurrentScriptList = scriptList.Count;
        CallNextScriptItem();
    }

    void CallNextScriptItem()
    {
        nCurrentScriptList++;
        if (nCurrentScriptList >= scriptList.Count)
        {
            nCurrentScriptList = 0;
        }
        currentScriptItem = scriptList[nCurrentScriptList];
        string txt = currentScriptItem.ToString();
        SendMessage(txt);
        if (currentScriptItem == ScriptItem.FindPackage)
        {
            countFindPackage++;
            Debug.Log("countFindPackage:" + countFindPackage + "\n");
        }

    }
    
    void GrabImage()
    {
        currentActiveRT = RenderTexture.active;
        RenderTexture.active = renderTexture;
        texGrabbed = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
        texGrabbed.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        pixels = texGrabbed.GetPixels();
        RenderTexture.active = currentActiveRT;
        //DestroyImmediate(texGrabbed); // memory leak solution, red/black feedback goes away
    }
    
    void AdjustTargetXY()
    {
        if (ynError) return;
        float dx = scaleXY * (maxRedX - texGrabbedWidth / 2);
        float dy = scaleXY * (maxRedY - texGrabbedHeight / 2);
        targetPrev.transform.position = target.transform.position;
        targetPrev.transform.eulerAngles = target.transform.eulerAngles;
        target.transform.Translate(dx, dy, 0);
        if (!IsInRange(target.transform.position))
        {
            target.transform.position = targetPrev.transform.position;
            target.transform.eulerAngles = targetPrev.transform.eulerAngles;
        }
//        Debug.Log("maxRed:" + maxRedX + "," + maxRedY + " scale:" + scaleXY + " dx,dy:" + dx + "," + dy + "\n");
    }


    void AdjustTargetZ()
    {
        if (ynError) return;
        float sideTarget = Mathf.Sqrt(totalRedPixelsTarget);
        float side = Mathf.Sqrt(totalRedPixels);
        float delta = sideTarget - side;
        float dz = delta * scaleZ;
        targetPrev.transform.position = target.transform.position;
        targetPrev.transform.eulerAngles = target.transform.eulerAngles;
        target.transform.Translate(0, 0, dz);
        if (!IsInRange(target.transform.position))
        {
            target.transform.position = targetPrev.transform.position;
            target.transform.eulerAngles = targetPrev.transform.eulerAngles;
        }
    }

    void CheckError()
    {
        ynError = false;
        Color color = Color.green;
        bool ynTotalRedPixelsZero = totalRedPixels == 0;
        txtError = "";
        if (ynDistDxDyDz) txtError += ",DistDxDyDz";
        if (ynTotalRedPixelsZero || ynBorderRed || ynMinRobotY)
        {
            if (ynTotalRedPixelsZero) txtError += ",TotalRedPixelsZero";
            if (ynBorderRed) txtError += ",BorderRed";
            if (ynMinRobotY) txtError += ",MinRobotY";
            ynError = true;
            color = Color.red;
            if (currentScriptItem == ScriptItem.FindPackage) audioSource.PlayOneShot(clipPressureOff);
        }
        displayBorderRenderer.material.color = color;
        if (txtError.Length > 0) txtError = txtError.Substring(1);
//        Debug.Log(txtError + "|" + ynError + "|" + totalRedPixels + " ------------------\n");
    }

    void FindHandle() 
    {
        totalRedPixels = 0;
        maxRed = 0;
        maxRedX = 0;
        maxRedY = 0;
        ynBorderRed = false;

        for (int y = 0; y < texGrabbedHeight; y++)
        {
            for (int x = 0; x < texGrabbedWidth; x++)
            {
                Color color = pixels[y * texGrabbedWidth + x];
                if (color.r > redThreshold && color.r > color.g && color.r > color.b && color.g < .5f && color.b < .5f)
                {
                    if (color.r > maxRed)
                    {
                        maxRed = color.r;
                        maxRedX = x;
                        maxRedY = y;
                    }
                    if (x == 0 || x == texGrabbedWidth - 1 || y == 0 || y == texGrabbedHeight - 1)
                    {
                        ynBorderRed = true;
                    }
                    pixels[y * texGrabbedWidth + x] = Color.red;
                    totalRedPixels++;
                }
                else
                {
                    pixels[y * texGrabbedWidth + x] = Color.black;
                }
            }
        }
        texGrabbed.SetPixels(pixels);
        texGrabbed.Apply();
        display2.GetComponent<Renderer>().material.mainTexture = texGrabbed;
    }
    
    void SetCenterOfMass(bool yn)
    {
        return;
        Rigidbody rb = package.GetComponent<Rigidbody>(); 
        if (yn)
        {
            rb.useGravity = false;
            rb.angularVelocity = Vector3.zero;
            rb.linearVelocity = Vector3.zero;
            rb.centerOfMass = new Vector3(0, 0, .25f);
        }
        else
        {
            rb.useGravity = true;
            rb.centerOfMass = new Vector3(0, -.25f, 0);
        }
    }

    public void PressureOn(UpperLower upperLower)
    {
        switch (upperLower)
        {
            case UpperLower.lower:
                angleGripperLowerNew = angleGripperLower;
                SetColor(gripperLower, Color.black);
                SetCenterOfMass(true);
                break;
            case UpperLower.upper:
                angleGripperUpperNew = angleGripperUpper;
                SetColor(gripperUpper, Color.black);
                SetCenterOfMass(true);
                break;
        }
    }

    public void PressureOff(UpperLower upperLower)
    {
        switch (upperLower)
        {
            case UpperLower.upper:
                SetColor(gripperUpper, colorGripperUpper);
                SetCenterOfMass(false);
                break;
            case UpperLower.lower:
                SetColor(gripperLower, colorGripperLower);
                SetCenterOfMass(false);
                break;
        }
    }
    
    void Fps()
    {
//        Debug.Log("Fps: " +  fpsCount + "\n");
        fpsCount = 0;
    }

    void SetColor(GameObject go, Color color)
    {
        go.GetComponentInChildren<Renderer>().material.color = color;
    }

    Color GetColor(GameObject go)
    {   
        return go.GetComponentInChildren<Renderer>().material.color;
    }
    
    void Smooth()
    {
        angleTorso = (1 - smooth) * angleTorso + smooth * angleTorsoNew;
        angleArmUpper = (1 - smooth) * angleArmUpper + smooth * angleArmUpperNew;
        angleArmLower = (1 - smooth) * angleArmLower + smooth * angleArmLowerNew;
        angleGripperUpper = (1 - smooth) * angleGripperUpper + smooth * angleGripperUpperNew;
        angleGripperLower = (1 - smooth) * angleGripperLower + smooth * angleGripperLowerNew;
    }

    void SetSmooth()
    {
        smooth = smoothSlow;
        if (currentScriptItem == ScriptItem.FindPackage) smooth = smoothFast;
        if (currentScriptItem == ScriptItem.GripperOpen) smooth = smoothFast;
        if (currentScriptItem == ScriptItem.GripperClose) smooth = smoothFast;
    }

    void LevelGrippers()
    {
        Vector3 eul = grippers.transform.eulerAngles;
        grippers.transform.eulerAngles = new (0, eul.y, 0);
    }

    // call next item when done (not with invoke timer)
    void GripperClose()
    {
        currentScriptItem = ScriptItem.GripperClose;
        SetSmooth();
        angleGripperUpperNew = 0;
        angleGripperLowerNew = 0;
        package.transform.SetParent(grippers.transform);
    }

    void GripperOpen()
    {
        currentScriptItem = ScriptItem.GripperOpen;
        SetSmooth();
        angleGripperUpperNew = -45;
        angleGripperLowerNew = 45;
        package.transform.SetParent(null);
    }
    
    void CheckGripperOpenClose()
    {
        if (currentScriptItem != ScriptItem.GripperClose && currentScriptItem != ScriptItem.GripperOpen) return;
        float deltaUpper = Mathf.Abs(angleGripperUpper - angleGripperUpperNew);
        float deltaLower = Mathf.Abs(angleGripperLower - angleGripperLowerNew);
        if (deltaUpper <= tolerance && deltaLower <= tolerance)
        {
            CallNextScriptItem();
        }
    }

    void FindPackage()
    {
        currentScriptItem = ScriptItem.FindPackage;
        SetSmooth();
        timeFindPackage = Time.realtimeSinceStartup;
    }

    void CheckFindPackage()
    {
        if (currentScriptItem != ScriptItem.FindPackage) return;
        float delta = Time.realtimeSinceStartup - timeFindPackage;
        if (delta >= durationFindPackage)
        {
            CallNextScriptItem();
        }
    }
    
    void Goto0()
    {
        currentScriptItem = ScriptItem.Goto0;
        SetSmooth();
        SetTarget(target0);
    }

    void CheckGoto0()
    {
        if (currentScriptItem != ScriptItem.Goto0) return;
        float delta = Vector3.Distance(grippers.transform.position, target0.transform.position);
        if (delta <= tolerance)
        {
            CallNextScriptItem();            
        }
    }

    void Goto1()
    {
        currentScriptItem = ScriptItem.Goto1;
        SetSmooth();
        SetTarget(target1);
    }

    void CheckGoto1()
    {
        if (currentScriptItem != ScriptItem.Goto1) return;
        float delta = Vector3.Distance(grippers.transform.position, target1.transform.position);
        if (delta <= tolerance)
        {
            CallNextScriptItem();            
        }
    }

    void GotoUpperLeft()
    {
        currentScriptItem = ScriptItem.GotoUpperLeft;
        SetSmooth();
        SetTarget(targetUpperLeft);
    }

    void CheckGotoUpperLeft()
    {
        if (currentScriptItem != ScriptItem.GotoUpperLeft) return;
        float delta = Vector3.Distance(grippers.transform.position, targetUpperLeft.transform.position);
        if (delta <= tolerance)
        {
            CallNextScriptItem();            
        }
    }
    
    void GotoUpperRight()
    {
        currentScriptItem = ScriptItem.GotoUpperRight;
        SetSmooth();
        SetTarget(targetUpperRight);
    }

    void CheckGotoUpperRight()
    {
        if (currentScriptItem != ScriptItem.GotoUpperRight) return;
        float delta = Vector3.Distance(grippers.transform.position, targetUpperRight.transform.position);
        if (delta <= tolerance)
        {
            CallNextScriptItem();            
        }
    }
    
    void SetTarget(GameObject targetNew)
    {
        target.transform.position = targetNew.transform.position;
    }

    void RotateTorso()
    {
        float dx = target.transform.position.x - torso.transform.position.x;
        float dz = target.transform.position.z - torso.transform.position.z;
        if (angleTorso < -60) angleTorso = -60;
        if (angleTorso > 60) angleTorso = 60;
        angleTorsoNew = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
    }

    bool IsInRange(Vector3 pos)
    {
        float dy = pos.y - armUpper.transform.position.y;
        float dx = pos.x - armUpper.transform.position.x;
        float dz = pos.z - armUpper.transform.position.z;
        
        distdxdydz = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        ynDistDxDyDz = distdxdydz >= 2 * lengthArms;

        ynMinRobotY = pos.y <= minRobotY;
        if (ynDistDxDyDz || ynMinRobotY)
        {
            return false;
        } else 
        { 
            return true;
        }
    }
    
    void SetAngleArms()
    {
        float dy = target.transform.position.y - armUpper.transform.position.y;
        float dx = target.transform.position.x - armUpper.transform.position.x;
        float dz = target.transform.position.z - armUpper.transform.position.z;
        
        distdxdydz = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        ynDistDxDyDz = distdxdydz >= 2 * lengthArms;
        if (ynDistDxDyDz)
        {
            return;
        }
//        distdxdydz -= .06f; // forgot 

        float distdxdz = Mathf.Sqrt(dx * dx + dz * dz);
        float angTarget = -Mathf.Atan2(dy, distdxdz) * Mathf.Rad2Deg; 

        float dist2 = distdxdydz / 2;
        
        float h = Mathf.Sqrt(lengthArms * lengthArms - dist2 * dist2);
        
        float angleArmUpperToTarget = Mathf.Atan2(h, dist2) * Mathf.Rad2Deg;

        angleArmUpperNew = angTarget + angleArmUpperToTarget;

        float angleArmLowerRaw = 2 * Mathf.Atan2(dist2, h) * Mathf.Rad2Deg;
        angleArmLowerNew = -180 + angleArmLowerRaw;
    }
    
    public enum ScriptItem
    {
        FindPackage,
        GripperOpen,
        GripperClose,
        Goto0,
        Goto1,
        GotoUpperLeft,
        GotoUpperRight
    }
}
