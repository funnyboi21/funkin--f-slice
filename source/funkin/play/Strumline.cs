using Godot;
using System.Collections.Generic;

namespace FSlice.Gameplay
{
	/// <summary>
	/// One strumline: four receptors + the notes falling toward them.
	/// Two strumlines exist per song (player + opponent).
	/// </summary>
	[GlobalClass]
	public partial class Strumline : Node2D
	{
		// ── Configuration ─────────────────────────────────────────────
		[Export] public bool IsPlayer     = true;
		[Export] public float ScrollSpeed = 600f;   // pixels per second
		[Export] public float HitWindow   = 0.135f; // ±seconds (Bad = ±135 ms)

		// Pixel gap between receptors
		public const float Spacing = 120f;

		// Y position of receptors (relative to this node's origin)
		public const float ReceptorY = 0f;

		// ── Input map names (reassignable in Project > Input Map) ──────
		private static readonly string[] InputActions =
			{ "note_left", "note_down", "note_up", "note_right" };

		// ── Runtime state ─────────────────────────────────────────────
		private Receptor[]       _receptors  = new Receptor[4];
		private Node2D           _noteLayer  = null!;
		private List<Note>       _notes      = new();
		private Queue<NoteData>  _spawnQueue = new();
		private double           _songTime   = 0.0;

		// ── Signals ───────────────────────────────────────────────────
		[Signal] public delegate void NoteHitEventHandler(int direction, StringName rating);
		[Signal] public delegate void NoteMissedEventHandler(int direction);

		// ── Godot callbacks ───────────────────────────────────────────
		public override void _Ready()
		{
			_noteLayer = new Node2D { Name = "NoteLayer" };
			AddChild(_noteLayer);

			for (int i = 0; i < 4; i++)
			{
				var r = new Receptor { Direction = i };
				r.Position = new Vector2(i * Spacing - Spacing * 1.5f, ReceptorY);
				AddChild(r);
				_receptors[i] = r;
			}
		}

		public override void _Process(double delta)
		{
			_songTime += delta;

			SpawnPendingNotes();
			ProcessHoldNotes(delta);
			PruneMissedNotes();
		}

		public override void _Input(InputEvent @event)
		{
			if (!IsPlayer) return;

			for (int dir = 0; dir < 4; dir++)
			{
				if (@event.IsActionPressed(InputActions[dir], exactMatch: true))
					OnDirectionPressed(dir);
				else if (@event.IsActionReleased(InputActions[dir], exactMatch: true))
					OnDirectionReleased(dir);
			}
		}

		// ── Public API ────────────────────────────────────────────────

		/// <summary>Feed in all notes for this strumline before the song starts.</summary>
		public void LoadNotes(IEnumerable<NoteData> notes)
		{
			_spawnQueue.Clear();
			_notes.Clear();

			// Pre-sort and enqueue
			var list = new List<NoteData>(notes);
			list.Sort((a, b) => a.Time.CompareTo(b.Time));
			foreach (var n in list)
				_spawnQueue.Enqueue(n);
		}

		public void SetSongTime(double t) => _songTime = t;

		// ── Input handling ────────────────────────────────────────────
		private void OnDirectionPressed(int dir)
		{
			_receptors[dir].OnPress();

			Note? best = FindBestNote(dir);
			if (best == null) return;  // ghost press — no note nearby

			best.MarkHit();
			_notes.Remove(best);
			_receptors[dir].OnConfirm();

			string rating = GetRating(Mathf.Abs((float)(_songTime - best.Data.Time)));
			EmitSignal(SignalName.NoteHit, dir, rating);
		}

		private void OnDirectionReleased(int dir)
		{
			_receptors[dir].OnRelease();
		}

		// ── Spawning ──────────────────────────────────────────────────
		private void SpawnPendingNotes()
		{
			// Spawn notes whose scroll-in time has arrived.
			// A note should appear (ScrollOffset) pixels above the receptor
			// so it reaches the receptor exactly at its hit time.
			float scrollOffset = (float)(ScrollSpeed * HitWindow * 4f);

			while (_spawnQueue.Count > 0)
			{
				var next = _spawnQueue.Peek();
				double spawnTime = next.Time - (scrollOffset / ScrollSpeed);

				if (_songTime < spawnTime) break;
				_spawnQueue.Dequeue();

				SpawnNote(next);
			}
		}

		private void SpawnNote(NoteData data)
		{
			if (!IsPlayer && !data.MustHit) { /* auto-play handled separately */ }

			var note = new Note
			{
				Data        = data,
				ScrollSpeed = ScrollSpeed,
				ReceptorY   = ReceptorY,
			};

			// X aligns with receptor
			float x = (data.Direction * Spacing) - Spacing * 1.5f;
			// Y starts above the receptor, will scroll down to it
			float travelTime  = (float)(data.Time - _songTime);
			float startY      = ReceptorY - travelTime * ScrollSpeed;
			note.Position     = new Vector2(x, startY);

			_noteLayer.AddChild(note);
			_notes.Add(note);

			// Auto-play opponent strumline
			if (!IsPlayer)
			{
				double timeUntilHit = data.Time - _songTime;
				var timer = GetTree().CreateTimer(Mathf.Max(0f, (float)timeUntilHit));
				timer.Connect(Timer.SignalName.Timeout, Callable.From(() => AutoPlay(note)));
			}
		}

		private void AutoPlay(Note note)
		{
			if (!note.IsInsideTree() || note.IsQueuedForDeletion()) return;
			note.MarkHit();
			_notes.Remove(note);
			_receptors[note.Data.Direction].OnConfirm();
		}

		// ── Hold note processing ──────────────────────────────────────
		private void ProcessHoldNotes(double delta)
		{
			foreach (var note in _notes)
			{
				if (!note.Data.IsHold || !note.Hit) continue;

				// Check if the matching key is still held
				bool held = IsPlayer
					? Input.IsActionPressed(InputActions[note.Data.Direction])
					: true; // auto-hold for opponent

				if (held)
				{
					// note.HoldBody?.Consume((float)(ScrollSpeed * delta));
					if (note.IsHoldComplete(_songTime))
					{
						_notes.Remove(note);
						note.QueueFree();
						break; // list modified — will catch rest next frame
					}
				}
				else
				{
					// Released early — lose the remaining hold
					note.MarkMissed();
					_notes.Remove(note);
					EmitSignal(SignalName.NoteMissed, note.Data.Direction);
					break;
				}
			}
		}

		// ── Miss detection ────────────────────────────────────────────
		private void PruneMissedNotes()
		{
			for (int i = _notes.Count - 1; i >= 0; i--)
			{
				var note = _notes[i];
				if (note.Hit) continue;

				double diff = _songTime - note.Data.Time;
				if (diff > HitWindow)
				{
					note.MarkMissed();
					_notes.RemoveAt(i);
					if (IsPlayer)
						EmitSignal(SignalName.NoteMissed, note.Data.Direction);
				}
			}
		}

		// ── Scoring helpers ───────────────────────────────────────────
		private Note? FindBestNote(int dir)
		{
			Note? best   = null;
			double bestD = double.MaxValue;

			foreach (var note in _notes)
			{
				if (note.Hit || note.Missed) continue;
				if (note.Data.Direction != dir) continue;

				double diff = Mathf.Abs(_songTime - note.Data.Time);
				if (diff <= HitWindow && diff < bestD)
				{
					best  = note;
					bestD = diff;
				}
			}

			return best;
		}

		private static string GetRating(float diff)
		{
			if (diff <= 0.022f) return "Sick";
			if (diff <= 0.045f) return "Good";
			if (diff <= 0.090f) return "Bad";
			return "Shit";
		}
	}
}
