# Mini Project 3：Save & Load（4.E.1）

Unity 6000.3.25f1

玩家操控一個角色在亭台場景中移動，兩隻龍會在場景中自己遊走、低頭嗅地面。任何時候都可以存檔和讀檔，角色和所有生物的狀態都會寫進存檔檔案，讀檔後還原。

## 操作

| 按鍵 | 功能 |
|---|---|
| W / A / S / D | 移動（依鏡頭方向） |
| Left Shift | 跑步 |
| Space | 跳躍 |
| 滑鼠右鍵拖曳 | 旋轉鏡頭 |
| 滑鼠滾輪 | 鏡頭拉近／拉遠 |
| **F5** | 存檔 |
| **F9** | 讀檔 |

## 存檔內容

存檔位置：`Application.persistentDataPath/save_slot1.json`（Windows 為 `%USERPROFILE%/AppData/LocalLow/<Company>/<Product>/`）

| 對象 | 存下的狀態 |
|---|---|
| 角色 | 位置、朝向、走路速度、動作（Idle / Walk / Run / Jump）、垂直速度、動畫狀態與播放進度 |
| 每隻龍 | id、位置、朝向、目前速度、設定速度、動作（Idle / Walk / Sniff）、AI 狀態、狀態計時器、遊走目標點、動畫狀態與播放進度 |

讀檔後，跳到一半的角色會從存檔時的高度和垂直速度繼續；走到一半的龍會朝原本的目標點繼續走，嗅到一半的龍會嗅完剩下的時間。

## 本次新增的內容

- **存讀檔系統**：`ISaveable` 介面讓每個物件自己負責寫入和還原自己的狀態，`SaveManager` 只負責找出所有 `ISaveable`、組成 `GameSaveData`、轉成 JSON 寫檔或讀檔。
  - 角色還原時先停用 `CharacterController` 再設定位置，避免被拉回原位。
  - 龍用 `NavMeshAgent.Warp` 還原位置。
  - 動畫用 `Animator.Play(stateHash, 0, normalizedTime)` 從存檔當下的瞬間繼續播放。
  - 龍用固定的 id 對應存檔中的資料。
- **龍的遊走 AI**：Idle → Wander（在 NavMesh 上隨機挑點走過去）或 Sniff（低頭嗅地面）→ Idle 的狀態機；兩隻龍的移動速度不同，走路動畫的播放速度隨實際移動速度調整。
- **場景物件產生**：`SceneScatter` 用迴圈在範圍內產生 20 顆寶石。
  - 位置：從上方打射線找地形表面，避開陡坡、彼此保持距離，並避開角色和龍的出生點。
  - 外觀：每份寶石模型只保留隨機一顆，並讓底部貼齊地面，自動加上碰撞箱。
  - 固定 seed，每次產生的位置都相同，存讀檔時場景一致。
- **其他**：第三人稱角色移動、跟隨鏡頭（含防穿牆）。

## 程式檔案

| 檔案 | 說明 |
|---|---|
| `ISaveable.cs` | 可存檔物件的介面 |
| `SaveData.cs` | `PlayerState`、`CreatureState`、`GameSaveData` 資料類別 |
| `SaveManager.cs` | F5 / F9、JSON 讀寫 |
| `PlayerSaveable.cs` | 角色的存檔與還原 |
| `CreatureSaveable.cs` | 龍的存檔與還原 |
| `PlayerMovement.cs` | 角色移動、跳躍、動畫參數 |
| `DragonWander.cs` | 龍的遊走 AI |
| `CameraFollow.cs` | 第三人稱跟隨鏡頭 |
| `SceneScatter.cs` | 用迴圈產生場景物件 |
| `Editor/MP3AnimatorBuilder.cs` | 設定模型匯入選項並產生 Animator Controller |

## 參考資料

以下模型皆來自 Sketchfab：

| 用途 | 模型 | 作者 | 授權 |
|---|---|---|---|
| 角色 | [3rd person model](https://sketchfab.com/3d-models/3rd-person-model-0634f275b68747b79f6f2925a8c26a0f) | DJMaesen | CC BY 4.0 |
| 生物 | [Black Dragon with Idle Animation](https://sketchfab.com/3d-models/black-dragon-with-idle-animation-fb0053a2e59b43868e934c239bf4eb36) | 3DHaupt | CC BY-NC 4.0 |
| 場景 | [Gumijeong (pavilion)](https://sketchfab.com/3d-models/gumijeong-pavilion-0e898b6bd141434ba316dc55847a9f0a) | lacomi1975 | CC BY 4.0 |
| 場景物件 | [Stylized Crystal Gem pack - Handpainted](https://sketchfab.com/3d-models/stylized-crystal-gem-pack-handpainted-9aa1af6e18b24c99af00e850f9fd2658) | ashkan.fancy | CC BY 4.0 |

授權條款：[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)、[CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/)

## 動畫

- 龍的 Idle 動畫為原模型附帶。
- 角色的 Idle / Walk / Run / Jump，以及龍的 Walk / Sniff，是在 Blender 中以 Python 腳本對原模型骨架製作的關鍵影格動畫（AI 輔助製作）。