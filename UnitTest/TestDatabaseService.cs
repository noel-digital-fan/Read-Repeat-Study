using SQLite;
using Read_Repeat_Study;

namespace UnitTest
{
    public class TestDatabaseService
    {
        private readonly SQLiteAsyncConnection _db;

        public TestDatabaseService()
        {
            // Database file in temp directory for testing
            var dbPath = Path.Combine(Path.GetTempPath(), $"test_readrepeatstudy_{Guid.NewGuid()}.db3");
            _db = new SQLiteAsyncConnection(dbPath);

            // Create tables if they don't exist
            _db.CreateTableAsync<Flags>().Wait();
            _db.CreateTableAsync<ImportedDocument>().Wait();
        }

        // Flags methods
        public async Task<List<Flags>> GetAllFlagsAsync() =>
            await _db.Table<Flags>().ToListAsync();

        public async Task<Flags> GetFlagAsync(int id) =>
            await _db.Table<Flags>().Where(f => f.ID == id).FirstOrDefaultAsync();

        public async Task SaveFlagAsync(Flags flag)
        {
            if (flag.ID != 0)
                await _db.UpdateAsync(flag);
            else
                await _db.InsertAsync(flag);
        }

        public async Task DeleteFlagAsync(Flags flag) =>
            await _db.DeleteAsync(flag);

        // ImportedDocument methods
        public async Task<List<ImportedDocument>> GetAllDocumentsAsync() =>
            await _db.Table<ImportedDocument>().ToListAsync();

        public async Task<ImportedDocument> GetDocumentByIdAsync(int id) =>
            await _db.Table<ImportedDocument>().Where(d => d.ID == id).FirstOrDefaultAsync();

        public async Task SaveDocumentAsync(ImportedDocument document)
        {
            if (document.ID != 0)
                await _db.UpdateAsync(document);
            else
                await _db.InsertAsync(document);
        }

        public async Task DeleteDocumentAsync(ImportedDocument document) =>
            await _db.DeleteAsync(document);
    }
}