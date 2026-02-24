using UnityEngine;
using System.Collections.Generic;

public class VoteHandler : MonoBehaviour
{
    public static VoteHandler Instance { get; private set; }

    public List<PlayerVote> playerVotes = new List<PlayerVote>();
    public StageManagerServer stageManagerServer;

    [SerializeField] private GameObject playerCell;

    public int numberPlayersVoted = 0;
    public bool allPlayersVoted;

    public bool listMade = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }   
    }

    private void Start()
    {
        numberPlayersVoted = 0;
        allPlayersVoted = false;
        listMade = false;

        stageManagerServer = FindFirstObjectByType<StageManagerServer>();
    }

    private void Update()
    {
        CheckVotes();

        if (numberPlayersVoted == playerVotes.Count && playerVotes.Count >= 2)
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

    public void SetUpTheThing()
    {
        if (listMade) { return; }
        else
        {
            listMade = true;

            //make list
            //for (int i = 0; i < stageManagerServer.player.Count; i++)

            //need list of player references and names in stage manager server to do this, need to add that first

            //{

            //create cell and populate data for each player in the list, need to add player reference and name list first in stage manager server for this to work

            //    AddCellAndPopulateData(stageManagerServer.playerReferences[i], stageManagerServer.playerNames[i]);
            //}
        }
    }

    public void AddCellAndPopulateData(string playerReference, string playerName)
    {
        GameObject newCell = Instantiate(playerCell, gameObject.transform);

        PlayerVote playerVote = newCell.GetComponent<PlayerVote>();

        if (playerVote != null)
        {
            playerVote.assignedPlayer = playerReference;
            playerVote.playerNameText.text = playerName;
            playerVotes.Add(playerVote);
        }
    }

    public void PlayerVoted(string playerReference)
    {
        foreach (PlayerVote playerVote in playerVotes)
        {
            if (playerVote.assignedPlayer == playerReference)
            {
                playerVote.hasVoted = true;
                break;
            }
        }
    }

    public void ResetVotes()
    {
        foreach (PlayerVote playerVote in playerVotes)
        {
            playerVote.hasVoted = false;
        }
    }
}
