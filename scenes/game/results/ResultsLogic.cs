using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class ResultsLogic : Node2D
{
	// These would be passed from the Gameplay scene
	public int Sicks = 0;
	public int Goods = 0;
	public int Bads = 0;
	public int Shits = 0;
	public int Misses = 0;
	public int TotalScore = 0;

	[Export] public AudioStream TallySound;
	[Export] public AudioStream ConfirmSound;

	private AnimatedSprite2D _soundSystem;
	private AnimatedSprite2D _bf;
	private AnimatedSprite2D _gf;
	private Node2D _tallyContainer;
	private Node2D _resultsBanner;

	public override void _Ready()
	{
		_soundSystem = GetNode<AnimatedSprite2D>("SoundSystem");
		_bf = GetNode<AnimatedSprite2D>("Characters/BF");
		_gf = GetNode<AnimatedSprite2D>("Characters/GF");
		_tallyContainer = GetNode<Node2D>("UI/TallyContainer");
		_resultsBanner = GetNode<Node2D>("UI/ResultsBanner");

		// Hide everything for the intro sequence
		_tallyContainer.Modulate = new Color(1, 1, 1, 0);
		_resultsBanner.Position = new Vector2(_resultsBanner.Position.X, -500);

		StartSequence();
	}

	private async void StartSequence()
	{
		// 1. Drop the Sound System
		var tween = CreateTween().SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
		_soundSystem.Position = new Vector2(_soundSystem.Position.X, -1000);
		tween.TweenProperty(_soundSystem, "position:y", 400, 0.8f);
		
		await ToSignal(GetTree().CreateTimer(0.8f), "timeout");

		// 2. Pop in the Results Banner
		var bannerTween = CreateTween().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		bannerTween.TweenProperty(_resultsBanner, "position:y", 100, 0.5f);
		
		// 3. Play Character Anims
		_gf.Play("Girlfriend Good Anim");
		_bf.Play("Boyfriend Good Anim");

		await ToSignal(GetTree().CreateTimer(0.5f), "timeout");

		// 4. Sequence the Tallies
		_tallyContainer.Modulate = Colors.White;
		await TallyCategory("Sicks", Sicks);
		await TallyCategory("Goods", Goods);
		await TallyCategory("Bads", Bads);
		await TallyCategory("Shits", Shits);
		await TallyCategory("Misses", Misses);

		// 5. Final Score Tally
		await TallyScore(TotalScore);
	}

	private async Task TallyCategory(string category, int count)
	{
		// Here you would instantiate your 'tallieNumber.xml' digits
		GD.Print($"Tallying {category}: {count}");
		
		// Play the tiny "tic" sound FNF uses
		// GlobalAudio.Play(TallySound); 
		
		// Visual pop effect for the specific category
		Node2D catNode = _tallyContainer.GetNode<Node2D>(category);
		var t = CreateTween();
		t.TweenProperty(catNode, "scale", new Vector2(1.2f, 1.2f), 0.05f);
		t.TweenProperty(catNode, "scale", Vector2.One, 0.1f);

		await ToSignal(GetTree().CreateTimer(0.2f), "timeout");
	}

	private async Task TallyScore(int finalScore)
	{
		// Smoothly roll the numbers up
		int displayScore = 0;
		var tween = CreateTween();
		tween.TweenMethod(Callable.From<int>((v) => {
			displayScore = v;
			UpdateScoreDisplay(displayScore);
		}), 0, finalScore, 1.5f);
		
		await ToSignal(tween, "finished");
	}

	private void UpdateScoreDisplay(int value)
	{
		// This function would use your 'score-digital-numbers.xml' 
		// to update the visual digits on screen
	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_accept"))
		{
			// GlobalAudio.Play(ConfirmSound);
			// SceneManager.SwitchTo("res://scenes/menus/freeplay_menu.tscn");
		}
	}
}
