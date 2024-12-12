
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class ParticleSelect : UdonSharpBehaviour
{
    [SerializeField]
    MoleculeExperiment experiment;
    [SerializeField]
    string[] particleNames;
    [SerializeField]
    float[] molecularWeights;

    [SerializeField]
    Button btnNext;
    [SerializeField]
    Button btnPrev;

    [SerializeField, UdonSynced, FieldChangeCallback(nameof(ParticleIndex))]
    int particleIndex = 0;

    private VRCPlayerApi player;
    private bool locallyOwned = false;
   
    [SerializeField]
    private TextMeshProUGUI txtParticleWeight;
    [SerializeField]
    private TextMeshProUGUI txtParticleName;

    

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        locallyOwned = Networking.IsOwner(this.gameObject);
    }

    private int prevIndex = -1;
    private int ParticleIndex 
    { 
        get => particleIndex;
        set 
        {
            int len = particleNames != null ? particleNames.Length-1 : 0;
            particleIndex = Mathf.Clamp(value, 0, len);
            if (particleIndex != prevIndex)
            {
                if (btnPrev != null)
                    btnPrev.interactable = particleIndex > 0;
                if (btnNext != null)
                    btnNext.interactable = particleIndex < len;
            }
            RequestSerialization();  
            if (experiment != null)
                experiment.MolecularWeight = molecularWeights[particleIndex];
            if (txtParticleWeight != null)
                txtParticleWeight.text = string.Format("{0:#.##}amu", molecularWeights[particleIndex]);
            if (txtParticleName != null)
                txtParticleName.text = particleNames[particleIndex];

            prevIndex = particleIndex;
        }
    }

    // Button Events
    public void onNext()
    {
        Debug.Log(gameObject.name + "next");
        if (!locallyOwned)
        if (!locallyOwned)
            Networking.SetOwner(player, gameObject);
        ParticleIndex = particleIndex + 1;
    }

    public void onPrev()
    {
        Debug.Log(gameObject.name + "prev");
        if (!locallyOwned)
            Networking.SetOwner(player, gameObject);
        ParticleIndex = particleIndex - 1;
    }


    void Start()
    {
        player = Networking.LocalPlayer;
        locallyOwned = Networking.IsOwner(gameObject);

        if (particleNames != null && particleNames.Length > 0) 
        { 
            int weightLen = (molecularWeights != null ? molecularWeights.Length : 0);
            if ((molecularWeights == null) || (weightLen < particleNames.Length))
            {
                float[] newWeights = new float[particleNames.Length];
                for (int i = 0; i < particleNames.Length; i++)
                {
                    newWeights[i] = i < weightLen ? molecularWeights[i] : i;
                }
                molecularWeights = newWeights;
            }
        }
        if (locallyOwned) 
            ParticleIndex = particleIndex;
    }
}
