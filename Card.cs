using UnityEngine;
using System.Collections;

public class Card
{
	// Numeric value of this card
	public int Number{get; set;}
	// String symbol for this card
	public char NumChar{get; set;}
	// Size of the suite (for comparison)
	public int SuiteSize{get; set;}
	// Unicode symbol for the suite of this card
	public char Suite{get; set;}
	
	// Use this for initialization
	public void Start () 
	{
	
	}
	
	// Update is called once per frame
	public void Update () 
	{
	
	}
	
	// Sets the char representation of this card's value
	// Assumes Jack == 11, Queen == 12, King == 13
	public void DefaultRoyals()
	{
		switch (Number)
		{
			case 1:
				NumChar = 'A';
				break;
			case 11:
				NumChar = 'J';
				break;
			case 12:
				NumChar = 'Q';
				break;
			case 13:
				NumChar = 'K';
				break;
		}
	}
	
	// Sets the unicode of this card's suite to traditional playing card suites
	// Assumes Spade (4) > Heart (3) > Club (2) > Diamond (1)
	// For some reason, the diamond symbol doesn't show up in unity 
	// In its place is the mathematical diamond
	public void DefaultSuites()
	{
		switch (SuiteSize)
		{
			case 4:
				Suite = '\u2660';
				break;
			case 3:
				Suite = '\u2665';
				break;
			case 2:
				Suite = '\u2663';
				break;
			case 1:
				Suite = '\u25C6';
				break;
		}
	}
}
