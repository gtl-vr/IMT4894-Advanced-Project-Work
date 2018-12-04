using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour {
    public Text _scoreText;
    public Text _experimentText;
    public Text _tutorialText1;
    public Text _tutorialText2;
    public Text _tutorialText3;
    public float _menuDisplaySpeed = 2f;

    private void Start()
    {
        _scoreText.rectTransform.parent.localScale = new Vector3(1, 0, 1);
        _experimentText.rectTransform.parent.localScale = new Vector3(1, 0, 1);
    }
#if UNITY_EDITOR
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            StartCoroutine(SwitchMenuStateAnimation(true));
        }
    }
#endif
    public void DisplayScore(int timeTaken, int numberOfHits, int totalScore, int participantID, int experimentID, int scorePlacement, int totalParticipants)
    {
        var scoreText = "Time Taken:\n~" + timeTaken + "s\n" + "Hits Taken:\n" + numberOfHits + "\n" + "Score:\n" + totalScore;
        _scoreText.text = scoreText;
        var experimentText = "Participant ID:\n" + participantID + "\n" + "Experiment ID:\n" + experimentID + "\n" + "Score Placement:\n" + scorePlacement + "/" + totalParticipants;
        _experimentText.text = experimentText;
        StartCoroutine(SwitchMenuStateAnimation(true));
    }

    public void CloseTutorials()
    {
        StartCoroutine(SwitchMenuStateAnimation(false));
    }

    private IEnumerator SwitchMenuStateAnimation(bool displayingScore)
    {
        var lerpTimer = 0f;
        var newScale = displayingScore ? _scoreText.rectTransform.parent.localScale : _tutorialText1.rectTransform.parent.localScale;
        var transform0 = displayingScore ? _scoreText.rectTransform.parent : _tutorialText1.rectTransform.parent;
        var transform1 = displayingScore ? _experimentText.rectTransform.parent : _tutorialText2.rectTransform.parent;
        Transform transform2 = null;
        var isControl = SceneManager.GetActiveScene().name == "Control";
        if(!isControl && !displayingScore)
        {
            transform2 = _tutorialText3.rectTransform.parent;
        }

        while (lerpTimer <= 1f)
        {
            lerpTimer += Time.deltaTime * _menuDisplaySpeed;
            newScale.y = Mathf.Lerp(displayingScore ? 0 : 1, displayingScore ? 1 : 0, lerpTimer);
            transform0.localScale = newScale;
            transform1.localScale = newScale;
            if(!isControl && !displayingScore)
            {
                transform2.localScale = newScale;
            }
            yield return null;
        }

        transform0.localScale = displayingScore ? Vector3.one : Vector3.zero;
        transform1.localScale = displayingScore ? Vector3.one : Vector3.zero;
        if(!isControl && !displayingScore)
        {
            transform2.localScale = Vector3.zero;
        }
    }
}
