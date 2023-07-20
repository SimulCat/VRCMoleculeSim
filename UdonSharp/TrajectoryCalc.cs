
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class TrajectoryCalc : UdonSharpBehaviour
{
    [Header("Number of Lookup Points")]
    [SerializeField,Range(128,4096)]
    private int LookupPoints = 256;
    Vector3[] launchVelocities;
    bool isValid = false;

    public float gratingDistance;
    public Vector2 speedMaxMin;
    public float gravity;
    public bool hasGravity;

    public void setupTrajectories(float speedMax, float speedMin, float gravity , bool hasGravity)
    {
        if ((launchVelocities == null) || (launchVelocities.Length != LookupPoints))
            launchVelocities = new Vector3[LookupPoints];
        float vDelta = speedMax - speedMin;
        for (int i = 0; i < LookupPoints; i++)
        {
            float frac = (float)i / LookupPoints;
            launchVelocities[i] = new Vector3(Mathf.Lerp(speedMin, speedMax, frac),0,0);
        }
        isValid = true;
    }
    void Start()
    {
        setupTrajectories(speedMaxMin.x,speedMaxMin.y,gravity,hasGravity);
    }
}
