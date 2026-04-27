using UnityEngine;

namespace HiddenCats.UI
{
    /// <summary>
    /// Marker component used by <see cref="HintBubbleService"/> to identify spawned bubble instances.
    /// This prevents the service from accidentally destroying unrelated UI children when clearing.
    /// </summary>
    public sealed class HintBubbleTag : MonoBehaviour
    {
    }
}

