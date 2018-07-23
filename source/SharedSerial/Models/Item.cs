using SQLite;

namespace SharedSerial.Models
{
    public class Item
    {
        [PrimaryKey]
        [AutoIncrement]
        public long Id { get; set; }
        public string Text { get; set; }
        public string Description { get; set; }

        public string Display => $"{Text} - {Description}";
    }
}
