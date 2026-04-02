## =============================================================================
## AuraVT — Drop Overlay
## Visual feedback when dragging files over window
## =============================================================================
extends ColorRect

func _ready() -> void:
	visible = false

func _notification(what: int) -> void:
	match what:
		NOTIFICATION_DRAG_BEGIN:
			# Check if dragging files
			visible = true
		NOTIFICATION_DRAG_END:
			visible = false
