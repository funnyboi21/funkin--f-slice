class_name OptionsMenu extends Node2D

const default_target_scene: String = 'res://scenes/menus/main_menu.tscn'
static var target_scene: String = default_target_scene

@onready var interface: Control = %interface
# WARNING: Ensure %categories is NOT inside a CenterContainer. 
# Its parent should be a standard Control so we can freely tween its position.x!
@onready var categories: HBoxContainer = %categories 

@onready var section: Node2D = %section
@onready var options_label: AnimatedSprite2D = %options_label
@onready var section_label: Alphabet = %section_label
@onready var conductor: Conductor = %conductor

var section_tween: Tween
var carousel_tween: Tween # New tween for sliding the icons

var selected: int = 0
var active: bool = true

func _ready() -> void:
	var music_player: AudioStreamPlayer = GlobalAudio.music
	music_player.stream = load('uid://ddoyqrhrcjw1j')
	music_player.play()
	
	conductor.reset()
	conductor.tempo = 137.0
	conductor.target_audio = music_player
	conductor.beat_hit.connect(_on_beat_hit)
	
	# Wait one frame for the HBoxContainer to calculate the sizes of the icons
	await get_tree().process_frame 
	change_selection()

func _input(event: InputEvent) -> void:
	if not active or event.is_echo() or not event.is_pressed():
		return
		
	# V-Slice Horizontal Carousel Logic
	if event.is_action(&'ui_left'):
		change_selection(-1)
	elif event.is_action(&'ui_right'):
		change_selection(1)
		
	if event.is_action(&'ui_accept'):
		select_current()
		
	if event.is_action(&'ui_cancel'):
		active = false
		GlobalAudio.get_player('MENU/CANCEL').play()
		SceneManager.switch_to(load(target_scene))
		target_scene = default_target_scene

func change_selection(amount: int = 0) -> void:
	if amount != 0:
		GlobalAudio.get_player('MENU/SCROLL').play()
		
	selected = wrapi(selected + amount, 0, categories.get_child_count())

	# --- 1. Text Juice (Fixed for Alphabet Node2D) ---
	section_label.text = categories.get_child(selected).name.to_upper()
	
	# Force the F-Slice Alphabet node to build from the center out
	section_label.horizontal_alignment = "Center" 
	
	# Hard-set the X position to the exact center of the screen (1280 / 2 = 640)
	section_label.position.x = 640.0 
	
	section_label.position.y = 20.0 # Start slightly higher
	section_label.modulate.a = 0.0

	if is_instance_valid(section_tween) and section_tween.is_running():
		section_tween.kill()
		
	section_tween = create_tween().set_trans(Tween.TRANS_EXPO).set_ease(Tween.EASE_OUT).set_parallel()
	section_tween.tween_property(section_label, ^'position:y', 48.0, 0.4)
	section_tween.tween_property(section_label, ^'modulate:a', 1.0, 0.4).set_trans(Tween.TRANS_CUBIC)

	# --- 2. Icon Scaling ---
	for i: int in categories.get_child_count():
		var child: Category = categories.get_child(i)
		if i == selected:
			child.target_alpha = 1.0
			child.target_scale = 1.0
			
			# Little pop effect on the selected icon
			child.scale = Vector2(1.2, 1.2) 
		else:
			child.target_alpha = 0.5
			child.target_scale = 0.7

	# --- 3. Carousel Centering (The Secret Sauce) ---
	var target_node: Control = categories.get_child(selected)
	
	# Formula: (Screen Width / 2) - (Node's X Position) - (Half of Node's Width)
	# Assuming Global.game_size.x is 1280
	var center_x: float = (1280.0 / 2.0) - target_node.position.x - (target_node.size.x / 2.0)
	
	if is_instance_valid(carousel_tween) and carousel_tween.is_running():
		carousel_tween.kill()
		
	carousel_tween = create_tween().set_trans(Tween.TRANS_EXPO).set_ease(Tween.EASE_OUT)
	carousel_tween.tween_property(categories, ^"position:x", center_x, 0.35)

func select_current() -> void:
	active = false
	GlobalAudio.get_player('MENU/CONFIRM').play()

	# Slide the interface out to the left, and the submenu in from the right
	var tween: Tween = create_tween().set_trans(Tween.TRANS_EXPO).set_ease(Tween.EASE_OUT).set_parallel()
	tween.tween_property(interface, ^'position:x', -1280.0, 0.6)
	tween.tween_property(section, ^'position:x', 640.0, 0.6)

	var current_selected: Category = categories.get_child(selected)
	var options_section: BaseOptionsSection = current_selected.category.instantiate()
	section.add_child(options_section)

func deselect_current() -> void:
	active = true
	GlobalAudio.get_player('MENU/CANCEL').play()
	
	# Slide everything back into place
	var tween: Tween = create_tween().set_trans(Tween.TRANS_EXPO).set_ease(Tween.EASE_OUT).set_parallel()
	tween.tween_property(interface, ^'position:x', 0.0, 0.6)
	tween.tween_property(section, ^'position:x', 1920.0, 0.6)
	
	var children: Array[Node] = section.get_children()
	tween.tween_callback(GameUtils.free_from_array.bind(children)).set_delay(0.6)

func _process(delta: float) -> void:
	options_label.scale = options_label.scale.lerp(Vector2(0.6, 0.6), delta * 4.5)

func _on_beat_hit(_beat: int) -> void:
	# The "SETTINGS" title bumps to the music
	options_label.scale = Vector2(0.65, 0.65)

func _exit_tree() -> void:
	GlobalAudio.music.stream = load('uid://dergcpn8f5cju')
	GlobalAudio.music.play()
