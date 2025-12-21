using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;

public class TextFloating : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI text;

    [SerializeField] float moveUpDistance = 80f;
    [SerializeField] float duration = 0.8f;

    public void SetCondition(string value)
    {
        text.text = value;

        RectTransform rt = text.rectTransform;
        Vector2 startPos = rt.anchoredPosition;

        Sequence seq = DOTween.Sequence();

        seq.Append(
            rt.DOAnchorPosY(startPos.y + moveUpDistance, duration)
              .SetEase(Ease.OutCubic)
        );

        seq.Join(
            text.DOFade(0f, duration)
        );

        seq.OnComplete(() =>
        {
            Destroy(gameObject);
        });
    }
}
