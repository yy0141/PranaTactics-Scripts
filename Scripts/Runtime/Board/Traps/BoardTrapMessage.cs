namespace PranaTactics.Board
{
    public enum BoardTrapCommand
    {
        RequestPlacement,
        Placed,
        Rejected,
        Triggered,
        Activated
    }

    /// <summary>
    /// ボードの罠設置、検知と発動を同期する値だけの通信データです。
    /// </summary>
    public sealed class BoardTrapMessage
    {
        public BoardTrapCommand Command { get; set; }
        public string TrapId { get; set; }
        public int OwnerId { get; set; }
        public int OwnerTeamId { get; set; }
        public bool IsMain { get; set; }
        public int Slot { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Floor { get; set; }
        public int Range { get; set; }
        public float ActivationDelay { get; set; }
        public string PrefabPath { get; set; }
        public string SoundProfileId { get; set; }
        public int TargetId { get; set; } = -1;
    }
}
