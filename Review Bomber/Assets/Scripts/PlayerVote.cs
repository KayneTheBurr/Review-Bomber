using Unity.VisualScripting;
using UnityEngine;
using TMPro;

public class PlayerVote : MonoBehaviour
{
    public string assignedPlayerName;
    public bool hasVoted;
    [SerializeField] private GameObject votedImage;
    public TMP_Text playerNameText;
    [SerializeField] private Color notVotedColor;
    [SerializeField] private Color yesVotedColor;

    private void Start()
    {
        hasVoted = false;
    }

    private void Update()
    {
        if (hasVoted)
        {
            votedImage.SetActive(true);
        }
        else
        {
            votedImage.SetActive(false);
        }
    }
}
