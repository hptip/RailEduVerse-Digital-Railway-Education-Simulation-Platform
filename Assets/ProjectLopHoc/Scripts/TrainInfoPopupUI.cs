using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TrainInfoPopupUI : MonoBehaviour
{
    public static TrainInfoPopupUI Instance;

    [Header("Giao diện Popup")]
    public GameObject popupPanel;
    public TMP_Text titleText;
    public TMP_Text detailsText;
    public Button closeButton;

    [Header("Thanh cuộn")]
    public ScrollRect scrollRect;

    private void Awake()
    {
        Instance = this;

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePopup);
        }

        ClosePopup();
    }

    public void OpenPopup(string trainName, string specs)
    {
        if (popupPanel == null) return;

        if (titleText != null) titleText.text = trainName;
        if (detailsText != null) detailsText.text = specs;

        popupPanel.SetActive(true);

        UttDesktopCursor.SetModal(popupPanel, true);

        // Reset vị trí cuộn về dòng đầu tiên
        if (scrollRect != null)
        {
            scrollRect.StopMovement();
            Canvas.ForceUpdateCanvases();
            if (scrollRect.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    public void ClosePopup()
    {
        if (popupPanel != null) popupPanel.SetActive(false);

        UttDesktopCursor.SetModal(popupPanel, false);
    }
}
