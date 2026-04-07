using Godot;
using System;
using System.Collections.Generic;

public partial class CharacterEditor : Node2D
{
	// Path based on your screenshot
	private const string CharactersPath = "res://scenes/game/characters/";

	// --- UI Nodes ---
	private OptionButton _charList;
	private Node2D _charPreviewAnchor; // A Node2D to hold the instantiated character

	// --- State ---
	private List<string> _characterScenes = new List<string>();
	private Node2D _currentCharacter;

	public override void _Ready()
	{
		// Adjust these paths to match your Scene Tree exactly
		_charList = GetNode<OptionButton>("UI/tab_container/Basic/Char_List");
		_charPreviewAnchor = GetNode<Node2D>("Char_Preview");

		// Connect signal
		_charList.ItemSelected += OnCharacterSelected;

		LoadCharacterFiles();
	}

	private void LoadCharacterFiles()
	{
		_charList.Clear();
		_characterScenes.Clear();

		using var dir = DirAccess.Open(CharactersPath);
		if (dir == null)
		{
			GD.PrintErr("Could not open characters directory!");
			return;
		}

		dir.ListDirBegin();
		string fileName = dir.GetNext();
		int index = 0;

		while (fileName != "")
		{
			// Only add .tscn files and ignore base scripts or gameover scenes
			if (fileName.EndsWith(".tscn") && !fileName.Contains("dead"))
			{
				_characterScenes.Add(fileName);
				// Display the name without the extension (e.g., "bf_car")
				_charList.AddItem(fileName.Replace(".tscn", ""), index);
				index++;
			}
			fileName = dir.GetNext();
		}

		if (_characterScenes.Count > 0)
			OnCharacterSelected(0);
	}

	private void OnCharacterSelected(long index)
	{
		string sceneName = _characterScenes[(int)index];
		string fullPath = CharactersPath + sceneName;

		// 1. Remove the old character instance
		if (_currentCharacter != null)
		{
			_currentCharacter.QueueFree();
		}

		// 2. Load and Instantiate the new character
		try
		{
			PackedScene charScene = GD.Load<PackedScene>(fullPath);
			_currentCharacter = charScene.Instantiate<Node2D>();
			
			// 3. Add to the anchor
			_charPreviewAnchor.AddChild(_currentCharacter);
			
			// Optional: Center the character if they have weird offsets
			_currentCharacter.Position = Vector2.Zero;
			
			GD.Print($"Successfully loaded: {sceneName}");
		}
		catch (Exception e)
		{
			GD.PrintErr($"Failed to load character scene: {e.Message}");
		}
	}
}
