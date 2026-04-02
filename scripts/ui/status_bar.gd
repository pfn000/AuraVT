## =============================================================================
## AuraVT — Status Bar
## Shows status messages and FPS
## =============================================================================
extends PanelContainer

@onready var status_label: Label = $HBox/StatusLabel
@onready var fps_label: Label = $HBox/FPSLabel

var _fps_update_timer: float = 0.0

func _ready() -> void:
	# Make status bar semi-transparent
	modulate.a = 0.8

func _process(delta: float) -> void:
	_fps_update_timer += delta
	if _fps_update_timer >= 0.5:
		_fps_update_timer = 0.0
		fps_label.text = "%d FPS" % Engine.get_frames_per_second()
