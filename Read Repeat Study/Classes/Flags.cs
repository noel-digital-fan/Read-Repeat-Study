using SQLite;

namespace Read_Repeat_Study
{
    public class Flags
    {
        [PrimaryKey, AutoIncrement]
        public int ID { get; set; }

        public string Name { get; set; }
        public string Color { get; set; }
    }
}
