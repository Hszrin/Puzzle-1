using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CellView : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler
{
    public int X { get; private set; }
    public int Y { get; private set; }

    [Header("References")]
    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI numberText;

    [Header("Colors")]
    [SerializeField] private Color normalColor    = new Color(0f, 0f, 0f, 0f);          // 기본: 완전 투명
    [SerializeField] private Color selectionColor = new Color(0.30f, 0.55f, 0.98f, 0.35f); // 선택 하이라이트
    [SerializeField] private Color hintColor      = new Color(0.25f, 0.90f, 0.80f, 0.40f);

    [Header("Text Color")]
    [SerializeField] private Color numberColor    = new Color(0.95f, 0.96f, 1f, 1f);    // 밝은 숫자 색 (다크모드용)

    private int value;          // -1 = 공백, 1~k = 숫자
    private BoardManager board;

    public bool HasNumber => value > 0;
    public int Value => value;

    private bool isSelected = false;
    private bool isHint     = false;

    public void Init(BoardManager board, int x, int y, int initialValue)
    {
        this.board = board;
        this.X = x;
        this.Y = y;
        SetValue(initialValue);
    }

    public void SetValue(int newValue)
    {
        value = newValue;

        if (HasNumber)
        {
            numberText.text = value.ToString();
            numberText.enabled = true;
            numberText.color = numberColor;   // ← 여기서 항상 밝은 색으로 강제
        }
        else
        {
            numberText.text = "";
            numberText.enabled = false;
        }

        isSelected = false;
        isHint = false;
        UpdateVisualState();
    }

    // 기존 SetHighlight → 선택 하이라이트로 사용
    public void SetHighlight(bool on)
    {
        SetSelectionHighlight(on);
    }

    public void SetSelectionHighlight(bool on)
    {
        isSelected = on;
        UpdateVisualState();
    }

    public void SetHintHighlight(bool on)
    {
        isHint = on;
        UpdateVisualState();
    }

    public void SetHint(bool on)
    {
        SetHintHighlight(on);
    }

    private void UpdateVisualState()
    {
        if (background != null)
        {
            Color targetColor = normalColor;

            if (isHint)
                targetColor = hintColor;

            if (isSelected)
                targetColor = selectionColor;

            background.color = targetColor;
        }

        if (isSelected)
            transform.localScale = Vector3.one * 1.10f;
        else if (isHint)
            transform.localScale = Vector3.one * 1.08f;
        else
            transform.localScale = Vector3.one;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (board != null)
            board.OnCellPointerDown(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (board != null && Input.GetMouseButton(0))
            board.OnCellPointerEnter(this);
    }
}
