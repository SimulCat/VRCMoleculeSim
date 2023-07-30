
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class MoleculeExperiment : UdonSharpBehaviour
{
    [Tooltip("Particle speed at middle of range")]
    public float avgMoleculeSpeed=150;
    public float molecularWeight = 514.5389f;
    public string moleculeName = "Pthalocyanine";
    [Header("Constants")]
    [SerializeField,UdonSynced,FieldChangeCallback(nameof(UseQuantumScatter))] private bool useQuantumScatter;
    public float h = 6.62607015e-34f; // 
    public float AMU_ToKg = 1.66054e-27f;

    [Header("Gravity")]
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(HasGravity))]
    private bool hasGravity;

    private bool settingsChanged = false;
    private bool planckChanged = false;
    private bool gravityChanged = false;
    private bool trajectoryChanged = false;
    public bool HasGravity
    {
        get => hasGravity;
        set
        {
            if (hasGravity != value)
            {
                hasGravity = value;
                gravityChanged = true;
            }
            if ((togGravity != null) && (togGravity.isOn != value))
                togGravity.isOn = value;
            RequestSerialization();
        }
    }
    [SerializeField]
    private float gravityAcceleration; // Required because forceoverlifetime is hidden and must be copied here    ParticleSystem.MainModule mainModule;
    [SerializeField]
    private float gravitySim;

    [Header("Scaled Values")]
    [SerializeField]
    private float emitToGratingSim;
    [SerializeField]
    private float gratingToTargetSim;
    [SerializeField]
    private float minLifeTimeAfterGrating = 20f;


    [Header("Scaling")]

    private int gravityScale = 10;
    private int GravityScale
    {
        get => gravityScale;
        set
        {
            if (gravityScale != value)
            {
                gravityScale = value;
                gravityChanged = true;
            }
        }
    }

    private int planckScale = 10;
    private int PlanckScale
    {
        get => planckScale;
        set 
        {
            if (planckScale != value)
            {
                planckChanged = true;
                planckScale = value;
                gratingVersion = -1;
            }
        }
    }

    [Tooltip("Slow Motion"),SerializeField, Range(0.001f, 1f)]
    private float slowFactor = 0.03f;
    [SerializeField]
    private float slowScaled = 0.03f;
    [SerializeField,UdonSynced,FieldChangeCallback(nameof(ScaleIsChanging))] 
    private bool scaleIsChanging = true;
    private bool ScaleIsChanging
    {
        get => scaleIsChanging;
        set
        {
            scaleIsChanging = value;
            if (hasSource)
            {
                if (scaleIsChanging)
                {
                    Debug.Log("Scale Changing");

                    particleEmitter.Pause();
                    particleEmitter.Clear();
                    if (hasTargetDecorator)
                        targetDisplay.Clear();
                    if (hasFloor)
                        floorDisplay.Clear();
                    if (hasGratingDecorator)
                        gratingDecals.Clear();
                }
                else
                {
                    Debug.Log("Scale Stopped");
                    gratingVersion = -1;
                    settingsChanged = true;
                    gravityChanged = true;
                    planckChanged = true; 
                    particleEmitter.Play();
                }
            }
        }
    }
    [SerializeField]
    private float graphicsScale = 1f;
    [Tooltip("Scale of objects at design (10x)"),SerializeField,UdonSynced,FieldChangeCallback(nameof(NativeGraphicsRatio))]
    private int nativeGraphicsRatio = 10;
    public int NativeGraphicsRatio 
    { 
        get => nativeGraphicsRatio > 0 ? nativeGraphicsRatio : 1; 
        set
        {
            value = value > 0 ? value : 1;
            if (value != nativeGraphicsRatio)
            {
                nativeGraphicsRatio = value;
                settingsChanged = true;
            }
        }
    }

    [Tooltip("Spatial Scaling"), UdonSynced, FieldChangeCallback(nameof(ExperimentScale))]
    public float experimentScale = 10f; 
    public float ExperimentScale
    {
        get => experimentScale;
        set
        {
            if (experimentScale != value)
            {
                experimentScale = value;
                graphicsScale = experimentScale / NativeGraphicsRatio;
                slowScaled = slowFactor * graphicsScale;
                emitToGratingSim = -(L1mm * experimentScale) / 1000;
                gratingToTargetSim = (L2mm * experimentScale) / 1000;
                settingsChanged = true;
                gravityChanged = true;
                planckChanged = true;
                gratingVersion = 0;
            }
        }
    }
    [Header("Speed Calculations")]
    [SerializeField]
    private float avgSimulationSpeed = 5f;
    [SerializeField, Range(0f,0.7f),Tooltip("Fraction of avg velocity +- e.g. 0.5 = +-50% of average")]
    private float randomRange= 0.6f;
    [SerializeField]
    private float maxSimSpeed;
    [SerializeField]
    private float minSimSpeed;
    [SerializeField,Range(0,1)] float userSpeed;
    [SerializeField] bool randomizeSpeeds = true;
    public float RandomRange
    {
        get => randomRange;
        set
        {
            randomRange = Mathf.Clamp(value, 0,0.7f);        
        }
    }

    public bool UseQuantumScatter
    {
        get => useQuantumScatter;
        set
        {
            if (useQuantumScatter != value)
            {
                useQuantumScatter = value;
            }
            if ((togQuantum != null) && (togQuantum.isOn != value))
                togQuantum.isOn = value;
            RequestSerialization();
        }
    }


    [Header("System Components")]
    [SerializeField]
    Transform collimatorProp;
    [SerializeField]
    Transform targetProp;
    [SerializeField]
    ParticleSystem particleEmitter;
    [Tooltip("Default Particle Size"), SerializeField, UdonSynced, FieldChangeCallback(nameof(ParticleStartSize))]
    float particleStartSize = 0.001f;

    public float ParticleStartSize
    {
        get => particleStartSize;
        set
        {
            if (value != particleStartSize)
            {
                particleStartSize = value;
                RequestSerialization();
            }
        }
    }

    Transform sourceXfrm;
    [SerializeField]
    QuantumScatter horizontalScatter;
    [SerializeField]
    QuantumScatter verticalScatter;
    [SerializeField]
    GratingControl gratingControl;
    Transform gratingXfrm;
    [SerializeField]
    Vector3 gratingPosition = Vector3.zero;
    [SerializeField]
    Vector3 targetPosition = Vector3.zero;
    float gratingThickness = 0.001f;
    [SerializeField]
    TargetDisplay floorDisplay;
    [SerializeField]
    Transform floorTransform;
    [SerializeField]
    Transform targetTransform;
    [SerializeField]
    TargetDisplay targetDisplay;
    [SerializeField]
    TargetDisplay gratingDecals;
    [SerializeField]
    bool hasFloor;
    [SerializeField]
    bool hasTarget;
    bool hasTargetDecorator;
    bool hasGrating;
    bool hasGratingDecorator;
    bool hasSource;
    bool hasHorizontalScatter;
    bool hasVerticalScatter;
    bool hasTrajectoryModule = false;
    bool trajectoryValid = false;
    [SerializeField]
    TrajectoryModule trajectoryModule;
    [Header("Grating and Detector Distances")]
    public float L1mm = 200;
    public float L2mm = 564;
    [Header("UI Elements")]
    [SerializeField] TextMeshProUGUI gravityLabel;
    [SerializeField] TextMeshProUGUI planckLabel;
    [SerializeField] TextMeshProUGUI gravScaleLabel;
    [SerializeField] TextMeshProUGUI planckScaleLabel;
    [SerializeField] Toggle togGravity;
    [SerializeField] Toggle togQuantum;
    public SyncedSlider targetScaleSlider;

    [SerializeField,Range(0.1f,2f),UdonSynced,FieldChangeCallback(nameof(TargetPointScale))] float targetPointScale = 1;
    public float TargetPointScale
    {
        get => targetPointScale;
        set
        {
            value = Mathf.Clamp(value,0.1f,2.0f);
            if (targetPointScale != value)
            {
                targetPointScale = value;
                setMarkerSizes(targetPointScale);
                if (targetScaleSlider != null)
                    targetScaleSlider.SetValues(targetPointScale, 0.1f, 2.0f);
            }
            RequestSerialization();
        }
    }

    public SyncedSlider beamScaleSlider;

    [SerializeField, Range(0.1f, 3f), UdonSynced, FieldChangeCallback(nameof(BeamScale))] float beamScale = 1;
    public float BeamScale
    {
        get => beamScale;
        set
        {
            value = Mathf.Clamp(value, 0.1f, 4.0f);
            if (beamScale != value)
            {
                beamScale = value;
                if (hasSource)
                    mainModule.startSize = particleStartSize * beamScale * Mathf.Sqrt(experimentScale);
                if (beamScaleSlider != null)
                    beamScaleSlider.SetValues(beamScale, 0.1f, 4.0f);
            }
            RequestSerialization();
        }
    }

    [Header("Debug Values")]

    public float minDeBroglieWL = 0.1f; // h/mv
    //[SerializeField]
    //private float minDeBroglieSim = 0.1f;

    ParticleSystem.MainModule mainModule;

    ParticleSystem.Particle[] particles = null;
    int numParticles;

    void setText(TextMeshProUGUI tmproLabel, string text)
    {
        if (tmproLabel != null)
            tmproLabel.text = text;
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        bool isLocal = Networking.IsOwner(this.gameObject);
        if (targetScaleSlider != null)
            targetScaleSlider.IsInteractible = isLocal;
    }
    private void UpdateLabels()
    {
        setText(gravityLabel, string.Format("g={0:#.##}", GravityScale * gravityAcceleration));
        setText(planckLabel, string.Format("h={0:#.##e+0}", h * PlanckScale));
        setText(gravScaleLabel, string.Format("g x {0}", GravityScale));
        setText(planckScaleLabel, string.Format("h x {0}", PlanckScale));
    }

    private int[] scaleSteps = { 1, 2, 5, 10, 20, 50, 100, 200, 500, 1000 };
    private int[] planckSteps = { 1, 5, 10, 50, 100, 500, 1000, 5000, 10000, 50000 };

    private int CheckScaleIndex(int newIndex , int[] steps)
    {
        return Mathf.Clamp(newIndex, 0, steps.Length - 1);
    }

    [Tooltip("Index of Gravity Multiplier"),SerializeField,UdonSynced,FieldChangeCallback(nameof(GravityIndex))]
    private int gravityIndex = 0;
    private int GravityIndex
    {
        set
        {
            gravityIndex = CheckScaleIndex(value,scaleSteps);
            GravityScale = scaleSteps[gravityIndex];
            RequestSerialization();
        }
    }

    [Tooltip("Index of Planck Multiplier"),SerializeField, UdonSynced, FieldChangeCallback(nameof(PlanckIndex))]
    private int planckIndex = 0;
    private int PlanckIndex
    {
        set
        {
            planckIndex = CheckScaleIndex(value, planckSteps);
            PlanckScale = planckSteps[planckIndex];
            RequestSerialization();
        }
    }

    public void OnGravScaleDown()
    {
        GravityIndex = gravityIndex-1;
    }
    public void OnGravScaleUp()
    {
        GravityIndex = gravityIndex + 1;
    }

    public void OnPlanckScaleDown()
    {
        PlanckIndex = planckIndex-1;
    }

    public void OnPlanckScaleUp()
    {
        PlanckIndex =  planckIndex + 1;
    }
    public void OnGravityToggle()
    {
        bool newGravity = !hasGravity;
        if (togGravity != null)
            newGravity  = togGravity.isOn;
        HasGravity = newGravity;
    }

    public void OnQuantumToggle()
    {
        bool newQuantum = !useQuantumScatter;
        if (togQuantum != null)
            newQuantum = togQuantum.isOn;
        UseQuantumScatter = newQuantum;
    }

    Color calcParticleColor(float delta)
    {
        delta -= 0.5f;
        float t;
        Color result;
        {
            if (delta <= -0.1)
            {
                t = (delta + 0.5f) * 2.5f;
                result = new Color(Mathf.Lerp(1.0f, 0.0f, t), Mathf.Lerp(0.0f, 1.0f, t), 0);
            }
            else if (delta <= 0.2f)
            {
                t = (delta + 0.1f) * 3.33330f;
                result = new Color(0, 1, Mathf.Lerp(0.0f, 1.0f, t));
            }
            else if (delta <= 0.3f)
            {
                t = (delta - 0.1f) * 5.0f;
                result = new Color(0, Mathf.Lerp(1.0f, 0.0f, t), 1);
            }
            else
            {
                t = (delta - 0.3f) * 5.0f;
                result = new Color(Mathf.Lerp(0.0f, 0.5f, t), 0, Mathf.Lerp(1.0f, 0.5f, t));
            }
        }
        return result;
    }


    private void LateUpdate()
    {
        int nUpdated = 0;
        Vector3 launchVelocity;
        Vector3 launchPosition;
        float speedScale;
        float timeToGrating;
        double particleFallVelocity;

        if (hasSource)
        {

            numParticles = particleEmitter.particleCount;
            particles = new ParticleSystem.Particle[numParticles];
            numParticles = particleEmitter.GetParticles(particles);
            
            float spreadHigh = UnityEngine.Random.Range(-startDimensions.y, startDimensions.y);
            float spreadWide = UnityEngine.Random.Range(-startDimensions.x, startDimensions.x);

            for (int i = 0; i < numParticles; i++)
            {
                // Startlifetime stores the state; < 10 means newborn
                if (particles[i].startLifetime < 10)
                {
                    nUpdated++;
                    particles[i].startLifetime = 250;
                    particles[i].randomSeed = 250;
                    particles[i].remainingLifetime = 100;
                    launchPosition = new Vector3(gratingThickness, spreadHigh, spreadWide);
                    particles[i].axisOfRotation = launchPosition;
                    launchPosition += sourceXfrm.position;
                    float speedTrim = randomizeSpeeds ? UnityEngine.Random.Range(0f, 1f) : userSpeed;
                    speedScale = 1 + Mathf.Lerp(-randomRange, randomRange, speedTrim);
                    float particleSpeed = avgSimulationSpeed * speedScale;
                    launchVelocity = new Vector3(particleSpeed, 0, 0);
                    Color launchColour;
                    if (trajectoryValid)
                    {
                        int velocityIndex = (int)Mathf.Lerp(0, trajectoryModule.LookupPoints, speedTrim);
                        launchVelocity = trajectoryModule.lookupVelocity(velocityIndex);
                        launchColour = trajectoryModule.lookupColour(velocityIndex);
                    }
                    else 
                    { 
                        if (hasGravity)
                        {
                            timeToGrating = emitToGratingSim / particleSpeed;
                            particleFallVelocity = timeToGrating * gravitySim; // V=AT
                            //particleHeightDelta = 0.5d * timeToGrating * particleFallVelocity; // s = 0.5 AT^2

                            launchVelocity.y = (float)(-particleFallVelocity/2.0); // Calculate initial upward velocity.
                            //launchPosition.y += (float)particleHeightDelta;
                        }
                        launchColour = calcParticleColor(speedTrim);
                    }
                    particles[i].rotation3D = launchVelocity;
                    particles[i].position = launchPosition;
                    particles[i].velocity = launchVelocity;
                    particles[i].startColor = launchColour;
                    //Debug.Log(tmpVel);
                    //particles[i].rotation = particleSpeed;
                    /*    
                    else
                    {
                        speedScale = 1 + Mathf.Lerp(-randomRange, randomRange, speedTrim);
                        float particleSpeed = avgSimulationSpeed * speedScale;
                        launchVelocity = new Vector3(particleSpeed, 0, 0);
                        if (hasGravity)
                        {
                            timeToGrating = emitToGratingSim / particleSpeed;
                            particleFallVelocity = timeToGrating * gravitySim; // V=AT
                            //particleHeightDelta = 0.5d * timeToGrating * particleFallVelocity; // s = 0.5 AT^2
                            launchVelocity.y = (float)(-particleFallVelocity/2.0); // Calculate initial upward velocity.
                            //launchPosition.y += (float)particleHeightDelta;
                        }
                    } */
                }
                else
                {   // Particles below 50 are deemed stopped already and are fading
                    // Any Above 50 and stopped have collided with something and need to be handled
                    uint particleStage = particles[i].randomSeed;
                    float x = particles[i].position.x;
                    float particleGratingDelta = x-gratingPosition.x;
                    bool processedGrating = particleStage <= 240;
                    bool afterGrating = (particleGratingDelta >= gratingThickness);
                    float particleTargetDelta = x-targetPosition.x;
                    if (particleStage >= 50)
                    {   // Handles Stopped Particle
                        if (particles[i].velocity.x < 0.01)
                        {
                            Vector3 decalPos = particles[i].position;
                            Vector3 contactVelocity = particles[i].rotation3D;
                            contactVelocity.y = -contactVelocity.y;
                            if (processedGrating)
                            {
                                particleStage = 43;
                                bool atTarget = hasTarget && (particleTargetDelta >= -0.01f);
                                bool atFloor = hasFloor && (!atTarget);
                                if (atTarget)
                                {
                                    if (hasTargetDecorator)
                                        targetDisplay.PlotParticle(decalPos, particles[i].startColor, 30f);
                                    particles[i].remainingLifetime = 0;
                                }
                                else if (atFloor)
                                {
                                    floorDisplay.PlotParticle(decalPos, particles[i].startColor, 30f);
                                    particles[i].remainingLifetime = 0;
                                    particles[i].velocity = Vector3.zero;
                                }
                                else // Anywhere else
                                {
                                    particles[i].velocity = Vector3.zero;
                                    particles[i].startSize = particles[i].startSize * 0.3f;
                                    particles[i].remainingLifetime = 5;
                                }
                            }
                            else
                            { // Stopped and not processed for grating
                                if (Mathf.Abs(particleGratingDelta) <= 0.01f)
                                { // Here if close to grating
                                    Vector3 upDatedPosition = particles[i].axisOfRotation;
                                    if (hasGrating)
                                        afterGrating = !gratingControl.checkLatticeCollision(upDatedPosition);
                                    if (afterGrating)
                                    {
                                        particles[i].position = upDatedPosition;
                                        particles[i].velocity = contactVelocity;
                                    }
                                    else
                                    {
                                        upDatedPosition.x = decalPos.x;
                                        if (hasGratingDecorator)
                                            gratingDecals.PlotParticle(upDatedPosition, particles[i].startColor, 0.5f);
                                        particles[i].remainingLifetime = 0;
                                        particleStage = 43;
                                    }
                                }
                                else
                                {
                                    particles[i].velocity = Vector3.zero;
                                    particles[i].startSize = particles[i].startSize * 0.1f;
                                    particles[i].remainingLifetime = 5;
                                }
                            }
                            nUpdated++;
                        }
                        if (afterGrating && particleStage > 240)
                        {
                            nUpdated++;
                            particleStage = 240;
                            float speedFraction = particles[i].rotation3D.x / maxSimSpeed;
                            particles[i].remainingLifetime = minLifeTimeAfterGrating / speedFraction;
                            if (useQuantumScatter)
                            {
                                Vector3 unitX = Vector3.right;
                                if (hasHorizontalScatter)
                                    unitX.z = horizontalScatter.RandomImpulseFrac(speedFraction); // * planckValue);
                                if (hasVerticalScatter)
                                    unitX.y = verticalScatter.RandomImpulseFrac(speedFraction);
                                unitX.x = Mathf.Sqrt(1 - Mathf.Clamp01(unitX.z * unitX.z + unitX.y * unitX.y));
                                /*
                                if (vertScatter != null)
                                {
                                    vUpdated.y += (vertScatter.RandomImpulse * _planckToDeltaV);
                                }*/
                                particles[i].velocity = unitX * (speedFraction * maxSimSpeed);
                            }
                        }
                        particles[i].randomSeed = particleStage;
                    }
                }
            }
            
            if (nUpdated > 0)
            {
                particleEmitter.SetParticles(particles, numParticles);
            }
        }
    }

    private void updateGravity()
    {
        if (!hasSource || scaleIsChanging)
            return;
        Debug.Log("Update Grav");
        gravityChanged = false;
        gravitySim = hasGravity ? GravityScale * gravityAcceleration * (slowScaled * slowScaled) / experimentScale : 0.0f;
        var fo = particleEmitter.forceOverLifetime;
        fo.enabled = false;
        fo.y = gravitySim;
        fo.enabled = hasGravity;
        particleEmitter.Clear(); // Restart.
        particleEmitter.Play();
        if (hasTrajectoryModule)
        {
            trajectoryModule.GravitySim = gravitySim;
            trajectoryModule.HasGravity = hasGravity;
        }
    }
    private float targetMarkerSize = 1;
    private float gratingMarkerSize = 1;
    private void setMarkerSizes(float value)
    {
        float mul = particleStartSize * experimentScale / nativeGraphicsRatio;
        targetMarkerSize = Mathf.Lerp(0.1f,1,value) * mul *.3f;
        if (hasTargetDecorator)
            targetDisplay.ParticleSize = targetMarkerSize;
    }

    private void dissolveDisplays()
    {
        if (hasSource)
            particleEmitter.Clear();
        if (hasFloor)
            floorDisplay.Dissolve();
        if (hasTargetDecorator)
            targetDisplay.Dissolve();
        if (hasGratingDecorator)
            gratingDecals.Dissolve();
        Debug.Log("dissolveDisplays()");
    }
    private void updateSettings()
    {
        if (hasSource) 
            mainModule.startSize = particleStartSize * beamScale * Mathf.Sqrt(experimentScale);
        setMarkerSizes(targetPointScale);
        settingsChanged = false;
        avgSimulationSpeed = avgMoleculeSpeed * slowScaled;
        
        maxSimSpeed = avgSimulationSpeed * (1 + randomRange);
        minSimSpeed = avgSimulationSpeed * (1 - randomRange);
        minLifeTimeAfterGrating = 1.25f * gratingToTargetSim / maxSimSpeed;
        minDeBroglieWL = (h * PlanckScale) / (AMU_ToKg * molecularWeight * avgMoleculeSpeed*(1+randomRange));
        if (hasTrajectoryModule)
        {
            trajectoryModule.loadSettings(maxSimSpeed, minSimSpeed, gravitySim, hasGravity, emitToGratingSim);
            trajectoryValid = trajectoryModule.SettingsValid;
        }
        else
            trajectoryValid = false;
        trajectoryChanged = false;
        Vector3 newPosition;

        // Set position of grating
        if (hasSource)
        {
            newPosition = gratingPosition;
            newPosition.x -= emitToGratingSim;
            sourceXfrm.position = newPosition;
            if (collimatorProp != null)
                collimatorProp.localScale = new Vector3(graphicsScale, graphicsScale, graphicsScale);
        }
        if (hasTarget)
        {
            targetPosition = gratingPosition;
            targetPosition.x += gratingToTargetSim;
            targetTransform.position = targetPosition;
            if (targetProp != null)
                targetProp.localScale = new Vector3(graphicsScale, graphicsScale, graphicsScale);
        }
    }
    //[SerializeField]
    private int gratingVersion = -1;
    private Vector2Int apertureCounts = Vector2Int.zero;
    //[SerializeField]
    private Vector2 aperturePitches = Vector2.zero;
    //[SerializeField] 
    private Vector2 apertureSize = Vector2.zero;
    // Grating Dimensions in World Space
    //[SerializeField] 
    private Vector2 gratingSize = Vector2.zero;
    private Vector2 startDimensions = Vector2.zero;
    public int GratingVersion
    {
        get => gratingVersion;
        set
        {
            bool force = gratingVersion < 0 || planckChanged;
            gratingVersion = value;
            if (!hasGrating)
                return;
            planckChanged = false;
            gratingSize = gratingControl.GratingGraphicsSize;
            gratingThickness = gratingControl.panelThickness*1.5f;
            startDimensions = gratingSize/1.8f;
            int rowCount = gratingControl.RowCount;
            int colCount = gratingControl.ColumnCount;
            float holeWidth = gratingControl.ApertureWidthMetres;
            float holeHeight = gratingControl.ApertureHeightMetres;
            float colPitch = gratingControl.ColumnPitchMetres;
            float rowPitch = gratingControl.RowPitchMetres;
            bool horizChanged = force || ((colCount != apertureCounts.x) || (holeWidth != apertureSize.x) || (colPitch != aperturePitches.x));
            bool vertChanged = force || ((rowCount != apertureCounts.y) || (holeHeight != apertureSize.y) || (rowPitch != aperturePitches.y));
            apertureCounts.x = colCount; apertureCounts.y = rowCount;
            apertureSize.x = holeWidth; apertureSize.y = holeHeight; 
            aperturePitches.x = colPitch; aperturePitches.y = rowPitch;
            gratingMarkerSize = experimentScale * Mathf.Min(gratingControl.ApertureHeightMetres, gratingControl.ApertureWidthMetres);
            if (hasGratingDecorator)
                gratingDecals.ParticleSize = gratingMarkerSize;

            if (horizChanged && horizReady && apertureCounts.x > 0)
                horizontalScatter.SetGratingByPitch(apertureCounts.x, apertureSize.x, aperturePitches.x, minDeBroglieWL);
            if (vertChanged && vertReady && apertureCounts.y > 0)
                verticalScatter.SetGratingByPitch(apertureCounts.y, apertureSize.y, aperturePitches.y, minDeBroglieWL);
            if (horizChanged || vertChanged)
                dissolveDisplays();
        }
    }

    float polltime = 1;
    [SerializeField]
    bool horizReady = false;
    [SerializeField]
    bool vertReady = false;
    [SerializeField]
    bool gratingReady = false;

    private void Update()
    {
        polltime -= Time.deltaTime;
        if (polltime > 0)
            return;
        polltime += ScaleIsChanging ? 0.1f : 0.3f;
        
        if (hasVerticalScatter && !vertReady)
        {
            vertReady = verticalScatter.Started;
            if (vertReady)
                gratingVersion = -1;
            else
                return;
            //Debug.Log("Got Vert");
        }
        if (hasHorizontalScatter && !horizReady)
        {
            horizReady = horizontalScatter.Started;
            if (horizReady)
                gratingVersion = -1;
            else
                return;
            //Debug.Log("Got Horiz");
        }
        if (hasGrating && !gratingReady)
        {
            gratingReady = gratingControl.Started;
            if (gratingReady)
                gratingVersion = -1;
            else
                return;
            //Debug.Log("Got Grating");
        }

        bool updateUI = planckChanged || gravityChanged || settingsChanged || trajectoryChanged;
        trajectoryChanged = trajectoryChanged || gravityChanged;
        if (gravityChanged)
            updateGravity();
        if (settingsChanged || planckChanged || trajectoryChanged)
            updateSettings();
        if (hasGrating)
        {
            int gcVersion = gratingControl.GratingVersion;
            if (gcVersion != gratingVersion)
            {
                GratingVersion = gcVersion;
                Debug.Log($"Grating version: {gcVersion}");
            }
        }
        if (updateUI)
        {
            UpdateLabels();
            if (!scaleIsChanging)
                dissolveDisplays();
        }
    }

    void Start()
    {
        if (trajectoryModule == null)
            trajectoryModule = GetComponent<TrajectoryModule>();
        hasTrajectoryModule = trajectoryModule != null;

        hasGratingDecorator = gratingDecals != null;

        if (gratingControl != null)
        {
            hasGrating = true;
            gratingXfrm = gratingControl.transform;
            gratingPosition = gratingXfrm.position;
            //gratingPosition.x -= 0.001f;
        }
        float tmp = experimentScale;
        experimentScale = 0;
        ExperimentScale = tmp;
        if (particleEmitter == null)
            particleEmitter = GetComponent<ParticleSystem>();
        hasSource = particleEmitter != null;
        if (hasSource)
        {
            sourceXfrm = particleEmitter.transform;
            //sourceXfrm.Rotate(new Vector3(0, 90, 0));
            mainModule = particleEmitter.main;
            mainModule.startSpeed = 0.1f;
            mainModule.playOnAwake = true;
        }
        if (targetScaleSlider != null)
            targetScaleSlider.SetValues(targetPointScale, 0.1f, 3.0f);
        if (beamScaleSlider != null)
            beamScaleSlider.SetValues(beamScale, 0.1f, 4f);

        hasHorizontalScatter = (horizontalScatter != null);
        hasVerticalScatter = (verticalScatter != null);
        hasFloor = floorDisplay != null;
        hasTarget = targetTransform != null;
        hasTargetDecorator = targetDisplay != null;
        if (hasTarget)
        {
            targetPosition = targetTransform.position;
        }
        RandomRange = randomRange;
        minLifeTimeAfterGrating = 1.25f * gratingToTargetSim / maxSimSpeed;
        // Initialise checkboxes if present.
        HasGravity = hasGravity;
        UseQuantumScatter = useQuantumScatter;
        GravityIndex = gravityIndex;
        PlanckIndex = planckIndex;
        gravityChanged = true;
        settingsChanged = true;
        trajectoryChanged = true;
    }
}
