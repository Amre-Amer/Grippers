using System;
using UnityEngine;
using Random = UnityEngine.Random;

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
    public GameObject targetMid;
    public GameObject targetLow;
    public GameObject targetUpperLeft;
    public GameObject targetUpperRight;
    public Camera cam;
    public GameObject display;
    public GameObject displayBorder;
    
    public GameObject target;

    private float angleTorsoNew = 0;
    private float angleArmUpperNew = 0;
    private float angleArmLowerNew = 0;
    private float angleGripperUpperNew = 0;
    private float angleGripperLowerNew = 0;
    private Vector3 posHandle;
    private Vector3 eulHandle;

    public float lengthArms;
    public float smooth;
    public float speed;
    
    private Texture2D texGrabbed;
    private float totalRedPixels;
    public ScriptItem currentScriptItem;
    private Color colorGripperUpper;
    private Color colorGripperLower;
    private ScriptItem lastScriptItem;

    private int fpsCount;
    
    public AudioSource audioSource;
    public AudioClip clipPressureOn;
    public AudioClip clipPressureOff;

    public RenderTexture renderTexture;
    private int maxRedX;
    private int maxRedY;
    private float maxRed;
    private bool ynBorderRed;
    float redThreshold;
    private Renderer displayBorderRenderer;
    private bool ynError;
    private float timeLastFindPackage;
    
    void Start()
    {
        Rigidbody rb = package.GetComponent<Rigidbody>(); 
        Destroy(rb);    
        displayBorderRenderer = displayBorder.GetComponent<Renderer>();
        redThreshold = .95f;
        smooth = .025f; //.05f;
        lengthArms = .75f;
        speed = 1.5f;

        colorGripperUpper = GetColor(gripperUpper);
        colorGripperLower = GetColor(gripperLower);
        currentScriptItem = ScriptItem.None;

        Script();
        
        InvokeRepeating(nameof(Fps), 1, 1);
    }

    void Update()
    {
        //if (ynError) return;
        RotateTorso();
        SetAngleArms();
        Smooth();
        torso.transform.localEulerAngles = new (0, angleTorso, 0);        
        armUpper.transform.localEulerAngles = new(angleArmUpper, 0, 0);        
        armLower.transform.localEulerAngles = new(angleArmLower, 0, 0);        
        LevelGrippers();
        gripperUpper.transform.localEulerAngles = new(angleGripperUpper, 0, 0);
        gripperLower.transform.localEulerAngles = new(angleGripperLower, 0, 0);
        if (currentScriptItem == ScriptItem.FindPackage)
        {
//            if (Time.realtimeSinceStartup - timeLastFindPackage > 2)
//            {
                FindPackage();
                CheckError();
//                timeLastFindPackage = Time.realtimeSinceStartup;
//            }
        }

        if (lastScriptItem != currentScriptItem)
        {
            if (currentScriptItem == ScriptItem.FindPackage)
            {
                display.SetActive(true);
            }
            else
            {
                display.SetActive(false);
            }
        }
        lastScriptItem = currentScriptItem;
        fpsCount++;
    }

    void Script()
    {
        CallScriptItem(nameof(GotoLow), 0);
        CallScriptItem(nameof(GripOpen), .5f);
        CallScriptItem(nameof(Goto0), 1);
        CallScriptItem(nameof(FindPackage), 2.5f);
//        CallScriptItem(nameof(AdjustTargetXY), 3);
//        CallScriptItem(nameof(AdjustTargetZ), 4);
        return;
        CallScriptItem(nameof(GripClose), 4.5f);
        CallScriptItem(nameof(GrabPackage), 5);
        
        CallScriptItem(nameof(GotoUpperLeft), 5.5f);
        CallScriptItem(nameof(GotoMid), 6);
        CallScriptItem(nameof(GotoUpperRight), 8.5f);
        CallScriptItem(nameof(Goto1), 9);
        CallScriptItem(nameof(ReleasePackage), 9.5f);
        CallScriptItem(nameof(GripOpen), 10);
        
        CallScriptItem(nameof(GotoLow), 10.5f);
        CallScriptItem(nameof(GripClose), 11);
        CallScriptItem(nameof(GripOpen), 12.5f);

        CallScriptItem(nameof(Goto1), 13);
        CallScriptItem(nameof(FindPackage), 13.5f);
        CallScriptItem(nameof(GripClose), 15.5f);
        CallScriptItem(nameof(GrabPackage), 16);
        CallScriptItem(nameof(GotoMid), 16.5f);
        CallScriptItem(nameof(Goto0), 18);
        CallScriptItem(nameof(ReleasePackage), 18.5f);
        CallScriptItem(nameof(GripOpen), 19);

        CallScriptItem(nameof(GotoLow), 19.5f);
        CallScriptItem(nameof(GripClose), 21f);
        CallScriptItem(nameof(GripOpen), 21.5f);

        CallScriptItem(nameof(Script), 20);
    }

    void CallScriptItem(string txtCall, float t)
    {
        Invoke(txtCall, t * speed);
    }
    
    void FindPackage()
    {
        currentScriptItem = ScriptItem.FindPackage;
        GrabImage();
        FindHandle();
        AdjustTargetXY();
        AdjustTargetZ();
    }
    
    void GrabImage()
    {
        RenderTexture rTex = renderTexture;
        RenderTexture currentActiveRT = RenderTexture.active;
        RenderTexture.active = rTex;
        texGrabbed = new Texture2D(rTex.width, rTex.height, TextureFormat.RGBA32, false);
        texGrabbed.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
        texGrabbed.Apply();
        RenderTexture.active = currentActiveRT;
    }
    
    void AdjustTargetXY()
    {
        //currentScriptItem = ScriptItem.AdjustTargetXY;
        if (ynError) return;
        //        return;
        float scaleX = .00025f;
        float scaleY = scaleX; 
        float dx = scaleX * (maxRedX - texGrabbed.width / 2);
        float dy = scaleY * (maxRedY - texGrabbed.height / 2);
        float s = .5f;
        if (Mathf.Abs(dx) <= scaleX * s && Mathf.Abs(dy) <= scaleY * s)
        {
            Debug.Log("------------------\n");
            return;
        }
        target.transform.eulerAngles = cam.transform.eulerAngles;
        target.transform.Translate(dx, dy, 0);
        Debug.Log("dx,dy:" + dx + "," + dy + "\n");
    }


    void AdjustTargetZ()
    {
        //currentScriptItem = ScriptItem.AdjustTargetZ;
        //return;
        if (ynError) return;
//        Debug.Log(totalRedPixels + ":" + maxRed + "|" + maxRedX + "," + maxRedY + "\n");
        int totalRedPixelsTarget = 30; 
        float scaleZ = .0035f;
        float sideTarget = Mathf.Sqrt(totalRedPixelsTarget);
        float side = Mathf.Sqrt(totalRedPixels);
        float delta = sideTarget - side;
        float dz = delta * scaleZ;
        target.transform.Translate(0, 0, dz);
    }

    void CheckError()
    {
        ynError = false;
        Color color = Color.green;
        if (totalRedPixels == 0 || ynBorderRed)
        {
            ynError = true;
            color = Color.red;
            audioSource.PlayOneShot(clipPressureOff);
        }
        displayBorderRenderer.material.color = color;
    }

    void FindHandle() 
    {
        Color[] pixels = texGrabbed.GetPixels();
        totalRedPixels = 0;
        maxRed = 0;
        maxRedX = 0;
        maxRedY = 0;
        ynBorderRed = false;

        for (int y = 0; y < texGrabbed.height; y++)
        {
            for (int x = 0; x < texGrabbed.width; x++)
            {
                Color color = pixels[y * texGrabbed.width + x];
                if (color.r > redThreshold && color.r > color.g && color.r > color.b)
                {
                    totalRedPixels++;
                }

                if (color.r > maxRed)
                {
                    maxRed = color.r;
                    maxRedX = x;
                    maxRedY = y;
                }
                if (x == 0 || x == texGrabbed.width - 1 || y == 0 || y == texGrabbed.height - 1)
                {
                    if (color.r >= redThreshold)
                    {
                        ynBorderRed = true;
                    }
                }
            }
        }
    }
    
    void SetCenterOfMass(bool yn)
    {
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
                SetColor(gripperLower, Color.red);
                //SetCenterOfMass(true);
                break;
            case UpperLower.upper:
                angleGripperUpperNew = angleGripperUpper;
                SetColor(gripperUpper, Color.red);
                //SetCenterOfMass(true);
                break;
        }
    }

    public void PressureOff(UpperLower upperLower)
    {
        switch (upperLower)
        {
            case UpperLower.upper:
                SetColor(gripperUpper, colorGripperUpper);
                //SetCenterOfMass(false);
                break;
            case UpperLower.lower:
                SetColor(gripperLower, colorGripperLower);
                //SetCenterOfMass(false);
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
        angleTorso = (1 - smooth) * angleTorso + (smooth) * angleTorsoNew;
        angleArmUpper = (1 - smooth) * angleArmUpper + (smooth) * angleArmUpperNew;
        angleArmLower = (1 - smooth) * angleArmLower + (smooth) * angleArmLowerNew;
        angleGripperUpper = (1 - smooth) * angleGripperUpper + (smooth) * angleGripperUpperNew;
        angleGripperLower = (1 - smooth) * angleGripperLower + (smooth) * angleGripperLowerNew;
    }

    void LevelGrippers()
    {
        Vector3 eul = grippers.transform.eulerAngles;
        grippers.transform.eulerAngles = new (0, eul.y, 0);
    }

    void GripClose()
    {
        currentScriptItem = ScriptItem.GripClose;
        angleGripperUpperNew = 0;
        angleGripperLowerNew = 0;
    }

    void GripOpen()
    {
        currentScriptItem = ScriptItem.GripOpen;
        angleGripperUpperNew = -45;
        angleGripperLowerNew = 45;
    }

    public enum ScriptItem
    {
        None,
        GotoLow,
        GripOpen,
        Goto0,
        FindPackage,
        GripClose,
        GrabPackage,
        AdjustTargetXY,
        AdjustTargetZ,
        GotoUpperLeft,
        GotoUpperRight,
        GotoMid,
        Goto1,
        ReleasePackage
    }
    
    void GrabPackage()
    {
        currentScriptItem = ScriptItem.GrabPackage;
        package.transform.SetParent(grippers.transform);
    }

    void ReleasePackage()
    {
        currentScriptItem = ScriptItem.ReleasePackage;
        package.transform.SetParent(null);
    }

    void Goto0()
    {
        currentScriptItem = ScriptItem.Goto0;
        SetTarget(target0);
    }

    void Goto1()
    {
        currentScriptItem = ScriptItem.Goto1;
        SetTarget(target1);
    }

    void GotoMid()
    {
        currentScriptItem = ScriptItem.GotoMid;
        SetTarget(targetMid);
    }

    void GotoLow()
    {
        currentScriptItem = ScriptItem.GotoLow;
        SetTarget(targetLow);
    }

    void GotoUpperLeft()
    {
        currentScriptItem = ScriptItem.GotoUpperLeft;
        SetTarget(targetUpperLeft);
    }

    void GotoUpperRight()
    {
        currentScriptItem = ScriptItem.GotoUpperRight;
        SetTarget(targetUpperRight);
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
    
    void SetAngleArms()
    {
        float dy = target.transform.position.y - armUpper.transform.position.y;
        float dx = target.transform.position.x - armUpper.transform.position.x;
        float dz = target.transform.position.z - armUpper.transform.position.z;
        
        float distdxdydz = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        distdxdydz -= .06f;

        if (distdxdydz >= 2 * lengthArms)
        {
            return;
        }
        
        float distdxdz = Mathf.Sqrt(dx * dx + dz * dz);
        float angTarget = -Mathf.Atan2(dy, distdxdz) * Mathf.Rad2Deg; 

        float dist2 = distdxdydz / 2;
        
        float h = Mathf.Sqrt(lengthArms * lengthArms - dist2 * dist2);
        
        float angleArmUpperToTarget = Mathf.Atan2(h, dist2) * Mathf.Rad2Deg;

        angleArmUpperNew = angTarget + angleArmUpperToTarget;

        float angleArmLower0 = 2 * Mathf.Atan2(dist2, h) * Mathf.Rad2Deg;
        angleArmLowerNew = -180 + angleArmLower0;
    }
}
