using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SaveAndExitButton : MonoBehaviour
{
    public void OnSaveAndExitClicked()
    {
        // 只保存到临时存档，不碰任何正式槽位
        SaveSystem.currentSlotID = SaveSystem.TempSaveSlot;

        foreach (var entity in Object.FindObjectsOfType<SaveableEntity>())
        {
            entity.SaveData();
        }

        // 退出游戏
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}