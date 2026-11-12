using UnityEngine;
using System;              // DateTime
using System.IO;           // Path, File
using System.Linq;         // OfType
using UnityEngine.InputSystem;  // Keyboard

namespace MP3_Save
{
    public class SaveManager : MonoBehaviour
    {
        //
        // 找到所有 ISaveble -> 組合 GameSaveData -> 存 JSON
        //
        public string fileName = "save_slot1.json";
        public string FullPath => Path.Combine(Application.persistentDataPath, fileName);

        [Header("畫面提示")]
        [SerializeField] float messageSeconds = 2.5f;   // 存讀檔訊息顯示多久
        [SerializeField] int fontSize = 22;

        string message = "";
        float messageUntil;
        GUIStyle hintStyle, msgStyle;

        void Save()
        {
            var data = new GameSaveData {
                version = 1,
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            foreach (var s in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ISaveable>())
            {
                s.CaptureState(data);
            }
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(FullPath, json);
            Debug.Log($"Saved to {FullPath}");
            ShowMessage($"已存檔  {data.savedAt}");
        }
        void Load()
        {
            if (!File.Exists(FullPath))
            {
                Debug.LogWarning("No save file");
                ShowMessage("沒有存檔，請先按 F5");
                return;
            }
            string json = File.ReadAllText(FullPath);
            var data = JsonUtility.FromJson<GameSaveData>(json);
            foreach (var s in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ISaveable>())
            {
                s.RestoreState(data);
            }
            Debug.Log(data.savedAt);
            ShowMessage($"已讀檔  {data.savedAt}");
        }
        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f5Key.wasPressedThisFrame) Save();
            if (kb != null && kb.f9Key.wasPressedThisFrame) Load();
        }

        void ShowMessage(string text)
        {
            message = text;
            messageUntil = Time.unscaledTime + messageSeconds;
        }

        // 畫面左上角：常駐的按鍵提示；按下 F5/F9 後在畫面上方中央顯示結果
        void OnGUI()
        {
            if (hintStyle == null)
            {
                hintStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = fontSize,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(12, 12, 8, 8),
                };
                hintStyle.normal.textColor = Color.white;
                msgStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = fontSize + 4,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(16, 16, 10, 10),
                };
                msgStyle.normal.textColor = new Color(1f, 0.9f, 0.4f);
            }

            var hint = new GUIContent("F5  存檔\nF9  讀檔");
            Vector2 hs = hintStyle.CalcSize(hint);
            GUI.Box(new Rect(16, 16, hs.x, hs.y), hint, hintStyle);

            if (Time.unscaledTime < messageUntil)
            {
                var msg = new GUIContent(message);
                Vector2 ms = msgStyle.CalcSize(msg);
                GUI.Box(new Rect((Screen.width - ms.x) / 2f, 24, ms.x, ms.y), msg, msgStyle);
            }
        }
    }
}