using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// AuraVT — AvatarLoadProgressUI
///
/// Minimal loading indicator using Unity UI Toolkit.
/// Shows a progress bar + status text while an avatar is loading.
/// Hides automatically when loading completes or errors.
///
/// UXML setup (create AvatarLoadProgress.uxml):
///   <VisualElement name="load-overlay">
///     <Label name="status-label" />
///     <VisualElement name="progress-bar">
///       <VisualElement name="progress-fill" />
///     </VisualElement>
///   </VisualElement>
/// </summary>
public class AvatarLoadProgressUI : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private AvatarManager avatarManager;

    private VisualElement _overlay;
    private VisualElement _progressFill;
    private Label         _statusLabel;

    void OnEnable()
    {
        if (uiDocument == null) return;

        var root = uiDocument.rootVisualElement;
        _overlay      = root.Q<VisualElement>("load-overlay");
        _progressFill = root.Q<VisualElement>("progress-fill");
        _statusLabel  = root.Q<Label>("status-label");

        SetVisible(false);

        if (avatarManager != null)
        {
            avatarManager.OnLoadProgress  += HandleProgress;
            avatarManager.OnAvatarLoaded  += _ => StartCoroutine(HideAfterDelay(0.5f));
            avatarManager.OnLoadError     += _ => StartCoroutine(HideAfterDelay(2.0f));
        }
    }

    void OnDisable()
    {
        if (avatarManager != null)
        {
            avatarManager.OnLoadProgress  -= HandleProgress;
        }
    }

    private void HandleProgress(float value, string status)
    {
        SetVisible(true);
        if (_progressFill != null)
            _progressFill.style.width = Length.Percent(value * 100f);
        if (_statusLabel != null)
            _statusLabel.text = status;
    }

    private IEnumerator HideAfterDelay(float delay)
    {
        // Fill to 100% visually
        if (_progressFill != null)
            _progressFill.style.width = Length.Percent(100f);
        yield return new WaitForSeconds(delay);
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (_overlay != null)
            _overlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
