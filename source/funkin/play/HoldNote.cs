using Godot;

namespace FSlice.Gameplay
{
    /// <summary>
    /// The graphical body + end cap of a hold note.
    /// Parented under a Note node so it inherits position automatically.
    /// Uses NOTE_hold_assets.png (tiled body pieces + end caps).
    /// </summary>
    [GlobalClass]
    public partial class HoldNote : Node2D
    {
        public int   Direction  = 0;
        public float HoldLength = 100f;  // total pixel length of the hold body

        // Hold assets piece size from the XML (51×44 body, 51×64 end cap)
        private const float PieceH = 44f;
        private const float CapH   = 64f;
        private const float PieceW = 51f;

        // How much of the hold body remains (shrinks as the player holds)
        public float RemainingLength;

        private Node2D _bodyContainer = null!;
        private Sprite2D _endCap      = null!;
        private Texture2D? _bodyTex;
        private Texture2D? _capTex;

        public override void _Ready()
        {
            RemainingLength = HoldLength;

            // Load the hold asset texture
            _bodyTex = GD.Load<Texture2D>("res://assets/NOTE_hold_assets.png");
            _capTex  = _bodyTex;

            _bodyContainer = new Node2D();
            AddChild(_bodyContainer);

            _endCap = new Sprite2D();
            AddChild(_endCap);

            BuildBody();
        }

        // ── Called every frame by the Note while being held ──────────────
        public void Consume(float pixelsConsumed)
        {
            RemainingLength = Mathf.Max(0f, RemainingLength - pixelsConsumed);
            BuildBody();
        }

        public bool IsComplete => RemainingLength <= 0f;

        // ── Builds tiled body sprites + end cap ──────────────────────────
        private void BuildBody()
        {
            // Clear previous sprites
            foreach (Node child in _bodyContainer.GetChildren())
                child.QueueFree();

            if (_bodyTex == null || RemainingLength <= 0f)
            {
                _endCap.Visible = false;
                return;
            }

            // Color column in the hold asset sheet: left=0 down=1 up=2 right=3
            // The hold asset PNG is a horizontal strip of coloured pieces.
            // We tile Sprite2D nodes with region_rect to stack the body downward.

            float y        = 0f;
            float remaining = RemainingLength;

            while (remaining > 0f)
            {
                float h      = Mathf.Min(remaining, PieceH);
                var piece    = new Sprite2D();
                piece.Texture    = _bodyTex;
                piece.RegionEnabled = true;
                piece.RegionRect = GetBodyRegion(Direction, h);
                piece.Position   = new Vector2(0f, y + h * 0.5f);
                _bodyContainer.AddChild(piece);
                y         += h;
                remaining -= h;
            }

            // End cap at the bottom
            _endCap.Texture        = _capTex;
            _endCap.RegionEnabled  = true;
            _endCap.RegionRect     = GetCapRegion(Direction);
            _endCap.Position       = new Vector2(0f, y + CapH * 0.5f);
            _endCap.Visible        = true;
        }

        // ── Region helpers ────────────────────────────────────────────────
        // NOTE_hold_assets.png layout (from XML):
        //   red   hold piece  x=1172 y=457 w=51 h=44
        //   green hold piece  x=1227 y=457 w=51 h=44
        //   blue  hold piece  x=1282 y=457 w=51 h=44
        //   purple hold piece x=1337 y=457 w=51 h=44
        //   red   hold end    x=952  y=452 w=51 h=64
        //   green hold end    x=1007 y=452 w=51 h=64
        //   blue  hold end    x=1062 y=452 w=51 h=64
        //   purple hold end   x=1117 y=452 w=51 h=64
        //   (all inside NOTE_assets.png)
        //   Direction mapping: 0=purple 1=blue 2=green 3=red

        private static readonly Rect2[] PieceRegions =
        {
            new(1337, 457, 51, 44),  // 0 purple
            new(1282, 457, 51, 44),  // 1 blue
            new(1227, 457, 51, 44),  // 2 green
            new(1172, 457, 51, 44),  // 3 red
        };

        private static readonly Rect2[] CapRegions =
        {
            new(1117, 452, 51, 64),  // 0 purple
            new(1062, 452, 51, 64),  // 1 blue
            new(1007, 452, 51, 64),  // 2 green
            new(952,  452, 51, 64),  // 3 red
        };

        private Rect2 GetBodyRegion(int dir, float h)
        {
            var r = PieceRegions[Mathf.Clamp(dir, 0, 3)];
            return new Rect2(r.Position.X, r.Position.Y, r.Size.X, h);
        }

        private Rect2 GetCapRegion(int dir) =>
            CapRegions[Mathf.Clamp(dir, 0, 3)];
    }
}
