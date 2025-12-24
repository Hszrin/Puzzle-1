using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ScoreUnit : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI rankText;
    [SerializeField] TextMeshProUGUI maxComboText;
    [SerializeField] TextMeshProUGUI scoreText;
    public void SetCondition(int rank, int maxCombo, int score)
    {
        rankText.text = rank.ToString();
        maxComboText.text = maxCombo.ToString();
        scoreText.text = score.ToString();
    }
}
