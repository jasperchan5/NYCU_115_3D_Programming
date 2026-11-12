namespace MP3_Save
{
    public interface ISaveable
    {
        string Id { get; }
        void CaptureState(GameSaveData data);   // 把自己的狀態寫進 data
        void RestoreState(GameSaveData data); // 從 data 找到自己那筆並還原
    }
    
}