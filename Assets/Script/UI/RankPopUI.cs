using System;
using System.Globalization;
using HiddenCats.Core;
using HiddenCats.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Rank popup: binds <see cref="SpeedrunService"/> records to row UI.
/// Rows are parented under a dedicated <see cref="RankRowsContainerName"/> under ScrollRect content,
/// laid out with <see cref="VerticalLayoutGroup"/> so layout no longer fights manual positions.
/// Row visuals: optional per-place prefabs, else inactive prototypes RankBar01–04 on content (rank N uses RankBar0N;
/// 4th+ uses RankBar04), else <see cref="rankBarPrefab"/>.
/// </summary>
public sealed class RankPopUI : MonoBehaviour
{
    private const string RankRowsContainerName = "RankRows";

    private static readonly string[] RankBarPrototypeNames =
    {
        "RankBar01", "RankBar02", "RankBar03", "RankBar04",
    };

    [Header("Ranking List")]
    [Tooltip("Scroll View root or its Content; Content is resolved via ScrollRect when needed.")]
    [SerializeField] private Transform contentRoot;

    [Tooltip("Fallback when no prototype / override prefab is set for a place.")]
    [SerializeField] private GameObject rankBarPrefab;

    [Tooltip("If set, used instead of scene prototype RankBar01 for 1st place.")]
    [SerializeField] private GameObject rankRowPrefabFirst;

    [Tooltip("If set, used instead of scene prototype RankBar02 for 2nd place.")]
    [SerializeField] private GameObject rankRowPrefabSecond;

    [Tooltip("If set, used instead of scene prototype RankBar03 for 3rd place.")]
    [SerializeField] private GameObject rankRowPrefabThird;

    [Tooltip("If set, used instead of scene prototype RankBar04 for 4th+.")]
    [SerializeField] private GameObject rankRowPrefabFourthPlus;

    [Header("List layout")]
    [SerializeField] private float rankRowSpacing;
    [SerializeField] private int rankListPaddingTop = 80;
    [SerializeField] private int rankListPaddingBottom = 100;
    [Tooltip("Row height when template RectTransform has no reliable height (e.g. stretch + sizeDelta 0 while inactive).")]
    [SerializeField] private float minRankRowHeight = 144f;

    [Header("Empty state")]
    [SerializeField] private GameObject emptyStateRoot;

    [Header("Rank number (Num01 TMP)")]
    [Tooltip("If off, only the digit text is set; TMP color/material/outline stay as in the row prefab (matches RankPop design).")]
    [SerializeField] private bool applyScriptedRankNumberStyle;

    [SerializeField] private Color32 rankNumColor1 = ParseHexColor32("FFE03F");
    [SerializeField] private Color32 rankNumColor2 = ParseHexColor32("E2E2E2");
    [SerializeField] private Color32 rankNumColor3 = ParseHexColor32("FFBD4D");
    [SerializeField] private Color32 rankNumColor4Plus = ParseHexColor32("B2641A");
    [SerializeField] private Color32 rankNumOutlineColor = ParseHexColor32("B2641A");
    [SerializeField] private float rankNumOutlineWidth = 4f;

    private RectTransform _rankRowsRoot;

    private void OnEnable()
    {
        if (SpeedrunService.Instance != null)
        {
            SpeedrunService.Instance.OnRunCompleted += HandleRunCompleted;
        }

        RefreshList();
    }

    private void OnDisable()
    {
        if (SpeedrunService.Instance != null)
        {
            SpeedrunService.Instance.OnRunCompleted -= HandleRunCompleted;
        }
    }

    public void OnClick_Close()
    {
        if (WindowManager.Instance == null)
        {
            Debug.LogError("[RankPopUI] WindowManager.Instance is null.");
            return;
        }

        WindowManager.Instance.HideCurrentPopup();
    }

    private void HandleRunCompleted(SpeedrunRecord _)
    {
        RefreshList();
    }

    private void RefreshList()
    {
        EnsureEmptyStateRootReference();

        var records = SpeedrunService.Instance != null ? SpeedrunService.Instance.Records : null;
        bool hasRecords = records != null && records.Count > 0;
        ApplyEmptyStateVisibility(hasRecords);

        if (contentRoot == null)
        {
            Debug.LogWarning("[RankPopUI] contentRoot is not assigned.");
            return;
        }

        Transform scrollContentTransform = contentRoot;
        ScrollRect scrollRect = contentRoot.GetComponent<ScrollRect>()
                                ?? contentRoot.GetComponentInParent<ScrollRect>();
        if (scrollRect != null && scrollRect.content != null)
        {
            scrollContentTransform = scrollRect.content;
            scrollRect.vertical = true;
            scrollRect.horizontal = false;
        }

        var scrollContent = scrollContentTransform as RectTransform;
        if (scrollContent == null)
        {
            Debug.LogWarning("[RankPopUI] Scroll content must be a RectTransform.");
            return;
        }

        DisableScrollContentAutoLayout(scrollContent);

        _rankRowsRoot = EnsureRankRowsRoot(scrollContent);
        ClearChildren(_rankRowsRoot);

        SetPrototypesInactive(scrollContent);

        if (rankBarPrefab == null && !HasRowPrototype(scrollContent, "RankBar01"))
        {
            Debug.LogWarning("[RankPopUI] Assign rankBarPrefab and/or RankBar01–04 prototypes under scroll content.");
            return;
        }

        if (SpeedrunService.Instance == null || !hasRecords)
        {
            ResetScrollContentHeightForEmptyList(scrollRect);
            RebuildListLayout(scrollContent, scrollRect);
            return;
        }

        int latestIndex = SpeedrunService.Instance.LatestRecordIndex;

        for (int i = 0; i < records.Count; i++)
        {
            var record = records[i];
            int rankOneBased = i + 1;

            RectTransform rowTemplate = GetRowPrototypeForPlace(scrollContent, rankOneBased);
            GameObject source = ResolveRowPrefab(rankOneBased, scrollContent, rowTemplate);
            if (source == null)
            {
                Debug.LogWarning($"[RankPopUI] No row source for rank {rankOneBased}.");
                continue;
            }

            GameObject rowGo = Instantiate(source, _rankRowsRoot, false);
            rowGo.name = $"RankRow_{rankOneBased}";
            rowGo.SetActive(true);

            var rowRt = rowGo.transform as RectTransform;
            if (rowRt != null)
            {
                RectTransform heightRef = rowTemplate != null && ReferenceEquals(source, rowTemplate.gameObject)
                    ? rowTemplate
                    : null;
                float rowH = ComputeRowHeight(heightRef, rowRt);
                ApplyRowStackLayout(rowRt, heightRef, rowH);
            }

            BindRowData(rowGo.transform, record, rankOneBased, i == latestIndex);
        }

        RebuildListLayout(scrollContent, scrollRect);
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform c = parent.GetChild(i);
            if (c != null)
            {
                Destroy(c.gameObject);
            }
        }
    }

    private RectTransform EnsureRankRowsRoot(RectTransform scrollContent)
    {
        if (_rankRowsRoot != null && _rankRowsRoot.parent == scrollContent)
        {
            return _rankRowsRoot;
        }

        Transform found = scrollContent.Find(RankRowsContainerName);
        RectTransform rt = found != null ? found as RectTransform : null;
        if (rt == null)
        {
            var go = new GameObject(RankRowsContainerName, typeof(RectTransform));
            rt = go.GetComponent<RectTransform>();
            rt.SetParent(scrollContent, false);
        }

        rt.SetAsLastSibling();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.localScale = Vector3.one;

        var vlg = rt.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
        {
            vlg = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = rankRowSpacing;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(0, 0, rankListPaddingTop, rankListPaddingBottom);

        var csf = rt.GetComponent<ContentSizeFitter>();
        if (csf == null)
        {
            csf = rt.gameObject.AddComponent<ContentSizeFitter>();
        }

        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _rankRowsRoot = rt;
        return rt;
    }

    private void RebuildListLayout(RectTransform scrollContent, ScrollRect scrollRect)
    {
        Canvas.ForceUpdateCanvases();
        if (_rankRowsRoot != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rankRowsRoot);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent);

        if (scrollRect == null || scrollRect.content == null || scrollRect.viewport == null)
        {
            return;
        }

        float viewportH = Mathf.Max(0f, scrollRect.viewport.rect.height);
        float inner = 0f;
        if (_rankRowsRoot != null)
        {
            inner = LayoutUtility.GetPreferredHeight(_rankRowsRoot);
            if (inner <= 0.01f)
            {
                inner = _rankRowsRoot.rect.height;
            }
        }

        float needed = Mathf.Max(viewportH, inner);
        Vector2 sd = scrollRect.content.sizeDelta;
        scrollRect.content.sizeDelta = new Vector2(sd.x, needed);
    }

    private GameObject ResolveRowPrefab(int rankOneBased, RectTransform scrollContent, RectTransform rowTemplate)
    {
        GameObject ovr = rankOneBased switch
        {
            1 => rankRowPrefabFirst,
            2 => rankRowPrefabSecond,
            3 => rankRowPrefabThird,
            _ => rankRowPrefabFourthPlus,
        };
        if (ovr != null)
        {
            return ovr;
        }

        if (rowTemplate != null)
        {
            return rowTemplate.gameObject;
        }

        return rankBarPrefab;
    }

    private static RectTransform GetRowPrototypeForPlace(RectTransform scrollContent, int rankOneBased)
    {
        string primary = rankOneBased switch
        {
            1 => "RankBar01",
            2 => "RankBar02",
            3 => "RankBar03",
            _ => "RankBar04",
        };

        RectTransform t = GetRowPrototypeDirectChild(scrollContent, primary);
        if (t != null)
        {
            return t;
        }

        return GetRowPrototypeDirectChild(scrollContent, "RankBar04")
            ?? GetRowPrototypeDirectChild(scrollContent, "RankBar03")
            ?? GetRowPrototypeDirectChild(scrollContent, "RankBar02")
            ?? GetRowPrototypeDirectChild(scrollContent, "RankBar01");
    }

    private static bool HasRowPrototype(RectTransform scrollContent, string name)
    {
        return GetRowPrototypeDirectChild(scrollContent, name) != null;
    }

    private float ComputeRowHeight(RectTransform rowTemplate, RectTransform instantiatedRow)
    {
        float h = 0f;
        if (rowTemplate != null)
        {
            h = rowTemplate.rect.height;
        }

        if (h <= 1f && instantiatedRow != null)
        {
            h = instantiatedRow.rect.height;
        }

        if (h <= 1f && rankBarPrefab != null && rankBarPrefab.transform is RectTransform pr)
        {
            h = pr.rect.height;
        }

        return Mathf.Max(minRankRowHeight, h);
    }

    private static void ApplyRowStackLayout(RectTransform rowRt, RectTransform templateForScale, float rowHeight)
    {
        if (templateForScale != null)
        {
            rowRt.localScale = templateForScale.localScale;
        }

        rowRt.anchorMin = new Vector2(0f, 1f);
        rowRt.anchorMax = new Vector2(1f, 1f);
        rowRt.pivot = new Vector2(0.5f, 1f);
        rowRt.anchoredPosition = Vector2.zero;

        rowRt.sizeDelta = new Vector2(0f, rowHeight);

        LayoutElement le = rowRt.GetComponent<LayoutElement>();
        if (le == null)
        {
            le = rowRt.gameObject.AddComponent<LayoutElement>();
        }

        le.minHeight = rowHeight;
        le.preferredHeight = rowHeight;
    }

    private void BindRowData(Transform rowRoot, SpeedrunRecord record, int rankOneBased, bool isLatest)
    {
        TMP_Text numText = FindChildTMP(rowRoot, "Num01(TMP)");
        if (numText != null)
        {
            numText.text = rankOneBased.ToString();
            if (applyScriptedRankNumberStyle)
            {
                ApplyRankNumStyle(numText, rankOneBased);
            }
        }

        TMP_Text dateText = FindChildTMP(rowRoot, "DateText (TMP)");
        if (dateText != null)
        {
            dateText.text = FormatDateOnly(record.completedAtLocal);
        }

        TMP_Text timeText = FindChildTMP(rowRoot, "TimeText (TMP)");
        if (timeText != null)
        {
            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(record.timeSeconds));
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            timeText.text = $"{minutes:00} : {seconds:00}";
        }

        Transform newFlag = FindChildTransform(rowRoot, "NewFlag");
        SetLatestRowDecorActive(newFlag, isLatest);

        Transform highlight = FindChildTransform(rowRoot, "HighlightBg");
        SetLatestRowDecorActive(highlight, isLatest);
    }

    /// <summary>
    /// Avoid enabling Image-based decor with no sprite (Unity shows a magenta/red placeholder).
    /// </summary>
    private static void SetLatestRowDecorActive(Transform t, bool wantActive)
    {
        if (t == null)
        {
            return;
        }

        bool show = wantActive;
        if (show)
        {
            Image img = t.GetComponent<Image>();
            if (img != null && img.sprite == null)
            {
                show = false;
            }
        }

        t.gameObject.SetActive(show);
    }

    private void EnsureEmptyStateRootReference()
    {
        if (emptyStateRoot != null)
        {
            return;
        }

        Transform t = FindChildTransform(transform, "Empty");
        if (t != null)
        {
            emptyStateRoot = t.gameObject;
        }
    }

    private void ApplyEmptyStateVisibility(bool hasRecords)
    {
        if (emptyStateRoot == null)
        {
            return;
        }

        emptyStateRoot.SetActive(!hasRecords);
    }

    private static void ResetScrollContentHeightForEmptyList(ScrollRect scrollRect)
    {
        if (scrollRect == null || scrollRect.content == null || scrollRect.viewport == null)
        {
            return;
        }

        RectTransform contentRt = scrollRect.content;
        float minHeight = Mathf.Max(0f, scrollRect.viewport.rect.height);
        Vector2 sd = contentRt.sizeDelta;
        contentRt.sizeDelta = new Vector2(sd.x, minHeight);
    }

    private static TMP_Text FindChildTMP(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in children)
        {
            if (t != null && t.name == childName)
            {
                return t.GetComponent<TMP_Text>();
            }
        }

        return null;
    }

    private void ApplyRankNumStyle(TMP_Text numText, int rankOneBased)
    {
        if (numText == null)
        {
            return;
        }

        Color32 fill = rankOneBased switch
        {
            <= 1 => rankNumColor1,
            2 => rankNumColor2,
            3 => rankNumColor3,
            _ => rankNumColor4Plus,
        };

        numText.color = fill;
        Color32 outline = rankNumOutlineColor;
        outline.a = 255;
        numText.outlineColor = outline;
        numText.outlineWidth = Mathf.Max(0f, rankNumOutlineWidth);
    }

    private static Color32 ParseHexColor32(string hex)
    {
        if (string.IsNullOrEmpty(hex))
        {
            return Color.white;
        }

        string s = hex.StartsWith("#", StringComparison.Ordinal) ? hex : "#" + hex;
        if (ColorUtility.TryParseHtmlString(s, out Color c))
        {
            var c32 = (Color32)c;
            c32.a = 255;
            return c32;
        }

        return Color.white;
    }

    private static Transform FindChildTransform(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in children)
        {
            if (t != null && t.name == childName)
            {
                return t;
            }
        }

        string targetNorm = NormalizeUiName(childName);
        if (!string.IsNullOrEmpty(targetNorm))
        {
            foreach (Transform t in children)
            {
                if (t == null)
                {
                    continue;
                }

                if (NormalizeUiName(t.name) == targetNorm)
                {
                    return t;
                }
            }
        }

        string targetLower = childName.ToLowerInvariant();
        foreach (Transform t in children)
        {
            if (t == null || string.IsNullOrEmpty(t.name))
            {
                continue;
            }

            string n = t.name.ToLowerInvariant();
            if (n.Contains("(clone)"))
            {
                continue;
            }

            if (n.Contains(targetLower))
            {
                return t;
            }
        }

        return null;
    }

    private static string NormalizeUiName(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.Empty;
        }

        return s.Replace(" ", string.Empty)
            .Replace("\t", string.Empty)
            .Replace("\n", string.Empty)
            .Replace("\r", string.Empty)
            .ToLowerInvariant();
    }

    private static void DisableScrollContentAutoLayout(RectTransform contentRt)
    {
        if (contentRt == null)
        {
            return;
        }

        VerticalLayoutGroup vlg = contentRt.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.enabled = false;
        }

        HorizontalLayoutGroup hlg = contentRt.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
        {
            hlg.enabled = false;
        }

        GridLayoutGroup grid = contentRt.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.enabled = false;
        }

        ContentSizeFitter csf = contentRt.GetComponent<ContentSizeFitter>();
        if (csf != null)
        {
            csf.enabled = false;
        }
    }

    private static RectTransform GetRowPrototypeDirectChild(Transform listRoot, string exactName)
    {
        if (listRoot == null || string.IsNullOrEmpty(exactName))
        {
            return null;
        }

        for (int i = 0; i < listRoot.childCount; i++)
        {
            Transform c = listRoot.GetChild(i);
            if (c != null && c.name == exactName)
            {
                return c as RectTransform;
            }
        }

        return null;
    }

    private static void SetPrototypesInactive(Transform listRoot)
    {
        if (listRoot == null)
        {
            return;
        }

        for (int i = 0; i < listRoot.childCount; i++)
        {
            Transform c = listRoot.GetChild(i);
            if (c != null && c.name == RankRowsContainerName)
            {
                continue;
            }

            if (c != null && IsRankBarPrototypeName(c.name))
            {
                c.gameObject.SetActive(false);
            }
        }
    }

    private static bool IsRankBarPrototypeName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return false;
        }

        for (int i = 0; i < RankBarPrototypeNames.Length; i++)
        {
            if (objectName == RankBarPrototypeNames[i])
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatDateOnly(string completedAtLocal)
    {
        if (string.IsNullOrEmpty(completedAtLocal))
        {
            return string.Empty;
        }

        if (DateTime.TryParseExact(
                completedAtLocal,
                "yyyy-MM-dd HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime dt))
        {
            return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        int space = completedAtLocal.IndexOf(' ');
        if (space > 0)
        {
            return completedAtLocal.Substring(0, space);
        }

        return completedAtLocal;
    }
}
