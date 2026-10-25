using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// 剛下載的專案沒有 Library/，Unity 不知道上次開的是哪個場景，會開一個空的 Untitled。
// 這個腳本在開啟專案時，若目前是空白場景，就自動打開 BattleField。
[InitializeOnLoad]
static class AutoOpenBattleField
{
    const string ScenePath = "Assets/Scenes/BattleField.unity";
    const string SessionKey = "AutoOpenBattleField.Done";

    static AutoOpenBattleField()
    {
        // 每次開啟編輯器只檢查一次；之後重新編譯腳本不會再觸發
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);

        // 等編輯器載入完成再開場景，太早開會失敗
        EditorApplication.delayCall += OpenIfUntitled;
    }

    static void OpenIfUntitled()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        Scene active = SceneManager.GetActiveScene();
        bool untitled = string.IsNullOrEmpty(active.path);
        if (!untitled || active.isDirty) return;   // 已經開了別的場景，或空白場景裡有未存的東西，就不動

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
            EditorSceneManager.OpenScene(ScenePath);
    }
}