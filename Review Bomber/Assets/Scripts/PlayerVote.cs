using Unity.VisualScripting;
using UnityEngine;

public class PlayerVote : MonoBehaviour
{
    public bool hasVoted;
    [SerializeField] private GameObject votedImage;
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
