// StageManagerServer.cs
// Review Bomber - minimal LAN party game server (Unity + Fleck)
//
// Scenes:
//   Lobby   -> Prompt  (fill in blanks A/B for tagline)
//          -> Review  (write a review for someone else's tagline, with an assigned rating)
//          -> Vote    (rate each entry 1-5 stars; sequential "Option A")
//          -> Results -> Wait -> Prompt ...
//
// Client messages (kept intentionally generic):
//   { type:"join",  name:"Kaine" }
//   { type:"start" }
//   { type:"input", a:"interns", b:"crunch" }     // Prompt scene
//   { type:"input", text:"This ruined my life" }    // Review scene
//   { type:"choice", index:3 }                      // Vote scene: index 0..4 => stars 1..5
//
// Notes:
// - No unicode stars are used (to avoid encoding/storage issues).
// - ReviewRating is an enum; JsonUtility serializes it as an int by default.
//   We also send a string label for convenience.
// - starButtons is an int[] {1,2,3,4,5} as requested.

using UnityEngine;
using Fleck;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System;

public class StageManagerServer : MonoBehaviour
{
    // ------------------------
    // Inspector-exposed fields
    // ------------------------
    [Header("Network Settings")]
    public string listenAddress = "0.0.0.0";
    public int port = 8080;

    [Header("Themes and Prompts List")]
    public List<ThemePrompt> themePrompts;
    private ThemePrompt currentThemePrompt;

    [Header("Review Bomber Settings")]
    public string theme = "Review Bomber";
    private bool themeTimerRequested = false;
    private bool themeTimerRunning = false;
    public float themeDurationSeconds = 10f;

    // Keep your variable name "prompt" (this is the tagline template)
    // Tip: prefer using {A} and {B} tokens, e.g. "Don't let your {A} ever cause {B} again!"
    public string prompt = "Dont let your {A} ever cause {B} again!";

    // No unicode in buttons; just ints
    public int[] starButtons = new int[] { 1, 2, 3, 4, 5 };

    // ------------------------
    // Game state tracking
    // ------------------------
    public enum SceneState { Lobby, Theme, Prompt, Review, Vote, Results}

    // You asked about making this an enum: YES, it works fine.
    // JsonUtility will serialize it as an int (0,1,2...).
    public enum ReviewRating { Good = 0, Average = 1, Bad = 2 }

    public SceneState currentState = SceneState.Lobby;

    //debugging trying to fix randomizer giving an error bc of fleck things
    private readonly System.Random _rng = new System.Random();

    void PickThemeAndPromptForRound()
    {
        if (themePrompts == null || themePrompts.Count == 0)
        {
            currentThemePrompt = new ThemePrompt
            {
                theme = "Default Theme",
                promptTemplate = "Don't let your {A} ever cause {B} again!"
            };
            return;
        }
        else
        {
            int index = _rng.Next(themePrompts.Count);
            currentThemePrompt = themePrompts[index];

        }
        theme = currentThemePrompt.theme;
        prompt = currentThemePrompt.promptTemplate;

        if (string.IsNullOrEmpty(prompt) || !prompt.Contains("{A}") || !prompt.Contains("{B}"))
        {
            Debug.LogError($"[Server] PromptTemplate missing {{A}} or {{B}}: '{prompt}'");
            prompt = "Don't let your {A} ever cause {B} again!";
        }

        Debug.Log($"[Server] Selected Theme: {currentThemePrompt.theme}");
    }

    // ------------------------
    // Player + Round structures
    // ------------------------
    class Player
    {
        public string name;
        public bool isFirst;

        // Prompt phase inputs
        public string blankA;
        public string blankB;

        // Review phase assignment
        public int assignedEntryIndex = -1;
        public ReviewRating rating = ReviewRating.Average;
        public string reviewText = "";

        // Vote phase
        public bool hasVotedThisEntry = false;
    }

    class Entry
    {
        public int playerIndex;          // author index (in orderedConnections)
        public string playerName;        // author name
        public string promptFinal;       // resolved tagline

        public string reviewerName;      // who wrote the review
        public ReviewRating reviewRating;
        public string reviewText;

        // Ratings from all players: playerName -> stars
        public Dictionary<string, int> ratings = new Dictionary<string, int>();

        public float AverageStars
        {
            get
            {
                if (ratings == null || ratings.Count == 0) return 0f;
                float sum = 0f;
                foreach (var kv in ratings) sum += kv.Value;
                return sum / ratings.Count;
            }
        }
    }

    // Connection -> Player
    private Dictionary<IWebSocketConnection, Player> players = new Dictionary<IWebSocketConnection, Player>();
    private bool firstAssigned = false;
    private WebSocketServer server;

    // Round state
    private List<IWebSocketConnection> orderedConnections = new List<IWebSocketConnection>();
    private List<Entry> entries = new List<Entry>();
    private int currentEntryIndex = 0;

    // Auto-advance counter: counts submissions/votes expected per step
    private int responsesReceived = 0;


    


    // ------------------------
    // Unity Start
    // ------------------------
    void Start()
    {
        string url = $"ws://{listenAddress}:{port}";
        server = new WebSocketServer(url);

        server.Start(socket =>
        {
            socket.OnOpen = () =>
            {
                var p = new Player();

                if (!firstAssigned)
                {
                    p.isFirst = true;
                    firstAssigned = true;
                    Debug.Log("[Server] First player assigned");
                }

                players[socket] = p;
                RebuildOrderedConnections();
                SendStateTo(socket);
            };

            socket.OnClose = () =>
            {
                bool wasFirst = players.ContainsKey(socket) && players[socket].isFirst;

                players.Remove(socket);
                RebuildOrderedConnections();

                if (wasFirst)
                {
                    firstAssigned = false;
                    foreach (var kv in players)
                    {
                        kv.Value.isFirst = true;
                        firstAssigned = true;
                        break;
                    }
                }


                SendStateToAll();
            };

            socket.OnMessage = message =>
            {
                Incoming msg = JsonUtility.FromJson<Incoming>(message);
                if (!players.ContainsKey(socket)) return;

                Player p = players[socket];

                switch (msg.type)
                {
                    case "join":
                        p.name = msg.name;
                        Debug.Log($"[Server] join from {p.name}");
                        break;

                    case "start":
                        if (p.isFirst && (currentState == SceneState.Lobby))
                        {
                            AdvanceState();
                        }
                        break;

                    case "input":
                        HandleInput(socket, p, msg);
                        break;

                    case "choice":
                        HandleChoice(socket, p, msg);
                        break;
                }

                SendStateToAll();
            };
        });

        Debug.Log($"[Server] StageManagerServer started on {url}");
    }

    private void Update()
    {
        if (currentState == SceneState.Theme && themeTimerRequested && !themeTimerRunning)
        {
            themeTimerRequested = false;
            themeTimerRunning = true;
            Debug.Log("[Server] Theme timer actually starting (main thread)");
            StartCoroutine(ThemeCountdown());
        }
    }
    IEnumerator ThemeCountdown()
    {
        yield return new WaitForSeconds(themeDurationSeconds);

        if (currentState == SceneState.Theme)
        {
            Debug.Log("[Server] Theme timer finished, advancing to Prompt");
            AdvanceState();
            SendStateToAll();
        }

        themeTimerRunning = false;
    }

    // ------------------------
    // Phase input handling
    // ------------------------
    void HandleInput(IWebSocketConnection conn, Player p, Incoming msg)
    {
        if (currentState == SceneState.Prompt)
        {
            // Two blanks A/B
            p.blankA = msg.a;
            p.blankB = msg.b;

            responsesReceived++;

            Debug.Log($"[Server] Prompt received: {responsesReceived}/{players.Count} from {(p.name ?? "(unnamed)")}");

            if (responsesReceived >= players.Count)
            {
                AdvanceState();
            }
            return;
        }

        if (currentState == SceneState.Review)
        {
            p.reviewText = msg.text;

            responsesReceived++;
            if (responsesReceived >= players.Count)
            {
                AdvanceState();
            }
            return;
        }
    }

    void HandleChoice(IWebSocketConnection conn, Player p, Incoming msg)
    {
        if (currentState != SceneState.Vote) return;
        if (entries == null || entries.Count == 0) return;
        if (currentEntryIndex < 0 || currentEntryIndex >= entries.Count) return;
        if (p.hasVotedThisEntry) return;

        // Vote index 0..4 => starButtons[index] (1..5)
        int idx = Mathf.Clamp(msg.index, 0, starButtons.Length - 1);
        int stars = starButtons[idx];

        var e = entries[currentEntryIndex];
        string voterName = string.IsNullOrEmpty(p.name) ? "(unnamed)" : p.name;

        e.ratings[voterName] = stars;
        p.hasVotedThisEntry = true;

        responsesReceived++;
        if (responsesReceived >= players.Count)
        {
            AdvanceVoteTargetOrFinish();
        }
    }

    

    // ------------------------
    // State transitions
    // ------------------------
    void AdvanceState()
    {
        responsesReceived = 0;

        switch (currentState)
        {
            case SceneState.Lobby:
                Debug.Log("[Server] Transition Lobby -> Theme");
                StartNewRound();
                PickThemeAndPromptForRound();
                currentState = SceneState.Theme;
                themeTimerRequested = true;
                break;

            case SceneState.Theme:
                Debug.Log("[Server] Transition Theme -> Prompt");
                StartNewRound();
                currentState = SceneState.Prompt;
                break;

            case SceneState.Prompt:
                Debug.Log("[Server] Transition Prompt -> Review (about to BuildEntriesFromPrompt)");
                BuildEntriesFromPrompt();
                Debug.Log("[Server] Transition Prompt -> Review (about to AssignReviews)");
                AssignReviews();
                Debug.Log("[Server] Transition Prompt -> Review (setting state now)");
                currentState = SceneState.Review;
                break;

            case SceneState.Review:
                Debug.Log("[Server] Transition Review -> Vote");
                ApplyReviewsToEntries();
                BeginVoting();
                currentState = SceneState.Vote;
                break;

            case SceneState.Vote:
                Debug.Log("[Server] Transition Vote -> Results");
                AdvanceVoteTargetOrFinish();
                currentState = SceneState.Results;
                break;

            case SceneState.Results:
                Debug.Log("[Server] Transition Results -> Theme");
                PickThemeAndPromptForRound();
                currentState = SceneState.Theme;
                themeTimerRequested = true;
                break;

            
        }
        SendStateToAll();
        Debug.Log("[Server] Advanced to " + currentState);
    }

    void StartNewRound()
    {
        responsesReceived = 0;
        currentEntryIndex = 0;
        entries = new List<Entry>();

        RebuildOrderedConnections();

        foreach (var kv in players)
        {
            var p = kv.Value;
            p.blankA = "";
            p.blankB = "";
            p.assignedEntryIndex = -1;
            p.rating = ReviewRating.Average;
            p.reviewText = "";
            p.hasVotedThisEntry = false;
        }

        Debug.Log("[Server] New round started");
    }

    public void DebugAdvance()
    {
        AdvanceState();
        SendStateToAll();
    }


    // ------------------------
    // Round building logic
    // ------------------------
    void BuildEntriesFromPrompt()
    {
        entries = new List<Entry>();
        RebuildOrderedConnections();

        for (int i = 0; i < orderedConnections.Count; i++)
        {
            var conn = orderedConnections[i];
            var p = players[conn];

            string a = string.IsNullOrWhiteSpace(p.blankA) ? "____" : p.blankA.Trim();
            string b = string.IsNullOrWhiteSpace(p.blankB) ? "____" : p.blankB.Trim();

            string final = ResolvePromptTemplate(prompt, a, b);

            entries.Add(new Entry
            {
                playerIndex = i,
                playerName = string.IsNullOrEmpty(p.name) ? $"Player{i + 1}" : p.name,
                promptFinal = final
            });
        }

        Debug.Log($"[Server] Built {entries.Count} entries from Prompt");
    }

    // Handles both "{A}/{B}" token style and the user's original "A/B" style
    string ResolvePromptTemplate(string template, string a, string b)
    {
        if (string.IsNullOrEmpty(template)) return $"{a} / {b}";

        string t = template;

        // Preferred token style
        t = t.Replace("{A}", a).Replace("{B}", b);

        // Back-compat: " A " and " B " placeholders
        // (We avoid a global Replace("A", ...) because that would destroy words.)
        t = t.Replace(" A ", " " + a + " ");
        t = t.Replace(" B ", " " + b + " ");

        // Edge cases: ending with A or B (rare, but safe)
        if (t.EndsWith(" A")) t = t.Substring(0, t.Length - 2) + " " + a;
        if (t.EndsWith(" B")) t = t.Substring(0, t.Length - 2) + " " + b;

        return t;
    }

    void AssignReviews()
    {
        RebuildOrderedConnections();
        int n = orderedConnections.Count;

        Debug.Log($"[Server] AssignReviews: orderedConnections={n}, players={players.Count}");

        if (n < 2)
        {
            Debug.LogWarning("[Server] Need at least 2 players to assign reviews");
            return;
        }

        Debug.Log("Starting the assignment of reviews now...");

        for (int i = 0; i < n; i++)
        {
            try
            {
                Debug.Log("Assigning review for " + i);
                var conn = orderedConnections[i];

                Debug.Log("Test A");
                var p = players[conn];
                Debug.Log("Test B");

                p.assignedEntryIndex = (i + 1) % n;
                Debug.Log("Test C");

                p.rating = (ReviewRating)_rng.Next(0, 3);
                Debug.Log("Test D");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[Server] AssignReviews exception at i=" + i + ": " + ex);
                break;
            }
        }

        Debug.Log("[Server] Assigned review targets + ratings (done)");
    }

    void ApplyReviewsToEntries()
    {
        for (int i = 0; i < orderedConnections.Count; i++)
        {
            var conn = orderedConnections[i];
            var p = players[conn];

            if (p.assignedEntryIndex < 0 || p.assignedEntryIndex >= entries.Count) continue;

            var e = entries[p.assignedEntryIndex];
            e.reviewerName = string.IsNullOrEmpty(p.name) ? $"Player{i + 1}" : p.name;
            e.reviewRating = p.rating;
            e.reviewText = string.IsNullOrWhiteSpace(p.reviewText) ? "(no review submitted)" : p.reviewText.Trim();
        }

        Debug.Log("[Server] Applied reviews to entries");
    }

    // ------------------------
    // Voting (Option A sequential)
    // ------------------------
    void BeginVoting()
    {
        currentEntryIndex = 0;
        ResetVotesForCurrentEntry();
    }

    void ResetVotesForCurrentEntry()
    {
        responsesReceived = 0;
        foreach (var kv in players)
            kv.Value.hasVotedThisEntry = false;
    }

    void AdvanceVoteTargetOrFinish()
    {
        currentEntryIndex++;

        if (currentEntryIndex >= entries.Count)
        {
            // Finished all entries
            currentState = SceneState.Results;
            responsesReceived = 0;
            Debug.Log("[Server] Voting complete -> Results");
            return;
        }

        ResetVotesForCurrentEntry();
    }

    // ------------------------
    // Sending state to clients
    // ------------------------
    void SendStateToAll()
    {
        foreach (var conn in players.Keys)
            SendStateTo(conn);
    }

    void SendStateTo(IWebSocketConnection conn)
    {
        Player p = players[conn];

        GameState state = new GameState
        {
            scene = currentState.ToString(),
            isFirst = p.isFirst,

            theme = theme,
            prompt = BuildPromptTextForClient(p),

            // Prompt template (so client can display it if desired)
            taglineTemplate = prompt,

            // Review assignment for this player
            assignedEntryIndex = p.assignedEntryIndex,
            assignedTagline = GetAssignedTaglineFor(p),
            reviewRating = (int)p.rating,
            reviewRatingLabel = p.rating.ToString(),

            // Vote display data
            entryIndex = currentEntryIndex,
            entryCount = entries != null ? entries.Count : 0,
            currentTagline = GetCurrentEntryTagline(),
            currentReview = GetCurrentEntryReview(),
            currentReviewRating = GetCurrentEntryRatingLabel(),

            // Buttons (ints) for Vote phase only
            starButtons = (currentState == SceneState.Vote) ? starButtons : null,

            // Results
            resultsText = (currentState == SceneState.Results) ? BuildResultsSummary() : null
        };

        try
        {
            conn.Send(JsonUtility.ToJson(state));
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Server] SendStateTo failed for a connection: " + ex.Message);
        }
    }

    string BuildPromptTextForClient(Player p)
    {
        // Keep this very simple; the phone UI can display more specific instructions
        switch (currentState)
        {
            case SceneState.Lobby:
                return "Enter your name to join.";

            case SceneState.Prompt:
                return "Fill in the two blanks for the tagline.";

            case SceneState.Review:
                return "Write a review that matches your assigned rating.";

            case SceneState.Vote:
                return "Rate the current entry 1-5.";

            case SceneState.Results:
                return "Results";

            case SceneState.Theme:
                return $"Theme: {theme}\nNext prompt in {themeDurationSeconds:0} seconds...";

            default:
                return "";
        }
    }

    string GetAssignedTaglineFor(Player p)
    {
        if (entries == null || entries.Count == 0) return "";
        if (p.assignedEntryIndex < 0 || p.assignedEntryIndex >= entries.Count) return "";
        return entries[p.assignedEntryIndex].promptFinal;
    }

    string GetCurrentEntryTagline()
    {
        if (entries == null || entries.Count == 0) return "";
        if (currentEntryIndex < 0 || currentEntryIndex >= entries.Count) return "";
        return entries[currentEntryIndex].promptFinal;
    }

    string GetCurrentEntryReview()
    {
        if (entries == null || entries.Count == 0) return "";
        if (currentEntryIndex < 0 || currentEntryIndex >= entries.Count) return "";
        return entries[currentEntryIndex].reviewText;
    }

    string GetCurrentEntryRatingLabel()
    {
        if (entries == null || entries.Count == 0) return "";
        if (currentEntryIndex < 0 || currentEntryIndex >= entries.Count) return "";
        return entries[currentEntryIndex].reviewRating.ToString();
    }

    string BuildResultsSummary()
    {
        if (entries == null || entries.Count == 0) return "No entries.";

        var ranked = entries
            .Select(e => new { name = e.playerName, avg = e.AverageStars })
            .OrderByDescending(x => x.avg)
            .ToList();

        int count = Mathf.Min(3, ranked.Count);
        string s = "";
        for (int i = 0; i < count; i++)
        {
            s += $"{i + 1}) {ranked[i].name} - {ranked[i].avg:0.00}\n";
        }
        return s.TrimEnd();
    }

    // ------------------------
    // Ordering
    // ------------------------
    void RebuildOrderedConnections()
    {
        orderedConnections = players.Keys.ToList();

        // Stable-ish ordering by name, then hash
        orderedConnections.Sort((a, b) =>
        {
            string an = players[a].name ?? "";
            string bn = players[b].name ?? "";
            int c = string.Compare(an, bn);
            if (c != 0) return c;
            return a.GetHashCode().CompareTo(b.GetHashCode());
        });
    }

    // ------------------------
    // DTOs
    // ------------------------
    [System.Serializable]
    class Incoming
    {
        public string type; // join/start/input/choice
        public string name; // join

        // review submission
        public string text;

        // vote selection 0..4
        public int index;

        // Prompt blanks
        public string a;
        public string b;
    }

    [System.Serializable]
    class GameState
    {
        public string scene;
        public bool isFirst;

        public string theme;
        public string prompt;

        // Prompt template
        public string taglineTemplate;

        // Review assignment
        public int assignedEntryIndex;
        public string assignedTagline;
        public int reviewRating;
        public string reviewRatingLabel;

        // Vote target data (what the host should show)
        public int entryIndex;
        public int entryCount;
        public string currentTagline;
        public string currentReview;
        public string currentReviewRating;

        // Vote buttons: int[] 1..5
        public int[] starButtons;

        // Results
        public string resultsText;
    }
}
[Serializable]
public class ThemePrompt
{
    public string theme;
    [TextArea] public string promptTemplate;
}
