using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]

public class PlatformSlide : UdonSharpBehaviour
{
    [SerializeField, Tooltip("Slide damping"), Range(0.1f,1f)]
    private float smoothRate = 0.5f;
    [SerializeField, Range(0.25f, 1f), Tooltip("Rabbit speed")]
    private float rabbitRate = 0.5f;
    [SerializeField]
    private Toggle baseToggle;
    [SerializeField]
    private Toggle screenToggle;
    [SerializeField,UdonSynced,FieldChangeCallback(nameof(BaseToggleState))]
    private bool baseToggleState = false;
    [SerializeField]
    Transform TargetTransForm;
    [SerializeField] Vector3[] startPositions;

    [SerializeField]
    Transform portalTransform;
    [SerializeField]
    Vector3[] portalPositions;
    [SerializeField, FieldChangeCallback(nameof(ScaleIndex))]
    int scaleIndex  =1;

    private bool iamOwner = false;
    private VRCPlayerApi player;

    /* 
    * Udon Sync Stuff
    */
    private void ReviewOwnerShip()
    {
        iamOwner = Networking.IsOwner(this.gameObject);
    }
    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        ReviewOwnerShip();
    }

    public int ScaleIndex
    {
        get => scaleIndex; 
        set
        {
            if (scaleIndex != value)
            {
                scaleIndex = value;
                ReviewPlatformSituation();
                SetPortalStops();
            }
        }
    }

    public bool BaseToggleState
    {
        get => baseToggleState;
        set
        {
            bool oldVal = baseToggleState;
            baseToggleState = value;
            if (oldVal != value) 
                ReviewPlatformSituation();
            RequestSerialization();
        }
    }


    [Header("For testing")]
    [SerializeField] private Vector3 basePostion = Vector3.zero;
    [SerializeField] private Vector3 targetPosition = Vector3.right;
    [SerializeField] private Vector3 rabbitPosition = Vector3.zero;

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
                ReviewPlatformSituation();
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
                ReviewPlatformSituation();
            }
        }
    }

    public void onBaseToggle()
    {
        bool togVal = (baseToggle != null) ? baseToggle.isOn : true;
        if (!iamOwner)
           Networking.SetOwner(player, gameObject);
        Debug.Log($"Click Base {togVal}");
        BaseToggleState = togVal;
    }

    public void onScreenToggle()
    {
        bool togVal = (screenToggle != null) ? screenToggle.isOn : true;
        if (!iamOwner)
            Networking.SetOwner(player, gameObject);
        Debug.Log($"ScreenToggle {togVal}");
        if (!baseToggleState && togVal)
            ReviewPlatformSituation();
    }
    private void SetPortalStops()
    {
        if (portalTransform == null || portalPositions == null || portalPositions.Length <= ScaleIndex)
            return;
        portalWas = portalStop;
        portalStop = portalPositions[ScaleIndex];
    }

    // Sets stop locations according to scale
    private void ReviewPlatformSituation()
    {
        if (!hasTarget)
        {
            Debug.LogWarning($"{gameObject.name}: No target transform set for platform slide, using start position as target");
            return;
        }
        basePostion = startPositions[ScaleIndex];
        targetPosition = baseToggleState ? basePostion : TargetTransForm.position;
    }

    public void updatePortal(float shift)
    {
        if (portalTransform == null)
            return;
        portalTransform.position = Vector3.Lerp(portalWas, portalStop, shift);
    }

    private bool isInitialized = false;
    private Vector3 currentVelocity = Vector3.zero;
    [SerializeField]
    private Vector3 currentPosition = Vector3.zero;

    private void Update()
    {
        if (!isInitialized)
        {
            SetPortalStops();
            updatePortal(1);
            isInitialized = true;
            targetPosition = transform.position;
            rabbitPosition = transform.position;
            return;
        }
        if (!scaleIsChanging)
        {
            if (rabbitPosition != targetPosition)
            {
                float step = rabbitRate * Time.deltaTime;
                if (Vector3.Dot(rabbitPosition - targetPosition, rabbitPosition - targetPosition) > 0.00003f)
                    rabbitPosition = Vector3.MoveTowards(rabbitPosition, targetPosition, step);
                else
                    rabbitPosition = targetPosition;
            }
            currentPosition = transform.position;

            if (currentPosition == rabbitPosition)
                return;
            Vector3 diff = currentPosition - rabbitPosition;
            float diffSq = Vector3.Dot(diff, diff);
            if (diffSq > 0.00003f)
                transform.position = Vector3.SmoothDamp(currentPosition, rabbitPosition, ref currentVelocity, smoothRate);
            else
            {
                Debug.Log($"Arrived at rabbit {rabbitPosition}");
                transform.position = rabbitPosition;
            }
        }
    }

    void Start()
    {
        player = Networking.LocalPlayer;
        if (baseToggle != null)
            baseToggleState = baseToggle.isOn;

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
        ReviewPlatformSituation();
        isInitialized = false;
    }
}
