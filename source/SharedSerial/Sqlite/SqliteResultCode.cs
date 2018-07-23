//Code from here: https://github.com/ellisnet/SimpleAdo/blob/master/SimpleAdo.Sqlite/SqliteResultCode.cs

using System.Linq;

namespace SharedSerial.Sqlite
{
    /// <summary>
    /// SQLite result codes.  Actually, this enumeration represents a return code, which may also
    /// indicate success in one of several ways (e.g. SQLITE_OK, SQLITE_ROW, and SQLITE_DONE).
    /// </summary>
	public enum SqliteResultCode
    {
        /// <summary>
        /// The error code is unknown.  This error code
        /// is only used by the managed wrapper itself.
        /// </summary>
        Unknown = -1,

        /// <summary>
        /// Successful result
        /// </summary>
        Ok /* 0 */,

        /// <summary>
        /// SQL error or missing database
        /// </summary>
        Error /* 1 */,

        /// <summary>
        /// Internal logic error in SQLite
        /// </summary>
        Internal /* 2 */,

        /// <summary>
        /// Access permission denied
        /// </summary>
        Perm /* 3 */,

        /// <summary>
        /// Callback routine requested an abort
        /// </summary>
        Abort /* 4 */,

        /// <summary>
        /// The database file is locked
        /// </summary>
        Busy /* 5 */,

        /// <summary>
        /// A table in the database is locked
        /// </summary>
        Locked /* 6 */,

        /// <summary>
        /// A malloc() failed
        /// </summary>
        NoMem /* 7 */,

        /// <summary>
        /// Attempt to write a readonly database
        /// </summary>
        ReadOnly /* 8 */,

        /// <summary>
        /// Operation terminated by sqlite3_interrupt()
        /// </summary>
        Interrupt /* 9 */,

        /// <summary>
        /// Some kind of disk I/O error occurred
        /// </summary>
        IoErr /* 10 */,

        /// <summary>
        /// The database disk image is malformed
        /// </summary>
        Corrupt /* 11 */,

        /// <summary>
        /// Unknown opcode in sqlite3_file_control()
        /// </summary>
        NotFound /* 12 */,

        /// <summary>
        /// Insertion failed because database is full
        /// </summary>
        Full /* 13 */,

        /// <summary>
        /// Unable to open the database file
        /// </summary>
        CantOpen /* 14 */,

        /// <summary>
        /// Database lock protocol error
        /// </summary>
        Protocol /* 15 */,

        /// <summary>
        /// Database is empty
        /// </summary>
        Empty /* 16 */,

        /// <summary>
        /// The database schema changed
        /// </summary>
        Schema /* 17 */,

        /// <summary>
        /// String or BLOB exceeds size limit
        /// </summary>
        TooBig /* 18 */,

        /// <summary>
        /// Abort due to constraint violation
        /// </summary>
        Constraint /* 19 */,

        /// <summary>
        /// Data type mismatch
        /// </summary>
        Mismatch /* 20 */,

        /// <summary>
        /// Library used incorrectly
        /// </summary>
        Misuse /* 21 */,

        /// <summary>
        /// Uses OS features not supported on host
        /// </summary>
        NoLfs /* 22 */,

        /// <summary>
        /// Authorization denied
        /// </summary>
        Auth /* 23 */,

        /// <summary>
        /// Auxiliary database format error
        /// </summary>
        Format /* 24 */,

        /// <summary>
        /// 2nd parameter to sqlite3_bind out of range
        /// </summary>
        Range /* 25 */,

        /// <summary>
        /// File opened that is not a database file
        /// </summary>
        NotADb /* 26 */,

        /// <summary>
        /// Notifications from sqlite3_log()
        /// </summary>
        Notice /* 27 */,

        /// <summary>
        /// Warnings from sqlite3_log()
        /// </summary>
        Warning /* 28 */,

        /// <summary>
        /// sqlite3_step() has another row ready
        /// </summary>
        Row = 100,

        /// <summary>
        /// sqlite3_step() has finished executing
        /// </summary>
        Done, /* 101 */

        /// <summary>
        /// Used to mask off extended result codes
        /// </summary>
        NonExtendedMask = 0xFF
    }

    /// <summary> A sqlite result code extensions. </summary>
    public static class SqliteResultCodeExtensions
    {
        /// <summary> The success codes. </summary>
        private static readonly SqliteResultCode[] successCodes = { SqliteResultCode.Ok, SqliteResultCode.Done, SqliteResultCode.Row };

        /// <summary>
        /// A SqliteResultCode extension method that query if 'resultCode' is success code.
        /// </summary>
        /// <param name="resultCode"> The resultCode to act on. </param>
        /// <returns> True if success code, false if not. </returns>
        public static bool IsSuccessCode(this SqliteResultCode resultCode)
        {
            return successCodes.Contains(resultCode);
        }

        /// <summary> A SqliteResultCode extension method that throw on error. </summary>
        /// <exception cref="SqliteException"> Thrown when a Sqlite error condition occurs. </exception>
        /// <param name="resultCode"> The resultCode to act on. </param>
        public static void ThrowOnError(this SqliteResultCode resultCode)
        {
            if (!resultCode.IsSuccessCode())
            {
                throw new SqliteException(resultCode);
            }
        }
    }
}
