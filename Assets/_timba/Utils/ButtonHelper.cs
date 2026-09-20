using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonHelper : MonoBehaviour
{
    [SerializeField] private Sprite[] sprites;
    private Button button;
    private byte currentSpriteIndex = 0;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    public void NextSprite()
    {
        if (sprites is null or { Length: 0 })
        { 
            Debug.LogWarning("No sprites assigned to the ButtonHelper component.");
            return;
        }

        currentSpriteIndex++;

        if (currentSpriteIndex >= sprites.Length)
            currentSpriteIndex = 0;

        button.image.sprite = sprites[currentSpriteIndex];
    }
}
