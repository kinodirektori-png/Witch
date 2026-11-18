using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Witch.Data;

namespace Witch.Gameplay
{
    /// <summary>
    /// The runtime physical state of a kitchen item: what it currently holds (<see cref="KitchenItemData"/>), the form
    /// it is in (<see cref="PhysicalState"/>) and whether it is empty. A glass starts empty and is filled with lemon
    /// juice by squeezing; a lemon starts Solid and becomes Squeezed. Also keeps its placeholder image and label in sync.
    /// </summary>
    /// <remarks>
    /// Pure state: dragging lives in <see cref="DraggableItem"/>, and squeezing/pouring live in the interaction
    /// components that change this state. <see cref="IngredientItem.Recipe"/> reads <see cref="Substance"/> from here.
    /// </remarks>
    [DisallowMultipleComponent]
    public class ItemState : MonoBehaviour
    {
        [Tooltip("What this item holds when the scene starts. Leave empty for an empty container (e.g. a glass).")]
        [SerializeField] private KitchenItemData startContents;

        [Tooltip("Label shown while the item is empty, e.g. \"Empty glass\".")]
        [SerializeField] private string emptyName = "Empty glass";

        [Header("Visuals (optional)")]
        [Tooltip("Image showing the item. Defaults to an Image on this GameObject.")]
        [SerializeField] private Image image;

        [Tooltip("Shows the item's name, e.g. \"Lemon Juice\".")]
        [SerializeField] private TMP_Text nameLabel;

        [Tooltip("Shows the small caption, e.g. \"in the fruit bowl\" or the state (\"powder\").")]
        [SerializeField] private TMP_Text captionLabel;

        [Tooltip("Placeholder colour while empty.")]
        [SerializeField] private Color emptyColor = new Color(0.85f, 0.9f, 0.95f, 0.6f);

        [Header("Events")]
        public UnityEvent<ItemState> onChanged = new UnityEvent<ItemState>();

        /// <summary>Raised whenever the contents or state change.</summary>
        public event Action<ItemState> Changed;

        private Sprite defaultSprite;
        private Image.Type defaultImageType;
        private bool defaultPreserveAspect;
        private bool initialised;

        /// <summary>What the item holds now, or null if it is empty.</summary>
        public KitchenItemData Contents { get; private set; }

        /// <summary>The form the contents are in. Meaningless while <see cref="IsEmpty"/>.</summary>
        public PhysicalState State { get; private set; }

        public bool IsEmpty => Contents == null;

        public bool IsWater => Contents != null && Contents.isWater;

        public bool IsSqueezed => Contents != null && State == PhysicalState.Squeezed;

        /// <summary>True if the contents can be poured right now (a powder or a liquid).</summary>
        public bool CanPour => Contents != null && PhysicalStates.CanPour(State);

        /// <summary>
        /// The chemical the item can give to a recipe right now: null while empty, after squeezing (the juice went
        /// elsewhere) or for water (water dilutes, it isn't an ingredient).
        /// </summary>
        public RecipeData Substance => Contents == null || Contents.isWater || IsSqueezed ? null : Contents.substance;

        /// <summary>Name to show, e.g. "Lemon", "Lemon Juice" or "Empty glass".</summary>
        public string DisplayName
        {
            get
            {
                if (Contents == null)
                {
                    return emptyName;
                }

                return IsSqueezed ? $"Squeezed {Contents.displayName}" : Contents.displayName;
            }
        }

        /// <summary>The start contents, for setup code and the scene builder.</summary>
        public KitchenItemData StartContents => startContents;

        private void Awake()
        {
            Initialise();
        }

        private void Initialise()
        {
            if (initialised)
            {
                return;
            }

            initialised = true;
            if (image == null)
            {
                image = GetComponent<Image>();
            }

            if (image != null)
            {
                defaultSprite = image.sprite;
                defaultImageType = image.type;
                defaultPreserveAspect = image.preserveAspect;
            }

            SetContents(startContents, startContents != null ? startContents.startState : PhysicalState.Solid, notify: false);
        }

        /// <summary>Fills the item with <paramref name="contents"/> in its start state (e.g. a glass filled with juice).</summary>
        public void Fill(KitchenItemData contents)
        {
            Initialise();
            SetContents(contents, contents != null ? contents.startState : PhysicalState.Solid, notify: true);
        }

        /// <summary>Empties the item (e.g. after its juice was poured into the pot).</summary>
        public void Empty()
        {
            Initialise();
            if (!IsEmpty)
            {
                SetContents(null, PhysicalState.Solid, notify: true);
            }
        }

        /// <summary>Marks the item as squeezed (its juice is gone). Returns false if it can't be squeezed.</summary>
        public bool MarkSqueezed()
        {
            Initialise();
            if (Contents == null || !Contents.CanBeSqueezed || IsSqueezed)
            {
                return false;
            }

            SetContents(Contents, PhysicalState.Squeezed, notify: true);
            return true;
        }

        /// <summary>Changes the form of the current contents (e.g. a future grinder turning salt crystals into powder).</summary>
        public void SetState(PhysicalState newState)
        {
            Initialise();
            if (Contents != null && newState != State)
            {
                SetContents(Contents, newState, notify: true);
            }
        }

        /// <summary>
        /// Shows a short note in the caption (e.g. "Gather more lemons in the garden!") until the item next changes.
        /// </summary>
        public void SetNote(string note)
        {
            if (captionLabel != null && !string.IsNullOrEmpty(note))
            {
                captionLabel.text = note;
            }
        }

        /// <summary>Puts the item back the way the scene started (e.g. a fresh lemon in the bowl).</summary>
        public void ResetToStart()
        {
            Initialise();
            SetContents(startContents, startContents != null ? startContents.startState : PhysicalState.Solid, notify: true);
        }

        private void SetContents(KitchenItemData contents, PhysicalState state, bool notify)
        {
            Contents = contents;
            State = state;
            RefreshVisuals();

            if (notify)
            {
                Changed?.Invoke(this);
                onChanged.Invoke(this);
            }
        }

        private void RefreshVisuals()
        {
            if (nameLabel != null)
            {
                nameLabel.text = DisplayName;
            }

            if (captionLabel != null)
            {
                captionLabel.text = GetCaption();
            }

            if (image == null)
            {
                return;
            }

            if (Contents == null)
            {
                ShowSprite(null, emptyColor);
                return;
            }

            if (IsSqueezed)
            {
                if (Contents.squeezedSprite != null)
                {
                    ShowSprite(Contents.squeezedSprite, Color.white);
                }
                else
                {
                    // No squeezed art yet: darken the fresh art (or the placeholder card) instead.
                    Color dull = Color.Lerp(Contents.placeholderColor, new Color(0.45f, 0.4f, 0.3f), 0.55f);
                    ShowSprite(Contents.shelfSprite, Contents.shelfSprite != null ? new Color(0.7f, 0.65f, 0.55f) : dull);
                }

                return;
            }

            ShowSprite(Contents.shelfSprite, Contents.shelfSprite != null ? Color.white : Contents.placeholderColor);
        }

        /// <summary>
        /// Shows ingredient art as-is (simple, keeping its proportions), or the placeholder card when
        /// <paramref name="art"/> is null.
        /// </summary>
        private void ShowSprite(Sprite art, Color color)
        {
            image.color = color;
            if (art == null)
            {
                image.sprite = defaultSprite;
                image.type = defaultImageType;
                image.preserveAspect = defaultPreserveAspect;
                return;
            }

            image.sprite = art;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
        }

        private string GetCaption()
        {
            if (Contents == null)
            {
                return string.Empty;
            }

            if (IsSqueezed)
            {
                return "all juice squeezed out";
            }

            if (Contents.substance != null && !Contents.isWater)
            {
                // e.g. "Citric Acid · liquid": the real chemical plus the form it is in.
                return $"{Contents.substance.commonName} · {PhysicalStates.KidName(State)}";
            }

            return Contents.placeDescription;
        }

        private void OnValidate()
        {
            if (image == null)
            {
                image = GetComponent<Image>();
            }
        }
    }
}
