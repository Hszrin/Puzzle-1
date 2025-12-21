using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;

public class ComboTextFade : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI text;
    Tween fadeTween;
    Tween scaleTween;


    public void SetCondition(string value)
    {
        text.text = value;

        // 초기 상태 세팅
        text.alpha = 1f;
        transform.localScale = Vector3.one;

        // 페이드 아웃
        fadeTween = text.DOFade(0f, 0.4f)
            .SetEase(Ease.InQuad);

        // 스케일 축소
        scaleTween = transform.DOScale(0.6f, 0.4f)
            .SetEase(Ease.InQuad);

        Invoke(nameof(DestroySelf), 0.3f);
    }
    private void DestroySelf()
    {
        fadeTween?.Kill();
        scaleTween?.Kill();
        Destroy(gameObject);
    }
}
