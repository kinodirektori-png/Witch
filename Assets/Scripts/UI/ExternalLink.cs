using UnityEngine;

namespace Witch.UI
{
    /// <summary>
    /// The only place the game may open something outside itself. Every link goes through a
    /// <see cref="ParentalGate"/> first; without a gate the link does nothing (and warns), never opens directly.
    /// Hook a Button's OnClick to <see cref="Open"/>.
    /// </summary>
    /// <remarks>
    /// A kid-safety test fails if Application.OpenURL is called anywhere else, so new links must use this component.
    /// </remarks>
    public class ExternalLink : MonoBehaviour
    {
        [Tooltip("https address to open, e.g. the privacy policy page.")]
        [SerializeField] private string url;

        [SerializeField] private ParentalGate parentalGate;

        public string Url => url;

        public void Open()
        {
            if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("https://"))
            {
                Debug.LogWarning($"{nameof(ExternalLink)} on '{name}' needs an https:// address.", this);
                return;
            }

            if (parentalGate == null)
            {
                Debug.LogWarning($"{nameof(ExternalLink)} on '{name}' has no {nameof(ParentalGate)}, so it won't open.", this);
                return;
            }

            string target = url;
            parentalGate.Request(() => Application.OpenURL(target));
        }
    }
}
