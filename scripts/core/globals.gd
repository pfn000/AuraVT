## =============================================================================
## AuraVT — Globals Autoload
## Shared state and signals across the application
## =============================================================================
extends Node

## Signals
signal avatar_loaded(avatar_node: Node3D)
signal avatar_unloaded()
signal avatar_load_progress(progress: float)
signal avatar_load_error(message: String)
signal settings_changed(key: String, value: Variant)

## Application info
const VERSION := "1.0.0"
const APP_NAME := "AuraVT"

## Current state
var current_avatar: Node3D = null
var current_avatar_path: String = ""
var is_click_through: bool = false
var is_settings_visible: bool = false

## Supported file extensions
const SUPPORTED_EXTENSIONS := ["vrm", "glb", "gltf"]

func _ready() -> void:
	print("[AuraVT] Version %s initialized" % VERSION)
	
	# Enable file drop handling
	get_tree().root.files_dropped.connect(_on_files_dropped)

func _on_files_dropped(files: PackedStringArray) -> void:
	for file_path in files:
		var ext := file_path.get_extension().to_lower()
		if ext in SUPPORTED_EXTENSIONS:
			print("[AuraVT] File dropped: %s" % file_path)
			AvatarManager.load_avatar(file_path)
			return
	
	push_warning("[AuraVT] Unsupported file type dropped")

func is_supported_file(path: String) -> bool:
	return path.get_extension().to_lower() in SUPPORTED_EXTENSIONS
