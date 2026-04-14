using Godot;

public partial class Loading : Control
{
	[Export] public string TargetScene = "res://scenes/Main.tscn";

	private Array<double> _progress = new();
	private bool _loadStarted = false;

	public override void _Ready()
	{
		ResourceLoader.LoadThreadedRequest(TargetScene);
		_loadStarted = true;

		// Keep your animated sprite playing — no changes needed in the scene
		var spinner = GetNode<AnimatedSprite2D>("animated_sprite_2d");
		spinner.Play("loading export");
	}

	public override void _Process(double delta)
	{
		if (!_loadStarted) return;

		var status = ResourceLoader.LoadThreadedGetStatus(TargetScene, _progress);

		switch (status)
		{
			case ResourceLoader.ThreadLoadStatus.Loaded:
				var packed = (PackedScene)ResourceLoader.LoadThreadedGet(TargetScene);
				GetTree().ChangeSceneToPacked(packed);
				break;

			case ResourceLoader.ThreadLoadStatus.Failed:
				GD.PushError($"Loading: failed to load {TargetScene}");
				break;
		}
	}
}
