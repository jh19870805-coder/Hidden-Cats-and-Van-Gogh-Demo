using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// RoomWnd: while the closed Window overlay is active, the Cafe button stays disabled.
/// When the window is hidden (e.g. after finding the window cat), the button becomes interactable.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class RoomWindowGate : MonoBehaviour
{
    public static RoomWindowGate Instance { get; private set; }

    [SerializeField] private GameObject windowOverlay;
    [SerializeField] private Button cafeButton;

    /// <summary>
    /// Runtime paths after GameSceneUI may move RoomBg under __ContentRoot; resolve by name when Inspector refs break.
    /// </summary>
    public static GameObject FindWindowOverlay(Transform roomWndRoot)
    {
        if (roomWndRoot == null)
        {
            return null;
        }

        Transform t = roomWndRoot.Find("__ContentRoot/RoomBg/Window");
        if (t == null)
        {
            t = roomWndRoot.Find("RoomBg/Window");
        }

        return t != null ? t.gameObject : null;
    }

    public static Button FindCafeButton(Transform roomWndRoot)
    {
        if (roomWndRoot == null)
        {
            return null;
        }

        Transform t = roomWndRoot.Find("__ContentRoot/RoomBg/CafeBtn");
        if (t == null)
        {
            t = roomWndRoot.Find("RoomBg/CafeBtn");
        }

        return t != null ? t.GetComponent<Button>() : null;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        RefreshCafeButton();
    }

    private void EnsureSerializedRefs()
    {
        Transform root = transform;
        if (windowOverlay == null || !windowOverlay.transform.IsChildOf(root))
        {
            windowOverlay = FindWindowOverlay(root);
        }

        if (cafeButton == null || !cafeButton.transform.IsChildOf(root))
        {
            cafeButton = FindCafeButton(root);
        }
    }

    /// <summary>
    /// Call after the window overlay is shown or hidden (e.g. from NormalCatInteractable).
    /// </summary>
    public void RefreshCafeButton()
    {
        EnsureSerializedRefs();

        if (cafeButton == null)
        {
            return;
        }

        if (windowOverlay == null)
        {
            cafeButton.interactable = true;
            return;
        }

        cafeButton.interactable = !windowOverlay.activeSelf;
    }

    public static void RefreshIfInstanceExists()
    {
        Instance?.RefreshCafeButton();
    }
}
