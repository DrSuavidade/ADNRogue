using UnityEngine;


[CreateAssetMenu(menuName = "Geneforge/Animal Essence", fileName = "NewAnimalEssence")]
public class AnimalEssence : ScriptableObject
{
    [Header("Presentation")]
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;


    // Later: stats/ability payloads go here
    // e.g., refs to Ability assets or simple numeric modifiers
}