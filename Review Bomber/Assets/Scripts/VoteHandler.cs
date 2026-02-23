using UnityEngine;
using System.Collections.Generic;

public class VoteHandler : MonoBehaviour
{
    public List<PlayerVote> playerVotes = new List<PlayerVote>();

    public int numberPlayersVoted = 0;
    public bool allPlayersVoted;

    private void Start()
    {
        numberPlayersVoted = 0;
        allPlayersVoted = false;
    }

    private void Update()
    {
        CheckVotes();

        if (numberPlayersVoted == playerVotes.Count)
        {
            allPlayersVoted = true;
        }
        else
        {
            allPlayersVoted = false;
        }
    }

    private void CheckVotes()
    {
        numberPlayersVoted = 0;
        foreach (PlayerVote playerVote in playerVotes)
        {
            if (playerVote.hasVoted)
            {
                numberPlayersVoted++;
            }
        }
    }
}
