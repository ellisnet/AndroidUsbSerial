using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharedSerial.Sqlite;
using SharedSerial.Models;
using SQLite;
using SQLitePCL;

namespace SharedSerial.Services
{
    public class SqliteDataStoreService : IDataStoreService<Item>
    {
        private readonly string _databasePath;
        private readonly string _databaseFolder = "database";
        private readonly string _databaseFilename = "db.sqlite";
        private readonly string _databaseName = "main";

        private bool _tableCreated;

        private readonly SemaphoreSlim _dbOperationLocker = new SemaphoreSlim(1, 1);
        private readonly object _dbFileLocker = new object();

        //Super inefficient to always be creating my database connection, disposing it, and then
        // doing a checkpoint; but really want to make sure the data is complete and up-to-date.
        // And want to be able to overwrite the database easily.

        private bool CheckpointDatabase()
        {
            bool result = false;

            lock (_dbFileLocker)
            {
                bool isDatabaseOpen = false;
                sqlite3 db;

                var resultCode = (SqliteResultCode)raw.sqlite3_open_v2(_databasePath, out db, (int)SqliteOpenFlags.ReadWrite, null);

                if (!resultCode.IsSuccessCode())
                {
                    Debugger.Break();
                }
                else
                {
                    isDatabaseOpen = true;
                    resultCode = (SqliteResultCode)raw.sqlite3_wal_checkpoint_v2(db, _databaseName,
                        (int)SqliteCheckpointMode.Full, out int logSize, out int framesCheckPointed);
                    result = resultCode.IsSuccessCode();
                    if (!result)
                    {
                        Debugger.Break();
                    }
                }

                if (isDatabaseOpen)
                {
                    resultCode = (SqliteResultCode)raw.sqlite3_close_v2(db);
                    if (!resultCode.IsSuccessCode())
                    {
                        Debugger.Break();
                    }
                    db.Dispose();
                }
            }

            return result;
        }

        public async Task<bool> AddItemAsync(Item item)
        {
            var tcs = new TaskCompletionSource<bool>();
            await _dbOperationLocker.WaitAsync();

            try
            {
                await Task.Run(() =>
                {
                    int result;
                    lock (_dbFileLocker)
                    {
                        using (var db = new SQLiteConnection(_databasePath, SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite))
                        {
                            result = db.Insert(item);
                        }
                    }
                    CheckpointDatabase();
                    tcs.SetResult(result > 0);
                });
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
                Debugger.Break();
                throw;
            }
            finally
            {
                _dbOperationLocker.Release();
            }

            return await tcs.Task;
        }

        public async Task<bool> UpdateItemAsync(Item item)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> DeleteItemAsync(Item item)
        {
            throw new NotImplementedException();
        }

        public async Task<Item> GetItemAsync(string id)
        {
            throw new NotImplementedException();
        }

        public async Task<IList<Item>> GetItemsAsync()
        {
            var tcs = new TaskCompletionSource<IList<Item>>();
            await _dbOperationLocker.WaitAsync();

            try
            {
                await Task.Run(() =>
                {
                    IList<Item> result;
                    lock (_dbFileLocker)
                    {
                        using (var db = new SQLiteConnection(_databasePath, SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite))
                        {
                            if (!_tableCreated)
                            {
                                db.CreateTable<Item>();
                                _tableCreated = true;
                            }

                            result = db.Table<Item>().ToArray();
                        }
                    }
                    CheckpointDatabase();
                    tcs.SetResult(result);
                });
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
                Debugger.Break();
                throw;
            }
            finally
            {
                _dbOperationLocker.Release();
            }

            return await tcs.Task;
        }

        public async Task<byte[]> GetDatabaseAsBytes()
        {
            var tcs = new TaskCompletionSource<byte[]>();

            await _dbOperationLocker.WaitAsync();
            try
            {
                await Task.Run(() =>
                {
                    byte[] result;
                    lock (_dbFileLocker)
                    {
                        using (var fs = new FileStream(_databasePath, FileMode.Open, FileAccess.Read))
                        {
                            using (var br = new BinaryReader(fs))
                            {
                                result = br.ReadBytes((int)fs.Length);
                            }
                        }
                    }
                    tcs.SetResult(result);
                });
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
                Debugger.Break();
                throw;
            }
            finally
            {
                _dbOperationLocker.Release();
            }

            return await tcs.Task;
        }

        public async Task<bool> ResetDatabaseFromBytes(byte[] databaseFile)
        {
            var tcs = new TaskCompletionSource<bool>();

            if (databaseFile != null && databaseFile.Any())
            {
                await _dbOperationLocker.WaitAsync();

                try
                {
                    await Task.Run(() =>
                    {
                        bool result;
                        lock (_dbFileLocker)
                        {
                            if (File.Exists(_databasePath))
                            {
                                File.Delete(_databasePath);
                            }

                            using (var fs = new FileStream(_databasePath, FileMode.CreateNew, FileAccess.Write))
                            {
                                using (var bw = new BinaryWriter(fs))
                                {
                                    bw.Write(databaseFile);
                                    result = true;
                                }
                            }
                        }
                        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                        tcs.SetResult(result);
                    });
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                    Debugger.Break();
                    throw;
                }
                finally
                {
                    _dbOperationLocker.Release();
                }
            }
            else
            {
                tcs.SetResult(false);
            }

            return await tcs.Task;
        }

        public SqliteDataStoreService(string libraryPath)
        {
            if (String.IsNullOrWhiteSpace(libraryPath) || (!Directory.Exists(libraryPath)))
            {
                throw new ArgumentOutOfRangeException(nameof(libraryPath));
            }

            string folderPath = Path.Combine(libraryPath, _databaseFolder);

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            _databasePath = Path.Combine(folderPath, _databaseFilename);
        }
    }
}
