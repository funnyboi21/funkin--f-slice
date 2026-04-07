class_name FreeplayDJ extends Node2D

# --- V-Slice Enums ---
enum DJState {
	INTRO,
	IDLE,
	IDLE_EASTER_EGG,
	CARTOON,
	CONFIRM,
	FIST_PUMP_INTRO,
	FIST_PUMP,
	NEW_UNLOCK,
	CHAR_SELECT
}

# --- Configuration ---
@export_group("DJ Settings")
## The folder path to the atlas that is loaded.
@export_dir var dj_atlas: String = "res://assets/menus/freeplay/dj/bf/":
	set(v):
		dj_atlas = v
		# Update the child sprite atlas whenever this changes
		if is_node_ready() and is_instance_valid(anim_sprite):
			anim_sprite.atlas = v

## The prefix used in the Adobe Animate Atlas (e.g., "Boyfriend DJ" or "Pico DJ")
@export var dj_prefix: String = "Boyfriend DJ"
@export var idle_egg_period: float = 60.0 # 1 minute AFK
@export var idle_cartoon_period: float = 120.0 # 2 minutes AFK

# --- State ---
var current_state: DJState = DJState.INTRO
var time_idling: float = 0.0
var seen_idle_easter_egg: bool = false
var has_new_character_unlock: bool = false 

# --- Signals ---
signal on_intro_done
signal on_idle_easter_egg
signal on_char_select_complete

# --- Nodes ---
@onready var anim_sprite: AnimateSymbol = $Sprite

func _ready() -> void:
	if is_instance_valid(anim_sprite):
		# Sync the atlas path to the sprite on startup
		anim_sprite.atlas = dj_atlas
		
		if not anim_sprite.finished.is_connected(_on_animation_finished):
			anim_sprite.finished.connect(_on_animation_finished)
			
		play_animation("intro")

func _process(delta: float) -> void:
	if current_state == DJState.IDLE:
		time_idling += delta

		if time_idling >= idle_cartoon_period:
			trigger_cartoon_easter_egg()
		elif time_idling >= idle_egg_period and not seen_idle_easter_egg:
			trigger_idle_easter_egg()

func on_player_action() -> void:
	time_idling = 0.0
	seen_idle_easter_egg = false
	
	if current_state == DJState.IDLE_EASTER_EGG or current_state == DJState.CARTOON:
		current_state = DJState.IDLE
		play_animation("idle")

func on_beat_hit(_beat: int) -> void:
	if current_state == DJState.IDLE:
		play_animation("idle")

# --- V-Slice Transitions ---

func on_confirm() -> void:
	if has_new_character_unlock:
		current_state = DJState.NEW_UNLOCK
		play_animation("newUnlock")
		return

	current_state = DJState.CONFIRM
	play_animation("confirm")

func to_char_select() -> void:
	current_state = DJState.CHAR_SELECT
	play_animation("charSelect")

func fist_pump_intro(bad_score: bool = false) -> void:
	if has_new_character_unlock:
		current_state = DJState.NEW_UNLOCK
		return

	current_state = DJState.FIST_PUMP_INTRO
	play_animation("lossIntro" if bad_score else "fistPumpIntro")

func fist_pump(bad_score: bool = false) -> void:
	if has_new_character_unlock:
		current_state = DJState.NEW_UNLOCK
		return

	current_state = DJState.FIST_PUMP
	play_animation("lossLoop" if bad_score else "fistPumpLoop")

# --- Easter Eggs ---

func trigger_idle_easter_egg() -> void:
	current_state = DJState.IDLE_EASTER_EGG
	seen_idle_easter_egg = true
	play_animation("idleEgg")
	on_idle_easter_egg.emit()

func trigger_cartoon_easter_egg() -> void:
	current_state = DJState.CARTOON
	play_animation("cartoon")

# --- Animation Logic ---

func play_animation(anim_name: String) -> void:
	if not is_instance_valid(anim_sprite): return
	
	var target_symbol: String = ""
	
	match anim_name:
		"intro": target_symbol = dj_prefix + " intro"
		"idle": target_symbol = dj_prefix
		"idleEgg": target_symbol = dj_prefix + " afk"
		"confirm": target_symbol = dj_prefix + " confirm"
		"fistPumpIntro": target_symbol = dj_prefix + " pump intro"
		"fistPumpLoop": target_symbol = dj_prefix + " pump loop"
		"lossIntro": target_symbol = dj_prefix + " loss intro"
		"lossLoop": target_symbol = dj_prefix + " loss loop"
		"charSelect": target_symbol = dj_prefix + " to CS"
		"newUnlock": target_symbol = dj_prefix + " new unlock"
		
	if target_symbol == "": return

	anim_sprite.symbol = target_symbol
	anim_sprite.playing = true

	# Set Loop Mode
	if anim_name.ends_with("Intro") or anim_name == "confirm" or anim_name == "charSelect" or anim_name == "intro":
		anim_sprite.loop_mode = 'Play Once'
	else:
		anim_sprite.loop_mode = 'Loop'

func _on_animation_finished() -> void:
	if not is_instance_valid(anim_sprite): return
	var finished_anim: String = anim_sprite.symbol

	match current_state:
		DJState.INTRO:
			current_state = DJState.IDLE
			play_animation("idle")
			on_intro_done.emit()
			
		DJState.IDLE_EASTER_EGG:
			current_state = DJState.IDLE
			play_animation("idle")
			
		DJState.CHAR_SELECT:
			on_char_select_complete.emit()
			
		DJState.FIST_PUMP_INTRO:
			fist_pump(finished_anim == dj_prefix + " loss intro")
