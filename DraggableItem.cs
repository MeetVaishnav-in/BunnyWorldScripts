using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Draggable UI item that:
/// - creates a placeholder in the HorizontalLayoutGroup while dragging
/// - preserves pointer-to-item offset so it doesn't jump on reparent
/// - drops into PlayArea (keeps pointer alignment) or back into the layout (inserts at placeholder)
/// - reduces placeholder jitter by requiring a small buffer/hysteresis
/// - resolves scroll-drag conflict by detecting drag intent
/// 
/// Usage:
/// - assign rootCanvas (or let it auto-find),
/// - assign layoutParent (the Content RectTransform with HorizontalLayoutGroup),
/// - assign playArea (RectTransform where items can be dropped freely).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
{
    [Header("References (assign in inspector)")]
    public Canvas rootCanvas;
    public RectTransform layoutParent;
    public RectTransform playArea;

    [Header("Tweakables")]
    [Tooltip("How many screen pixels beyond layoutParent count as 'near' (to reinsert)")]
    public float layoutProximity = 80f;

    [Tooltip("Pixels of hysteresis around child midpoints to avoid jitter")]
    public float placeholderBuffer = 18f;

    [Tooltip("Time (seconds) to hold pointer before starting drag")]
    public float dragStartDelay = 0.2f;

    [Tooltip("Minimum distance (pixels) to move before starting drag")]
    public float dragStartDistance = 10f;

    RectTransform rect;
    CanvasGroup cg;
    Transform originalParent;
    int originalSiblingIndex;
    GameObject placeholder;
    ScrollRect parentScrollRect;

    // world-space offset between the item's position and the pointer world position at drag start
    Vector3 dragOffset;

    // cached canvas rect transform for ScreenPoint -> World conversions
    RectTransform canvasRect;

    // Variables for drag detection
    private bool isDraggingItem;
    private Vector2 pointerStartPosition;
    private float pointerDownTime;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();

        if (rootCanvas != null)
            canvasRect = rootCanvas.transform as RectTransform;

        // Try to find scrollrect if content is under one
        if (layoutParent != null)
            parentScrollRect = layoutParent.GetComponentInParent<ScrollRect>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Record the initial pointer position and time
        pointerStartPosition = eventData.position;
        pointerDownTime = Time.unscaledTime;
        isDraggingItem = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Check if drag conditions are met (time and distance)
        float timeSincePointerDown = Time.unscaledTime - pointerDownTime;
        float distanceMoved = Vector2.Distance(eventData.position, pointerStartPosition);

        if (timeSincePointerDown < dragStartDelay || distanceMoved < dragStartDistance)
        {
            // Not enough time or distance to start dragging the item; allow scrolling
            isDraggingItem = false;
            return;
        }

        isDraggingItem = true;

        originalParent = rect.parent;
        originalSiblingIndex = rect.GetSiblingIndex();

        Vector3 pointerWorld;
        if (ScreenPointToWorld(eventData.position, out pointerWorld))
            dragOffset = rect.position - pointerWorld;
        else
            dragOffset = Vector3.zero;

        if (originalParent == layoutParent)
        {
            CreatePlaceholder();
        }

        rect.SetParent(rootCanvas.transform, true);
        rect.SetAsLastSibling();

        cg.blocksRaycasts = false;

        if (parentScrollRect != null)
            parentScrollRect.enabled = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDraggingItem)
            return;

        Vector3 pointerWorld;
        if (ScreenPointToWorld(eventData.position, out pointerWorld))
        {
            rect.position = pointerWorld + dragOffset;
        }

        // If pointer is over or near the layout content:
        if (layoutParent != null && IsPointerOverRect(layoutParent, eventData, layoutProximity))
        {
            // If we don’t have a placeholder yet (e.g. item came from PlayArea), create one now
            if (placeholder == null)
            {
                originalParent = layoutParent;  // treat layout as the “origin” now
                originalSiblingIndex = layoutParent.childCount; // default to end
                CreatePlaceholder();
            }

            UpdatePlaceholderSibling(eventData);
        }
        else
        {
            // If we leave the layout while dragging, optionally remove placeholder
            if (placeholder != null)
            {
                CleanupPlaceholder();
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDraggingItem)
            return;

        cg.blocksRaycasts = true;
        if (parentScrollRect != null)
            parentScrollRect.enabled = true;

        Vector3 pointerWorld;
        ScreenPointToWorld(eventData.position, out pointerWorld); // pointerWorld may be used below

        // 1) If inside play area -> keep world-position alignment and reparent to playArea
        if (IsPointerOverRect(playArea, eventData, 0f))
        {
            // Set the world position first (using stored offset to keep grab point consistent)
            if (pointerWorld != Vector3.zero)
                rect.position = pointerWorld + dragOffset;

            // Reparent while preserving world position so there is no visible jump.
            rect.SetParent(playArea, true);
            rect.localScale = Vector3.one; // ensure scale sanity
            CleanupPlaceholder();
            return;
        }

        // 2) If inside or near layout parent -> insert into layout at placeholder index
        if (layoutParent != null && IsPointerOverRect(layoutParent, eventData, layoutProximity) && placeholder != null)
        {
            // Reparent to layout content. Use worldPositionStays = false so the layout can control positioning.
            rect.SetParent(layoutParent, false);
            rect.localScale = Vector3.one;

            int idx = placeholder.transform.GetSiblingIndex();
            // clamp to valid range (safety)
            idx = Mathf.Clamp(idx, 0, Mathf.Max(0, layoutParent.childCount - 1));
            rect.SetSiblingIndex(idx);

            // force immediate rebuild so layout takes effect right away (reduces flicker)
            LayoutRebuilder.ForceRebuildLayoutImmediate(layoutParent);

            CleanupPlaceholder();
            return;
        }

        // 3) Else: snap back to original parent slot
        rect.SetParent(originalParent, false);
        rect.localScale = Vector3.one;
        rect.SetSiblingIndex(originalSiblingIndex);
        CleanupPlaceholder();
    }

    void CreatePlaceholder()
    {
        placeholder = new GameObject("placeholder");
        var prt = placeholder.AddComponent<RectTransform>();
        prt.SetParent(originalParent, false);

        // Make a LayoutElement to reserve space (take values from this item if it has them)
        var le = placeholder.AddComponent<LayoutElement>();

        // If this item has a LayoutElement, copy its sizing so placeholder matches exactly.
        var myLE = GetComponent<LayoutElement>();
        if (myLE != null)
        {
            le.preferredWidth = myLE.preferredWidth;
            le.preferredHeight = myLE.preferredHeight;
            le.minWidth = myLE.minWidth;
            le.minHeight = myLE.minHeight;
            le.flexibleWidth = myLE.flexibleWidth;
            le.flexibleHeight = myLE.flexibleHeight;
        }
        else
        {
            // Fallback: use the rect size
            le.preferredWidth = rect.rect.width;
            le.preferredHeight = rect.rect.height;
            le.flexibleWidth = 0;
            le.flexibleHeight = 0;
        }

        placeholder.transform.SetSiblingIndex(originalSiblingIndex);
    }

    void CleanupPlaceholder()
    {
        if (placeholder != null)
        {
            Destroy(placeholder);
            placeholder = null;
        }
    }

    void UpdatePlaceholderSibling(PointerEventData eventData)
    {
        if (placeholder == null || layoutParent == null) return;

        int currentIndex = placeholder.transform.GetSiblingIndex();
        int newIndex = currentIndex; // Start with current index to avoid unnecessary moves
        float pointerX = eventData.position.x;

        // Get the layout's screen-space bounds
        Rect layoutRect = GetScreenRect(layoutParent, rootCanvas);
        float layoutLeft = layoutRect.xMin;
        float layoutRight = layoutRect.xMax;

        // Early exit if pointer is outside layout bounds (with buffer)
        if (pointerX < layoutLeft - placeholderBuffer || pointerX > layoutRight + placeholderBuffer)
            return;

        // Find closest sibling based on pointer position
        float closestDistance = float.MaxValue;
        for (int i = 0; i < layoutParent.childCount; i++)
        {
            var child = layoutParent.GetChild(i);
            if (child == placeholder.transform) continue;

            var childRect = child as RectTransform;
            Vector3[] corners = new Vector3[4];
            childRect.GetWorldCorners(corners);
            Vector2 leftScreen = RectTransformUtility.WorldToScreenPoint(rootCanvas.worldCamera, corners[0]);
            Vector2 rightScreen = RectTransformUtility.WorldToScreenPoint(rootCanvas.worldCamera, corners[2]);
            float childMidX = (leftScreen.x + rightScreen.x) * 0.5f;

            float distance = Mathf.Abs(pointerX - childMidX);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                // If pointer is to the left of child's midpoint, insert before; else after
                newIndex = pointerX < childMidX ? i : i + 1;
            }
        }

        // Apply hysteresis to prevent rapid flipping
        if (newIndex != currentIndex)
        {
            float moveThreshold = 20f; // Increased threshold for stability
            Vector3[] phCorners = new Vector3[4];
            placeholder.GetComponent<RectTransform>().GetWorldCorners(phCorners);
            Vector2 phLeft = RectTransformUtility.WorldToScreenPoint(rootCanvas.worldCamera, phCorners[0]);
            Vector2 phRight = RectTransformUtility.WorldToScreenPoint(rootCanvas.worldCamera, phCorners[2]);
            float phMidX = (phLeft.x + phRight.x) * 0.5f;

            if (Mathf.Abs(pointerX - phMidX) > moveThreshold)
            {
                if (newIndex >= layoutParent.childCount)
                    placeholder.transform.SetAsLastSibling();
                else
                    placeholder.transform.SetSiblingIndex(newIndex);
            }
        }
    }

    bool IsPointerOverRect(RectTransform target, PointerEventData eventData, float padding)
    {
        if (target == null || rootCanvas == null || canvasRect == null) return false;

        Rect screenRect = GetScreenRect(target, rootCanvas);
        // expand by padding in pixels
        screenRect.xMin -= padding;
        screenRect.yMin -= padding;
        screenRect.xMax += padding;
        screenRect.yMax += padding;

        return screenRect.Contains(eventData.position);
    }

    // convert a screen point to a world point within the canvas RectTransform.
    // returns true when conversion succeeded.
    bool ScreenPointToWorld(Vector2 screenPoint, out Vector3 world)
    {
        world = Vector3.zero;
        if (canvasRect == null) return false;
        return RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, screenPoint, rootCanvas.worldCamera, out world);
    }

    // returns a screen-space rect for a RectTransform
    Rect GetScreenRect(RectTransform r, Canvas usedCanvas)
    {
        Vector3[] corners = new Vector3[4];
        r.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(usedCanvas.worldCamera, corners[0]);
        Vector2 max = min;
        for (int i = 1; i < 4; i++)
        {
            Vector2 sp = RectTransformUtility.WorldToScreenPoint(usedCanvas.worldCamera, corners[i]);
            min = Vector2.Min(min, sp);
            max = Vector2.Max(max, sp);
        }
        return new Rect(min, max - min);
    }
}