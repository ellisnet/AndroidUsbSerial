using System.Collections.Generic;
using System.Threading.Tasks;

namespace SharedSerial.Services
{
    public interface IDataStoreService<T>
    {
        Task<bool> AddItemAsync(T item);
        Task<bool> UpdateItemAsync(T item);
        Task<bool> DeleteItemAsync(T item);
        Task<T> GetItemAsync(string id);
        Task<IList<T>> GetItemsAsync();

        Task<byte[]> GetDatabaseAsBytes();
        Task<bool> ResetDatabaseFromBytes(byte[] databaseFile);
    }
}
