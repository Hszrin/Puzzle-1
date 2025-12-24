using System.Collections;
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

    [Header("Number Cell Sprites")]
    [SerializeField] private Sprite normalSprite;       // 기본 셀
    [SerializeField] private Sprite selectedSprite;     // 선택된 셀
    [SerializeField] private Sprite hintSprite;         // 힌트 셀

    [Header("Blank Cell Sprites")]
    [SerializeField] private Sprite blankNormalSprite;      // 공백 셀
    [SerializeField] private Sprite blankSelectedSprite;    // 선택된 공백 셀
    [SerializeField] private Sprite blankHintSprite;        // 공백 셀 힌트

    [Header("Text Color")]
    [SerializeField] private Color numberColor    = new Color(0.1f, 0.1f, 0.25f, 1f);      // 검은색-남색 계열

    [Header("Animation Settings")]
    [SerializeField] private float selectAnimDuration = 0.3f;
    [SerializeField] private float selectScalePunch = 1.15f;     // 처음 커지는 정도
    [SerializeField] private float selectScaleHold = 0.92f;      // 누르고 있을 때 최종 배율
    [SerializeField] private float hintWiggleAngle = 12f;        // 흔들림 각도
    [SerializeField] private float hintWiggleSpeed = 25f;        // 흔들림 속도
    [SerializeField] private float hintPauseDuration = 0.8f;     // 흔들림 사이 멈춤 시간

    private int value;          // -1 = 공백, 1~k = 숫자
    private BoardManager board;

    public bool HasNumber => value > 0;
    public int Value => value;

    private bool isSelected = false;
    private bool isHint     = false;

    // 애니메이션 관련
    private Coroutine selectAnimCoroutine;
    private Coroutine hintAnimCoroutine;

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
            numberText.color = numberColor;
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
            Sprite targetSprite = GetCurrentSprite();
            if (targetSprite != null)
                background.sprite = targetSprite;

            background.color = Color.white; // 스프라이트 자체 색상 유지
        }

        // 애니메이션 처리
        if (isSelected)
        {
            StopHintAnimation();
            PlaySelectAnimation();
        }
        else if (isHint)
        {
            StopSelectAnimation();
            PlayHintAnimation();
        }
        else
        {
            StopAllCellAnimations();
            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;
        }
    }

    private void PlaySelectAnimation()
    {
        if (selectAnimCoroutine != null)
            StopCoroutine(selectAnimCoroutine);
        selectAnimCoroutine = StartCoroutine(SelectBounceCoroutine());
    }

    private void StopSelectAnimation()
    {
        if (selectAnimCoroutine != null)
        {
            StopCoroutine(selectAnimCoroutine);
            selectAnimCoroutine = null;
        }
    }

    private void PlayHintAnimation()
    {
        if (hintAnimCoroutine != null)
            StopCoroutine(hintAnimCoroutine);
        hintAnimCoroutine = StartCoroutine(HintWiggleCoroutine());
    }

    private void StopHintAnimation()
    {
        if (hintAnimCoroutine != null)
        {
            StopCoroutine(hintAnimCoroutine);
            hintAnimCoroutine = null;
            transform.localRotation = Quaternion.identity;
        }
    }

    private void StopAllCellAnimations()
    {
        StopSelectAnimation();
        StopHintAnimation();
    }

    // 선택 시 보잉 애니메이션 (Elastic Ease Out)
    private IEnumerator SelectBounceCoroutine()
    {
        float elapsed = 0f;
        Vector3 startScale = Vector3.one * selectScalePunch;
        Vector3 endScale = Vector3.one * selectScaleHold;  // 최종 배율 (살짝 작게)

        // 처음에 바로 커지게
        transform.localScale = startScale;

        while (elapsed < selectAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / selectAnimDuration;

            // Elastic Ease Out - 보잉 효과
            float elasticT = ElasticEaseOut(t);
            transform.localScale = Vector3.LerpUnclamped(startScale, endScale, elasticT);

            yield return null;
        }

        transform.localScale = endScale;
        selectAnimCoroutine = null;
    }

    // 힌트 시 좌우 흔들림 애니메이션
    private IEnumerator HintWiggleCoroutine()
    {
        while (isHint)
        {
            // 흔들림 페이즈 (3번 흔들기)
            float wiggleTime = 0f;
            float wiggleDuration = 0.4f;

            while (wiggleTime < wiggleDuration)
            {
                wiggleTime += Time.deltaTime;

                // 감쇠하는 사인파 - 처음엔 크게, 점점 작게
                float decay = 1f - (wiggleTime / wiggleDuration);
                float angle = Mathf.Sin(wiggleTime * hintWiggleSpeed) * hintWiggleAngle * decay;
                transform.localRotation = Quaternion.Euler(0, 0, angle);

                yield return null;
            }

            // 원래 각도로 복귀
            transform.localRotation = Quaternion.identity;

            // 멈춤 페이즈
            yield return new WaitForSeconds(hintPauseDuration);
        }

        transform.localRotation = Quaternion.identity;
        hintAnimCoroutine = null;
    }

    // Elastic Ease Out 함수 - 탄성있게 튕기는 효과
    private float ElasticEaseOut(float t)
    {
        if (t == 0) return 0;
        if (t == 1) return 1;

        float p = 0.3f;  // period
        float s = p / 4f;

        return Mathf.Pow(2, -10 * t) * Mathf.Sin((t - s) * (2 * Mathf.PI) / p) + 1;
    }

    private Sprite GetCurrentSprite()
    {
        bool isBlankCell = value <= 0;

        if (isBlankCell)
        {
            // 공백 셀
            if (isSelected && blankSelectedSprite != null)
                return blankSelectedSprite;
            if (isHint && blankHintSprite != null)
                return blankHintSprite;
            return blankNormalSprite;
        }
        else
        {
            // 숫자 셀
            if (isSelected && selectedSprite != null)
                return selectedSprite;
            if (isHint && hintSprite != null)
                return hintSprite;
            return normalSprite;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        board.OnCellPointerDown(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        board.OnCellPointerEnter(this);
    }
}
