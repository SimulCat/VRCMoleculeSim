
using System.Runtime.Remoting.Messaging;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]

public class PlatformSlide : UdonSharpBehaviour
{
    [SerializeField]
    Transform GratingTransForm;
    [SerializeField]
    Transform TargetTransForm;
    [SerializeField]
    Transform LookAtTransform;
    [SerializeField]
    Vector3[] startPositions;
    [SerializeField] 
    private float targetOffset = -0.65f;

    [SerializeField]
    Transform portalTransform;
    [SerializeField]
    Vector3[] portalPositions;
    [SerializeField, FieldChangeCallback(nameof(ScaleIndex))]
    int scaleIndex  =1;
    public int ScaleIndex
    {
        get => scaleIndex; 
        set
        {
            if (scaleIndex != value)
            {
                scaleIndex = value;
                SetPlatformStop();
                SetPortalStops();
            }
        }
    }

    [SerializeField,UdonSynced,Range(0,1),FieldChangeCallback(nameof(LocationTween))]
    float locationTween = 0;
    [Header("For testing")]
    [SerializeField] private Vector3 gratingStop = Vector3.zero;
    [SerializeField] private Vector3 targetStop = Vector3.right;
    [SerializeField] private bool hasTransforms = false;
    [SerializeField] private Vector3 portalStop = Vector3.zero;
    [SerializeField] private Vector3 portalWas = Vector3.zero;

    [SerializeField,FieldChangeCallback(nameof(ScaleIsChanging))] private bool scaleIsChanging = false;
    public bool ScaleIsChanging
    {
        get => scaleIsChanging;
        set
        {
            scaleIsChanging = value;
            if (!scaleIsChanging)
            {
                SetPlatformStop();
                updatePortal(1f);
            }
        }
    }

    [Tooltip("Spatial Scaling"), FieldChangeCallback(nameof(ExperimentScale))]
    public float experimentScale = 10f;
    public float ExperimentScale
    {
        get => experimentScale;
        set
        {
            if (experimentScale != value)
            {
                experimentScale = value;
                SetPlatformStop();
            }
        }
    }

    private void SetPortalStops()
    {
        portalWas = portalStop;
        portalStop = portalPositions[ScaleIndex];
    }

    // Sets stop locations according to scale
    private void SetPlatformStop()
    {
        if (hasTransforms) 
            gratingStop = startPositions[ScaleIndex];
    }

    public void updatePortal(float shift)
    {
        if (portalTransform == null)
            return;
        portalTransform.position = Vector3.Lerp(portalWas, portalStop, shift);
    }

    private void UpdateLocation(float shift)
    {
        if (!hasTransforms)
            return;
        targetStop = gratingStop;
        targetStop.x = TargetTransForm.position.x + targetOffset;

        transform.position = Vector3.Lerp(gratingStop, targetStop, shift);
    }

    public float LocationTween 
    {
        get => locationTween;
        set 
        {
            if (locationTween != value)
            {
                locationTween = value;
                UpdateLocation(value);
            }
        } 
    }

    private float previousTargetX;
    private bool isInitialized = false;
    private void Update()
    {
        if (!hasTransforms) 
            return;
        float tX = TargetTransForm.position.x;
        if (tX != previousTargetX)
        {
            previousTargetX = tX;
            UpdateLocation(locationTween);
        }
        if (!isInitialized)
        {
            SetPortalStops();
            updatePortal(1);
            isInitialized = true;
        }
    }

    void Start()
    {
        hasTransforms = GratingTransForm != null && TargetTransForm != null;
        if (LookAtTransform == null)
            LookAtTransform = transform;
        if ((startPositions == null) || (startPositions.Length < 3))
        {
            startPositions = new Vector3[3];
            for (int i = 0; i< startPositions.Length; i++)
                startPositions[i] = transform.position;
        }
        if ((portalPositions == null) || (portalPositions.Length < 3))
        {
            portalStop = portalTransform.position;
            portalWas = portalStop;
            portalPositions = new Vector3[3];
            Vector3 defaultPos = portalTransform != null ? portalTransform.position : Vector3.zero;
            for (int i = 0;i< portalPositions.Length;i++)
                portalPositions[i] = defaultPos;
        }
        SetPlatformStop();
        if (hasTransforms)
        {
            previousTargetX = TargetTransForm.position.x;
        }
        isInitialized = false;
    }
}
