using System.Text;
using Read_Repeat_Study;

namespace UnitTest
{
    public class TestReportService
    {
        public async Task<string> ExportToCsvAsync(IEnumerable<ImportedDocument> documents, string? customPath = null)
        {
            try
            {
                var csv = new StringBuilder();
                csv.AppendLine($"# Document Report - Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                csv.AppendLine();
                csv.AppendLine("ID,Document Name,Flag,Imported Date,Last Page Index,Content Length,Reading Progress");
                foreach (var doc in documents.OrderBy(d => d.Name))
                {
                    var flagName = doc.Flag?.Name ?? "No Flag";
                    var lastPage = doc.LastPageIndex?.ToString() ?? "0";
                    var contentLength = doc.Content?.Length.ToString() ?? "0";
                    var readingProgress = GetReadingProgress(doc);
                    var name = EscapeCsvField(doc.Name);
                    var flag = EscapeCsvField(flagName);
                    csv.AppendLine($"{doc.ID},{name},{flag},{doc.ImportedDate:yyyy-MM-dd HH:mm},{lastPage},{contentLength},{readingProgress}%");
                }
                var fileName = $"DocumentReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var filePath = string.IsNullOrEmpty(customPath)
                    ? Path.Combine(GetDownloadsPath() ?? Path.GetTempPath(), fileName)
                    : Path.Combine(customPath, fileName);
                await File.WriteAllTextAsync(filePath, csv.ToString());
                return filePath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to export CSV: {ex.Message}", ex);
            }
        }

        public async Task<string> ExportToTextAsync(IEnumerable<ImportedDocument> documents, string? customPath = null)
        {
            try
            {
                var report = new StringBuilder();
                var documentsList = documents.OrderBy(d => d.Name).ToList();
                report.AppendLine("???????????????????????????????????????");
                report.AppendLine("       READ REPEAT STUDY - DOCUMENT REPORT");
                report.AppendLine("???????????????????????????????????????");
                report.AppendLine($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                report.AppendLine();
                report.AppendLine("SUMMARY");
                report.AppendLine("???????????????????????????????????????");
                report.AppendLine($"Total Documents: {documentsList.Count}");
                report.AppendLine($"Flagged Documents: {documentsList.Count(d => d.Flag != null)}");
                report.AppendLine($"Recently Imported (7 days): {documentsList.Count(d => d.ImportedDate >= DateTime.Now.AddDays(-7))}");
                if (documentsList.Any())
                    report.AppendLine($"Average Reading Progress: {documentsList.Average(d => GetReadingProgress(d)):F1}%");
                report.AppendLine();
                report.AppendLine("DOCUMENTS LIST");
                report.AppendLine("???????????????????????????????????????");
                foreach (var doc in documentsList)
                {
                    report.AppendLine($"• {doc.Name}");
                    report.AppendLine($"  Flag: {doc.Flag?.Name ?? "No Flag"}");
                    report.AppendLine($"  Imported: {doc.ImportedDate:MM/dd/yyyy HH:mm}");
                    report.AppendLine($"  Last Page: {(doc.LastPageIndex + 1) ?? 1}");
                    report.AppendLine($"  Content Length: {doc.Content?.Length ?? 0:N0} characters");
                    report.AppendLine($"  Reading Progress: {GetReadingProgress(doc):F1}%");
                    report.AppendLine();
                }
                var flagGroups = documentsList
                    .Where(d => d.Flag != null)
                    .GroupBy(d => d.Flag!.Name)
                    .OrderBy(g => g.Key)
                    .ToList();
                if (flagGroups.Any())
                {
                    report.AppendLine("DOCUMENTS BY FLAG");
                    report.AppendLine("???????????????????????????????????????");
                    foreach (var flagGroup in flagGroups)
                    {
                        var count = flagGroup.Count();
                        var percentage = (count * 100.0) / documentsList.Count;
                        report.AppendLine($"• {flagGroup.Key}: {count} documents ({percentage:F1}%)");
                    }
                }
                report.AppendLine();
                report.AppendLine("???????????????????????????????????????");
                report.AppendLine("         End of Report");
                report.AppendLine("???????????????????????????????????????");
                var fileName = $"DocumentReport_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                var filePath = string.IsNullOrEmpty(customPath)
                    ? Path.Combine(GetDownloadsPath() ?? Path.GetTempPath(), fileName)
                    : Path.Combine(customPath, fileName);
                await File.WriteAllTextAsync(filePath, report.ToString());
                return filePath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to export text report: {ex.Message}", ex);
            }
        }

        private string? GetDownloadsPath()
        {
            try
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(userProfile))
                {
                    var downloads = Path.Combine(userProfile, "Downloads");
                    if (Directory.Exists(downloads))
                        return downloads;
                }
            }
            catch { }
            return null;
        }

        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field)) return string.Empty;
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            return field;
        }

        private double GetReadingProgress(ImportedDocument doc)
        {
            if (!doc.LastPageIndex.HasValue || doc.LastPageIndex <= 0) return 0.0;
            var estimatedTotalPages = Math.Max(1, (doc.Content?.Length ?? 0) / 2000);
            var currentPage = doc.LastPageIndex.Value + 1;
            return Math.Min(100.0, (currentPage * 100.0) / estimatedTotalPages);
        }
    }
}