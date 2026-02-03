// StageManagerServer.cs
// Author: ChatGPT
// Description:
//   This Unity MonoBehaviour hosts a minimal WebSocket server for a multiplayer game.
//   The server manages connected players, tracks the current game phase (scene),
//   collects player input and votes, and broadcasts state updates to clients.

using UnityEngine;
using Fleck;               // WebSocket server library
using System.Collections.Generic;

public class StageManagerServer : MonoBehaviour
{
	// ------------------------
	// Inspector-exposed fields
	// ------------------------
	[Header("Network Settings")]
	public string listenAddress = "0.0.0.0";	  // Address to listen on (0.0.0.0 binds all interfaces)
	public int port = 8080;						  // Port for WebSocket connections

	// ------------------------
	// Player data structure
	// ------------------------
	class Player
	{
		public string name;      // The display name of the player
		public bool isFirst;     // Flag indicating if this is the first player to join
		public string lastInput; // Stores the text submitted in the Prompt phase
		public int? lastVote;    // Stores the index of the button the player chose in the Vote phase
	}

	// Dictionary mapping each WebSocket connection to its Player object
	// All player state for the current round is stored here
	private Dictionary<IWebSocketConnection, Player> players = new Dictionary<IWebSocketConnection, Player>();

	// ------------------------
	// Game state tracking
	// ------------------------
	// Enumeration of the different game phases (scenes)
	public enum SceneState { Lobby, Prompt, Vote, Wait }

	// Current scene
	public SceneState currentState = SceneState.Lobby;

	// Tracks whether the first player has already been assigned
	private bool firstAssigned = false;

	// Fleck WebSocket server instance
	private WebSocketServer server;

	// ------------------------
	// Auto-advance tracking
	// ------------------------
	private int responsesReceived = 0; // counts inputs or votes in the current scene


	// ------------------------
	// Unity Start method
	// Initializes the WebSocket server
	// ------------------------
	void Start()
	{
		string url = $"ws://{listenAddress}:{port}";
		server = new WebSocketServer(url);

		// Start listening for connections
		server.Start(socket =>
		{
			// ---------------------------------
			// Client connected
			// ---------------------------------
			socket.OnOpen = () =>
			{
				Player p = new Player();

				// Assign the first player flag if not yet assigned
				if (!firstAssigned)
				{
					p.isFirst = true;
					firstAssigned = true;
					Debug.Log("First player assigned");
				}

				// Add the new player to the dictionary
				players[socket] = p;

				// Send initial state to this client (Lobby)
				SendStateTo(socket);
			};

			// ---------------------------------
			// Client disconnected
			// ---------------------------------
			socket.OnClose = () =>
			{
				// Remove player from dictionary when they disconnect
				players.Remove(socket);
			};

			// ---------------------------------
			// Message received from client
			// ---------------------------------
			socket.OnMessage = message =>
			{
				// Deserialize incoming JSON to the Incoming class
				Incoming msg = JsonUtility.FromJson<Incoming>(message);

				if (!players.ContainsKey(socket)) return; // Safety check

				Player p = players[socket];
				string playerName = string.IsNullOrEmpty(p.name) ? "(unnamed)" : p.name;

				// Handle message based on type
				switch (msg.type)
				{
					case "join":
						// Player submitted their name in the Lobby
						p.name = msg.name;
						Debug.Log($"[Server] Received join from {msg.name}");
						break;

					case "start":
						// First player pressed "Start Game" button
						Debug.Log($"[Server] Received start from {playerName}");
						if (p.isFirst && currentState == SceneState.Lobby)
							AdvanceState();
						break;

					case "input":
						// Player submitted text in Prompt phase
						if (currentState == SceneState.Prompt)
							p.lastInput = msg.text;
						Debug.Log($"[Server] Received input from {playerName}: \"{msg.text}\"");
						IncrementAndCheckForAdvance();
						break;

					case "choice":
						// Player submitted a vote in Vote phase
						if (currentState == SceneState.Vote)
							p.lastVote = msg.index;
						Debug.Log($"[Server] Received vote from {playerName}: button index {msg.index}");
						IncrementAndCheckForAdvance();
						break;

					default:
						// Unknown message type
						Debug.Log($"[Server] Received unknown message type '{msg.type}' from {playerName}");
						break;
				}
			};
		});

		Debug.Log($"StageManagerServer started on {url}");
	}

	// ------------------------
	// Increment the counter and auto-advance if all players responded
	// ------------------------
	void IncrementAndCheckForAdvance()
	{
		responsesReceived++;

		// Only auto-advance for Prompt or Vote scenes
		if ((currentState == SceneState.Prompt || currentState == SceneState.Vote)
			&& responsesReceived >= players.Count)
		{
			Debug.Log("All players responded; auto-advancing...");
			AdvanceState();
		}
	}

	// ------------------------
	// Advance the game state
	// ------------------------
	// Moves from one scene to the next and broadcasts to all clients
	public void AdvanceState()
	{
		responsesReceived = 0;	// clear for next collection

		// Cycle through states in order: Lobby -> Prompt -> Vote -> Wait -> Prompt -> ...
		switch (currentState)
		{
			case SceneState.Lobby:
				currentState = SceneState.Prompt;
				break;
			case SceneState.Prompt:
				currentState = SceneState.Vote;
				break;
			case SceneState.Vote:
				currentState = SceneState.Wait;
				break;
			case SceneState.Wait:
				currentState = SceneState.Prompt; // wrap around after Wait
				break;
		}

		Debug.Log("Advanced to " + currentState);

		// Broadcast updated state to all connected clients
		SendStateToAll();
	}

	// ------------------------
	// Broadcast state to all clients
	// ------------------------
	void SendStateToAll()
	{
		foreach (var conn in players.Keys)
			SendStateTo(conn);
	}

	// ------------------------
	// Send state to a single client
	// ------------------------
	void SendStateTo(IWebSocketConnection conn)
	{
		Player p = players[conn];

		// Determine the prompt text dynamically based on current scene
		string promptText;
		switch (currentState)
		{
			case SceneState.Lobby:
				promptText = "Enter your name to join.";
				break;
			case SceneState.Prompt:
				promptText = "Type a line of dialogue.";
				break;
			case SceneState.Vote:
				promptText = "Vote for the funniest line.";
				break;
			case SceneState.Wait:
				promptText = "Waiting for the next round...";
				break;
			default:
				promptText = "";
				break;
		}

		// Create the GameState object to send
		GameState state = new GameState
		{
			scene = currentState.ToString(),                    // Current scene name as string
			prompt = promptText,                                // Prompt text for this scene
			isFirst = p.isFirst,                                // Whether this client is the first player
			buttons = currentState == SceneState.Vote ? BuildVoteButtons() : null // Vote button labels if Vote phase
		};

		// Serialize to JSON and send to the client
		conn.Send(JsonUtility.ToJson(state));
	}

	// ------------------------
	// Build button labels for Vote phase
	// ------------------------
	private List<string> BuildVoteButtons()
	{
		List<string> buttons = new List<string>();

		// Collect the lastInput from every player as a vote option
		foreach (var pl in players.Values)
		{
			if (!string.IsNullOrEmpty(pl.lastInput))
				buttons.Add(pl.lastInput);
		}

		return buttons;
	}

	// ------------------------
	// Class representing messages received from clients
	// ------------------------
	[System.Serializable]
	class Incoming
	{
		public string type; // "join", "start", "input", "choice"
		public string name; // Player's name (for join)
		public string text; // Text submitted (for input)
		public int index;   // Button index (for choice)
	}

	// ------------------------
	// Class representing the game state sent to clients
	// ------------------------
	[System.Serializable]
	class GameState
	{
		public string scene;       // Current scene name
		public string prompt;      // Prompt text
		public bool isFirst;       // Whether this client is first player
		public List<string> buttons; // Vote options if in Vote phase
	}
}
