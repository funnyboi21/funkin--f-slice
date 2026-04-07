using Godot;
using System.Collections.Generic;

public partial class ModMenuLogic : Node2D
{
	private modingAPI _api; // Reference to your Autoload

	// UI Nodes
	private Node2D _modList;
	private Sprite2D _modIcon;
	private RichTextLabel _modDesc;
	private AcceptDialog _errorPopup;
	
	// Assuming you have an AudioStreamPlayer for the menu music based on your snippet
	private AudioStreamPlayer _music; 

	private List<Label> _uiItems = new List<Label>();
	private int _selectedIndex = 0;
	private float _targetY = 0f;

	public override void _Ready()
	{
		// Access the Autoloads
		_api = GetNode<modingAPI>("/root/modingAPI");
		
		// Grab local nodes
		_modList = GetNode<Node2D>("UI/ModListTarget/ModList");
		_modIcon = GetNode<Sprite2D>("UI/ModIcon");
		_modDesc = GetNode<RichTextLabel>("UI/ModDesc");
		_errorPopup = GetNode<AcceptDialog>("UI/errorPopup");
		
		// Grab the local music player if you have one attached to this scene
		_music = GetNodeOrNull<AudioStreamPlayer>("Music"); 

		BuildUI();
		UpdateSelection();
	}

	public override void _Process(double delta)
	{
		float currentY = _modList.Position.Y;
		_modList.Position = new Vector2(_modList.Position.X, Mathf.Lerp(currentY, _targetY, (float)delta * 12f));
	}

	public override void _Input(InputEvent @event)
	{
		// Filter out echoes and unpressed events just like your GDScript snippet
		if (@event.IsEcho() || !@event.IsPressed()) return;

		// Don't process list navigation if the list is empty or the error popup is showing
		if (_api.LoadedMods.Count == 0 || _errorPopup.Visible) return;

		if (@event.IsActionPressed("ui_up")) 
		{
			ChangeSelection(-1);
		}
		else if (@event.IsActionPressed("ui_down")) 
		{
			ChangeSelection(1);
		}
		else if (@event.IsActionPressed("ui_accept")) 
		{
			ToggleMod();
		}
		else if (@event.IsActionPressed("ui_cancel")) 
		{
			// 1. Save Mod States
			_api.SaveModStates();
			
			// 2. Stop local music
			_music?.Stop();
			
			// 3. Play GlobalAudio music (Translating GDScript Autoload to C#)
			Node globalAudio = GetNode<Node>("/root/GlobalAudio");
			if (globalAudio != null)
			{
				AudioStreamPlayer globalMusic = globalAudio.Get("music").As<AudioStreamPlayer>();
				globalMusic?.Play();
			}
			
			// 4. Switch Scene using SceneManager Autoload
			Node sceneManager = GetNode<Node>("/root/SceneManager");
			if (sceneManager != null)
			{
				PackedScene mainMenu = GD.Load<PackedScene>("res://scenes/menus/main_menu.tscn");
				sceneManager.Call("switch_to", mainMenu);
			}
		}
	}

	private void ChangeSelection(int change)
	{
		_selectedIndex = Mathf.PosMod(_selectedIndex + change, _api.LoadedMods.Count);
		UpdateSelection();
	}

	private void UpdateSelection()
	{
		_targetY = -(_selectedIndex * 120f);
		var mod = _api.LoadedMods[_selectedIndex];

		for (int i = 0; i < _uiItems.Count; i++)
		{
			_uiItems[i].Modulate = (i == _selectedIndex) ? Colors.Yellow : new Color(1, 1, 1, 0.5f);
			_uiItems[i].Scale = (i == _selectedIndex) ? new Vector2(1.1f, 1.1f) : Vector2.One;
		}

		_modIcon.Texture = mod.Icon;
		_modDesc.Text = $"[b]{mod.Title}[/b]\n{mod.Description}";
	}

	private void ToggleMod()
	{
		var mod = _api.LoadedMods[_selectedIndex];

		if (!mod.IsEnabled && !mod.IsCompatible)
		{
			_errorPopup.DialogText = $"Mod '{mod.Title}' is for API {mod.ApiVersion}.\nEngine is {modingAPI.EngineApiVersion}.";
			_errorPopup.PopupCentered();
			return;
		}

		mod.IsEnabled = !mod.IsEnabled;
		_uiItems[_selectedIndex].Text = $"{(mod.IsEnabled ? "[ON]" : "[OFF]")} {mod.Title}";
	}

	private void BuildUI()
	{
		foreach (var mod in _api.LoadedMods)
		{
			Label l = new Label { Text = $"{(mod.IsEnabled ? "[ON]" : "[OFF]")} {mod.Title}" };
			l.Position = new Vector2(0, _uiItems.Count * 120f);
			l.AddThemeFontSizeOverride("font_size", 42);
			_modList.AddChild(l);
			_uiItems.Add(l);
		}
	}
}
