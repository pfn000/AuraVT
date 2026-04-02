## =============================================================================
## AuraVT — Avatar Manager
## Handles runtime loading of VRM and GLB avatars
## =============================================================================
extends Node

## Reference to avatar container in scene
var avatar_root: Node3D = null

## VRM loader reference (from godot-vrm addon)
var _vrm_loader = null

func _ready() -> void:
	# Try to load VRM addon
	_try_load_vrm_addon()
	
	# Load last used avatar if exists
	var last_path := SettingsManager.get_string("last_avatar_path")
	if last_path and FileAccess.file_exists(last_path):
		call_deferred("load_avatar", last_path)
	
	print("[AvatarManager] Ready")

func _try_load_vrm_addon() -> void:
	# Check if VRM addon is available
	if ResourceLoader.exists("res://addons/vrm/vrm_loader.gd"):
		_vrm_loader = load("res://addons/vrm/vrm_loader.gd").new()
		print("[AvatarManager] VRM loader available")
	else:
		push_warning("[AvatarManager] VRM addon not found - VRM loading disabled")

func set_avatar_root(root: Node3D) -> void:
	avatar_root = root

func load_avatar(file_path: String) -> void:
	if not avatar_root:
		push_error("[AvatarManager] No avatar root set")
		Globals.avatar_load_error.emit("Internal error: No avatar root")
		return
	
	if not FileAccess.file_exists(file_path):
		push_error("[AvatarManager] File not found: %s" % file_path)
		Globals.avatar_load_error.emit("File not found")
		return
	
	var ext := file_path.get_extension().to_lower()
	
	print("[AvatarManager] Loading: %s" % file_path)
	Globals.avatar_load_progress.emit(0.1)
	
	# Unload current avatar first
	unload_avatar()
	
	match ext:
		"vrm":
			_load_vrm(file_path)
		"glb", "gltf":
			_load_gltf(file_path)
		_:
			Globals.avatar_load_error.emit("Unsupported format: %s" % ext)

func _load_vrm(file_path: String) -> void:
	Globals.avatar_load_progress.emit(0.2)
	
	if not _vrm_loader:
		# Fallback: Try loading as GLTF (VRM is based on GLTF)
		push_warning("[AvatarManager] VRM addon not available, trying GLTF fallback")
		_load_gltf(file_path)
		return
	
	# Use VRM loader from addon
	var gltf_state := GLTFState.new()
	var gltf_doc := GLTFDocument.new()
	
	# Register VRM extension if available
	if ClassDB.class_exists("GLTFDocumentExtensionVRM"):
		var vrm_extension = ClassDB.instantiate("GLTFDocumentExtensionVRM")
		gltf_doc.register_gltf_document_extension(vrm_extension)
	
	Globals.avatar_load_progress.emit(0.4)
	
	var error := gltf_doc.append_from_file(file_path, gltf_state)
	if error != OK:
		Globals.avatar_load_error.emit("Failed to parse VRM file")
		return
	
	Globals.avatar_load_progress.emit(0.7)
	
	var avatar := gltf_doc.generate_scene(gltf_state)
	if not avatar:
		Globals.avatar_load_error.emit("Failed to generate avatar scene")
		return
	
	_setup_avatar(avatar, file_path)

func _load_gltf(file_path: String) -> void:
	Globals.avatar_load_progress.emit(0.3)
	
	var gltf_state := GLTFState.new()
	var gltf_doc := GLTFDocument.new()
	
	var error := gltf_doc.append_from_file(file_path, gltf_state)
	if error != OK:
		Globals.avatar_load_error.emit("Failed to parse GLTF/GLB file")
		return
	
	Globals.avatar_load_progress.emit(0.6)
	
	var avatar := gltf_doc.generate_scene(gltf_state)
	if not avatar:
		Globals.avatar_load_error.emit("Failed to generate avatar scene")
		return
	
	_setup_avatar(avatar, file_path)

func _setup_avatar(avatar: Node, file_path: String) -> void:
	Globals.avatar_load_progress.emit(0.8)
	
	# Add to scene
	avatar_root.add_child(avatar)
	
	# Apply default transform
	if avatar is Node3D:
		avatar.position = Vector3(
			SettingsManager.get_float("avatar_position_x"),
			SettingsManager.get_float("avatar_position_y"),
			SettingsManager.get_float("avatar_position_z")
		)
		avatar.scale = Vector3.ONE * SettingsManager.get_float("avatar_scale")
	
	# Add controller script
	var controller_script := load("res://scripts/avatar/avatar_controller.gd")
	if controller_script:
		avatar.set_script(controller_script)
	
	# Store references
	Globals.current_avatar = avatar
	Globals.current_avatar_path = file_path
	SettingsManager.set_string("last_avatar_path", file_path)
	
	Globals.avatar_load_progress.emit(1.0)
	Globals.avatar_loaded.emit(avatar)
	
	print("[AvatarManager] Avatar loaded successfully: %s" % avatar.name)

func unload_avatar() -> void:
	if Globals.current_avatar:
		Globals.current_avatar.queue_free()
		Globals.current_avatar = null
		Globals.current_avatar_path = ""
		Globals.avatar_unloaded.emit()
		print("[AvatarManager] Avatar unloaded")

func set_avatar_scale(scale: float) -> void:
	if Globals.current_avatar:
		Globals.current_avatar.scale = Vector3.ONE * scale
		SettingsManager.set_float("avatar_scale", scale)

func set_avatar_position(pos: Vector3) -> void:
	if Globals.current_avatar:
		Globals.current_avatar.position = pos
		SettingsManager.set_float("avatar_position_x", pos.x)
		SettingsManager.set_float("avatar_position_y", pos.y)
		SettingsManager.set_float("avatar_position_z", pos.z)

func set_avatar_rotation_y(rot: float) -> void:
	if Globals.current_avatar:
		Globals.current_avatar.rotation.y = rot
		SettingsManager.set_float("avatar_rotation_y", rot)
