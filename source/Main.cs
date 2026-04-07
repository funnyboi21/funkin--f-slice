using Godot;
using System;

// Note: Ensure your namespaces match your project.
namespace Funkin;

/// <summary>
/// The main Autoload which initializes the game configurations and starts the initial state.
/// Set this as an Autoload in Project -> Project Settings -> Autoload.
/// </summary>
public partial class Main : Node
{
	[Export] public int GameWidth = 1280;
	[Export] public int GameHeight = 720;
	
	// Instead of a Class<FlxState>, we use a PackedScene in Godot.
	// Assign your InitState.tscn to this in the Godot Inspector.
	[Export] public PackedScene InitialState; 
	
	[Export] public bool SkipSplash = true;

	// A reference to a custom CanvasLayer scene for debug stats (FPS/RAM)
	public Node DebugDisplay;

	public override void _Ready()
	{
		// 1. Initialize Crash Handler First
		AppDomain.CurrentDomain.UnhandledException += CrashHandler;

		// Ensure the game doesn't close automatically so we can clean up resources first.
		GetTree().AutoAcceptQuit = false;

		// Custom Logging initialization (Godot's GD.Print handles standard logs)
		GD.Print("[Main] Booting up...");

		// Placeholder for Mod Loading (e.g., loading PCK files in Godot)
		// PolymodHandler.LoadAllMods();

		// Check rendering context (Godot handles this natively, but we can log it)
		RenderingServer.ViewportSetUpdateMode(GetViewport().GetViewportRid(), RenderingServer.ViewportUpdateMode.Always);
		
		SetupGame();
	}

	private void SetupGame()
	{
		// Initialize UI Framework (If you use a custom UI library in Godot, init here)
		// InitUI();

		// Setup Debug Display (Assuming you have a DebugDisplay.tscn)
		// var debugScene = GD.Load<PackedScene>("res://UI/DebugDisplay.tscn");
		// DebugDisplay = debugScene.Instantiate();
		// AddChild(DebugDisplay);

		// Load Saves
		// Save.Load();

		// Async Video Init (Placeholder for VLC/VideoPlayer initialization)
		InitializeVideoPlayer();

		// Set VSync mode
		// Note: Preferences would be your own custom singleton/static class
		// DisplayServer.WindowSetVsyncMode((DisplayServer.VSyncMode)Preferences.VsyncMode);

		// Set Framerate
		// int framerate = Preferences.UnlockedFramerate ? 0 : Preferences.Framerate;
		// Engine.MaxFps = framerate;

		// In Godot, scaling (FullScreenScaleMode) is handled in:
		// Project Settings -> Display -> Window -> Stretch

		// Load the initial state (FlxGame equivalent)
		if (InitialState != null)
		{
			GetTree().ChangeSceneToPacked(InitialState);
		}
		else
		{
			GD.PushError("InitialState is not assigned in the Main Autoload!");
		}

		// Mobile Repositioning
		if (OS.GetName() == "Android" || OS.GetName() == "iOS")
		{
			RepositionCounters(false, 0);
		}
	}

	public override void _Process(double delta)
	{
		HandleDebugDisplayKeys();

		// PreUpdate / PostUpdate mobile signal equivalent
		if (OS.GetName() == "Android" || OS.GetName() == "iOS")
		{
			RepositionCounters(true, delta);
		}
	}

	// Intercept Window Close events (sys & !mobile equivalent)
	public override void _Notification(int what)
	{
		if (what == NotificationWMCloseRequest)
		{
			GD.PrintRich("[color=red] EXITING [/color] Game is exiting, cleaning up resources...");
			
			// Clean up VLC threads / external resources to prevent memory leaks.
			// hxvlc.util.Handle.dispose() equivalent here
			
			GetTree().Quit(0); // Actually quit the game now
		}
	}

	private void HandleDebugDisplayKeys()
	{
		// Ensure "debug_display" is mapped in Project -> Project Settings -> Input Map
		if (Input.IsActionJustPressed("debug_display"))
		{
			/* Example Logic:
			Preferences.DebugDisplayMode nextMode = Preferences.DebugDisplay switch
			{
				DebugDisplayMode.Off => DebugDisplayMode.Simple,
				DebugDisplayMode.Simple => DebugDisplayMode.Advanced,
				DebugDisplayMode.Advanced => DebugDisplayMode.Off,
				_ => DebugDisplayMode.Off
			};
			Preferences.DebugDisplay = nextMode;
			*/
			GD.Print("Toggled Debug Display");
		}
	}

	private void RepositionCounters(bool lerp, double delta)
	{
		if (DebugDisplay == null) return;

		// In Godot, you usually handle notches via safe areas.
		Rect2 safeArea = DisplayServer.GetDisplaySafeArea();
		Vector2 windowSize = GetViewport().GetVisibleRect().Size;

		// Calculate scaling
		float scaleX = windowSize.X / GameWidth;
		float scaleY = windowSize.Y / GameHeight;
		float scale = Math.Max(Math.Min(scaleX, scaleY), 1);

		// Cast DebugDisplay to a Control node to manipulate its position
		if (DebugDisplay is Control debugUI)
		{
			debugUI.Scale = new Vector2(scale, scale);

			float targetX = Math.Max(safeArea.Position.X, 10);
			float targetY = safeArea.Position.Y + (3 * scale);

			if (lerp)
			{
				// FlxMath.Lerp equivalent
				float newX = Mathf.Lerp(debugUI.Position.X, targetX, (float)delta * 3f);
				debugUI.Position = new Vector2(newX, targetY);
			}
			else
			{
				debugUI.Position = new Vector2(targetX, targetY);
			}
		}
	}

	private void InitializeVideoPlayer()
	{
		// Placeholder for hxvlc async init
		// Godot 4 has built-in VideoStreamPlayer, but if using a GDExtension for VLC:
		GD.PrintRich("[color=orange] VIDEO [/color] Video extension initialized!");
	}

	private void CrashHandler(object sender, UnhandledExceptionEventArgs args)
	{
		Exception e = (Exception)args.ExceptionObject;
		GD.PrintErr($"FATAL CRASH: {e.Message}\n{e.StackTrace}");
		
		// Save crash log to user://crash.log
		using var file = FileAccess.Open("user://crash.log", FileAccess.ModeFlags.Write);
		file?.StoreString($"CRASH: {e.Message}\n{e.StackTrace}");
		
		// System.exit(1) equivalent
		GetTree().Quit(1); 
	}
}
