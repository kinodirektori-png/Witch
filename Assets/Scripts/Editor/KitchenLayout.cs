using UnityEngine;
using UnityEngine.UI;
using Witch.UI;

namespace Witch.EditorTools
{
    /// <summary>
    /// Makes the MainLab kitchen fill phones of any height (issue #41 follow-up). The scene is laid out for
    /// 1080x1920; on taller screens (most modern phones and foldables) the extra height is shared out instead of
    /// collecting as an empty band at the top:
    /// <list type="bullet">
    /// <item>The canvas always keeps the full 1080 width (Expand), so nothing is cut off at the sides.</item>
    /// <item>The counter stays at the bottom, the stove group moves up a little, the shelf moves up more.</item>
    /// <item>The HUD and title follow the device's safe area (notch, camera hole, home bar).</item>
    /// </list>
    /// On a 1080x1920 screen nothing moves. Used by <see cref="MainLabSceneBuilder"/>.
    /// </summary>
    public static class KitchenLayout
    {
        private const float DesignHeight = 1920f;

        /// <summary>Share of the extra height that goes below each group, 0 = stays on the bottom.</summary>
        private const float StoveShare = 0.3f;
        private const float ShelfShare = 0.62f;

        private static readonly string[] StoveGroup =
        {
            "Stove", "Pot", "Witch", "Temperature", "Strength", "EmptyPotButton", "HeatKnob",
        };

        public static void FitTallScreens(RectTransform canvas)
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            }

            foreach (string name in StoveGroup)
            {
                if (canvas.Find(name) is RectTransform rect)
                {
                    AnchorAtShare(rect, StoveShare);
                }
            }

            if (canvas.Find("Shelf") is RectTransform shelf)
            {
                AnchorAtShare(shelf, ShelfShare);
            }

            if (canvas.Find("HUD") is RectTransform hud)
            {
                hud.gameObject.AddComponent<SafeAreaFitter>();
                if (canvas.Find("Title") is RectTransform title)
                {
                    title.SetParent(hud, false);
                    title.SetAsFirstSibling();
                }
            }
        }

        /// <summary>
        /// Moves the rect's vertical anchors to <paramref name="share"/> of the screen height while keeping its exact
        /// position on a 1920-high canvas. On a taller canvas it rises by share x (extra height).
        /// </summary>
        private static void AnchorAtShare(RectTransform rect, float share)
        {
            Vector2 min = rect.anchorMin;
            Vector2 max = rect.anchorMax;
            bool stretchedVertically = !Mathf.Approximately(min.y, max.y);

            if (stretchedVertically)
            {
                // A full-screen container (e.g. the shelf): pin it to a 1920-high band that starts at share.
                Vector2 offsetMin = rect.offsetMin;
                Vector2 offsetMax = rect.offsetMax;
                float bottom = min.y * DesignHeight + offsetMin.y;
                float top = max.y * DesignHeight + offsetMax.y;
                rect.anchorMin = new Vector2(min.x, share);
                rect.anchorMax = new Vector2(max.x, share);
                rect.offsetMin = new Vector2(offsetMin.x, bottom - share * DesignHeight);
                rect.offsetMax = new Vector2(offsetMax.x, top - share * DesignHeight);
                return;
            }

            float y = min.y * DesignHeight + rect.anchoredPosition.y;
            rect.anchorMin = new Vector2(min.x, share);
            rect.anchorMax = new Vector2(max.x, share);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y - share * DesignHeight);
        }
    }
}
