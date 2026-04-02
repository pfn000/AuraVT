## =============================================================================
## AuraVT — Settings Panel
## UI for application settings
## =============================================================================
extends PanelContainer

@onready var close_btn: Button = $VBox/Header/CloseButton
@onready var always_on_top: CheckBox = $VBox/WindowSection/AlwaysOnTop
@onready var click_through: CheckBox = $VBox/WindowSection/ClickThrough
@onready var scale_slider: HSlider = $VBox/AvatarSection/ScaleSlider
@onready var load_btn: Button = $VBox/AvatarSection/LoadButton
@onready var idle_motion: CheckBox = $VBox/AnimSection/IdleMotion
@onready var auto_blink: CheckBox = $VBox/AnimSection/AutoBlink

func _ready() -> void:
	# Load current settings
	always_on_top.button_pressed = SettingsManager.get_bool("always_on_top")
	click_through.button_pressed = SettingsManager.get_bool("click_through")
	scale_slider.value = SettingsManager.get_float("avatar_scale")
	idle_motion.button_pressed = SettingsManager.get_bool("idle_motion")
	auto_blink.button_pressed = SettingsManager.get_bool("auto_blink")
	
	# Connect signals
	close_btn.pressed.connect(_on_close_pressed)
	always_on_top.toggled.connect(_on_always_on_top_toggled)
	click_through.toggled.connect(_on_click_through_toggled)
	scale_slider.value_changed.connect(_on_scale_changed)
	load_btn.pressed.connect(_on_load_pressed)
	idle_motion.toggled.connect(_on_idle_motion_toggled)
	auto_blink.toggled.connect(_on_auto_blink_toggled)

func _on_close_pressed() -> void:
	Globals.is_settings_visible = false

func _on_always_on_top_toggled(pressed: bool) -> void:
	WindowController.set_always_on_top(pressed)

func _on_click_through_toggled(pressed: bool) -> void:
	WindowController.set_click_through(pressed)

func _on_scale_changed(value: float) -> void:
	AvatarManager.set_avatar_scale(value)

func _on_load_pressed() -> void:
	# Open file dialog
	var dialog := FileDialog.new()
	dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
	dialog.access = FileDialog.ACCESS_FILESYSTEM
	dialog.filters = PackedStringArray(["*.vrm ; VRM Files", "*.glb ; GLB Files", "*.gltf ; GLTF Files"])
	dialog.title = "Select Avatar"
	
	add_child(dialog)
	dialog.popup_centered(Vector2i(600, 400))
	
	dialog.file_selected.connect(func(path: String):
		AvatarManager.load_avatar(path)
		dialog.queue_free()
	)
	dialog.canceled.connect(func():
		dialog.queue_free()
	)

func _on_idle_motion_toggled(pressed: bool) -> void:
	SettingsManager.set_bool("idle_motion", pressed)
	if Globals.current_avatar and Globals.current_avatar.has_method("set"):
		Globals.current_avatar.enable_idle_motion = pressed

func _on_auto_blink_toggled(pressed: bool) -> void:
	SettingsManager.set_bool("auto_blink", pressed)
	if Globals.current_avatar and Globals.current_avatar.has_method("set"):
		Globals.current_avatar.enable_auto_blink = pressed
