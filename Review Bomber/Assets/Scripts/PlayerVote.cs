using Unity.VisualScripting;
using UnityEngine;
using TMPro;

public class PlayerVote : MonoBehaviour
{
    public string assignedPlayer;
    public bool hasVoted;
    [SerializeField] private GameObject votedImage;
    [SerializeField] private TMP_Text playerNameText;
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
