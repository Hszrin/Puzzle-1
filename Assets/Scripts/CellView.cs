using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CellView : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler
{
    public int X { get; private set; }
    public int Y { get; private set; }

    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI numberText;

    private int value; // -1 = 공백, 1~k = 숫자
    private BoardManager board;

    public bool HasNumber => value > 0;
    public int Value => value;

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
        }
        else
        {
            numberText.text = "";
            numberText.enabled = false;
        }

        SetHighlight(false);
    }

    public void SetHighlight(bool on)
    {
        if (background != null)
        {
            // 하이라이트 색은 나중에 취향대로 바꿔도 됨
            background.color = on ? new Color(1f, 0.9f, 0.6f) : Color.white;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (board != null)
            board.OnCellPointerDown(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 마우스 왼쪽 버튼(에디터) / 터치 드래그 중일 때만
        if (board != null && Input.GetMouseButton(0))
            board.OnCellPointerEnter(this);
    }

    public void SetHint(bool on)
    {
        // 예시: scale + 색 조정
        transform.localScale = on ? Vector3.one * 1.1f : Vector3.one;

        // Image 색을 살짝 밝게 한다든지
        // image.color = on ? hintColor : normalColor;
    }
}
