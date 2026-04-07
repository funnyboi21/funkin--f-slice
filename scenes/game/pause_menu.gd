extends CanvasLayer

@onready var options: Node2D = %options
@onready var root: Control = $root
@onready var music: AudioStreamPlayer = $music

@onready var song_name: Alphabet = %song_name
@onready var play_type: Alphabet = %play_type

# --- MEGA PAUSE NEW NODES ---
@onready var disk: AnimatedSprite2D = $disk # I see this in your scene!
# Add a Sprite2D named "opponent_icon" to your scene for the bouncing head
@onready var opponent_icon: Sprite2D = $root/opponent_icon 

var active: bool = true
var selected: int = 0

func _ready() -> void:
	Engine.time_scale = 1.0
	
	# --- Mega Pause Opening Tweens ---
	root.modulate.a = 0.0
	var tween: Tween = create_tween().set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_QUINT).set_parallel(true)
	tween.tween_property(root, ^"modulate:a", 1.0, 0.4)
	
	# Slide options in from the right
	options.position.x += 400
	tween.tween_property(options, ^"position:x", options.position.x - 400, 0.5)
	
	# Slide Disk in from the bottom
	if is_instance_valid(disk):
		disk.position.y += 300
		disk.modulate.a = 0.0
		tween.tween_property(disk, ^"position:y", disk.position.y - 300, 0.5)
		tween.tween_property(disk, ^"modulate:a", 1.0, 0.4)
		disk.play("default") # Or whatever your disk spinning animation is named

	if is_instance_valid(opponent_icon):
		opponent_icon.position.y += 200
		opponent_icon.modulate.a = 0.0
		tween.tween_property(opponent_icon, ^"position:y", opponent_icon.position.y - 200, 0.5)
		tween.tween_property(opponent_icon, ^"modulate:a", 1.0, 0.4)

	_init_game_data()
	change_selection()

func _init_game_data() -> void:
	create_tween().tween_property(music, ^"volume_linear", 0.9, 2.0).set_delay(0.5)
	
	if not is_instance_valid(Game.instance): return
	
	# Setup Music
	if is_instance_valid(Game.instance.skin) and is_instance_valid(Game.instance.skin.pause_music):
		music.stream = Game.instance.skin.pause_music
		music.play()

	# Connect to Conductor for Beat Bouncing
	if is_instance_valid(Conductor.instance):
		Conductor.instance.beat_hit.connect(_on_beat_hit)

	# Format Song Name
	var keys: Array = Game.PlayMode.keys()
	song_name.text = "%s\n(%s)" %[Game.instance.metadata.get_full_name(), Game.difficulty.to_upper()]
	
	if song_name.size.x > Global.game_size.x:
		song_name.scale = Vector2.ONE * (Global.game_size.x / song_name.size.x * 0.9)
	song_name.position.x = Global.game_size.x - (float(song_name.size.x) * song_name.scale.x) - 16.0
	
	play_type.text = keys[Game.mode].to_upper()
	play_type.position = Global.game_size - (Vector2(play_type.size) * 0.75) - Vector2(16.0, 16.0)

	# Fetch the correct icon from the current game opponent
	if is_instance_valid(opponent_icon) and is_instance_valid(Game.instance.opponent):
		# F-Slice icons usually have a specific texture and frames setup
		var icon_sprite = Game.instance.opponent.icon
		if is_instance_valid(icon_sprite):
			opponent_icon.texture = icon_sprite.texture
			opponent_icon.hframes = icon_sprite.hframes
			opponent_icon.vframes = icon_sprite.vframes

func _on_beat_hit(beat: int) -> void:
	# Bounces the opponent icon to the beat!
	if is_instance_valid(opponent_icon):
		opponent_icon.scale = Vector2(1.15, 1.15)
		var bounce_tween = create_tween().set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_CIRC)
		bounce_tween.tween_property(opponent_icon, ^"scale", Vector2.ONE, 0.2)

func _input(event: InputEvent) -> void:
	if not visible or not active or not event.is_pressed() or event.is_echo(): return

	if event.is_action(&"ui_down") or event.is_action(&"ui_up"):
		change_selection(roundi(Input.get_axis("ui_up", "ui_down")))
		
	if event.is_action(&"ui_accept"):
		for option: ListedAlphabet in options.get_children():
			if option.target_y != 0: continue
			
			var type: StringName = option.name.to_lower()
			
			match type:
				&"resume":
					_close_menu()
				&"restart":
					_close_menu(true)
					get_tree().reload_current_scene()
				&"options":
					OptionsMenu.target_scene = "res://scenes/game/game.tscn"
					_close_menu()
					SceneManager.switch_to(load("res://scenes/menus/options_menu.tscn"))
				&"chart_editor":
					# Custom entry! Ensure you duplicate a ListedAlphabet node and name it "chart_editor"
					_close_menu()
					SceneManager.switch_to(load("res://scenes/editors/chart_editor.tscn"))
				&"quit":
					_close_menu()
					Game.instance.finish_song(true)
				_:
					printerr("Pause Option %s unimplemented." % type)

func change_selection(amount: int = 0) -> void:
	selected = wrapi(selected + amount, 0, options.get_child_count())

	if amount != 0:
		GlobalAudio.get_player("MENU/SCROLL").play()
		
	for i: int in options.get_child_count():
		var option: ListedAlphabet = options.get_child(i)
		option.target_y = i - selected
		option.modulate.a = 1.0 if option.target_y == 0 else 0.6
		
		# Optional: Scale the selected text up slightly for extra juice
		var target_scale = Vector2.ONE * (1.1 if option.target_y == 0 else 0.8)
		create_tween().tween_property(option, ^"scale", target_scale, 0.1)

func _close_menu(restarting: bool = false) -> void:
	active = false
	
	if not restarting:
		GlobalAudio.get_player("MENU/CONFIRM").play()
		
	# Smooth exit tweens
	var tween: Tween = create_tween().set_ease(Tween.EASE_IN).set_trans(Tween.TRANS_QUINT).set_parallel(true)
	tween.tween_property(root, ^"modulate:a", 0.0, 0.3)
	tween.tween_property(options, ^"position:x", options.position.x + 400, 0.3)
	if is_instance_valid(disk): tween.tween_property(disk, ^"position:y", disk.position.y + 300, 0.3)
	if is_instance_valid(opponent_icon): tween.tween_property(opponent_icon, ^"position:y", opponent_icon.position.y + 200, 0.3)
	
	await tween.finished
	close()

func close() -> void:
	queue_free()
	get_viewport().set_input_as_handled()
	visible = false
	get_tree().current_scene.process_mode = Node.PROCESS_MODE_INHERIT
	
	if is_instance_valid(Conductor.instance):
		Engine.time_scale = Conductor.instance.rate
		Conductor.instance.beat_hit.disconnect(_on_beat_hit)
		
	if is_instance_valid(Game.instance):
		Game.instance.conductor.active = true
		Game.instance.unpaused.emit()
