using UnityEngine;
using UnityEngine.EventSystems;

public class UIDragSnap2D : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [HideInInspector] public Vector3 correctPosition; // local offset position
    [HideInInspector] public GameObject mainBody;
    [HideInInspector] public Vector3 startPosition;

    private RectTransform rectTransform;
    private Canvas canvas;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        transform.SetAsLastSibling(); // bring to front
    }

    public void OnDrag(PointerEventData eventData)
    {
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Vector2 worldCorrectPos = mainBody.GetComponent<RectTransform>().anchoredPosition + (Vector2)correctPosition;

        float distance = Vector2.Distance(rectTransform.anchoredPosition, worldCorrectPos);

        if (distance < 150f) // snap threshold
        {
            rectTransform.anchoredPosition = worldCorrectPos; // snap
            // Optional: lock movement
            this.enabled = false;
        }
        else
        {
            rectTransform.anchoredPosition = startPosition; // reset
        }
    }
}
