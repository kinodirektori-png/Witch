using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Witch.Data;

namespace Witch.UI
{
    /// <summary>
    /// Medea's garden: a full-screen panel with the harvest spots (lemon tree, rock salt, old lake bed). Opens from
    /// its HUD button or from "Gather" on a lore page in the <see cref="RecipeBookUI"/>, which closes the book and
    /// highlights the matching spot. Switches the book's Gather buttons on.
    /// </summary>
    /// <remarks>
    /// Shown and hidden with its <see cref="CanvasGroup"/>, so it blocks the kitchen's drags only while open.
    /// Harvest rules live in <c>Witch.Gameplay.HarvestSpot</c>; this class only navigates.
    /// </remarks>
    public class GardenUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup panel;
        [SerializeField] private Button openButton;

        [Tooltip("Large \"Back to kitchen\" button.")]
        [SerializeField] private Button closeButton;

        [SerializeField] private RecipeBookUI book;
        [SerializeField] private List<HarvestSpotView> spots = new List<HarvestSpotView>();

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            SetVisible(false);
        }

        private void Start()
        {
            if (book != null)
            {
                book.SetGatherAvailable(true);
            }
        }

        private void OnEnable()
        {
            if (openButton != null)
            {
                openButton.onClick.AddListener(Open);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }

            if (book != null)
            {
                book.GatherRequested += HandleGatherRequested;
            }
        }

        private void OnDisable()
        {
            if (openButton != null)
            {
                openButton.onClick.RemoveListener(Open);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
            }

            if (book != null)
            {
                book.GatherRequested -= HandleGatherRequested;
            }
        }

        public void Open()
        {
            IsOpen = true;
            SetVisible(true);
            foreach (HarvestSpotView spot in spots)
            {
                if (spot != null)
                {
                    spot.Refresh();
                }
            }
        }

        /// <summary>Opens the garden and highlights the spot for <paramref name="entry"/>.</summary>
        public void Open(LoreEntryData entry)
        {
            Open();
            HarvestSpotView match = spots.Find(view => view != null && view.Spot != null && view.Spot.Entry == entry);
            if (match != null)
            {
                match.Highlight();
            }
        }

        public void Close()
        {
            IsOpen = false;
            SetVisible(false);
        }

        private void HandleGatherRequested(LoreEntryData entry)
        {
            if (book != null)
            {
                book.Close();
            }

            Open(entry);
        }

        private void SetVisible(bool visible)
        {
            if (panel == null)
            {
                return;
            }

            panel.alpha = visible ? 1f : 0f;
            panel.interactable = visible;
            panel.blocksRaycasts = visible;
        }
    }
}
