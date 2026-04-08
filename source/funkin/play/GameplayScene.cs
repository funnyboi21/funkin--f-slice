using Godot;

namespace FSlice.Gameplay
{
	/// <summary>
	/// Root gameplay scene.  Wire up child nodes in the Inspector or let
	/// this script build them procedurally (useful when starting from scratch).
	///
	/// Scene tree expected:
	///   GameplayScene (Node2D)
	///   ├── Music          (AudioStreamPlayer)
	///   ├── PlayerStrumline   (Strumline)   — IsPlayer = true
	///   ├── OpponentStrumline (Strumline)   — IsPlayer = false
	///   └── UI (CanvasLayer)
	///       ├── HealthBar   (ProgressBar)
	///       ├── ScoreLabel  (Label)
	///       └── ComboLabel  (Label)
	///
	/// Set ChartPath in the Inspector (e.g. "res://assets/songs/bopeebo/chart.json").
	/// Leave it empty to use the built-in demo chart.
	/// </summary>
	[GlobalClass]
	public partial class GameplayScene : Node2D
	{
		// ── Inspector exports ─────────────────────────────────────────
		[Export] public string ChartPath   = "";
		[Export] public NodePath MusicPath = "Music";

		// ── Node references ───────────────────────────────────────────
		private Strumline         _player   = null!;
		private Strumline         _opponent = null!;
		private AudioStreamPlayer _music    = null!;
		private ProgressBar       _healthBar = null!;
		private Label             _scoreLabel = null!;
		private Label             _comboLabel = null!;

		// ── Score / health state ──────────────────────────────────────
		private int   _score  = 0;
		private int   _combo  = 0;
		private float _health = 50f;   // 0–100; lose at 0
		private bool  _dead   = false;

		private static readonly (string name, int points, float health)[] RatingTable =
		{
			("Sick",  350, +2.5f),
			("Good",  200, +1.5f),
			("Bad",   50,  -1.0f),
			("Shit",  20,  -2.0f),
		};

		private const float MissHealthPenalty = -4.0f;
		private const float MaxHealth         = 100f;

		// ── Godot callbacks ───────────────────────────────────────────
		public override void _Ready()
		{
			// ── Strumlines ───────────────────────────────────────────
			_player   = GetOrCreate<Strumline>("PlayerStrumline");
			_opponent = GetOrCreate<Strumline>("OpponentStrumline");

			// Layout: opponent left, player right (classic FNF layout)
			float screenW = GetViewport().GetVisibleRect().Size.X;
			_opponent.Position = new Vector2(screenW * 0.25f, 100f);
			_player.Position   = new Vector2(screenW * 0.75f, 100f);

			_player.IsPlayer     = true;
			_opponent.IsPlayer   = false;
			_player.ScrollSpeed  = 600f;
			_opponent.ScrollSpeed = 600f;

			_player.NoteHit    += OnNoteHit;
			_player.NoteMissed += OnNoteMissed;

			// ── Music ────────────────────────────────────────────────
			_music = GetNode<AudioStreamPlayer>(MusicPath);

			// ── UI ───────────────────────────────────────────────────
			SetupUI();

			// ── Chart ────────────────────────────────────────────────
			Chart chart = string.IsNullOrEmpty(ChartPath)
				? Chart.Demo()
				: Chart.FromJson(ChartPath);

			_player.ScrollSpeed   = chart.Speed * 250f;
			_opponent.ScrollSpeed = chart.Speed * 250f;

			// Split notes by MustHit flag
			var playerNotes   = new System.Collections.Generic.List<NoteData>();
			var opponentNotes = new System.Collections.Generic.List<NoteData>();
			foreach (var n in chart.Notes)
				(n.MustHit ? playerNotes : opponentNotes).Add(n);

			_player.LoadNotes(playerNotes);
			_opponent.LoadNotes(opponentNotes);

			// Start after a short countdown
			var timer = GetTree().CreateTimer(1.0);
			timer.Connect(Timer.SignalName.Timeout, Callable.From(StartSong));
		}

		public override void _Process(double delta)
		{
			if (_dead) return;

			// Keep strumlines in sync with audio playback position
			if (_music.Playing)
			{
				double t = _music.GetPlaybackPosition();
				_player.SetSongTime(t);
				_opponent.SetSongTime(t);
			}

			// Lose condition
			if (_health <= 0f && !_dead)
				OnDeath();
		}

		// ── Signal handlers ───────────────────────────────────────────
		private void OnNoteHit(int direction, StringName rating)
		{
			string r = rating;
			_combo++;

			foreach (var row in RatingTable)
			{
				if (row.name == r)
				{
					_score  += row.points + (_combo > 10 ? _combo * 5 : 0);
					_health  = Mathf.Clamp(_health + row.health, 0f, MaxHealth);
					break;
				}
			}

			UpdateUI();
			FlashCombo(r);
		}

		private void OnNoteMissed(int direction)
		{
			_combo  = 0;
			_health = Mathf.Clamp(_health + MissHealthPenalty, 0f, MaxHealth);
			UpdateUI();
			if (_comboLabel != null)
				_comboLabel.Text = "x0";
		}

		// ── Song control ──────────────────────────────────────────────
		private void StartSong()
		{
			if (_music.Stream != null)
				_music.Play();
		}

		private void OnDeath()
		{
			_dead = true;
			_music.Stop();
			GD.Print("[GameplayScene] Player died! Implement game-over screen here.");
			// GetTree().ChangeSceneToFile("res://source/scenes/GameOver.tscn");
		}

		// ── UI setup & update ─────────────────────────────────────────
		private void SetupUI()
		{
			CanvasLayer? ui = GetNodeOrNull<CanvasLayer>("UI");
			if (ui == null)
			{
				ui = new CanvasLayer { Name = "UI" };
				AddChild(ui);
			}

			_healthBar = GetNodeOrNull<ProgressBar>("UI/HealthBar")
						 ?? CreateHealthBar(ui);

			_scoreLabel = GetNodeOrNull<Label>("UI/ScoreLabel")
						  ?? CreateLabel(ui, "ScoreLabel", new Vector2(20, 10));

			_comboLabel = GetNodeOrNull<Label>("UI/ComboLabel")
						  ?? CreateLabel(ui, "ComboLabel",
							  new Vector2(GetViewport().GetVisibleRect().Size.X * 0.5f - 60f, 200f));

			UpdateUI();
		}

		private void UpdateUI()
		{
			if (_healthBar  != null) _healthBar.Value   = _health;
			if (_scoreLabel != null) _scoreLabel.Text   = $"Score: {_score:N0}";
		}

		private void FlashCombo(string rating)
		{
			if (_comboLabel == null) return;
			_comboLabel.Text = $"{rating}!  x{_combo}";
			_comboLabel.Modulate = Colors.White;

			var tween = CreateTween();
			tween.TweenProperty(_comboLabel, "modulate:a", 0f, 0.6)
				 .SetDelay(0.3);
		}

		// ── Quick node constructors ────────────────────────────────────
		private ProgressBar CreateHealthBar(CanvasLayer parent)
		{
			var bar       = new ProgressBar { Name = "HealthBar" };
			float w       = GetViewport().GetVisibleRect().Size.X;
			bar.Position  = new Vector2(w * 0.2f, 20f);
			bar.Size      = new Vector2(w * 0.6f, 20f);
			bar.MinValue  = 0;
			bar.MaxValue  = MaxHealth;
			bar.Value     = _health;
			parent.AddChild(bar);
			return bar;
		}

		private Label CreateLabel(CanvasLayer parent, string name, Vector2 pos)
		{
			var lbl       = new Label { Name = name };
			lbl.Position  = pos;
			lbl.AddThemeColorOverride("font_color", Colors.White);
			parent.AddChild(lbl);
			return lbl;
		}

		// ── Helpers ───────────────────────────────────────────────────
		private T GetOrCreate<T>(string nodeName) where T : Node2D, new()
		{
			var existing = GetNodeOrNull<T>(nodeName);
			if (existing != null) return existing;

			var node = new T { Name = nodeName };
			AddChild(node);
			return node;
		}
	}
}
