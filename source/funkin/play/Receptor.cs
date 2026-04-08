using Godot;

namespace FSlice.Gameplay
{
    /// <summary>
    /// One of the four arrow receptors on a strumline.
    /// Plays idle / press / confirm animations from NOTE_assets.
    /// </summary>
    [GlobalClass]
    public partial class Receptor : Node2D
    {
        // ── Direction: 0=Left 1=Down 2=Up 3=Right ──────────────────────
        [Export] public int Direction { get; set; } = 0;

        // Pixel size of one receptor sprite (used by Strumline for spacing)
        public const float Size = 112f;

        // ── State ───────────────────────────────────────────────────────
        public enum State { Idle, Press, Confirm }
        private State _state = State.Idle;

        private AnimatedSprite2D _sprite = null!;

        // How long (seconds) the confirm flash lasts before returning to idle
        private const double ConfirmDuration = 0.12;
        private double _confirmTimer = 0.0;

        // ── Godot callbacks ─────────────────────────────────────────────
        public override void _Ready()
        {
            _sprite = new AnimatedSprite2D();
            AddChild(_sprite);

            // SpriteFrames must have been set on this node via Inspector or
            // GameplayScene.SetupSpriteFrames() before _Ready runs.
            // The resource path matches what the project stores the atlas in.
            var frames = GD.Load<SpriteFrames>("res://assets/NOTE_assets.res");
            if (frames != null)
                _sprite.SpriteFrames = frames;
            else
                GD.PrintErr("[Receptor] Could not load res://assets/NOTE_assets.res");

            _sprite.Scale = Vector2.One * 0.7f;
            PlayIdle();
        }

        public override void _Process(double delta)
        {
            if (_state == State.Confirm)
            {
                _confirmTimer -= delta;
                if (_confirmTimer <= 0.0)
                    PlayIdle();
            }
        }

        // ── Public API ──────────────────────────────────────────────────

        /// <summary>Called while the matching key is held down (no note hit yet).</summary>
        public void OnPress()
        {
            if (_state == State.Confirm) return; // don't interrupt confirm flash
            SetState(State.Press);
        }

        /// <summary>Called when the player successfully hits a note.</summary>
        public void OnConfirm()
        {
            SetState(State.Confirm);
            _confirmTimer = ConfirmDuration;
        }

        /// <summary>Called when the key is released and no confirm is active.</summary>
        public void OnRelease()
        {
            if (_state == State.Press)
                SetState(State.Idle);
        }

        // ── Helpers ─────────────────────────────────────────────────────
        private void PlayIdle() => SetState(State.Idle);

        private void SetState(State s)
        {
            _state = s;
            string anim = s switch
            {
                State.Press   => DirName(Direction) + " press",
                State.Confirm => DirName(Direction) + " confirm",
                _             => "arrow static instance " + (Direction + 1) + "0000"
                // The XML has "arrow static instance 10000" through "40000"
                // mapped to directions 0-3 (Left/Down/Up/Right)
            };

            if (_sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation(anim))
                _sprite.Play(anim);
        }

        private static string DirName(int d) => d switch
        {
            0 => "left",
            1 => "down",
            2 => "up",
            3 => "right",
            _ => "left"
        };
    }
}
