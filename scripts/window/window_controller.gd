## =============================================================================
## AuraVT — Window Controller
## Manages transparent overlay window behavior
## =============================================================================
extends Node

## The polygon region for non-click-through areas (avatar bounds)
var _passthrough_region: PackedVector2Array = PackedVector2Array()
var _avatar_bounds: Rect2 = Rect2()

## State
var click_through_enabled: bool = false
var always_on_top: bool = true

func _ready() -> void:
	# Initialize transparent window
	_setup_transparent_window()
	
	# Connect to settings changes
	Globals.settings_changed.connect(_on_setting_changed)
	
	print("[WindowController] Initialized")

func _setup_transparent_window() -> void:
	var root := get_tree().root
	
	# Ensure transparent background
	root.transparent_bg = true
	
	# Set window flags
	DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_TRANSPARENT, true)
	DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_BORDERLESS, true)
	DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_ALWAYS_ON_TOP, always_on_top)
	
	# Enable per-pixel transparency
	# This allows the window to be truly transparent
	
	print("[WindowController] Transparent window configured")

func _input(event: InputEvent) -> void:
	# ESC toggles settings panel
	if event.is_action_pressed("toggle_settings"):
		Globals.is_settings_visible = not Globals.is_settings_visible
	
	# Ctrl+T toggles click-through
	if event.is_action_pressed("toggle_click_through"):
		set_click_through(not click_through_enabled)
	
	# Ctrl+R resets avatar position
	if event.is_action_pressed("reset_position"):
		_reset_avatar_position()

func set_click_through(enabled: bool) -> void:
	click_through_enabled = enabled
	Globals.is_click_through = enabled
	
	if enabled:
		# Make entire window click-through
		# Empty region = everything passes through
		DisplayServer.window_set_mouse_passthrough(PackedVector2Array())
	else:
		# Restore normal input (no passthrough region)
		# Full window region = nothing passes through
		var size := DisplayServer.window_get_size()
		var full_region := PackedVector2Array([
			Vector2(0, 0),
			Vector2(size.x, 0),
			Vector2(size.x, size.y),
			Vector2(0, size.y)
		])
		DisplayServer.window_set_mouse_passthrough(full_region)
	
	SettingsManager.set_bool("click_through", enabled)
	print("[WindowController] Click-through: %s" % enabled)

func set_always_on_top(enabled: bool) -> void:
	always_on_top = enabled
	DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_ALWAYS_ON_TOP, enabled)
	SettingsManager.set_bool("always_on_top", enabled)
	print("[WindowController] Always on top: %s" % enabled)

func set_window_opacity(opacity: float) -> void:
	# Note: Per-pixel transparency handles this through shaders
	# This is for the overall window alpha if needed
	SettingsManager.set_float("window_opacity", clampf(opacity, 0.1, 1.0))

func update_avatar_bounds(bounds: Rect2) -> void:
	_avatar_bounds = bounds
	_update_passthrough_region()

func _update_passthrough_region() -> void:
	if click_through_enabled:
		return  # Full passthrough when enabled
	
	# Create a polygon around the avatar for clickable area
	# Everything outside this passes through
	if _avatar_bounds.size.x > 0 and _avatar_bounds.size.y > 0:
		_passthrough_region = PackedVector2Array([
			_avatar_bounds.position,
			Vector2(_avatar_bounds.end.x, _avatar_bounds.position.y),
			_avatar_bounds.end,
			Vector2(_avatar_bounds.position.x, _avatar_bounds.end.y)
		])
		DisplayServer.window_set_mouse_passthrough(_passthrough_region)

func _reset_avatar_position() -> void:
	if Globals.current_avatar:
		Globals.current_avatar.position = Vector3(
			SettingsManager.get_float("avatar_position_x"),
			SettingsManager.get_float("avatar_position_y"),
			SettingsManager.get_float("avatar_position_z")
		)
		Globals.current_avatar.rotation.y = SettingsManager.get_float("avatar_rotation_y")
		print("[WindowController] Avatar position reset")

func _on_setting_changed(key: String, value: Variant) -> void:
	match key:
		"always_on_top":
			set_always_on_top(value)
		"click_through":
			set_click_through(value)

## Minimize to system tray (optional feature)
func minimize_to_tray() -> void:
	DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_MINIMIZED)

## Restore from tray
func restore_from_tray() -> void:
	DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
	DisplayServer.window_move_to_foreground()
