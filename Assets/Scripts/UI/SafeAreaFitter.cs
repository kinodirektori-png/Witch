using UnityEngine;

namespace Witch.UI
{
    /// <summary>
    /// Keeps a full-screen RectTransform inside the device's safe area, so HUD buttons and text never sit under a
    /// notch, camera hole or home indicator. Put it on a stretched container (e.g. the HUD) and parent the UI to it.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rect;
        private Rect appliedArea;
        private Vector2Int appliedScreen;

        private void Awake()
        {
            rect = (RectTransform)transform;
            Apply();
        }

        private void Update()
        {
            // Rotating, folding or unfolding a device changes the safe area at runtime.
            if (Screen.safeArea != appliedArea || Screen.width != appliedScreen.x || Screen.height != appliedScreen.y)
            {
                Apply();
            }
        }

        private void Apply()
        {
            appliedArea = Screen.safeArea;
            appliedScreen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Vector2 min = appliedArea.position;
            Vector2 max = appliedArea.position + appliedArea.size;
            rect.anchorMin = new Vector2(min.x / Screen.width, min.y / Screen.height);
            rect.anchorMax = new Vector2(max.x / Screen.width, max.y / Screen.height);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
