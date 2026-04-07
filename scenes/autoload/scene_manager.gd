extends CanvasLayer


@export_range(0.0, 1.0, 0.001, "or_greater") var tween_length: float = 0.367

@onready var transition: ColorRect = $transition

var current_path: String = ""
var tween: Tween

signal scene_changed


func _ready() -> void:
	transition.material.set_shader_parameter(&"progress", 0.0)
	visible = false


func switch_to(scene: PackedScene, use_transition: bool = true) -> void:
	if not Config.get_value("interface", "scene_transitions"):
		use_transition = false

	var tree: SceneTree = get_tree()
	var killed: bool = is_instance_valid(tween) and tween.is_running()
	if killed:
		tween.kill()

	if use_transition:
		if is_instance_valid(tween) and tween.is_running():
			tween.kill()

		tree.current_scene.process_mode = Node.PROCESS_MODE_DISABLED
		visible = true
		transition.scale = Vector2.ONE
		tween = create_tween().set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
		tween.tween_property(transition.material, ^"shader_parameter/progress", 1.0, tween_length)
		tween.tween_callback(func() -> void:
			transition.scale = Vector2.ONE * -1.0
			tree.change_scene_to_packed.call_deferred(scene)
			scene_changed.emit()

			tween = create_tween().set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
			tween.tween_property(transition.material, ^"shader_parameter/progress", 0.0, tween_length)
			tween.tween_callback(func() -> void:
				visible = false)
		)
	else:
		if killed:
			transition.material.set_shader_parameter(&"progress", 0.0)
			visible = false

		tree.change_scene_to_packed.call_deferred(scene)
		scene_changed.emit()
