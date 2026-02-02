using UnityEngine;
using TMPro;

public class ScoreDisplay : MonoBehaviour
{
    private TextMeshProUGUI textMesh;

    void Start()
    {
        textMesh = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        // Access the static score directly from the other script
        textMesh.text = TargetHit.score.ToString();
    }
}