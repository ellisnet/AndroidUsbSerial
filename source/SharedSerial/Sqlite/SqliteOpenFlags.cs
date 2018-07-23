//Code from here: https://github.com/ellisnet/SimpleAdo/blob/master/SimpleAdo.Sqlite/SqliteOpenFlags.cs

using System;

namespace SharedSerial.Sqlite
{
    /// <summary> A bit-field of flags for specifying sqlite open options. </summary>
    [Flags]
    public enum SqliteOpenFlags
    {
        /// <summary> A binary constant representing the none flag. </summary>
        None = 0,
        /// <summary> A binary constant representing the read only flag. </summary>
        ReadOnly = 0x01,
        /// <summary> A binary constant representing the read write flag. </summary>
        ReadWrite = 0x02,
        /// <summary> A binary constant representing the create flag. </summary>
        Create = 0x04,
        /// <summary> A binary constant representing the URI flag. </summary>
        Uri = 0x40,
        /// <summary> A binary constant representing the shared cache flag. </summary>
        SharedCache = 0x01000000,
    }
}
