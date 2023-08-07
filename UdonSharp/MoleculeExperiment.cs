﻿using System;
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
    [SerializeField, Range(0f, 0.7f), Tooltip("Fraction of avg velocity +- e.g. 0.5 = +-50% of average")]
    private float randomRange = 0.6f;
    [Tooltip("Slow Motion"), SerializeField, Range(0.001f, 1f)]
    private float slowMotion = 0.025f;

    public float molecularWeight = 514.5389f;
    public string moleculeName = "Pthalocyanine";
    [Header("Operating Settings")]
    [SerializeField,ColorUsage(true,true)]
    Color defaultColour = Color.green;
    [Tooltip("Default Particle Size"), SerializeField, UdonSynced, FieldChangeCallback(nameof(ParticleStartSize))]
    float particleStartSize = 0.001f;
    [SerializeField, Range(0.1f, 3f), UdonSynced, FieldChangeCallback(nameof(BeamVisibility))] float beamVisibility = 3;

    [SerializeField]
    bool useMonochrome = false;
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(UseQuantumScatter))] private bool useQuantumScatter;
    [SerializeField, UdonSynced, FieldChangeCallback(nameof(UseGravity))]
    private bool  useGravity = true;

    [Header("Grating and Detector Distances")]
    public float L1mm = 200;
    public float L2mm = 561;

    [Header("Constants")]
    public float h = 6.62607015e-34f; // 
    public float AMU_ToKg = 1.66054e-27f;

    [Header("Gravity")]
    private bool gravityChanged = false;
    private bool settingsChanged = false;
    private bool planckChanged = false;
    private bool trajectoryChanged = false;
    public bool UseGravity
    {
        get => useGravity;
        set
        {
            if (useGravity != value)
            {
                useGravity = value;
                gravityChanged = true;
            }
            if ((togGravity != null) && (togGravity.isOn != value))
                togGravity.isOn = value;
            RequestSerialization();
        }
    }
    [SerializeField]
    private float gravityAcceleration; // Required because forceoverlifetime is hidden and must be copied here    ParticleSystem.MainModule mainModule;

    //[Header("Calculated Scale Values")]
    //[SerializeField]
    private float gravitySim;
    //[SerializeField]
    private float emitToGratingSim;
    //[SerializeField]
    private float gratingToTargetSim;
    //[SerializeField]
    private float maxLifetimeAfterGrating = 20f;


    [Header("Scaling Control")]

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
                Debug.Log("Grav Scale=" + gravityScale.ToString());
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

    //[SerializeField]
    private float slowScaled = 0.025f;
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
                slowScaled = slowMotion * graphicsScale;
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
    //[SerializeField]
    //Transform floorTransform;
    [SerializeField]
    TargetDisplay floorDisplay;
    [SerializeField]
    Transform targetTransform;
    [SerializeField]
    TargetDisplay targetDisplay;
    [SerializeField]
    TargetDisplay gratingDecals;
    [SerializeField]
    bool hasFloor;
    [SerializeField]
    bool hasFloorDecorator;
    [SerializeField]
    bool hasTarget;
    [SerializeField]
    bool hasTargetDecorator;
    [SerializeField]
    bool hasGrating;
    [SerializeField]
    bool hasGratingDecorator;
    bool hasSource;
    bool hasHorizontalScatter;
    bool hasVerticalScatter;
    bool hasTrajectoryModule = false;
    bool trajectoryValid = false;

    [SerializeField]
    TrajectoryModule trajectoryModule;
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
                setMarkerSizes(targetPointScale/2.0f);
                if (targetScaleSlider != null)
                    targetScaleSlider.SetValues(targetPointScale, 0.1f, 2.0f);
            }
            RequestSerialization();
        }
    }

    public SyncedSlider beamScaleSlider;

    public float BeamVisibility
    {
        get => beamVisibility;
        set
        {
            value = Mathf.Clamp(value, 0.1f, 4.0f);
            if (beamVisibility != value)
            {
                beamVisibility = value;
                if (hasSource)
                    mainModule.startSize = particleStartSize * beamVisibility * Mathf.Sqrt(experimentScale);
                if (beamScaleSlider != null)
                    beamScaleSlider.SetValues(beamVisibility, 0.1f, 4.0f);
            }
            RequestSerialization();
        }
    }

    //[Header("Debug Values")]
    //[SerializeField]
    private float minDeBroglieWL = 0.1f; // h/mv
    //[SerializeField]
    private bool horizReady = false;
    //[SerializeField]
    private bool vertReady = false;
    //[SerializeField]
    private bool gratingReady = false;

    [Tooltip("Index of Gravity Multiplier"), UdonSynced, FieldChangeCallback(nameof(GravityIndex))]
    private int gravityIndex = 0;
    private int GravityIndex
    {
        set
        {
            gravityIndex = CheckScaleIndex(value, scaleSteps);
            GravityScale = scaleSteps[gravityIndex];
            RequestSerialization();
        }
    }

    [Tooltip("Index of Planck Multiplier"), UdonSynced, FieldChangeCallback(nameof(PlanckIndex))]
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


    // Internal Variables
    private ParticleSystem.MainModule mainModule;

    private ParticleSystem.Particle[] particles = null;
    private int numParticles;

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

    private int[] scaleSteps = { 1, 2, 5, 10, 15, 20, 50, 100, 200, 500, 1000 };
    private int[] planckSteps = { 1, 5, 10, 50, 100, 500, 1000 };

    private int CheckScaleIndex(int newIndex , int[] steps)
    {
        return Mathf.Clamp(newIndex, 0, steps.Length - 1);
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
        bool newGravity = !useGravity;
        if (togGravity != null)
            newGravity  = togGravity.isOn;
        UseGravity = newGravity;
    }

    public void OnQuantumToggle()
    {
        bool newQuantum = !useQuantumScatter;
        if (togQuantum != null)
            newQuantum = togQuantum.isOn;
        UseQuantumScatter = newQuantum;
    }

    private int fadeParticle(int particleIndex)
    {
        particles[particleIndex].velocity = Vector3.zero;
        particles[particleIndex].startLifetime = 43;
        particles[particleIndex].remainingLifetime = 0.5f;
        return 43;
    }

    private int fadeParticleColour(int particleIndex,Color theColour)
    {
        particles[particleIndex].velocity = Vector3.zero;
        particles[particleIndex].startLifetime = 43;
        particles[particleIndex].remainingLifetime = 0.5f;
        particles[particleIndex].startColor = theColour;
        return 43;
    }

    private int killParticle(int particleIndex)
    {
        particles[particleIndex].velocity = Vector3.zero;
        particles[particleIndex].startLifetime = 43;
        particles[particleIndex].remainingLifetime = 0f;
        return 42;
    }

    private void LateUpdate()
    {
        int nUpdated = 0;
        Vector3 launchVelocity;
        Vector3 launchPosition;
        float speedScale;

        if (hasSource)
        {

            numParticles = particleEmitter.particleCount;
            particles = new ParticleSystem.Particle[numParticles];
            numParticles = particleEmitter.GetParticles(particles);
            
            float spreadHigh = UnityEngine.Random.Range(-startDimensions.y, startDimensions.y);
            float spreadWide = UnityEngine.Random.Range(-startDimensions.x, startDimensions.x);

            for (int i = 0; i < numParticles; i++)
            {
                int particleStage = Mathf.RoundToInt(particles[i].startLifetime);
                // particleStage < 10 means newborn (unlaunched)
                if (particleStage < 10)
                {
                    nUpdated++;
                    particleStage = 250;
                    particles[i].remainingLifetime = 100;
                    launchPosition = new Vector3(gratingThickness, spreadHigh, spreadWide);
                    particles[i].axisOfRotation = launchPosition;
                    launchPosition += sourceXfrm.position;
                    float speedTrim = randomizeSpeeds ? UnityEngine.Random.Range(0f, 1f) : userSpeed;
                    speedScale = 1 + Mathf.Lerp(-randomRange, randomRange, speedTrim);
                    float particleSpeed = avgSimulationSpeed * speedScale;
                    launchVelocity = new Vector3(particleSpeed, 0, 0);
                    Color launchColour = defaultColour;
                    uint particleIndex = 0;
                    if (trajectoryValid)
                    {
                        int velocityIndex = (int)Mathf.Lerp(0, trajectoryModule.LookupPoints, speedTrim);
                        launchVelocity = trajectoryModule.lookupVelocity(velocityIndex);
                        if (!useMonochrome)
                            launchColour = trajectoryModule.lookupColour(velocityIndex);
                    }
                    particles[i].velocity = launchVelocity;
                    launchVelocity.y = -launchVelocity.y;
                    particles[i].rotation3D = launchVelocity;
                    particles[i].position = launchPosition;
                    particles[i].startColor = launchColour;
                    particles[i].startLifetime = particleStage;
                    particles[i].randomSeed = particleIndex;
                }
                else // not a newborn
                {
                    bool particleChanged = false;
                    Vector3 particlePos = particles[i].position;
                    bool afterTarget = particlePos.x > (targetPosition.x+0.1f) && particleStage > 50;
                    if (afterTarget) // Stray
                    {
                        particleStage = fadeParticle(i);
                        nUpdated++;
                    }
                    if (particleStage > 50)
                    {
                        Vector3 particleVelocity = particles[i].velocity;
                        float particleGratingDelta = particlePos.x - gratingPosition.x;
                        // Any Above 50 and stopped have collided with something and need to be handled
                        float particleTargetDelta = particlePos.x - targetPosition.x;
                        bool preGratingFilter = particleStage > 240;
                        bool stopped = (particleVelocity.x < 0.01f);
                        // Handle Stopped Particle
                        if (stopped)
                        {
                            Vector3 collideVelocity = particles[i].rotation3D;
                            //
                            // Process impact of particle stopping after initial launch
                            if (preGratingFilter)
                            { // Stopped and not processed for grating
                              // Now test to see if stopped at grating
                                if (Mathf.Abs(particleGratingDelta) <= 0.01f)
                                { // Here if close to grating
                                    Vector3 gratingHitPosition = particles[i].axisOfRotation;
                                    if (hasGrating && (!gratingControl.checkLatticeCollision(gratingHitPosition)))
                                    {
                                        particlePos = gratingHitPosition;
                                        particleVelocity = collideVelocity;
                                        particleStage = 240;
                                        particleChanged = true;
                                    }
                                    else
                                    {
                                        gratingHitPosition.x = particlePos.x;
                                        if (hasGratingDecorator)
                                        {
                                            gratingDecals.PlotParticle(gratingHitPosition, particles[i].startColor, 0.5f);
                                            particleStage = killParticle(i);
                                        }
                                        else
                                            particleStage = fadeParticle(i);
                                        nUpdated++;
                                    }
                                }
                                else
                                {
                                    particleStage = fadeParticle(i);
                                    nUpdated++;
                                }
                            }
                            else
                            { // Stopped and after grating use particle for decal or erase
                                bool atTarget = hasTarget && (particleTargetDelta >= -0.01f);
                                bool atFloor = hasFloor && (!atTarget);
                                nUpdated++;
                                if (atTarget)
                                {
                                    if (hasTargetDecorator)
                                    {
                                        targetDisplay.PlotParticle(particlePos, particles[i].startColor, 30f);
                                        particleStage = killParticle(i);
                                    }
                                    else
                                    {
                                        particleStage = fadeParticle(i);
                                    }
                                }
                                else if (atFloor)
                                {
                                    floorDisplay.PlotParticle(particlePos, particles[i].startColor, 30f);
                                    particleStage = killParticle(i);
                                }
                                else // Anywhere else
                                    particleStage = fadeParticle(i);
                            }
                        } // Stopped
                        if (particleStage == 240)
                        {
                            float speedFraction = particleVelocity.x / maxSimSpeed;
                            float speedRestore = (maxSimSpeed * speedFraction);
                            Vector3 unitVecScatter = Vector3.right;
                            particles[i].remainingLifetime = maxLifetimeAfterGrating;
                            particleChanged = true;
                            particleStage = 239;
                            if (useQuantumScatter)
                            {
                                float sY=0,sZ=0;
                                if (hasHorizontalScatter)
                                {
                                    sZ = horizontalScatter.RandomImpulseFrac(speedFraction);
                                    unitVecScatter.z = sZ;
                                }

                                if (hasVerticalScatter)
                                {
                                    sY = verticalScatter.RandomImpulseFrac(speedFraction);
                                    unitVecScatter.y = sY;
                                }
                                unitVecScatter.x = Mathf.Sqrt(1 - Mathf.Clamp01(sY * sY + sZ * sZ));
                                Vector3 updateV = unitVecScatter * speedRestore;
                                updateV.y += particleVelocity.y;
                                particleVelocity = updateV;
                            }
                        }
                        if (particleChanged)
                        {
                            particles[i].startLifetime = particleStage;
                            particles[i].velocity = particleVelocity;
                            particles[i].position = particlePos;
                            nUpdated++;
                        }
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
        Debug.Log("Update Gravity!!!");
        gravityChanged = false;
        gravitySim = useGravity ? GravityScale * gravityAcceleration * (slowScaled * slowScaled) / experimentScale : 0.0f;
        var fo = particleEmitter.forceOverLifetime;
        fo.enabled = false;
        fo.y = gravitySim;
        fo.enabled = useGravity;
        particleEmitter.Clear(); // Restart.
        particleEmitter.Play();
        if (hasTrajectoryModule)
        {
            trajectoryModule.GravitySim = gravitySim;
            trajectoryModule.UseGravity = useGravity;
        }
    }
    private float targetMarkerSize = 1;
    private float gratingMarkerSize = 1;
    private void setMarkerSizes(float value)
    {
        float mul = particleStartSize * experimentScale / nativeGraphicsRatio;
        targetMarkerSize = Mathf.Lerp(0.1f,1,value) * mul;
        if (hasTargetDecorator)
            targetDisplay.ParticleSize = targetMarkerSize;
        if (hasFloorDecorator)
            floorDisplay.ParticleSize = targetMarkerSize;
    }

    private void dissolveDisplays()
    {
        if (hasSource)
            particleEmitter.Clear();
        if (hasFloorDecorator)
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
            mainModule.startSize = particleStartSize * beamVisibility * Mathf.Sqrt(experimentScale);
        setMarkerSizes(targetPointScale);
        settingsChanged = false;
        avgSimulationSpeed = avgMoleculeSpeed * slowScaled;
        
        maxSimSpeed = avgSimulationSpeed * (1 + randomRange);
        minSimSpeed = avgSimulationSpeed * (1 - randomRange);
        maxLifetimeAfterGrating = 1.25f * gratingToTargetSim / minSimSpeed;
        minDeBroglieWL = (h * PlanckScale) / (AMU_ToKg * molecularWeight * avgMoleculeSpeed*(1+randomRange));
        if (hasTrajectoryModule)
        {
            trajectoryModule.loadSettings(maxSimSpeed, minSimSpeed, gravitySim, useGravity, emitToGratingSim);
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
            beamScaleSlider.SetValues(beamVisibility, 0.1f, 4f);

        hasHorizontalScatter = (horizontalScatter != null);
        hasVerticalScatter = (verticalScatter != null);
        //hasFloor = floorTransform != null;
        hasFloorDecorator = floorDisplay != null;
        hasTarget = targetTransform != null;
        hasTargetDecorator = targetDisplay != null;
        if (hasTarget)
        {
            targetPosition = targetTransform.position;
        }
        RandomRange = randomRange;
        // Initialise checkboxes if present.
        UseGravity = useGravity;
        UseQuantumScatter = useQuantumScatter;
        GravityIndex = gravityIndex;
        PlanckIndex = planckIndex;
        gravityChanged = true;
        settingsChanged = true;
        trajectoryChanged = true;
    }
}
