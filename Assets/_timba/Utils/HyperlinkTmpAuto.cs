using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class HyperlinkTmpHover : MonoBehaviour, IPointerClickHandler, IPointerMoveHandler
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private Color hoverColor = Color.cyan;

    private int lastHoverIndex = -1;
    private Color32[] originalVertexColors; // stores cache of original colors per link

    private Camera cam;

    void Awake()
    {
        // Detect canvas mode to pick the correct camera
        Canvas canvas = GetComponentInParent<Canvas>();
        cam = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;
    }

    void Start()
    {
        // Force TMP to generate geometry
        text.ForceMeshUpdate();
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        // Find which link the cursor is over
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(text, Input.mousePosition, cam);

        if (linkIndex != lastHoverIndex)
        {
            // If we left a previous link, restore its color
            if (lastHoverIndex != -1)
            {
                RestoreLinkColor(lastHoverIndex);
            }

            // If we entered a new link, highlight it
            if (linkIndex != -1)
            {
                ApplyHoverColor(linkIndex);
            }

            lastHoverIndex = linkIndex;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (text == null)
        {
            Debug.LogError("No TMP_Text assigned for hyperlink click detection", gameObject);
            return;
        }

        // Identify clicked link index
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(text, Input.mousePosition, cam);

        if (linkIndex >= 0 && linkIndex < text.textInfo.linkInfo.Length)
        {
            TMP_LinkInfo linkInfo = text.textInfo.linkInfo[linkIndex];
            string linkID = linkInfo.GetLinkID();
            Application.OpenURL(linkID);
        }
    }

    private void ApplyHoverColor(int linkIndex)
    {
        // *** English comment ***
        // This method recolors all characters belonging to the given hyperlink

        TMP_LinkInfo linkInfo = text.textInfo.linkInfo[linkIndex];
        TMP_TextInfo textInfo = text.textInfo;

        // We must store the original vertex colors to revert later
        originalVertexColors = new Color32[linkInfo.linkTextLength * 4];
        int cacheIndex = 0;

        for (int i = 0; i < linkInfo.linkTextLength; i++)
        {
            int charIndex = linkInfo.linkTextfirstCharacterIndex + i;
            TMP_CharacterInfo charInfo = textInfo.characterInfo[charIndex];

            if (!charInfo.isVisible)
                continue;

            int meshIndex = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;

            Color32[] vertexColors = textInfo.meshInfo[meshIndex].colors32;

            // Cache original colors
            originalVertexColors[cacheIndex++] = vertexColors[vertexIndex];
            originalVertexColors[cacheIndex++] = vertexColors[vertexIndex + 1];
            originalVertexColors[cacheIndex++] = vertexColors[vertexIndex + 2];
            originalVertexColors[cacheIndex++] = vertexColors[vertexIndex + 3];

            // Apply hover color
            vertexColors[vertexIndex] = hoverColor;
            vertexColors[vertexIndex + 1] = hoverColor;
            vertexColors[vertexIndex + 2] = hoverColor;
            vertexColors[vertexIndex + 3] = hoverColor;
        }

        // Apply the updated mesh data
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    private void RestoreLinkColor(int linkIndex)
    {
        // *** English comment ***
        // Restores the previously cached vertex colors when the cursor leaves the link

        if (originalVertexColors == null)
            return;

        TMP_LinkInfo linkInfo = text.textInfo.linkInfo[linkIndex];
        TMP_TextInfo textInfo = text.textInfo;

        int cacheIndex = 0;

        for (int i = 0; i < linkInfo.linkTextLength; i++)
        {
            int charIndex = linkInfo.linkTextfirstCharacterIndex + i;
            TMP_CharacterInfo charInfo = textInfo.characterInfo[charIndex];

            if (!charInfo.isVisible)
                continue;

            int meshIndex = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;

            Color32[] vertexColors = textInfo.meshInfo[meshIndex].colors32;

            // Restore original color
            vertexColors[vertexIndex] = originalVertexColors[cacheIndex++];
            vertexColors[vertexIndex + 1] = originalVertexColors[cacheIndex++];
            vertexColors[vertexIndex + 2] = originalVertexColors[cacheIndex++];
            vertexColors[vertexIndex + 3] = originalVertexColors[cacheIndex++];
        }

        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);

        // Clear cache
        originalVertexColors = null;
    }
}
