using UnityEngine;
using System.Collections.Generic;

public class VoteHandler : MonoBehaviour
{
    public static VoteHandler Instance { get; private set; }

    public List<PlayerVote> playerVotes = new List<PlayerVote>();
    public StageManagerServer stageManagerServer;

    [SerializeField] private GameObject playerCell;
    public Transform cellParent;

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
        cellParent.gameObject.SetActive(false);
        numberPlayersVoted = 0;
        allPlayersVoted = false;
        listMade = false;

        stageManagerServer = FindFirstObjectByType<StageManagerServer>();
    }

    private void Update()
    {
        //CheckVotes();

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

        if (playerVotes.Count == 0) { return; }

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

            for (int i = 0; i < playerNames.Count; i++)
            {
                Debug.Log(i);
                AddCellAndPopulateData(playerNames[i]);
            }
        }
    }

    public void AddPlayerToLists(string p)
    {
        if (!playerNames.Contains(p))
        {
            playerNames.Add(p);
        }
    }

    public void AddCellAndPopulateData(string playerName)
    {
        GameObject newCell = Instantiate(playerCell, cellParent);

        PlayerVote playerVote = newCell.GetComponent<PlayerVote>();

        if (playerVote != null)
        {
            playerVote.playerNameText.text = playerName;
            playerVote.assignedPlayerName = playerName;
            playerVotes.Add(playerVote);
        }
    }

    public void PlayerVoted(string playerName)
    {
        foreach (PlayerVote playerVote in playerVotes)
        {
            Debug.Log(playerName + " has voted");
            if (playerVote.assignedPlayerName == playerName)
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
