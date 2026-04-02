## =============================================================================
## AuraVT — Avatar Controller
## Attached to loaded avatar for movement, rotation, and animation
## =============================================================================
extends Node3D

## Movement settings
@export var move_speed: float = 2.0
@export var rotate_speed: float = 2.0
@export var zoom_speed: float = 0.3
@export var min_scale: float = 0.3
@export var max_scale: float = 3.0

## Animation settings
@export var enable_idle_motion: bool = true
@export var enable_auto_blink: bool = true
@export var blink_interval_min: float = 2.0
@export var blink_interval_max: float = 6.0

## State
var _is_dragging_rotate: bool = false
var _is_dragging_move: bool = false
var _last_mouse_pos: Vector2 = Vector2.ZERO
var _blink_timer: float = 0.0
var _next_blink: float = 3.0
var _idle_time: float = 0.0

## VRM references (populated after load)
var _blend_shapes: Dictionary = {}
var _skeleton: Skeleton3D = null

func _ready() -> void:
	# Load settings
	enable_idle_motion = SettingsManager.get_bool("idle_motion")
	enable_auto_blink = SettingsManager.get_bool("auto_blink")
	
	# Find skeleton and blend shapes
	_find_vrm_components()
	
	# Randomize first blink
	_next_blink = randf_range(blink_interval_min, blink_interval_max)
	
	print("[AvatarController] Ready")

func _find_vrm_components() -> void:
	# Find skeleton
	_skeleton = _find_child_of_type(self, "Skeleton3D")
	
	# Find mesh instances for blend shapes
	for child in _get_all_children(self):
		if child is MeshInstance3D:
			var mesh := child as MeshInstance3D
			if mesh.mesh and mesh.get_blend_shape_count() > 0:
				for i in range(mesh.get_blend_shape_count()):
					var shape_name := mesh.mesh.get_blend_shape_name(i)
					_blend_shapes[shape_name] = {"mesh": mesh, "index": i}
	
	if _blend_shapes.size() > 0:
		print("[AvatarController] Found %d blend shapes" % _blend_shapes.size())

func _input(event: InputEvent) -> void:
	if Globals.is_click_through or Globals.is_settings_visible:
		return
	
	# Right-click drag to rotate
	if event is InputEventMouseButton:
		if event.button_index == MOUSE_BUTTON_RIGHT:
			_is_dragging_rotate = event.pressed
			_last_mouse_pos = event.position
		elif event.button_index == MOUSE_BUTTON_MIDDLE:
			_is_dragging_move = event.pressed
			_last_mouse_pos = event.position
		elif event.button_index == MOUSE_BUTTON_WHEEL_UP:
			_zoom(zoom_speed)
		elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			_zoom(-zoom_speed)
	
	if event is InputEventMouseMotion:
		if _is_dragging_rotate:
			var delta := event.position - _last_mouse_pos
			rotation.y -= delta.x * rotate_speed * 0.01
			rotation.x += delta.y * rotate_speed * 0.01
			rotation.x = clampf(rotation.x, -PI/3, PI/3)
			_last_mouse_pos = event.position
		
		if _is_dragging_move:
			var delta := event.position - _last_mouse_pos
			position.x += delta.x * move_speed * 0.001
			position.y -= delta.y * move_speed * 0.001
			_last_mouse_pos = event.position

func _process(delta: float) -> void:
	if enable_idle_motion:
		_update_idle_motion(delta)
	
	if enable_auto_blink:
		_update_blink(delta)

func _zoom(amount: float) -> void:
	var new_scale := scale.x + amount
	new_scale = clampf(new_scale, min_scale, max_scale)
	scale = Vector3.ONE * new_scale
	SettingsManager.set_float("avatar_scale", new_scale)

func _update_idle_motion(delta: float) -> void:
	_idle_time += delta
	
	# Subtle breathing motion
	var breath := sin(_idle_time * 0.8) * 0.003
	
	# Subtle head sway
	var sway := sin(_idle_time * 0.3) * 0.02
	
	# Apply to local transform (not affecting saved rotation)
	# This creates a living, breathing effect
	if _skeleton:
		# Could animate chest/spine bones here for breathing
		pass

func _update_blink(delta: float) -> void:
	_blink_timer += delta
	
	if _blink_timer >= _next_blink:
		_do_blink()
		_blink_timer = 0.0
		_next_blink = randf_range(blink_interval_min, blink_interval_max)

func _do_blink() -> void:
	# Try different blink shape names (VRM 0.x and 1.0 naming)
	var blink_names := ["Blink", "blink", "Blink_L", "Blink_R", "blinkLeft", "blinkRight"]
	
	for shape_name in blink_names:
		if _blend_shapes.has(shape_name):
			_animate_blend_shape(shape_name)
			return
	
	# Try combined eye blinks
	if _blend_shapes.has("Blink_L") and _blend_shapes.has("Blink_R"):
		_animate_blend_shape("Blink_L")
		_animate_blend_shape("Blink_R")

func _animate_blend_shape(shape_name: String, duration: float = 0.1) -> void:
	if not _blend_shapes.has(shape_name):
		return
	
	var data: Dictionary = _blend_shapes[shape_name]
	var mesh: MeshInstance3D = data["mesh"]
	var idx: int = data["index"]
	
	# Create tween for smooth blink
	var tween := create_tween()
	tween.tween_method(
		func(value: float): mesh.set_blend_shape_value(idx, value),
		0.0, 1.0, duration * 0.5
	)
	tween.tween_method(
		func(value: float): mesh.set_blend_shape_value(idx, value),
		1.0, 0.0, duration * 0.5
	)

## Set expression by name
func set_expression(expression_name: String, weight: float) -> void:
	weight = clampf(weight, 0.0, 1.0)
	if _blend_shapes.has(expression_name):
		var data: Dictionary = _blend_shapes[expression_name]
		var mesh: MeshInstance3D = data["mesh"]
		var idx: int = data["index"]
		mesh.set_blend_shape_value(idx, weight)

## Helper to find child of specific type
func _find_child_of_type(node: Node, type_name: String) -> Node:
	for child in node.get_children():
		if child.get_class() == type_name:
			return child
		var found := _find_child_of_type(child, type_name)
		if found:
			return found
	return null

## Helper to get all descendants
func _get_all_children(node: Node) -> Array[Node]:
	var children: Array[Node] = []
	for child in node.get_children():
		children.append(child)
		children.append_array(_get_all_children(child))
	return children
