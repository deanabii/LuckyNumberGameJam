using UnityEngine;

[CreateAssetMenu(fileName = "New Card", menuName = "PotionGame/Card Data")]
public class CardData : ScriptableObject
{
    public IngredientType ingredientType; 
    
    [Range(1, 9)]
    public int cardValue; 

    public Sprite cardGraphic; 
}