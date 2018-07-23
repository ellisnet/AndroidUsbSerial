namespace SharedSerial.Sqlite
{
    public enum SqliteCheckpointMode
    {
        Passive = 0,
        Full = 1,
        Restart = 2,
        Truncate = 3,
    }
}
