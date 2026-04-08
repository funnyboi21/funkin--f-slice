using Godot;
using System;

public partial class Loading : Control
{
	private double _timer = 0;
	private double _delay = 2.0; // seconds

	public override void _Process(double delta)
	{
		_timer += delta;

		if (_timer >= _delay)
		{
			GetTree().ChangeSceneToFile("res://scenes/Main.tscn");
		}
	}
}
