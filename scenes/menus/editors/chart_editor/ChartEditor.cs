using Godot;
using System.Collections.Generic;

public partial class ChartEditor : Node2D
{
	// --- Chart Settings ---
	public float Bpm = 100.0f;
	public float ScrollSpeed = 2.0f;
	public int GridSize = 40; // Pixels per grid square
	public int Columns = 8;   // 4 for Dad, 4 for BF

	// --- State ---
	public float SongTime = 0.0f; // Current time in milliseconds
	public bool IsPlaying = false;
	
	// FNF Note Data: X = Time (ms), Y = Column (0-7), Z = Sustain Length (ms)
	public List<Vector3> Notes = new List<Vector3>();

	// --- Nodes ---
	private Control _gridDisplay;
	private AudioStreamPlayer _instrumental; // Add an AudioStreamPlayer to your scene!

	public override void _Ready()
	{
		// Get the node where we will draw the grid
		_gridDisplay = GetNode<Control>("UI/GridDisplay");
		
		// Tell the GridDisplay to use OUR _DrawGrid method
		_gridDisplay.Draw += DrawGrid;
		_gridDisplay.GuiInput += OnGridInput;
	}

	public override void _Process(double delta)
	{
		if (IsPlaying && _instrumental != null && _instrumental.Playing)
		{
			// Sync time to audio (converted to milliseconds)
			SongTime = _instrumental.GetPlaybackPosition() * 1000f;
		}

		// Force the grid to redraw every frame so it scrolls smoothly
		_gridDisplay.QueueRedraw();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// Spacebar to Play/Pause
		if (@event.IsActionPressed("ui_accept")) 
		{
			IsPlaying = !IsPlaying;
			if (IsPlaying) _instrumental?.Play((float)(SongTime / 1000.0));
			else _instrumental?.Stop();
		}

		// Mouse Wheel to scroll up and down the chart
		if (!IsPlaying && @event is InputEventMouseButton mouseBtn)
		{
			float stepMs = (60000f / Bpm) / 4f; // Time for one 16th note step
			if (mouseBtn.ButtonIndex == MouseButton.WheelUp) SongTime += stepMs;
			if (mouseBtn.ButtonIndex == MouseButton.WheelDown) SongTime -= stepMs;
			
			if (SongTime < 0) SongTime = 0;
		}
	}

	// --- Grid Interaction (Placing Notes) ---
	private void OnGridInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseBtn && mouseBtn.Pressed && mouseBtn.ButtonIndex == MouseButton.Left)
		{
			// Figure out where the user clicked on the grid
			Vector2 clickPos = mouseBtn.Position;
			
			int col = (int)(clickPos.X / GridSize);
			if (col < 0 || col >= Columns) return;

			// Calculate the time of the note based on the Y click position and current scroll
			// Note: In editors, the bottom of the screen usually represents the current SongTime
			float yOffset = _gridDisplay.Size.Y - clickPos.Y; 
			float clickTime = SongTime + (yOffset / ScrollSpeed);

			// Snap time to the nearest 16th note grid line
			float stepMs = (60000f / Bpm) / 4f;
			float snappedTime = Mathf.Round(clickTime / stepMs) * stepMs;

			ToggleNote(snappedTime, col);
		}
	}

	private void ToggleNote(float time, int col)
	{
		// Check if note already exists here, if so, remove it (Toggle off)
		for (int i = 0; i < Notes.Count; i++)
		{
			if (Mathf.Abs(Notes[i].X - time) < 5.0f && (int)Notes[i].Y == col)
			{
				Notes.RemoveAt(i);
				return;
			}
		}

		// Otherwise, place a new note (Z = 0 means no sustain yet)
		Notes.Add(new Vector3(time, col, 0));
		GD.Print($"Placed note at Column {col}, Time {time}ms");
	}

	// --- High Performance Drawing ---
	private void DrawGrid()
	{
		// Using Godot's built in _Draw functions is 100x faster than spawning hundreds of Sprite nodes!
		
		float width = Columns * GridSize;
		float height = _gridDisplay.Size.Y;
		Color gridColor = new Color(0.8f, 0.8f, 0.8f, 0.3f);
		Color beatColor = new Color(1.0f, 1.0f, 1.0f, 0.8f); // Thicker line for beats

		// 1. Draw Columns (Vertical Lines)
		for (int i = 0; i <= Columns; i++)
		{
			float x = i * GridSize;
			_gridDisplay.DrawLine(new Vector2(x, 0), new Vector2(x, height), gridColor, i == 4 ? 3f : 1f); // Middle line is thicker
		}

		// 2. Draw Rows (Horizontal Lines mapped to time)
		float stepMs = (60000f / Bpm) / 4f; // 16th note step in ms
		float startDrawTime = SongTime;
		float endDrawTime = SongTime + (height / ScrollSpeed);
		
		// Snap the starting draw line to the nearest step
		float currentLineTime = Mathf.Floor(startDrawTime / stepMs) * stepMs;

		int beatCount = 0;
		while (currentLineTime <= endDrawTime)
		{
			float yPos = height - ((currentLineTime - SongTime) * ScrollSpeed);
			
			// Every 4th line is a full Beat
			bool isBeat = Mathf.RoundToInt(currentLineTime / stepMs) % 4 == 0;
			
			_gridDisplay.DrawLine(new Vector2(0, yPos), new Vector2(width, yPos), isBeat ? beatColor : gridColor, isBeat ? 2f : 1f);
			
			currentLineTime += stepMs;
			beatCount++;
		}

		// 3. Draw The Notes
		foreach (var note in Notes)
		{
			float noteTime = note.X;
			int col = (int)note.Y;

			// Only draw notes that are visible on screen
			if (noteTime >= startDrawTime - 200 && noteTime <= endDrawTime + 200)
			{
				float nx = col * GridSize;
				float ny = height - ((noteTime - SongTime) * ScrollSpeed);

				// Draw a colored square for the note (You can replace this with DrawTexture for note sprites!)
				Color noteColor = GetNoteColor(col % 4); 
				_gridDisplay.DrawRect(new Rect2(nx, ny - GridSize, GridSize, GridSize), noteColor);
			}
		}
	}

	private Color GetNoteColor(int dir)
	{
		return dir switch
		{
			0 => Colors.Purple, // Left
			1 => Colors.Cyan,   // Down
			2 => Colors.Green,  // Up
			_ => Colors.Red     // Right
		};
	}
}
