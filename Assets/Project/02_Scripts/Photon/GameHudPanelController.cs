using UnityEngine;

public class GameHudPanelController : MonoBehaviour
{
    [Header("패널 컴포넌트들")]
    [SerializeField] private GameObject _enterRoomPanel;
    [SerializeField] private GameObject _selectSessionSizePanel;
    [SerializeField] private GameObject _panelPlayModeSelect;
    [SerializeField] private GameObject _panelReturnDefeat;
    [SerializeField] private GameObject _panelReturnWin;
    [SerializeField] private GameObject _panelAskReturn;
    [SerializeField] private GameObject _topPanel;

    // 프로퍼티 : esc 눌러서 음량 조절 창 띄우고 닫기 기능을 위해 넣어놓음
    public bool IsAskReturnPanelActive
    {
        get
        {
            if (_panelAskReturn != null && _panelAskReturn.activeSelf)
            {
                return true;  // 패널이 존재하고 켜져있음
            }
            else
            {
                return false; // 패널이 없거나 꺼져있음
            }
        }
    }


    public void ShowAskReturnPanel(bool flag)
    {
        if (_panelAskReturn == null) return;

        _panelAskReturn.SetActive(flag);
    }

    public void ShowTopPanel(bool flag)
    {
        if (_topPanel == null) return;

        _topPanel.SetActive(flag);
    }

    public void ShowEnterRoomPanel(bool flag)
    {
        GameObject panel = _enterRoomPanel != null ? _enterRoomPanel : _panelPlayModeSelect;
        if (panel == null) return;

        panel.SetActive(flag);
    }

    public void ShowSelectSessionSizePanel(bool flag)
    {
        if (_selectSessionSizePanel == null) return;

        _selectSessionSizePanel.SetActive(flag);
    }

    public void ShowSessionSizeSelection()
    {
        ShowEnterRoomPanel(false);
        ShowSelectSessionSizePanel(true);
    }

    public void HideRoomEntryPanels()
    {
        ShowEnterRoomPanel(false);
        ShowSelectSessionSizePanel(false);
    }

    public void ShowReturnPanelDefeat(bool flag)
    {
        if (_panelReturnDefeat == null)
        {
            Debug.LogWarning("질때 나오는 ReturnPanel이 GameHudPanelController에 할당되지 않음");
            return;
        }

        _panelReturnDefeat.SetActive(flag);
    }

    public void ShowReturnPanelWin(bool flag)
    {
        if (_panelReturnWin == null)
        {
            Debug.LogWarning("이길때 나오는 ReturnPanel이 GameHudPanelController에 할당되지 않음");
            return;
        }

        _panelReturnWin.SetActive(flag);
    }
}
