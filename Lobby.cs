using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

using Sfs2X;
using Sfs2X.Core;
using Sfs2X.Entities;
using Sfs2X.Entities.Variables;
using Sfs2X.Requests;
using Sfs2X.Logging;


public class Lobby : MonoBehaviour {

	private SmartFox smartFox;
	private string zone = "SimpleChat";
    private string serverName = "127.0.0.1";
	private int serverPort = 9933;
	private string username = "Halycon";
	private string loginErrorMessage = "";
	private bool isLoggedIn = false;
	
	private string newMessage = "";
	private ArrayList messages = new ArrayList();
		
	public GUISkin gSkin;
	
	//keep track of room we're in
	private Room currentActiveRoom;
	public Room CurrentActiveRoom{ get {return currentActiveRoom;} 
								   set {currentActiveRoom = value;}}
				
	private Vector2 roomScrollPosition, userScrollPosition, chatScrollPosition;
	private int roomSelection = -1;	  //For clicking on list box 
	private string[] roomNameStrings; //Names of rooms
	private string[] roomFullStrings; //Names and descriptions
	private int screenW;
	
	// Changed
	// Holds the user that sent a message last
	private User lastPMSender;
	
	// Popup window for room creation
	private Rect createRoom = new Rect(0, 0, 300, 300);
	
	// Skin for room creation
	public GUISkin createRoomSkin;
	
	// Toggles the drawing of the create room window
	private bool showCreateRoom = true;
	
	// Values for new room creation
	private string newRoomName = "Blackjack";
	private string newRoomPlayers = "2";
	private string newRoomType = "";
	
	void Start()
	{
		//Security.PrefetchSocketPolicy(serverName, serverPort); 
		bool debug = true;
		if (SmartFoxConnection.IsInitialized)
		{
			//If we've been here before, the connection has already been initialized. 
			//and we don't want to re-create this scene, therefore destroy the new one
			smartFox = SmartFoxConnection.Connection;
			Destroy(gameObject); 
		}
		else
		{
			//If this is the first time we've been here, keep the Lobby around
			//even when we load another scene, this will remain with all its data
			smartFox = new SmartFox(debug);
			DontDestroyOnLoad(gameObject);
		}
	
		smartFox.AddLogListener(LogLevel.INFO, OnDebugMessage);
		screenW = Screen.width;
		
		createRoom.x = Screen.width/2 - createRoom.width/2;
		createRoom.y = Screen.height/2 - createRoom.height/2;
		
		// Changed
		lastPMSender = null;
		
		AddEventListeners();
		//smartFox.Connect(serverName, serverPort);
	}
	
	private void AddEventListeners() {
		
		smartFox.RemoveAllEventListeners();
		
		smartFox.AddEventListener(SFSEvent.CONNECTION, OnConnection);
		smartFox.AddEventListener(SFSEvent.CONNECTION_LOST, OnConnectionLost);
		smartFox.AddEventListener(SFSEvent.LOGIN, OnLogin);
		smartFox.AddEventListener(SFSEvent.LOGIN_ERROR, OnLoginError);
		smartFox.AddEventListener(SFSEvent.LOGOUT, OnLogout);
		smartFox.AddEventListener(SFSEvent.ROOM_JOIN, OnJoinRoom);
		smartFox.AddEventListener(SFSEvent.PUBLIC_MESSAGE, OnPublicMessage);
		smartFox.AddEventListener(SFSEvent.PRIVATE_MESSAGE, OnPrivateMessage);
		smartFox.AddEventListener(SFSEvent.USER_ENTER_ROOM, OnUserEnterRoom);
		smartFox.AddEventListener(SFSEvent.USER_EXIT_ROOM, OnUserLeaveRoom);
		smartFox.AddEventListener(SFSEvent.USER_COUNT_CHANGE, OnUserCountChange);
		smartFox.AddEventListener(SFSEvent.ROOM_ADD, OnRoomAdded);
		smartFox.AddEventListener(SFSEvent.ROOM_CREATION_ERROR, OnRoomCreationError);
	}
	
	void FixedUpdate() {
		//this is necessary to have any smartfox action!
		smartFox.ProcessEvents();
	}
	
	// Changed
	// Checks a message to see if it is a valid command, and executes
	// the appropriate function
	private void CheckCommands(string message)
	{
		char delimiter = ' ';
		string[] messageList = message.Split(delimiter);
		string type = messageList[0];
		
		switch(type)
		{
			case "/w":
				if (messageList.Length >= 3)
				{
					string target = messageList[1];	
					User recipient = (User)smartFox.UserManager.GetUserByName(target);
				
					string privateMessage = messageList[2];
					for (int i = 3; i < messageList.Length; i++)
					{
						privateMessage += (" " + messageList[i]);
					}
				
					smartFox.Send(new PrivateMessageRequest(privateMessage, recipient.Id)); 
					messages.Add("To " + target + ": " + privateMessage);
				}
				break;
			case "/r":
				if (messageList.Length >= 2 && lastPMSender != null)
				{
					string reply = messageList[1];
					for (int i = 2; i < messageList.Length; i++)
					{
						reply += (" " + messageList[i]);
					}
					smartFox.Send(new PrivateMessageRequest(reply, lastPMSender.Id)); 
					messages.Add("To " + lastPMSender.Id + ": " + reply);
				}
				break;
			case "/join":
				if (messageList.Length >= 2)
				{
					string joinName = messageList[1];
					for (int i = 2; i < messageList.Length; i++)
					{
						joinName += (" " + messageList[i]);
					}
					Room targetJoin = (Room)smartFox.RoomManager.GetRoomByName(joinName);
					messages.Add("You joined " + joinName);
					smartFox.Send(new JoinRoomRequest(targetJoin));
				}
				break;
			case "/create":
				createRoom.x = Screen.width/2 - createRoom.width/2;
				createRoom.y = Screen.height/2 - createRoom.height/2;
				showCreateRoom = true;
				break;
			case "/info":
				if (messageList.Length >= 2)
				{
					string infoName = messageList[1];
					for (int i = 2; i < messageList.Length; i++)
					{
						infoName += (" " + messageList[i]);
					}
					Room targetInfo = (Room)smartFox.RoomManager.GetRoomByName(infoName);

					messages.Add("Room Name: " + targetInfo.Name);
					messages.Add("Variables Count: " + targetInfo.GetVariables().Count);
				}	
				break;
			default:
				messages.Add("No such command: " + messageList[0]);
				break;
		}
	}
	
	// Changed
	// A window to use for room creation
	private void onCreateWindow(int windowID)
	{
		GUI.skin = createRoomSkin;
		GUI.DragWindow(new Rect(0, 0, createRoom.width, 60));
		
		if (createRoom.x + createRoom.width > Screen.width)
		{
			createRoom.x = Screen.width - createRoom.width;
		}
		else if (createRoom.x < 0)
		{
			createRoom.x = 0;
		}
		
		if (createRoom.y + createRoom.height > Screen.height)
		{
			createRoom.y = Screen.height - createRoom.height;
		}
		else if (createRoom.y <0)
		{
			createRoom.y = 0;
		}
		
		newRoomName = GUI.TextField(new Rect(20, 70, 200, 30), newRoomName, 20);
		newRoomPlayers = GUI.TextField(new Rect(20, 130, 200, 30), newRoomPlayers, 20);
		newRoomType = GUI.TextField(new Rect(20, 190, 200, 30), newRoomType, 20);
	
		GUI.TextArea(new Rect(20, 40, 200, 30), "New Room Name:");
		GUI.TextArea(new Rect(20, 100, 200, 30), "Max Players:");
		GUI.TextArea(new Rect(20, 160, 200, 30), "Room Stage:");
		
		if (GUI.Button(new Rect(createRoom.width/2 - 110, 240, 100, 30), "Create!") ||
			(Event.current.type == EventType.keyDown && Event.current.character == '\n'))
		{
			short numMaxPlayers = 0;
			
			if (Int16.TryParse(newRoomPlayers, out numMaxPlayers))
			{
				RoomSettings settings = new RoomSettings(newRoomName);
				settings.MaxUsers = numMaxPlayers;
				
				List<RoomVariable> variablesArray = new List<RoomVariable>();
				RoomVariable newRoomVariable = new SFSRoomVariable(newRoomType, null);
				variablesArray.Add(newRoomVariable);
				//settings.Variables = variablesArray;
				
				smartFox.Send(new CreateRoomRequest(settings));
			
				newRoomName = "";
				newRoomPlayers = "";
				newRoomType = "";
				showCreateRoom = false;
			}
			else
			{
				newRoomPlayers = "";
			}
		}
		
		if (GUI.Button(new Rect(createRoom.width/2 + 10, 240, 100, 30), "Cancel"))
		{
			newRoomName = "";
			newRoomPlayers = "";
			newRoomType = "";
			showCreateRoom = false;
		}
	}
	
	private void UnregisterSFSSceneCallbacks() {
		smartFox.RemoveAllEventListeners();
	}
	
	public void OnConnection(BaseEvent evt) {
		bool success = (bool)evt.Params["success"];
		string error = (string)evt.Params["errorMessage"];
		
		Debug.Log("On Connection callback got: " + success + " (error : <" + error + ">)");

		if (success) 
        {
			SmartFoxConnection.Connection = smartFox;

			Debug.Log("Sending login request");
			smartFox.Send(new LoginRequest(username, "", zone));
		}
	}

	public void OnConnectionLost(BaseEvent evt) {
		Debug.Log("OnConnectionLost");
		isLoggedIn = false;
		currentActiveRoom = null;
		UnregisterSFSSceneCallbacks();
	}

	// Various SFS callbacks
	public void OnLogin(BaseEvent evt) {
		try {
			if (evt.Params.ContainsKey("success") && !(bool)evt.Params["success"]) {
				loginErrorMessage = (string)evt.Params["errorMessage"];
				Debug.Log("Login error: "+loginErrorMessage);
			}
			else {
				Debug.Log("Logged in successfully");
				PrepareLobby();	
			}
		}
		catch (Exception ex) {
			Debug.Log("Exception handling login request: "+ex.Message+" "+ex.StackTrace);
		}
	}

	public void OnLoginError(BaseEvent evt) {
		Debug.Log("Login error: "+(string)evt.Params["errorMessage"]);
	}
	
	void OnLogout(BaseEvent evt) {
		Debug.Log("OnLogout");
		isLoggedIn = false;
		currentActiveRoom = null;
		Application.LoadLevel("The Lobby");
		smartFox.Disconnect();
	}
	
	public void OnDebugMessage(BaseEvent evt) {
		string message = (string)evt.Params["message"];
		Debug.Log("[SFS DEBUG] " + message);
	}

	public void OnRoomAdded(BaseEvent evt){
		SetupRoomList();
	}
	public void OnRoomCreationError(BaseEvent evt){
		Debug.Log("Room creation failed: " + (string)evt.Params["errorMessage"]);
	}
	
	public void OnJoinRoom(BaseEvent evt)
	{
		Room room = (Room)evt.Params["room"];
		currentActiveRoom = room;
		// Changed
		if (currentActiveRoom.Name != "The Lobby")
		{
			Application.LoadLevel(room.Name);
			messages.Add("You joined " + room.Name);
		}
	}
	
	public void OnUserEnterRoom(BaseEvent evt) {
		User user = (User)evt.Params["user"];
			messages.Add( user.Name + " has entered the room.");
	}

	private void OnUserLeaveRoom(BaseEvent evt) {
		User user = (User)evt.Params["user"];
		if(user.Name!=username){
			messages.Add( user.Name + " has left the room.");
		}	
	}

	public void OnUserCountChange(BaseEvent evt) {
		Room room = (Room)evt.Params["room"];
		if (room.IsGame ) {
			SetupRoomList();
		}
	}
	
	void OnPublicMessage(BaseEvent evt) {
		try {
			string message = (string)evt.Params["message"];
			User sender = (User)evt.Params["sender"];
			messages.Add(sender.Name +": "+ message);
			
			chatScrollPosition.y = Mathf.Infinity;
			Debug.Log("User " + sender.Name + " said: " + message);
		}
		catch (Exception ex) {
			Debug.Log("Exception handling public message: "+ex.Message+ex.StackTrace);
		}
	}
	
	void OnPrivateMessage(BaseEvent evt) {
		try {
			string message = (string)evt.Params["message"];
			User sender = (User)evt.Params["sender"];
			
			// Changed
			if (sender.Name != smartFox.MySelf.Name)
			{
				messages.Add(sender.Name + " whispered: " + message);
				
				lastPMSender = sender;
				
				chatScrollPosition.y = Mathf.Infinity;
				Debug.Log(sender.Name + " whispered: " + message);
			}
		}
		catch (Exception ex) {
			Debug.Log("Exception handling private message: "+ex.Message+ex.StackTrace);
		}
	}
	
	private void PrepareLobby() {
		Debug.Log("Setting up the lobby");
		SetupRoomList();
		isLoggedIn = true;
	}
	
	
	void OnGUI() {
		if (smartFox == null) return;
		
		GUI.skin = gSkin;
		
		// Login
		if (!isLoggedIn) {
			GUI.Label(new Rect(2, -2, 680, 70), "", "SFSLogo");
			DrawLoginGUI();
		}
		else if (currentActiveRoom != null) 
		{
			// ****** Show full interface only in the Lobby ******* //
			if(currentActiveRoom.Name == "The Lobby")
			{
				GUI.Label(new Rect(2, -2, 680, 70), "", "SFSLogo");
				DrawUsersGUI();	
				DrawChatGUI();
	
				// Send message
				newMessage = GUI.TextField(new Rect(10, 480, 370, 20), newMessage, 50);
				
				if (GUI.Button(new Rect(390, 478, 90, 24), "Send")  || (Event.current.type == EventType.keyDown && Event.current.character == '\n'))
				{
					// Changed
					// Checks to message to see if it's a valid command
					if (newMessage.StartsWith("/"))
					{
						CheckCommands(newMessage);
						newMessage = "";
					}
					else
					{
						smartFox.Send( new PublicMessageRequest(newMessage) );
						newMessage = "";
					}
				}	
				
				// Changed
				if (showCreateRoom)
				{
					GUI.skin = createRoomSkin;
					//createRoom.x = Screen.width/2 - createRoom.width/2;
					//createRoom.y = Screen.height/2 - createRoom.height/2;
					createRoom = GUI.Window(0, createRoom, onCreateWindow, "Create a new room");
				}
				
				// ****** In other rooms, just show roomlist for switching ******* //
				GUI.skin = gSkin;
				DrawRoomsGUI();
			}
		}
		
		// Logout button
		if (GUI.Button (new Rect (screenW - 115, 20, 85, 24), "Logout")) 
		{
			Application.Quit();
			smartFox.Send( new LogoutRequest() );
		}
	}
	
	
	private void DrawLoginGUI(){
		GUI.Label(new Rect(10, 90, 100, 100), "Username: ");
		username = GUI.TextField(new Rect(100, 90, 200, 20), username, 25); 
	
		GUI.Label(new Rect(10, 180, 100, 100), "Server: ");
		serverName = GUI.TextField(new Rect(100, 180, 200, 20), serverName, 25);

		GUI.Label(new Rect(10, 210, 100, 100), "Port: ");
		serverPort = int.Parse(GUI.TextField(new Rect(100, 210, 200, 20), serverPort.ToString(), 4));

		GUI.Label(new Rect(10, 240, 100, 100), loginErrorMessage);

		if (GUI.Button(new Rect(100, 270, 100, 24), "Login")  || 
	    (Event.current.type == EventType.keyDown && Event.current.character == '\n'))
		{
			AddEventListeners();
			smartFox.Connect(serverName, serverPort);
		}	
	}
	
	private void DrawUsersGUI(){
		GUI.Box (new Rect (screenW - 200, 80, 180, 170), "Users");
		GUILayout.BeginArea (new Rect (screenW - 190, 110, 150, 160));
			userScrollPosition = GUILayout.BeginScrollView (userScrollPosition, GUILayout.Width (150), GUILayout.Height (130));
			GUILayout.BeginVertical ();
			
				List<User> userList = currentActiveRoom.UserList;
				foreach (User user in userList) {
					GUILayout.Label (user.Name); 
				}
			GUILayout.EndVertical ();
			GUILayout.EndScrollView ();
		GUILayout.EndArea ();
	}
	
	private void DrawRoomsGUI(){
		int screenW = Screen.width;
		GUI.Box (new Rect (screenW - 200, 260, 180, 130), "Room List");
		GUILayout.BeginArea (new Rect (screenW - 190, 290, 180, 150));
		if (smartFox.RoomList.Count != 1) {		
			roomScrollPosition = GUILayout.BeginScrollView (roomScrollPosition, GUILayout.Width (150), GUILayout.Height (160));
			
			roomSelection = GUILayout.SelectionGrid (roomSelection, roomFullStrings, 1, "RoomListButton");
			if (roomSelection >= 0 && roomNameStrings[roomSelection] != currentActiveRoom.Name) {

				smartFox.Send(new JoinRoomRequest(roomNameStrings[roomSelection]));
			}
			GUILayout.EndScrollView ();
			
		} else {
			GUILayout.Label ("No rooms available to join");
		}	
		
		GUILayout.EndArea();
	}
	
	private void DrawChatGUI(){
		GUI.Box(new Rect(10, 80, 470, 390), "Chat");

		GUILayout.BeginArea (new Rect(20, 110, 450, 350));
			chatScrollPosition = GUILayout.BeginScrollView (chatScrollPosition, GUILayout.Width (450), GUILayout.Height (350));
				GUILayout.BeginVertical();
					foreach (string message in messages) {
						//this displays text from messages arraylist in the chat window
						GUILayout.Label(message);
				}
				GUILayout.EndVertical();
			GUILayout.EndScrollView ();
		GUILayout.EndArea();		
	}

	private void SetupRoomList () {
		List<string> rooms = new List<string> ();
		List<string> roomsFull = new List<string> ();
		
		List<Room> allRooms = smartFox.RoomManager.GetRoomList();
		
		foreach (Room room in allRooms) {
			rooms.Add(room.Name);
			roomsFull.Add(room.Name + " (" + room.UserCount + "/" + room.MaxUsers + ")");
		}
		
		roomNameStrings = rooms.ToArray();
		roomFullStrings = roomsFull.ToArray();
		
		if (smartFox.LastJoinedRoom==null) {
			smartFox.Send(new JoinRoomRequest("The Lobby"));
		}
	}
}
