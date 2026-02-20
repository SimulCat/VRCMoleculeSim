using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]

public class PlatformSlide : UdonSharpBehaviour
{
    [SerializeField]
    Transform TargetTransForm;
    [SerializeField]
    Vector3[] startPositions;
    [SerializeField] 
    private Vector3 targetOffset = new Vector3(-0.65f,0,0);

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
    [SerializeField] private bool hasTarget = false;
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
        if (portalTransform == null || portalPositions == null || portalPositions.Length <= ScaleIndex)
            return;
        portalWas = portalStop;
        portalStop = portalPositions[ScaleIndex];
    }

    // Sets stop locations according to scale
    private void SetPlatformStop()
    {
        if (hasTarget) 
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
        if (!hasTarget)
            return;
        targetStop = gratingStop;
        targetStop = TargetTransForm.position + targetOffset;

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
        if (!hasTarget) 
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
        hasTarget = TargetTransForm != null;
        if ((startPositions == null) || (startPositions.Length < 3))
        {
            startPositions = new Vector3[3];
            for (int i = 0; i< startPositions.Length; i++)
                startPositions[i] = transform.position;
        }
        if ((portalStop != null) && ((portalPositions == null) || (portalPositions.Length < 3)))
        {
            portalStop = portalTransform.position;
            portalWas = portalStop;
            portalPositions = new Vector3[3];
            Vector3 defaultPos = portalTransform != null ? portalTransform.position : Vector3.zero;
            for (int i = 0;i< portalPositions.Length;i++)
                portalPositions[i] = defaultPos;
        }
        SetPlatformStop();
        if (hasTarget)
        {
            previousTargetX = TargetTransForm.position.x;
        }
        isInitialized = false;
    }
}
