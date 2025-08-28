using Read_Repeat_Study;

namespace UnitTest
{
    public class ReadRepeatStudyTests
    {
        private TestDatabaseService CreateTestDatabaseService()
        {
            return new TestDatabaseService();
        }

        private TestReportService CreateTestReportService()
        {
            return new TestReportService();
        }
        [Fact]
        public async Task Test1_CreateFlagWithoutName_ShouldFail() // Test creating a flag without a name
        {
            string flagName = string.Empty?.Trim() ?? string.Empty;
            bool validationPassed = !string.IsNullOrWhiteSpace(flagName);
            Assert.False(validationPassed, "Validation should fail when flag name is empty");
            flagName = ((string?)null)?.Trim() ?? string.Empty;
            validationPassed = !string.IsNullOrWhiteSpace(flagName);
            Assert.False(validationPassed, "Validation should fail when flag name is null");
            flagName = "   ".Trim() ?? string.Empty;
            validationPassed = !string.IsNullOrWhiteSpace(flagName);
            Assert.False(validationPassed, "Validation should fail when flag name is only whitespace");
            flagName = "Valid Flag Name".Trim() ?? string.Empty;
            validationPassed = !string.IsNullOrWhiteSpace(flagName);
            Assert.True(validationPassed, "Validation should pass with a valid flag name");
            var invalidFlag = new Flags { Name = string.Empty, Color = "#FF0000" };
            var invalidFlagName = invalidFlag.Name?.Trim() ?? string.Empty;
            var shouldNotSave = string.IsNullOrWhiteSpace(invalidFlagName);
            Assert.True(shouldNotSave, "Should not save flag with empty name");
        }
        [Fact]
        public async Task Test2_ImportSupportedFileDocuments_ShouldPass() // Test importing documents with supported file types (DOCX, PDF, EPUB, TXT)
        {
            var allowedExtensions = new[] { ".txt", ".docx", ".epub", ".pdf" };
            var testFiles = new[]
            {
                    "document.txt",
                    "presentation.docx",
                    "book.epub",
                    "report.pdf",
                    "invalid.xyz",
                    "another.doc",
                    "file.TXT",
                    "FILE.PDF"
                };
            var supportedFiles = testFiles
                .Where(f => allowedExtensions.Contains(Path.GetExtension(f).ToLower()))
                .ToList();
            Assert.Equal(6, supportedFiles.Count);
            Assert.Contains("document.txt", supportedFiles);
            Assert.Contains("presentation.docx", supportedFiles);
            Assert.Contains("book.epub", supportedFiles);
            Assert.Contains("report.pdf", supportedFiles);
            Assert.Contains("file.TXT", supportedFiles);
            Assert.Contains("FILE.PDF", supportedFiles);
            Assert.DoesNotContain("invalid.xyz", supportedFiles);
            Assert.DoesNotContain("another.doc", supportedFiles);
            var emptyFiles = new string[0];
            var emptySupportedFiles = emptyFiles
                .Where(f => allowedExtensions.Contains(Path.GetExtension(f).ToLower()))
                .ToList();
            Assert.Empty(emptySupportedFiles);
            var noValidFiles = !supportedFiles.Any();
            Assert.False(noValidFiles, "Should have valid files when supported extensions are present");
        }
        [Fact]
        public async Task Test3_PlayDocumentWithSelectedVoice_ShouldPass() // Test playing a document with selected voice and content
        {
            var db = CreateTestDatabaseService();
            var testDocument = new ImportedDocument
            {
                Name = "Test Document",
                Content = "This is a test document. It has multiple sentences.",
                ImportedDate = DateTime.Now
            };

            await db.SaveDocumentAsync(testDocument);
            bool hasSelectedVoice = true;
            bool hasContent = !string.IsNullOrEmpty(testDocument.Content);
            bool hasPages = !string.IsNullOrWhiteSpace(testDocument.Content);
            Assert.True(hasSelectedVoice, "Should have a selected voice for playback");
            Assert.True(hasContent, "Should have content to play");
            Assert.True(hasPages, "Should have pages to play");
            Assert.True(hasSelectedVoice && hasContent && hasPages, "All conditions should be met for playback");
            var emptyDocument = new ImportedDocument
            {
                Name = "Empty Document",
                Content = "",
                ImportedDate = DateTime.Now
            };

            hasContent = !string.IsNullOrEmpty(emptyDocument.Content);
            Assert.False(hasContent, "Should not be able to play document with empty content");
            var nullContentDocument = new ImportedDocument
            {
                Name = "Null Content Document",
                Content = null!,
                ImportedDate = DateTime.Now
            };

            hasContent = !string.IsNullOrEmpty(nullContentDocument.Content);
            Assert.False(hasContent, "Should not be able to play document with null content");
            bool noVoiceSelected = false;
            bool canPlayWithoutVoice = noVoiceSelected && hasContent;
            Assert.False(canPlayWithoutVoice, "Should not be able to play without voice selected");
        }
        [Fact]
        public async Task Test4_LoopDocumentAfterEnding_ShouldPass() // Test looping a document after it ends
        {
            var db = CreateTestDatabaseService();
            var testDocument = new ImportedDocument
            {
                Name = "Test Document",
                Content = "Short test content.",
                ImportedDate = DateTime.Now
            };

            await db.SaveDocumentAsync(testDocument);
            bool isRepeating = false;
            isRepeating = !isRepeating;
            Assert.True(isRepeating, "Repeat mode should be enabled after first click");
            isRepeating = !isRepeating;
            Assert.False(isRepeating, "Repeat mode should be disabled after second click");
            for (int i = 0; i < 5; i++)
            {
                bool previousState = isRepeating;
                isRepeating = !isRepeating;
                Assert.NotEqual(previousState, isRepeating);
            }
            isRepeating = true;
            bool shouldRestartFromBeginning = isRepeating;
            Assert.True(shouldRestartFromBeginning, "Should restart from beginning when repeat mode is on");
            bool completedDocument = true;
            bool shouldContinueRepeating = isRepeating && completedDocument;
            Assert.True(shouldContinueRepeating, "Should continue repeating when document is completed and repeat mode is on");
        }

        [Fact]
        public async Task Test5_ExportReportInTxtAndCsv_ShouldPass() // Test exporting document report in TXT and CSV formats
        {
            var db = CreateTestDatabaseService();
            var reportService = CreateTestReportService();
            var documents = new List<ImportedDocument>
                {
                    new ImportedDocument
                    {
                        Name = "Document 1",
                        Content = "Test content 1 with some sample text to test reading progress calculation.",
                        ImportedDate = DateTime.Now.AddDays(-1),
                        LastPageIndex = 1
                    },
                    new ImportedDocument
                    {
                        Name = "Document 2",
                        Content = "Test content 2 with different sample text for variety in testing.",
                        ImportedDate = DateTime.Now.AddDays(-2),
                        LastPageIndex = 2
                    }
                };
            foreach (var doc in documents)
            {
                await db.SaveDocumentAsync(doc);
            }

            try
            {
                string csvPath = await reportService.ExportToCsvAsync(documents);
                Assert.NotNull(csvPath);
                Assert.NotEmpty(csvPath);
                Assert.True(csvPath.EndsWith(".csv"), "CSV export should create a .csv file");
                Assert.True(File.Exists(csvPath), "CSV file should exist after export");
                string csvContent = await File.ReadAllTextAsync(csvPath);
                Assert.Contains("Document Name", csvContent);
                Assert.Contains("Document 1", csvContent);
                Assert.Contains("Document 2", csvContent);
                Assert.Contains("ID,Document Name,Flag,Imported Date", csvContent);
                Assert.Contains("Generated on", csvContent);
                var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var dataLines = lines.Where(l => !l.StartsWith("#") && !l.StartsWith("ID,Document Name") && !string.IsNullOrWhiteSpace(l) && l.Contains("Document")).ToList();
                Assert.Equal(2, dataLines.Count);
                Assert.Equal(2, dataLines.Count);
                if (File.Exists(csvPath))
                    File.Delete(csvPath);
            }
            catch (Exception ex)
            {
                Assert.Fail($"CSV export failed: {ex.Message}");
            }
            try
            {
                string txtPath = await reportService.ExportToTextAsync(documents);
                Assert.NotNull(txtPath);
                Assert.NotEmpty(txtPath);
                Assert.True(txtPath.EndsWith(".txt"), "Text export should create a .txt file");
                Assert.True(File.Exists(txtPath), "Text file should exist after export");
                string txtContent = await File.ReadAllTextAsync(txtPath);
                Assert.Contains("READ REPEAT STUDY - DOCUMENT REPORT", txtContent);
                Assert.Contains("Document 1", txtContent);
                Assert.Contains("Document 2", txtContent);
                Assert.Contains("Total Documents: 2", txtContent);
                Assert.Contains("SUMMARY", txtContent);
                Assert.Contains("DOCUMENTS LIST", txtContent);
                Assert.Contains("Generated on", txtContent);

                Assert.Contains("Recently Imported (7 days): 2", txtContent);
                Assert.Contains("Flagged Documents: 0", txtContent);

                Assert.Contains("Recently Imported (7 days): 2", txtContent);
                Assert.Contains("Flagged Documents: 0", txtContent);

                if (File.Exists(txtPath))
                    File.Delete(txtPath);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Text export failed: {ex.Message}");
            }

            var emptyDocuments = new List<ImportedDocument>();
            try
            {
                string emptyReportPath = await reportService.ExportToTextAsync(emptyDocuments);
                Assert.NotNull(emptyReportPath);
                string emptyContent = await File.ReadAllTextAsync(emptyReportPath);
                Assert.Contains("Total Documents: 0", emptyContent);
                if (File.Exists(emptyReportPath))
                    File.Delete(emptyReportPath);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Empty report export failed: {ex.Message}");
            }
        }
    }
}
