using Godot;

namespace FSlice.Gameplay
{
    /// <summary>
    /// A single falling arrow note (tap or hold head).
    /// The Strumline spawns these, moves them, and destroys them.
    /// </summary>
    [GlobalClass]
    public partial class Note : Node2D
    {
        // ── Data ─────────────────────────────────────────────────────────
        public NoteData Data;

        /// <summary>Has the player already scored this note?</summary>
        public bool Hit   { get; private set; } = false;

        /// <summary>Was this note missed (scrolled past the hit window)?</summary>
        public bool Missed { get; private set; } = false;

        // ── Visual ───────────────────────────────────────────────────────
        private AnimatedSprite2D _sprite = null!;

        // ── Hold-note body (null for tap notes) ──────────────────────────
        private HoldNote? _holdBody;

        // ── Strumline tells us our spawn Y and scroll speed ───────────────
        public float ScrollSpeed;   // pixels per second
        public float ReceptorY;     // Y position of the receptor (hit target)

        // ── Godot callbacks ──────────────────────────────────────────────
        public override void _Ready()
        {
            _sprite = new AnimatedSprite2D();
            AddChild(_sprite);

            var frames = GD.Load<SpriteFrames>("res://assets/NOTE_assets.res");
            if (frames != null)
                _sprite.SpriteFrames = frames;

            // Play the correct static arrow frame based on direction
            string color = ColorName(Data.Direction);
            string anim  = color + " instance 10000";
            if (_sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation(anim))
                _sprite.Play(anim);

            _sprite.Scale = Vector2.One * 0.7f;

            // Attach hold body if this is a hold note
            if (Data.IsHold)
            {
                _holdBody = new HoldNote();
                _holdBody.Direction    = Data.Direction;
                _holdBody.HoldLength   = (float)(Data.Length * ScrollSpeed);
                AddChild(_holdBody);
            }
        }

        public override void _Process(double delta)
        {
            if (Hit || Missed) return;

            // Move upward (negative Y) toward the receptor
            Position += new Vector2(0f, -(float)(ScrollSpeed * delta));
        }

        // ── Public API ───────────────────────────────────────────────────

        public void MarkHit()
        {
            Hit = true;
            // Hide the head sprite — hold body stays until length is consumed
            _sprite.Visible = false;
            if (!Data.IsHold)
                QueueFree();
        }

        public void MarkMissed()
        {
            Missed = true;
            Modulate = new Color(1f, 1f, 1f, 0.3f);
            // Fade out and free after a short delay
            var tween = CreateTween();
            tween.TweenProperty(this, "modulate:a", 0f, 0.3)
                 .SetDelay(0.15);
            tween.TweenCallback(Callable.From(QueueFree));
        }

        /// <summary>
        /// Returns true when the hold note's body has been fully consumed.
        /// Always false for tap notes.
        /// </summary>
        public bool IsHoldComplete(double songTime)
        {
            if (!Data.IsHold) return false;
            return songTime >= Data.Time + Data.Length;
        }

        // ── Helpers ──────────────────────────────────────────────────────
        public static string ColorName(int dir) => dir switch
        {
            0 => "purple",
            1 => "blue",
            2 => "green",
            3 => "red",
            _ => "purple"
        };

        /// <summary>Distance in pixels between this note's Y and the receptor Y.</summary>
        public float DistanceToReceptor => Position.Y - ReceptorY;
    }
}
