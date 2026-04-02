## =============================================================================
## AuraVT — Settings Manager
## Handles persistent user settings (saved to JSON file)
## =============================================================================
extends Node

const SETTINGS_FILE := "user://auravt_settings.json"

var _settings: Dictionary = {}
var _defaults: Dictionary = {
	"window_opacity": 1.0,
	"always_on_top": true,
	"click_through": false,
	"avatar_scale": 1.0,
	"avatar_position_x": 0.0,
	"avatar_position_y": -0.5,
	"avatar_position_z": 2.0,
	"avatar_rotation_y": 0.0,
	"target_fps": 60,
	"last_avatar_path": "",
	"idle_motion": true,
	"auto_blink": true,
	"low_power_mode": true,
}

func _ready() -> void:
	load_settings()
	apply_settings()

func load_settings() -> void:
	_settings = _defaults.duplicate(true)
	
	if FileAccess.file_exists(SETTINGS_FILE):
		var file := FileAccess.open(SETTINGS_FILE, FileAccess.READ)
		if file:
			var json_string := file.get_as_text()
			file.close()
			
			var json := JSON.new()
			var error := json.parse(json_string)
			if error == OK:
				var data = json.get_data()
				if data is Dictionary:
					for key in data:
						_settings[key] = data[key]
					print("[Settings] Loaded from %s" % SETTINGS_FILE)
			else:
				push_warning("[Settings] Failed to parse settings file")
	else:
		print("[Settings] Using defaults (no settings file)")
		save_settings()

func save_settings() -> void:
	var file := FileAccess.open(SETTINGS_FILE, FileAccess.WRITE)
	if file:
		var json_string := JSON.stringify(_settings, "\t")
		file.store_string(json_string)
		file.close()
		print("[Settings] Saved to %s" % SETTINGS_FILE)
	else:
		push_error("[Settings] Failed to save settings")

func apply_settings() -> void:
	# Apply FPS limit
	Engine.max_fps = get_int("target_fps")
	
	# Apply window settings
	DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_ALWAYS_ON_TOP, get_bool("always_on_top"))
	
	print("[Settings] Applied")

## Getters
func get_value(key: String, default: Variant = null) -> Variant:
	if _settings.has(key):
		return _settings[key]
	elif _defaults.has(key):
		return _defaults[key]
	return default

func get_float(key: String, default: float = 0.0) -> float:
	return float(get_value(key, default))

func get_int(key: String, default: int = 0) -> int:
	return int(get_value(key, default))

func get_bool(key: String, default: bool = false) -> bool:
	return bool(get_value(key, default))

func get_string(key: String, default: String = "") -> String:
	return str(get_value(key, default))

## Setters
func set_value(key: String, value: Variant, auto_save: bool = true) -> void:
	_settings[key] = value
	Globals.settings_changed.emit(key, value)
	if auto_save:
		save_settings()

func set_float(key: String, value: float) -> void:
	set_value(key, value)

func set_int(key: String, value: int) -> void:
	set_value(key, value)

func set_bool(key: String, value: bool) -> void:
	set_value(key, value)

func set_string(key: String, value: String) -> void:
	set_value(key, value)

## Reset to defaults
func reset_to_defaults() -> void:
	_settings = _defaults.duplicate(true)
	save_settings()
	apply_settings()
