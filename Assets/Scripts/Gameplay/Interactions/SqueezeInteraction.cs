using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using Witch.Data;

namespace Witch.Gameplay
{
    /// <summary>
    /// Tap a fruit to squeeze it. After a few taps (with a squash animation) the fruit is spent and its juice fills the
    /// target container, e.g. a lemon fills the glass next to it with Lemon Juice (citric acid, liquid).
    /// </summary>
    /// <remarks>
    /// Works next to <see cref="DraggableItem"/> on the same object: Unity only sends a click when the finger didn't
    /// drag, so tapping squeezes and dragging still moves the lemon. The fruit restocks (a fresh lemon from the bowl)
    /// once the target container is empty again.
    /// </remarks>
    [RequireComponent(typeof(ItemState))]
    public class SqueezeInteraction : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("Container that receives the juice (e.g. the glass or dropper on the counter).")]
        [SerializeField] private ItemState juiceTarget;

        [Tooltip("Put a fresh fruit back once the juice target has been emptied.")]
        [SerializeField] private bool restockWhenTargetEmptied = true;

        [Header("Squash animation")]
        [SerializeField] private Vector2 squashScale = new Vector2(1.15f, 0.8f);
        [SerializeField, Min(0.02f)] private float squashDuration = 0.15f;

        [Header("Events")]
        [Tooltip("0..1 after each tap. Hook squeeze sounds here (#23).")]
        public UnityEvent<float> onSqueezeProgress = new UnityEvent<float>();

        [Tooltip("Raised when the fruit is fully squeezed and the juice target filled.")]
        public UnityEvent onSqueezed = new UnityEvent();

        [Tooltip("Raised when squeezing isn't possible right now, e.g. the glass is already full.")]
        public UnityEvent<string> onSqueezeBlocked = new UnityEvent<string>();

        private ItemState itemState;
        private int taps;
        private Vector3 restScale;
        private Coroutine squashRoutine;

        public ItemState JuiceTarget
        {
            get => juiceTarget;
            set
            {
                UnsubscribeTarget();
                juiceTarget = value;
                if (isActiveAndEnabled)
                {
                    SubscribeTarget();
                }
            }
        }

        /// <summary>0..1 how far the current fruit has been squeezed.</summary>
        public float Progress
        {
            get
            {
                KitchenItemData contents = itemState != null ? itemState.Contents : null;
                int needed = contents != null ? Mathf.Max(1, contents.squeezeTaps) : 1;
                return Mathf.Clamp01((float)taps / needed);
            }
        }

        private void Awake()
        {
            itemState = GetComponent<ItemState>();
            restScale = transform.localScale;
        }

        private void OnEnable()
        {
            SubscribeTarget();
        }

        private void OnDisable()
        {
            UnsubscribeTarget();

            if (squashRoutine != null)
            {
                StopCoroutine(squashRoutine);
                squashRoutine = null;
                transform.localScale = restScale;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Drags never reach here (Unity drops the click once a drag starts), so this is a real tap.
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                Squeeze();
            }
        }

        /// <summary>One squeeze. Public so a tap-to-select alternative (#21) or tests can call it.</summary>
        public void Squeeze()
        {
            // A spent fruit with an empty glass: tapping the bowl takes a fresh one from the stock (e.g. after gathering).
            if (itemState.IsSqueezed && juiceTarget != null && juiceTarget.IsEmpty)
            {
                TryRestock();
                return;
            }

            KitchenItemData contents = itemState.Contents;
            if (contents == null || !contents.CanBeSqueezed || itemState.IsSqueezed)
            {
                onSqueezeBlocked.Invoke("There's no juice left in this one.");
                return;
            }

            if (juiceTarget == null)
            {
                Debug.LogWarning($"{nameof(SqueezeInteraction)} on '{name}' has no Juice Target to squeeze into.", this);
                return;
            }

            if (!juiceTarget.IsEmpty)
            {
                onSqueezeBlocked.Invoke($"The {juiceTarget.DisplayName.ToLowerInvariant()} is already full. Pour it first!");
                return;
            }

            PlaySquash();
            taps++;
            onSqueezeProgress.Invoke(Progress);

            if (taps < Mathf.Max(1, contents.squeezeTaps))
            {
                return;
            }

            taps = 0;
            juiceTarget.Fill(contents.squeezeYield);
            itemState.MarkSqueezed();
            onSqueezed.Invoke();
        }

        private void SubscribeTarget()
        {
            if (juiceTarget != null)
            {
                juiceTarget.Changed += HandleTargetChanged;
            }
        }

        private void UnsubscribeTarget()
        {
            if (juiceTarget != null)
            {
                juiceTarget.Changed -= HandleTargetChanged;
            }
        }

        private void HandleTargetChanged(ItemState target)
        {
            if (restockWhenTargetEmptied && target.IsEmpty && itemState.IsSqueezed)
            {
                TryRestock();
            }
        }

        /// <summary>
        /// Puts a fresh fruit in the bowl, taking one from the <see cref="StockedItem"/> if there is one. Returns false
        /// (and shows a note) when the stock is empty. Public so harvesting can refill an empty bowl straight away.
        /// </summary>
        public bool TryRestock()
        {
            if (!itemState.IsSqueezed)
            {
                return true;
            }

            var stocked = GetComponent<StockedItem>();
            if (stocked != null && !stocked.TryUse())
            {
                itemState.SetNote(stocked.EmptyHint);
                onSqueezeBlocked.Invoke(stocked.EmptyHint);
                return false;
            }

            taps = 0;
            itemState.ResetToStart();
            return true;
        }

        private void PlaySquash()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (squashRoutine != null)
            {
                StopCoroutine(squashRoutine);
                transform.localScale = restScale;
            }

            squashRoutine = StartCoroutine(SquashRoutine());
        }

        private IEnumerator SquashRoutine()
        {
            Vector3 squashed = new Vector3(restScale.x * squashScale.x, restScale.y * squashScale.y, restScale.z);
            float elapsed = 0f;

            while (elapsed < squashDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Sin(Mathf.Clamp01(elapsed / squashDuration) * Mathf.PI);
                transform.localScale = Vector3.LerpUnclamped(restScale, squashed, t);
                yield return null;
            }

            transform.localScale = restScale;
            squashRoutine = null;
        }
    }
}
