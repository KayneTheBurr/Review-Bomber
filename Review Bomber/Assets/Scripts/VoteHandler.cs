using UnityEngine;
using System.Collections.Generic;

public class VoteHandler : MonoBehaviour
{
    public static VoteHandler Instance { get; private set; }

    public List<PlayerVote> playerVotes = new List<PlayerVote>();
    public StageManagerServer stageManagerServer;

    [SerializeField] private GameObject playerCell;

    public List<string> playerReferences = new List<string>();
    public List<string> playerNames = new List<string>();

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

            for (int i = 0; i < playerReferences.Count; i++)
            {
                AddCellAndPopulateData(playerReferences[i], playerNames[i]);
            }
        }
    }

    public void AddPlayerToLists(string playerReference, string playerName)
    {
        if (!playerReferences.Contains(playerReference))
        {
            playerReferences.Add(playerReference);
            playerNames.Add(playerName);
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
