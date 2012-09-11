using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

using Sfs2X;
using Sfs2X.Core;
using Sfs2X.Entities;
using Sfs2X.Entities.Data;
using Sfs2X.Requests;
using Sfs2X.Protocol.Serialization;
using Sfs2X.Util;

public class BlackjackScript: MonoBehaviour 
{
	// Smartfox connection
	private SmartFox smartFox;
	
	// Stores this room
	private Room blackjackRoom;

	// Holds the cards in the deck
	private List<Card> decks;
	
	// Input field to determine number of decks we're playing with
	private int numberOfDecks = 1;
	
	// Tracks how many cards there are in the deck
	private int cardsRemaining;
	
	// Holds the dealer's hand
	private PlayerInfo house;
	
	// Holds information about the current player and other players
	private PlayerInfo myPlayer = null;
	private List<PlayerInfo> players;
	
	// Bools to track the game state flow
	// Start -> Bet -> Deal -> Decision -> Bet
	private bool startState = false;
	private bool betState = false;
	private bool dealState = false;
	private bool decisionState = false;
	private bool endState = false;
	
	// String to output gamestate
	private string stateString = "";
	
	// A list to keep track of who made a decision in order to
	// advance to the next step
	private List<bool> decisionFlags;
	
	// Styles for the GUI: Set in inspector
	public GUIStyle boxStyle;
	public GUIStyle infoStyle;
	public GUIStyle blackCardStyle;
	public GUIStyle redCardStyle;
	public GUIStyle blankCardStyle;
	public GUIStyle labelStyle;
	public GUIStyle playerStyle;
	public GUIStyle greenButton;
	public GUIStyle redButton;
	public GUIStyle gunMetalButton;
	
	// Dimensions for main UI box
	float mbWidth = 500.0f;
	float mbHeight = 700.0f;
	
	// Positions for drawing hands
	float cardX;
	float cardY;
	float cardWidth = 100.0f;
	float cardHeight = 100.0f * 7/5;
	float cardOffset = 10.0f;
	
	// RNG Seed
	int epoch;
	
	// Bool to check if this instance needs setting up
	bool needSetup = true;
	
	// Stores the lobby script
	Lobby lobScript;
	
	// Use this for initialization
	void Start () 
	{	
		// Create a local instance of SmartFox
		bool debug = true;
		if (SmartFoxConnection.IsInitialized)
		{
			smartFox = SmartFoxConnection.Connection;
		}
		else
		{
			smartFox = new SmartFox(debug);
		}
		
		// Initializes variables
		decks = new List<Card>();
		players = new List<PlayerInfo>();
		decisionFlags = new List<bool>();
		
		// Store this room
		GameObject lob = GameObject.Find("lobby");		
		lobScript = lob.GetComponent<Lobby>();	
		blackjackRoom = lobScript.CurrentActiveRoom;
		
		// Initializes the dealer
		house = new PlayerInfo();
		house.Name = "Holmes";
		house.Status = "Winning...";
		house.Active = true;
		
		// Initializes all the player placeholders
		for (int i = 0; i < blackjackRoom.MaxUsers; i++)
		{
			PlayerInfo newPlayer = new PlayerInfo();
			newPlayer.ID = i;
			players.Add(newPlayer);
			decisionFlags.Add(false);
		}
		
		// Check if we are the first person in the room 
		if (blackjackRoom.UserCount == 1)
		{
			needSetup = false;
			
			// Seeds Unity's built in RNG
			TimeSpan t = (DateTime.UtcNow - new DateTime(1970, 1, 1));
	     	epoch  = (int) t.TotalSeconds;
			UnityEngine.Random.seed = epoch;
			
			// Takes control of a player
			int seatNumber = 0;
			myPlayer = players[seatNumber];
			PrepPlayer(seatNumber, smartFox.MySelf.Name);
			startState = true;
		}
		
		SetPlayersStatus("Waiting...");
		
		AddEventListeners();
	}
	
	private void AddEventListeners() 
	{
		smartFox.AddEventListener(SFSEvent.OBJECT_MESSAGE, ObjectMessageReceived);
		smartFox.AddEventListener(SFSEvent.USER_ENTER_ROOM, OnUserEnterRoom);
		smartFox.AddEventListener(SFSEvent.USER_EXIT_ROOM, OnUserLeaveRoom);
	}
	
	// Update is called once per frame
	// Determines when to change states and calls any begin-of-state procedures
	private void Update () 
	{
		if (startState)
		{
			if (CheckDecisions())
			{
				startState = false;
				betState = true;
				ResetDecisions();
				SetPlayersStatus("Waiting...");
			}
		}
		else if (betState && !myPlayer.Wait)
		{
			if (decks.Count == 0)
			{
				LoadDecks(numberOfDecks);
				ShuffleDecks();
			}
			
			stateString = "Betting...";
			
			if (CheckDecisions())
			{
				betState = false;
				dealState = true;
				ResetDecisions();
				SetPlayersStatus("Waiting...");
			}
		}
		else if (dealState && !myPlayer.Wait)
		{
			stateString = "Dealing...";
			
			house.ResetHand();
			DealCard(house);
			DealCard(house);
			
			for (int i = 0; i < players.Count; i++)
			{
				if (players[i].Active)
				{
					players[i].ResetHand();
					DealCard(players[i]);
					DealCard(players[i]);
				}
			}
		
			dealState = false;
			decisionState = true;
			SetPlayersStatus("Waiting...");
		}
		else if (decisionState && !myPlayer.Wait)
		{
			stateString = "Decision...";

			if (CheckDecisions())
			{
				decisionState = false;
				endState = true;
				ResetDecisions();
				stateString = "End...?";
			}
		}
		else if (endState && !myPlayer.Wait)
		{
			SetPlayersStatus("Waiting...");
			while (house.BestSum() < 17)
			{
				DealCard(house);
				house.Busted = house.BestSum() > 21;
			}
			
			PayOff();
			
			if (CheckDecisions())
			{
				endState = false;
				betState = true;
				ResetDecisions();
				SetPlayersStatus("Waiting...");
				
				if (myPlayer.Wait)
				{
					myPlayer.Wait = false;
				}
			}
		}
	}
	
	// Draws the GUI depending on the game state
	private void OnGUI()
	{
		if (myPlayer != null)
		{
			if (startState)
			{
				DrawStartScreen();
				DrawMainBox();
				DrawRightPlayers();
				DrawLeftPlayers();
			}
			else if (betState)
			{
				DrawMainBox();
				DrawBetBox();
				DrawRightPlayers();
				DrawLeftPlayers();
			}

			else if (dealState)
			{
				DrawMainBox();
				DrawRightPlayers();
				DrawLeftPlayers();
			}
			else if (decisionState)
			{
				DrawMainBox();
				DrawDecisionBox();
				DrawRightPlayers();
				DrawLeftPlayers();
				
				cardX = Screen.width/2 - (((float)myPlayer.Hand.Count)/2 * (cardWidth + 2 * cardOffset));
				cardY = Screen.height - (300 + cardHeight + 4 * cardOffset);
				DrawCards(myPlayer, cardX, cardY, cardOffset, cardWidth);
			
				cardX = Screen.width/2 - (((float)house.Hand.Count)/2 * (cardWidth + 2 * cardOffset));
				cardY = 4 * cardOffset;
				DrawCards(house, cardX, cardY, cardOffset, cardWidth);
				MaskCards(house, 1, cardX, cardY, cardOffset, cardWidth);
			}
			else if (endState)
			{
				DrawMainBox();
				DrawRightPlayers();
				DrawLeftPlayers();
				DrawEndBox();
				
				cardX = Screen.width/2 - (((float)myPlayer.Hand.Count)/2 * (cardWidth + 2 * cardOffset));
				cardY = Screen.height - (300 + cardHeight + 4 * cardOffset);
				DrawCards(myPlayer, cardX, cardY, cardOffset, cardWidth);
			
				cardX = Screen.width/2 - (((float)house.Hand.Count)/2 * (cardWidth + 2 * cardOffset));
				cardY = 4 * cardOffset;
				DrawCards(house, cardX, cardY, cardOffset, cardWidth);
			}
		}
	}
	
	// Activates a player slot and assigns initial values
	private void PrepPlayer(int ID, string Name)
	{
		players[ID].Name = Name;
		players[ID].Chips = 500;
		players[ID].Active = true;
	}
	
	// Loads the decks, default is a deck of 52 cards
	private void LoadDecks(int numDecks = 1)
	{
		for (int i = 0; i < numDecks; i++)
		{
			for (int j = 1; j <= 13; j++)
			{
				for (int k = 4; k > 0; k--)
				{
					Card newCard = new Card();
					newCard.Number = j;
					newCard.SuiteSize = k;
					newCard.DefaultRoyals();
					newCard.DefaultSuites();
					decks.Add(newCard);
				}
			}
		}
		cardsRemaining = decks.Count;
	}
	
	// Shuffles the deck using the modern Fisher-Yates method
	private void ShuffleDecks()
	{
		for (int i = decks.Count - 1; i >= 1; i--)
		{
			int j = (int)(UnityEngine.Random.value * i);
			Card temp = decks[j];
			decks[j] = decks[i];
			decks[i] = temp;
		}
	}
	
	// Deals a card to a player
	private void DealCard(PlayerInfo target)
	{
		int nextIndex = decks.Count - 1;
		if (nextIndex < 0)
		{
			LoadDecks(numberOfDecks);
			ShuffleDecks();
		}
		Card newCard = decks[nextIndex];
		target.Hand.Add(newCard);
		decks.RemoveAt(nextIndex);
		cardsRemaining = decks.Count;
	}
	
	// Determines the payoff depending on the players' cards
	private void PayOff()
	{
		for (int i = 0; i < players.Count; i++)
		{
			if (players[i].Active && !players[i].Paid)
			{
				string result = "Not Evaluated";
				string netString = "Not Evaluated";
				int payOut = 0;
				
				if (players[i].Busted || (players[i].BestSum() < house.BestSum() && !house.Busted))
				{
					result = "You lose :(";
					netString = "-" + players[i].Bet.ToString();
					
				}
				else if (house.Busted || (players[i].BestSum() > house.BestSum()))
				{
					result = "You won! :D";
					payOut = 2 * players[i].Bet;
					netString = "+" + players[i].Bet.ToString();
					
				}
				else if (players[i].BestSum() == house.BestSum())
				{
					result = "You tied :|";
					payOut = players[i].Bet;
					netString = "+0";
				}
				
				players[i].Judgment = result;
				players[i].Net = netString;
				players[i].Pay(payOut);
				players[i].Paid = true;
			}
		}
	}
	
	// Resets all the decision flags of the players
	private void ResetDecisions()
	{
		for (int i = 0; i < decisionFlags.Count; i++)
		{
			decisionFlags[i] = false;
		}
	}
	
	// Returns true of all players have made a decision, false otherwise
	private bool CheckDecisions()
	{
		bool changeState = true;
		for (int i = 0; i < decisionFlags.Count; i++)
		{
			if (players[i].Active && !decisionFlags[i] && !players[i].Wait)
			{
				changeState = false;
				break;
			}
		}
		return changeState;
	}
	
	// Checks to see if any player has busted
	private void CheckBusted()
	{
		for (int i = 0; i < players.Count; i++)
		{
			PlayerInfo currentPlayer = players[i];
			if (currentPlayer.Active && currentPlayer.BestSum() > 21)
			{
				SendBustRequest(i);
			}
		}
	}
	
	// Checks to see if a player exists somewhere
	private bool CheckPlayer(string checkName)
	{
		bool exists = false;
		for (int i = 0; i < players.Count; i++)
		{
			if (players[i].Name == checkName)
			{
				exists = true;
				break;
			}
		}
		return exists;
	}
	
	// Sets the statuses of all players
	private void SetPlayersStatus(string s)
	{
		for (int i = 0; i < players.Count; i++)
		{
			if (players[i].Active)
				players[i].Status = s;
		}
	}
	
	// Returns the index of a player slot that is open
	private int FindSeat()
	{
		for (int i = 0; i < players.Count; i++)
		{
			if (!players[i].Active)
			{
				return i;
			}
		}
		
		return -1;
	}
	
	// Resets a player slot using the corresponding ID
	private void EmptySeat(int seatID)
	{
		players[seatID] = new PlayerInfo();
		players[seatID].ID = seatID;
	}
	
	// The starting screen lets you choose the number of decks
	// to play with
	private void DrawStartScreen()
	{
		float startWidth = 200.0f;
		float startHeight = startWidth * 7/5;

		GUI.Box(new Rect((Screen.width - startWidth)/2, 50, startWidth, startHeight), 
				"How many decks do you want to play with?", boxStyle);
		
		string numDeckString = numberOfDecks.ToString();
		GUI.Label(new Rect(Screen.width/2 - 10, 140, 20, 50), 
					numDeckString, labelStyle);
		
		if (GUI.Button(new Rect(Screen.width/2 - 40, 200, 30, 30), 
					"+", greenButton))
		{
			if (numberOfDecks < 9 && !decisionFlags[myPlayer.ID])
				SendDeckRequest(myPlayer.ID, 1);
		}
		
		if (GUI.Button(new Rect(Screen.width/2 + 10, 200, 30, 30), 
					"-", redButton))
		{
			if (numberOfDecks > 1 && !decisionFlags[myPlayer.ID])
				SendDeckRequest(myPlayer.ID, -1);
		}
		
		if (GUI.Button(new Rect(Screen.width/2 - 40, 250, 80, 30), 
					"Begin", gunMetalButton))
		{
			SendLockRequest(myPlayer.ID);
		}
	}
	
	// The main box holds information about the game
	private void DrawMainBox()
	{
		string info = 	"Name: " + myPlayer.Name +
						"\nChips: $" + myPlayer.Chips.ToString() + 
						"\nBet: $" + myPlayer.Bet.ToString() +
						"\nStatus: " + myPlayer.Status +
						"\n" + 
						"\nCards in Deck: " + cardsRemaining.ToString() + 
						"\nGame State: " + stateString;
				
		
		GUI.Box(new Rect((Screen.width - mbWidth)/2, Screen.height - 325, 
				mbWidth, mbHeight), "", boxStyle);
		GUI.Box(new Rect(Screen.width/2 + 20, Screen.height - 300,
				mbWidth * 0.4f, mbHeight * 0.4f), info, infoStyle);
		
	}
	
	// The bet box is used for betting in increments of 5, 25 and 100
	private void DrawBetBox()
	{
		GUI.Box(new Rect(Screen.width/2 - (20 + mbWidth * 0.4f), Screen.height - 300,
				mbWidth * 0.4f, mbHeight * 0.4f), "Betting:", infoStyle);
		
		GUI.Box(new Rect(Screen.width/2 - (mbWidth * 0.4f), Screen.height - 240, 80, 30),
				"$5", labelStyle);
		
		GUI.Box(new Rect(Screen.width/2 - (mbWidth * 0.4f), Screen.height - 190, 80, 30),
				"$25", labelStyle);
		
		GUI.Box(new Rect(Screen.width/2 - (mbWidth * 0.4f), Screen.height - 140, 80, 30),
				"$100", labelStyle);
		
		GUI.Box(new Rect(Screen.width/2 - (mbWidth * 0.4f), Screen.height - 90, 80, 30),
				"Lock", labelStyle);
			
		if (GUI.Button(new Rect(Screen.width/2 - (mbWidth * 0.4f - 90), Screen.height - 240, 30, 30), 
					"+", greenButton))
		{
			SendBetRequest(myPlayer.ID, 5);
		}
		
		if (GUI.Button(new Rect(Screen.width/2 - (mbWidth * 0.4f - 130), Screen.height - 240, 30, 30), 
					"-", redButton))
		{
			SendBetRequest(myPlayer.ID, -5);
		}
		
		if (GUI.Button(new Rect(Screen.width/2 - (mbWidth * 0.4f - 90), Screen.height - 190, 30, 30), 
					"+", greenButton))
		{
			SendBetRequest(myPlayer.ID, 25);
		}
		
		if (GUI.Button(new Rect(Screen.width/2 - (mbWidth * 0.4f - 130), Screen.height - 190, 30, 30), 
					"-", redButton))
		{
			SendBetRequest(myPlayer.ID, -25);
		}
		
		if (GUI.Button(new Rect(Screen.width/2 - (mbWidth * 0.4f - 90), Screen.height - 140, 30, 30), 
					"+", greenButton))
		{
			SendBetRequest(myPlayer.ID, 100);
		}
		
		if (GUI.Button(new Rect(Screen.width/2 - (mbWidth * 0.4f - 130), Screen.height - 140, 30, 30), 
					"-", redButton))
		{
			SendBetRequest(myPlayer.ID, -100);
		}
		
		if (GUI.Button(new Rect(Screen.width/2 - (mbWidth * 0.4f - 90), Screen.height - 90, 70, 30), 
					"Bet", gunMetalButton))
		{
			SendLockRequest(myPlayer.ID);
		}
	}
	
	// The decision box is allows the player to choose "Hit" or "Stand"
	// during the decision phase
	// "Double" and "Split" are not implemented due to time constraints
	// They are there as placeholders
	private void DrawDecisionBox()
	{
		GUI.Box(new Rect(Screen.width/2 - (20 + mbWidth * 0.4f), Screen.height - 300,
				mbWidth * 0.4f, mbHeight * 0.4f), "Decision:", infoStyle);
			
		if (GUI.Button(new Rect(Screen.width/2 - (mbWidth * 0.4f), Screen.height - 240, 100, 30), 
					"Hit", gunMetalButton) && !myPlayer.Busted && !myPlayer.Stand)
		{
			SendHitRequest(myPlayer.ID);
		}
		
		if (GUI.Button(new Rect(Screen.width/2 - (mbWidth * 0.4f), Screen.height - 190, 100, 30), 
					"Stand", gunMetalButton) && !myPlayer.Busted && !myPlayer.Stand)
		{
			SendStandRequest(myPlayer.ID);
		}
		
		
		if (GUI.Button(new Rect(Screen.width/2 - (mbWidth * 0.4f), Screen.height - 140, 100, 30), 
					"Double", gunMetalButton))
		{
			//myPlayer.Status = "Double";
			Debug.Log(myPlayer.BestSum());
		}
		
		
		if (GUI.Button(new Rect(Screen.width/2 - (mbWidth * 0.4f), Screen.height - 90, 100, 30), 
					"Split", gunMetalButton))
		{
			//myPlayer.Status = "Split";
		}
	}
	
	// The end box shows players if they won or lost against the dealer
	// and asks whether they would like to play again or leave
	private void DrawEndBox()
	{
		string result = "End: " +
						"\nYour Hand: " + myPlayer.BestSum() +
						"\nDealer's Hand: " + house.BestSum() +
						"\nResult: " + myPlayer.Judgment +
						"\nNet Change: " + myPlayer.Net;
		
		GUI.Box(new Rect(Screen.width/2 - (20 + mbWidth * 0.4f), Screen.height - 300,
				mbWidth * 0.4f, mbHeight * 0.4f), result, infoStyle);
				
		if (GUI.Button(new Rect(Screen.width/2 - (mbWidth * 0.4f), Screen.height - 140, 100, 30), 
					"Stay", gunMetalButton))
		{
			SendStayRequest(myPlayer.ID);
		}
		
		if (GUI.Button(new Rect(Screen.width/2 - (mbWidth * 0.4f), Screen.height - 90, 100, 30), 
					"Leave", gunMetalButton))
		{
			SendLeaveRequest();
		}
	}
	
	// Draws the cards in the chosen player's hand
	private void DrawCards(PlayerInfo targetPlayer, float x, float y, float offset, float width)
	{
		List<Card> currentHand = targetPlayer.Hand;
		float cWidth = width;
		float cHeight = cardWidth * 7/5;
		float cOffset = offset;
		float drawX = x;
		float drawY = y;
		
		for (int i = 0; i < currentHand.Count; i++)
		{
			if (currentHand[i].SuiteSize % 2 == 0 && currentHand[i].Number <= 10)
			{
				GUI.Box(new Rect(drawX + cOffset, drawY, cWidth, cHeight),
						currentHand[i].Number + "" + currentHand[i].Suite,
						blackCardStyle);
			}
			else if (currentHand[i].SuiteSize % 2 == 1 && currentHand[i].Number <= 10)
			{
				GUI.Box(new Rect(drawX + cOffset, drawY, cWidth, cHeight),
						currentHand[i].Number + "" + currentHand[i].Suite,
						redCardStyle);
			}
			else if(currentHand[i].SuiteSize % 2 == 0 && currentHand[i].Number > 10)
			{
				GUI.Box(new Rect(drawX + cOffset, drawY, cWidth, cHeight),
						currentHand[i].NumChar + "" + currentHand[i].Suite,
						blackCardStyle);
			}
			else if (currentHand[i].SuiteSize % 2 == 1 && currentHand[i].Number > 10)
			{
				GUI.Box(new Rect(drawX + cOffset, drawY, cWidth, cHeight),
						currentHand[i].NumChar + "" + currentHand[i].Suite,
						redCardStyle);
			}
			
			drawX += cWidth + 2 * cOffset;
		}
	}
	
	// Draws the box for players on the right
	private void DrawRightPlayers()
	{
		float cWidth = mbWidth * 0.3f;
		float cHeight = mbHeight * 0.3f;
		float cOffset = cardOffset;
		float drawX = Screen.width - cHeight - 20;
		float drawY = Screen.height - 300;
		
		if (!endState)
		{
			for (int i = 0; i < players.Count; i++)
			{
				if (i%2 == 1 && i != myPlayer.ID)
				{
					string info ="Name: " + players[i].Name +
								"\nChips: $" + players[i].Chips.ToString() + 
								"\nBet: $" + players[i].Bet.ToString() +
								"\nStatus: " + players[i].Status +
								"\nActive: " + players[i].Active;
			
					GUI.Box(new Rect(drawX, drawY, cHeight, cWidth), 
									info, infoStyle);
					
					drawY -= cHeight + 2 * cOffset;
				}
			}
		}
		else
		{
			for (int i = 0; i < players.Count; i++)
			{
				if (i%2 == 1 && i != myPlayer.ID)
				{
					string result = "Name: " + players[i].Name + 
									"\nChips: $" + players[i].Chips.ToString() + 
									"\nTheir Hand: " + players[i].BestSum() +
									"\nNet Change: " + players[i].Net +
									"\nStatus: " + players[i].Status;
			
					GUI.Box(new Rect(drawX, drawY, cHeight, cWidth), 
									result, infoStyle);
					
					drawY -= cHeight + 2 * cOffset;
				}
			}
		}
	}
	
	// Draws the box for players on the left
	private void DrawLeftPlayers()
	{
		float cWidth = mbWidth * 0.3f;
		float cHeight = mbHeight * 0.3f;
		float cOffset = cardOffset;
		float drawX = 20;
		float drawY = Screen.height - 300;
		
		if (!endState)
		{
			for (int i = 0; i < players.Count; i++)
			{
				if (i%2 == 0 && i != myPlayer.ID)
				{
					string info ="Name: " + players[i].Name +
								"\nChips: $" + players[i].Chips.ToString() + 
								"\nBet: $" + players[i].Bet.ToString() +
								"\nStatus: " + players[i].Status +
								"\nActive: " + players[i].Active;
			
					GUI.Box(new Rect(drawX, drawY, cHeight, cWidth), 
									info, infoStyle);
					
					drawY -= cHeight + 2 * cOffset;
				}
			}
		}
		else
		{
			for (int i = 0; i < players.Count; i++)
			{
				if (i%2 == 0 && i != myPlayer.ID)
				{
					string result = "Name: " + players[i].Name + 
									"\nChips: $" + players[i].Chips.ToString() + 
									"\nTheir Hand: " + players[i].BestSum() +
									"\nNet Change: " + players[i].Net +
									"\nStatus: " + players[i].Status;
			
					GUI.Box(new Rect(drawX, drawY, cHeight, cWidth), 
									result, infoStyle);
					
					drawY -= cHeight + 2 * cOffset;
				}
			}
			
		}
	}
	
	// Draws a mystery card over (n - revealed) where n is the number
	// of cards in a player's hands and revealed is the number of cards to leave visible
	private void MaskCards(PlayerInfo targetPlayer, int revealed, 
							float x, float y, float offset, float width)
	{
		List<Card> currentHand = targetPlayer.Hand;
		float cWidth = width;
		float cHeight = cardWidth * 7/5;
		float cOffset = offset;
		float drawX = x;
		float drawY = y;
		
		if (revealed <= currentHand.Count)
		{
			drawX += cWidth + 2 * cOffset * revealed;
		}
		
		for (int i = revealed; i < currentHand.Count; i++)
		{
			GUI.Box(new Rect(drawX + cOffset, drawY, cWidth, cHeight),
					"??",
					blankCardStyle);
			
			drawX += cWidth + 2 * cOffset;
		}
	}
	
	// Sends a join room request using the player ID to notify
	// others of this instance's presence
	private void SendJoinRequest(int ID) 
	{
		SFSObject dataObject = new SFSObject();
		dataObject.PutUtfString("type", "join");
		dataObject.PutInt("player", ID);
		smartFox.Send(new ObjectMessageRequest(dataObject));
	}
	
	// Sends a numDeck change request using the player ID and a number for the numDeck change
	private void SendDeckRequest(int ID, int numDeck)
	{
		numberOfDecks += numDeck;
		SFSObject dataObject = new SFSObject();
		dataObject.PutUtfString("type", "deck");
		dataObject.PutInt("player", ID);
		dataObject.PutInt("value", numberOfDecks);
		smartFox.Send(new ObjectMessageRequest(dataObject));
	}
	
	// Sends a bet request using the player ID and a number for the bet
	private void SendBetRequest(int ID, int newBet)
	{
		myPlayer.Status = "Bet";
		myPlayer.NewBet(newBet);
		
		SFSObject dataObject = new SFSObject();
		dataObject.PutUtfString("type", "bet");
		dataObject.PutInt("player", ID);
		dataObject.PutInt("value", newBet);
		smartFox.Send(new ObjectMessageRequest(dataObject));
	}
	
	// Sends a lock request using the player ID 
	private void SendLockRequest(int ID)
	{
		myPlayer.Status = "Locked in";
		decisionFlags[myPlayer.ID] = true;

		SFSObject dataObject = new SFSObject();
		dataObject.PutUtfString("type", "lock");
		dataObject.PutInt("player", ID);
		smartFox.Send(new ObjectMessageRequest(dataObject));
	}
	
	// Sends a hit request using the player ID
	private void SendHitRequest(int ID)
	{
		myPlayer.Status = "Hit";
		DealCard(myPlayer);
		CheckBusted();
		
		SFSObject dataObject = new SFSObject();
		dataObject.PutUtfString("type", "hit");
		dataObject.PutInt("player", ID);
		smartFox.Send(new ObjectMessageRequest(dataObject));	
	}
	
	// Sends a stand request using the player ID
	private void SendStandRequest(int ID)
	{
		myPlayer.Status = "Stand";
		decisionFlags[myPlayer.ID] = true;

		SFSObject dataObject = new SFSObject();
		dataObject.PutUtfString("type", "stand");
		dataObject.PutInt("player", ID);
		smartFox.Send(new ObjectMessageRequest(dataObject));	
	}
	
	// Sends a bust request using the player ID
	private void SendBustRequest(int ID)
	{
		myPlayer.Status = "Busted";
		myPlayer.Busted = true;
		decisionFlags[myPlayer.ID] = true;
		
		SFSObject dataObject = new SFSObject();
		dataObject.PutUtfString("type", "bust");
		dataObject.PutInt("player", ID);
		smartFox.Send(new ObjectMessageRequest(dataObject));
	}
	
	// Sends a stay request using the player ID
	private void SendStayRequest(int ID)
	{
		myPlayer.Status = "Staying...";
		decisionFlags[myPlayer.ID] = true;

		SFSObject dataObject = new SFSObject();
		dataObject.PutUtfString("type", "stay");
		dataObject.PutInt("player", ID);
		smartFox.Send(new ObjectMessageRequest(dataObject));
	}
	
	// Sends a current state request using the player ID
	private void SendCurrentRequest()
	{
		SFSObject dataObject = new SFSObject();
		dataObject.PutUtfString("type", "current");
		dataObject.PutInt("player", myPlayer.ID);
		dataObject.PutInt("chips", myPlayer.Chips);
		dataObject.PutInt("bet", myPlayer.Bet);
		smartFox.Send(new ObjectMessageRequest(dataObject));
	}
	
	// Sends a leave request
	private void SendLeaveRequest()
	{
		lobScript.CurrentActiveRoom = smartFox.RoomManager.GetRoomByName("The Lobby");
		Application.LoadLevel("The Lobby");
	}
			
	// Callback Functions
	// Determines what to do after reviewing an ObjectMessage
	private void ObjectMessageReceived(BaseEvent evt)
	{
		ISFSObject dataObject = (SFSObject)evt.Params["message"];
		User sender = (User)(evt.Params["sender"]);
		string request = dataObject.GetUtfString("type");
		int localID = dataObject.GetInt("player");
		;
		switch(request)
		{
			case "setup":
				if (needSetup)
				{
					UnityEngine.Random.seed = dataObject.GetInt("seed");
					needSetup = false;
					numberOfDecks = dataObject.GetInt("decks");
					startState = dataObject.GetBool("start");
					betState = dataObject.GetBool("bet");
					dealState = dataObject.GetBool("deal");
					decisionState = dataObject.GetBool("decision");
					endState = dataObject.GetBool("end");	
				
					LoadDecks(numberOfDecks);
					ShuffleDecks();
					cardsRemaining = dataObject.GetInt("remaining");
					if (!startState && decks.Count > cardsRemaining)
					{
						decks.RemoveRange(cardsRemaining - 1, decks.Count - cardsRemaining);
					}
					
					int openSeat = dataObject.GetInt("free");
					PrepPlayer(openSeat, smartFox.MySelf.Name);
					myPlayer = players[openSeat];
					SendJoinRequest(myPlayer.ID);
					myPlayer.Wait = !startState;
				}
				break;
			case "current":
				PrepPlayer(localID, sender.Name);
				players[localID].Chips = dataObject.GetInt("chips");
				players[localID].Bet = dataObject.GetInt("bet");
				break;
			case "join":
				PrepPlayer(localID, sender.Name);
				players[localID].Wait = !startState;
				break;
			case "deck":
				numberOfDecks = dataObject.GetInt("value");
				break;
			case "bet":
				int changeBet = dataObject.GetInt("value");
				players[localID].NewBet(changeBet);
				players[localID].Status = "Bet";
				break;
			case "lock":
				players[localID].Status = "Locked in";
				decisionFlags[localID] = true;
				break;
			case "hit":
				DealCard(players[localID]);
				players[localID].Status = "Hit";
				CheckBusted();
				break;
			case "stand":
				players[localID].Status = "Stand";
				decisionFlags[localID] = true;
				break;
			case "bust":
				players[localID].Status = "Busted";
				decisionFlags[localID] = true;
				break;
			case "stay":
				players[localID].Status = "Staying...";
				decisionFlags[localID] = true;
				break;
		}

	}
	
	private void OnUserEnterRoom(BaseEvent evt) 
	{
		SFSObject dataObject = new SFSObject();
		dataObject.PutUtfString("type", "setup");
		dataObject.PutInt("playerID", myPlayer.ID);
		dataObject.PutInt("seed", epoch);
		dataObject.PutInt("decks", numberOfDecks);
		dataObject.PutBool("start", startState);
		dataObject.PutBool("bet", betState);
		dataObject.PutBool("deal", dealState);
		dataObject.PutBool("decision", decisionState);
		dataObject.PutBool("end", endState);
		dataObject.PutInt("remaining", decks.Count);
		dataObject.PutInt("free", FindSeat());

		SendCurrentRequest();
		smartFox.Send(new ObjectMessageRequest(dataObject));
	}

	private void OnUserLeaveRoom(BaseEvent evt) 
	{
		User user = (User)(evt.Params["user"]);
		if 	(!blackjackRoom.ContainsUser(user))
		{
			for (int i = 0; i < players.Count; i++)
			{
				if (players[i].Name == user.Name)
				{
					EmptySeat(i);
				}
			}
		}
	}
}