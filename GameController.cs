using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SocketIO;
using UnityEngine.UI;

public class GameController : MonoBehaviour {

	private SocketIOComponent socket;

	public string Name = "Coop";
	public InputField ChatMessageInput;
	public Text PlayerListText;
	public Text ChatMessageText;

	public GameObject[] BoardSpaces;
	public GameObject XPrefab, OPrefab;
	public Text GameStatusText;
	public Text GameMatchText;
	public Button PlayButton;

	private string gameId = "";
	private int myPieceId = -1;
	private bool isMyTurn = false;
	private string opponentsId = "";
	private List<GameObject> boardPieces = new List<GameObject>();

	private Dictionary<string,string> players = new Dictionary<string,string>();

	void Start () {
		socket = GameObject.Find ("SocketIO").GetComponent<SocketIOComponent> ();
		socket.On ("chatMessage", OnChatMessage); //Listen for chat messages

		socket.On ("players", OnPlayers);
		socket.On ("playerJoined", OnPlayerJoined);
		socket.On ("playerLeft", OnPlayerLeft);

		socket.On ("startGame", OnStartGame);
		socket.On ("takeTurn", OnTakeTurn);
		socket.On ("gameFinish", OnGameFinish);

		Debug.Log("Started");
		StartCoroutine (WaitForConnection ());

		Screen.SetResolution (640, 400,false);
	}

	private IEnumerator WaitForConnection() {

		while (socket.sid == null) {
			yield return null;
		}
		Debug.Log ("Connection complete.");
		JSONObject json = new JSONObject (JSONObject.Type.STRING);
		json.Add (Name);
		socket.Emit ("join", json);
	}

	private void OnDestroy() {
		socket.Close (); //Close connection
	}

	private void OnPlayers(SocketIOEvent e) {
		JSONObject playersJson = e.data.GetField ("players");
		foreach (string plyrId in playersJson.keys) {
			string name = playersJson.GetField (plyrId).ToString().Trim('\"');
			PlayerListText.text += name + "\n";
			players [plyrId] = name;
		}
	}

	private void OnPlayerJoined(SocketIOEvent e) {
		string id = e.data.GetField("id").ToString().Trim('\"');
		string name = e.data.GetField ("name").ToString ().Trim ('\"');
		Debug.Log (name + " has joined.");
		players [id] = name;
		PlayerListText.text += name + "\n";
	}

	private void OnPlayerLeft(SocketIOEvent e) {
		string id = e.data.GetField("id").ToString().Trim('\"');
		if (players.ContainsKey (id)) {
			string name = players [id];
			PlayerListText.text = PlayerListText.text.Replace (name+"\n", "");
		}
	}

	public void SendMessagePress() { //Send your message to the server
		string message = ChatMessageInput.text;

		//The json you want to send should include your name and the message
		JSONObject json = new JSONObject ();
		json.AddField ("name", Name);
		json.AddField ("message", message);

		socket.Emit ("chatMessage", json);

		ChatMessageInput.text = ""; //Clear input after
	}

	private void OnChatMessage(SocketIOEvent e) {
		string name = e.data.GetField("name").ToString().Trim('\"');
		string message = e.data.GetField ("message").ToString ().Trim ('\"');

		ChatMessageText.text += "\n" + name + ": " + message;
	}

	public void PlayButtonPress() {
		PlayButton.gameObject.SetActive (false);
		ResetBoard ();
		GameStatusText.text = "Waiting for match..";
		socket.Emit ("playGame");
	}

	private void OnStartGame(SocketIOEvent e) {
		string xPlayerId = e.data.GetField("xPlayerId").ToString().Trim('\"');
		string oPlayerId = e.data.GetField ("oPlayerId").ToString ().Trim ('\"');
		string thisGameId = e.data.GetField ("gameId").ToString ().Trim ('\"');

		if (xPlayerId.Equals (socket.sid)) {
			myPieceId = 0;
			opponentsId = oPlayerId;
			GameMatchText.text = "Me (X) vs " + players [oPlayerId] + " (O)";
			isMyTurn = true;
		} else if (oPlayerId.Equals (socket.sid)) {
			myPieceId = 1;
			opponentsId = xPlayerId;
			GameMatchText.text = players [xPlayerId] + " (X) vs Me (O)";
		}

		if (myPieceId >= 0) {
			gameId = thisGameId;
			SetTurnText ();
		}
	}

	private void SetTurnText() {
		if (isMyTurn) {
			GameStatusText.text = "My turn!";
		} else {
			GameStatusText.text = "Opponents turn!";
		}
	}

	public void BoardSpacePress(int id) {
		if (isMyTurn) {
			isMyTurn = false;
			JSONObject data = new JSONObject ();
			data.AddField ("space", id);
			data.AddField ("pieceId", myPieceId);
			data.AddField ("gameId", gameId);
			socket.Emit ("takeTurn", data);
		}
	}

	private void OnTakeTurn(SocketIOEvent e) {
		string thisGameId = e.data.GetField("gameId").ToString().Trim('\"');
		if (gameId.Equals(thisGameId)) {
			int space = int.Parse(e.data.GetField ("space").ToString ());
			int pieceId = int.Parse(e.data.GetField ("pieceId").ToString ());
			placeOnBoard (space, pieceId);

			if (pieceId != myPieceId) { //Opponent just took turn, my turn now
				isMyTurn = true;
			}

			SetTurnText ();
		}
	}

	private void placeOnBoard(int space, int pieceId) {
		GameObject obj = (pieceId == 0) ? Instantiate (XPrefab) : Instantiate (OPrefab);
		obj.transform.SetParent (GameObject.Find ("GameBoard").transform, false);
		obj.transform.position = BoardSpaces [space].transform.position;
		boardPieces.Add (obj);
	}

	private void OnGameFinish(SocketIOEvent e) {
		string thisGameId = e.data.GetField("gameId").ToString().Trim('\"');
		if (gameId.Equals(thisGameId)) { //For my game
			int pieceIdWinner = int.Parse (e.data.GetField ("winner").ToString ());

			//Place last piece
			int space = int.Parse(e.data.GetField ("space").ToString ());
			int pieceId = int.Parse(e.data.GetField ("pieceId").ToString ());
			placeOnBoard (space, pieceId);

			if (pieceIdWinner < 0) {
				GameStatusText.text = "Tie Game!";
			} else if (pieceIdWinner == myPieceId) {
				GameStatusText.text = "You win!";
			} else {
				GameStatusText.text = players [opponentsId] + " wins.";
			}
			GameMatchText.text = "Click play to play again!";
			PlayButton.gameObject.SetActive (true);

			myPieceId = -1;
			opponentsId = "";
		}
	}

	private void ResetBoard() {
		foreach (GameObject piece in boardPieces) {
			Destroy (piece);
		}
		boardPieces.Clear ();
		GameStatusText.text = "";
		GameMatchText.text = "";
	}
}
