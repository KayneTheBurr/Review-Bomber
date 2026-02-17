using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;

    [Header("UI Object Parents")]
    public GameObject lobbyUI;
    public GameObject themeUI;
    public GameObject promptUI;
    public GameObject voteUI;
    public GameObject resultUI;

    [Header("Lobby UI Elements")]
    public TMP_Text p1Name;
    public TMP_Text p2Name;
    public TMP_Text p3Name;
    public TMP_Text p4Name;
    public TMP_Text p5Name;
    public TMP_Text p6Name;
    public TMP_Text p7Name;
    public TMP_Text p8Name;
    public TMP_Text p9Name;
    public TMP_Text p10Name;

    [Header("Theme UI Elements")]
    public TMP_Text theme;

    [Header("Prompt UI Elements")]
    public TMP_Text prompt;
    public TMP_Text prompt_theme;

    [Header("Vote UI Elements")]
    public TMP_Text finalPrompt;
    public TMP_Text finalReview;
    public Image starImage;
    public Sprite oneStar;
    public Sprite twoStar;
    public Sprite threeStar;
    public Sprite fourStar;
    public Sprite fiveStar;

    [Header("Results UI Elements")]
    //first place
    public TMP_Text firstPlacePromptName;
    public TMP_Text firstPlacePrompt;
    public TMP_Text firstPlaceReviewName;
    public TMP_Text firstPlaceReview;
    public Image firstPlaceStarRank;
    public TMP_Text firstPlaceScore;
    //second place
    public TMP_Text secondPlacePromptName;
    public TMP_Text secondPlacePrompt;
    public TMP_Text secondPlaceReviewName;
    public TMP_Text secondPlaceReview;
    public Image secondPlaceStarRank;
    public TMP_Text secondPlaceScore;
    //third place
    public TMP_Text thirdPlacePromptName;
    public TMP_Text thirdPlacePrompt;
    public TMP_Text thirdPlaceReviewName;
    public TMP_Text thirdPlaceReview;
    public Image thirdPlaceStarRank;
    public TMP_Text thirdPlaceScore;


    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    public void ShowScreen(string screenName)
    {
        if (lobbyUI) lobbyUI.SetActive(false);
        if (themeUI) themeUI.SetActive(false);
        if (promptUI) promptUI.SetActive(false);
        if (voteUI) voteUI.SetActive(false);
        if (resultUI) resultUI.SetActive(false);

        switch (screenName)
        {
            case "Lobby": if (lobbyUI) lobbyUI.SetActive(true); break;
            case "Theme": if (themeUI) themeUI.SetActive(true); break;
            case "Prompt": if (promptUI) promptUI.SetActive(true); break;
            case "Review":  // you said Review reuses Vote UI
            case "Vote": if (voteUI) voteUI.SetActive(true); break;
            case "Results": if (resultUI) resultUI.SetActive(true); break;
            default: if (lobbyUI) lobbyUI.SetActive(true); break;
        }
    }
}
