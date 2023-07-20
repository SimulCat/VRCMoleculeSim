
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TrajectoryCalc : UdonSharpBehaviour
{
    [Header("Number of Lookup Points")]
    [SerializeField,Range(128,4096)]
    private int LookupPoints = 256;
    void Start()
    {
        
    }
}
