## =============================================================================
## AuraVT — Main Scene Controller
## Entry point for the application
## =============================================================================
extends Node3D

@onready var avatar_root: Node3D = $AvatarRoot
@onready var camera: Camera3D = $Camera3D
@onready var settings_panel: PanelContainer = $UI/SettingsPanel
@onready var status_bar: PanelContainer = $UI/StatusBar
@onready var drop_overlay: ColorRect = $UI/DropOverlay

func _ready() -> void:
	# Set avatar root for AvatarManager
	AvatarManager.set_avatar_root(avatar_root)
	
	# Connect signals
	Globals.avatar_loaded.connect(_on_avatar_loaded)
	Globals.avatar_load_error.connect(_on_avatar_load_error)
	Globals.avatar_load_progress.connect(_on_avatar_load_progress)
	
	# Setup settings panel visibility binding
	Globals.settings_changed.connect(_on_globals_changed)
	
	print("[Main] Scene ready")

func _process(_delta: float) -> void:
	# Update settings panel visibility
	if settings_panel.visible != Globals.is_settings_visible:
		settings_panel.visible = Globals.is_settings_visible

func _on_avatar_loaded(avatar: Node3D) -> void:
	var status_label: Label = $UI/StatusBar/HBox/StatusLabel
	status_label.text = "Loaded: %s" % avatar.name
	drop_overlay.visible = false

func _on_avatar_load_error(message: String) -> void:
	var status_label: Label = $UI/StatusBar/HBox/StatusLabel
	status_label.text = "Error: %s" % message
	drop_overlay.visible = false

func _on_avatar_load_progress(progress: float) -> void:
	var status_label: Label = $UI/StatusBar/HBox/StatusLabel
	status_label.text = "Loading... %d%%" % int(progress * 100)

func _on_globals_changed(key: String, _value: Variant) -> void:
	# Handle global state changes if needed
	pass

func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_CLOSE_REQUEST:
		# Save settings before closing
		SettingsManager.save_settings()
		get_tree().quit()
