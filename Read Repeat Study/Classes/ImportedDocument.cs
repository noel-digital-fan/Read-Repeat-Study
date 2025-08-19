using SQLite;

namespace Read_Repeat_Study.Models
{
    public class ImportedDocument
    {
        [PrimaryKey, AutoIncrement]
        public int ID { get; set; }

        public string Name { get; set; }
        public string FilePath { get; set; }
        public string Content { get; set; }
        public DateTime ImportedDate { get; set; }
        public int? FlagId { get; set; } // Foreign key to Flags table

        // Navigation property (not stored in DB)
        [Ignore]
        public Flags Flag { get; set; }
    }
}
