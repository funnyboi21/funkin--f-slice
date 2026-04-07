using Godot;
using System;
using System.Collections.Generic;

namespace Funkin.Play;

/// <summary>
/// The main gameplay state, equivalent to PlayState.hx.
/// Attach this to the root Node of your PlayState.tscn.
/// </summary>
public partial class PlayState : Node
{
	public static PlayState Instance { get; private set; }

	#region Exports (Assign in Godot Inspector)
	[ExportCategory("Audio")]
	[Export] public AudioStreamPlayer InstPlayer;
	[Export] public AudioStreamPlayer VocalsPlayer;

	[ExportCategory("UI")]
	[Export] public TextureProgressBar HealthBar;
	[Export] public Label ScoreLabel;
	[Export] public CanvasLayer HudLayer; // Replaces camHUD
	[Export] public Camera2D GameCamera;  // Replaces camGame

	[ExportCategory("Gameplay Nodes")]
	[Export] public Node2D PlayerStrumline;   // Assume you have a Godot component for this
	[Export] public Node2D OpponentStrumline;
	#endregion

	#region State Variables
	public float Health = 50f; // Assuming max is 100
	public float SongScore = 0f;
	public int Combo = 0;
	public int Misses = 0;

	public bool IsBotPlayMode = false;
	public bool IsPracticeMode = false;
	public bool IsGameOver = false;
	public bool IsPaused = false;
	
	// Rhythm tracking
	public double SongPosition = 0;
	private double _audioLatency = 0;
	private float _playbackRate = 1.0f;
	#endregion

	#region Camera & Tweening
	private Tween _cameraZoomTween;
	private float _defaultCameraZoom = 1.05f;
	private float _currentCameraZoom = 1.05f;
	private float _cameraBopMultiplier = 1.0f;
	#endregion

	public override void _Ready()
	{
		if (Instance != null)
		{
			GD.PushWarning("PlayState instance already exists! Overwriting.");
		}
		Instance = this;

		// Godot equivalent of persistentUpdate = true / persistentDraw = true
		// is handled by ProcessMode
		ProcessMode = ProcessModeEnum.Pausable; 

		// 1. Initialize latency calculation (Godot handles this gracefully)
		_audioLatency = AudioServer.GetOutputLatency();

		// 2. Load Chart and initialize notes (Placeholder for your ChartParser)
		GenerateSong();

		// 3. Start the Vwoosh / Countdown sequence
		StartCountdown();
	}

	public override void _Process(double delta)
	{
		if (IsGameOver) return;

		UpdateAudioSync();
		UpdateCameraBop(delta);
		UpdateUI(delta);

		// Check for Pause input (equivalent to FlxG.keys.justPressed.ENTER)
		if (Input.IsActionJustPressed("pause") && !IsPaused)
		{
			PauseGame();
		}

		// Check Health Death
		if (Health <= 0 && !IsPracticeMode && !IsGameOver)
		{
			TriggerGameOver();
		}

		// Note: Note processing and moving is usually best handled in _Process 
		// by the Strumline/Note nodes themselves, rather than iterating a massive array here.
		ProcessNotes();
	}

	#region Audio & Rhythm Sync
	private void UpdateAudioSync()
	{
		if (InstPlayer.Playing)
		{
			// Godot's highly precise audio sync method
			double timeSinceLastMix = AudioServer.GetTimeSinceLastMix();
			SongPosition = InstPlayer.GetPlaybackPosition() + timeSinceLastMix - _audioLatency;

			// Optional: Resync Vocals if they drift too far (RESYNC_THRESHOLD equivalent)
			if (VocalsPlayer != null && VocalsPlayer.Playing)
			{
				double vocalDrift = Math.Abs(VocalsPlayer.GetPlaybackPosition() - SongPosition);
				if (vocalDrift > 0.04) // 40ms threshold
				{
					ResyncVocals();
				}
			}
		}
	}

	private void ResyncVocals()
	{
		if (VocalsPlayer == null) return;
		VocalsPlayer.Seek((float)SongPosition);
	}
	#endregion

	#region Input Handling (PreciseInputManager equivalent)
	public override void _UnhandledInput(InputEvent @event)
	{
		if (IsPaused || IsGameOver || IsBotPlayMode) return;

		// Using Godot's input map (e.g., "note_left", "note_down", "note_up", "note_right")
		if (@event is InputEventKey keyEvent && !keyEvent.IsEcho())
		{
			int direction = GetInputDirection(keyEvent);
			if (direction == -1) return;

			if (keyEvent.IsPressed())
			{
				OnKeyPress(direction, Time.GetTicksUsec()); // High precision OS time
			}
			else if (keyEvent.IsReleased())
			{
				OnKeyRelease(direction);
			}
		}
	}

	private void OnKeyPress(int direction, ulong timestamp)
	{
		// 1. Get notes in range from player strumline
		// var noteToHit = PlayerStrumline.GetHittableNote(direction);
		
		// Pseudo-logic representing the HaxeFlixel Input Queue loop
		bool hasNotes = false; // NoteToHit != null

		if (!hasNotes)
		{
			GhostMiss(direction);
		}
		else
		{
			// Calculate Hit difference based on SongPosition
			// double diff = SongPosition - noteToHit.StrumTime;
			// GoodNoteHit(noteToHit, diff);
		}
	}

	private void OnKeyRelease(int direction)
	{
		// Handle dropping hold notes
	}

	private int GetInputDirection(InputEventKey keyEvent)
	{
		if (keyEvent.IsAction("note_left")) return 0;
		if (keyEvent.IsAction("note_down")) return 1;
		if (keyEvent.IsAction("note_up")) return 2;
		if (keyEvent.IsAction("note_right")) return 3;
		return -1;
	}
	#endregion

	#region Gameplay Logic
	private void GoodNoteHit(/*Note note, double diff*/)
	{
		// Apply Score
		SongScore += 350; // Example Score
		Health += 0.5f;   // SICK_BONUS
		Combo++;

		UpdateScoreText();
		
		// Popups
		// PopUpStuff.DisplayRating("sick");
	}

	private void OnNoteMiss(/*Note note*/)
	{
		Health -= 2.0f; // MISS_PENALTY
		Combo = 0;
		Misses++;
		SongScore -= 10;

		UpdateScoreText();
		
		// Play miss sound
		// var missSound = GD.Load<AudioStream>("res://assets/sounds/missnote1.ogg");
		// AudioManager.Play(missSound);
	}

	private void GhostMiss(int direction)
	{
		Health -= 1.0f; // GHOST_MISS_PENALTY
		Combo = 0;
		SongScore -= 10;
		UpdateScoreText();
	}

	private void ProcessNotes()
	{
		// Let the Bot hit opponent notes
		// OpponentStrumline.ProcessBotPlay(SongPosition);

		// Check for missed notes falling off screen
		// PlayerStrumline.CheckForMissedNotes(SongPosition);
	}
	#endregion

	#region UI & Polish
	private void UpdateUI(double delta)
	{
		// Lerp health bar (Smooth health update)
		HealthBar.Value = Mathf.Lerp(HealthBar.Value, Health, 15f * delta);

		// Cap Health
		Health = Mathf.Clamp(Health, 0, 100);
	}

	private void UpdateScoreText()
	{
		ScoreLabel.Text = $"Score: {SongScore} | Combo: {Combo} | Misses: {Misses}";
	}

	public void OnBeatHit()
	{
		// Called by your Conductor equivalent every beat
		_cameraBopMultiplier = 1.015f; // DEFAULT_BOP_INTENSITY
	}

	private void UpdateCameraBop(double delta)
	{
		// Flixel lerps camera properties every frame back to 1.0
		float decayRate = 0.95f;
		float dt = (float)delta * 60f;

		_cameraBopMultiplier = Mathf.Lerp(1.0f, _cameraBopMultiplier, Mathf.Pow(decayRate, dt));
		GameCamera.Zoom = new Vector2(_currentCameraZoom * _cameraBopMultiplier, _currentCameraZoom * _cameraBopMultiplier);
	}

	public void TweenCameraZoom(float targetZoom, float duration = 0.5f)
	{
		// Godot 4 equivalent of FlxTween.tween
		_cameraZoomTween?.Kill(); // Cancel existing tween
		_cameraZoomTween = GetTree().CreateTween();
		
		_cameraZoomTween.TweenProperty(this, "_currentCameraZoom", targetZoom, duration)
						.SetTrans(Tween.TransitionType.Sine)
						.SetEase(Tween.EaseType.Out);
	}
	#endregion

	#region Flow Control (Substates)
	private void StartCountdown()
	{
		// Example: Godot timer for starting the song
		GetTree().CreateTimer(2.0f).Timeout += () => 
		{
			StartSong();
		};
	}

	private void StartSong()
	{
		InstPlayer.Play();
		VocalsPlayer?.Play();
	}

	private void PauseGame()
	{
		IsPaused = true;
		
		// Instead of openSubState, we pause the SceneTree and instance a PauseMenu UI
		GetTree().Paused = true;
		
		var pauseMenuScene = GD.Load<PackedScene>("res://UI/PauseMenu.tscn");
		var pauseMenu = pauseMenuScene.Instantiate<Control>();
		
		// Ensure PauseMenu's ProcessMode is set to ProcessModeEnum.Always in the inspector
		HudLayer.AddChild(pauseMenu);
	}

	private void TriggerGameOver()
	{
		IsGameOver = true;
		InstPlayer.Stop();
		VocalsPlayer?.Stop();

		// Transition to Game Over State
		var gameOverScene = GD.Load<PackedScene>("res://States/GameOverState.tscn");
		GetTree().ChangeSceneToPacked(gameOverScene);
	}

	private void GenerateSong()
	{
		// Load your chart format here
	}
	#endregion
}
