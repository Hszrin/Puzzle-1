using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class WorldScoreUnit : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI rankText;
    [SerializeField] TextMeshProUGUI IDText;
    [SerializeField] TextMeshProUGUI scoreText;
    public void SetCondition(int rank, string id, int score)
    {
        rankText.text = rank.ToString();
        if (!string.IsNullOrEmpty(id) && id.Length > 6)
        {
            // 6글자까지 자르고 뒤에 .. 추가 (예: ABCDEF..)
            IDText.text = id.Substring(0, 6) + "..";
        }
        else
        {
            IDText.text = id;
        }
        scoreText.text = score.ToString();
    }
}
