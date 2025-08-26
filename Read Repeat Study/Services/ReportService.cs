using System.Text;

namespace Read_Repeat_Study.Services
{
    public class ReportService
    {
        public async Task<string> ExportToCsvAsync(IEnumerable<ImportedDocument> documents, string? customPath = null)
        {
            try
            {
                var csv = new StringBuilder();
                
                // Add header with timestamp
                csv.AppendLine($"# Document Report - Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                csv.AppendLine();
                
                // Add CSV headers
                csv.AppendLine("ID,Document Name,Flag,Imported Date,Last Page Index,Content Length,Reading Progress");
                
                // Add document data
                foreach (var doc in documents.OrderBy(d => d.Name))
                {
                    var flagName = doc.Flag?.Name ?? "No Flag";
                    var lastPage = doc.LastPageIndex?.ToString() ?? "0";
                    var contentLength = doc.Content?.Length.ToString() ?? "0";
                    var readingProgress = GetReadingProgress(doc);
                    
                    // Escape commas and quotes in CSV data
                    var name = EscapeCsvField(doc.Name);
                    var flag = EscapeCsvField(flagName);
                    
                    csv.AppendLine($"{doc.ID},{name},{flag},{doc.ImportedDate:yyyy-MM-dd HH:mm},{lastPage},{contentLength},{readingProgress}%");
                }
                
                // Create filename with timestamp
                var fileName = $"DocumentReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                
                // Use custom path if provided, otherwise use downloads path
                var filePath = string.IsNullOrEmpty(customPath) 
                    ? Path.Combine(GetDownloadsPath() ?? FileSystem.AppDataDirectory, fileName)
                    : Path.Combine(customPath, fileName);
                
                // Write to file
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
                
                // Header
                report.AppendLine("???????????????????????????????????????????????????");
                report.AppendLine("       READ REPEAT STUDY - DOCUMENT REPORT");
                report.AppendLine("???????????????????????????????????????????????????");
                report.AppendLine($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                report.AppendLine();
                
                // Summary
                report.AppendLine("SUMMARY");
                report.AppendLine("???????????????????????????????????????????????????");
                report.AppendLine($"Total Documents: {documentsList.Count}");
                report.AppendLine($"Flagged Documents: {documentsList.Count(d => d.Flag != null)}");
                report.AppendLine($"Recently Imported (7 days): {documentsList.Count(d => d.ImportedDate >= DateTime.Now.AddDays(-7))}");
                
                if (documentsList.Any())
                {
                    report.AppendLine($"Average Reading Progress: {documentsList.Average(d => GetReadingProgress(d)):F1}%");
                }
                
                report.AppendLine();
                
                // Documents List
                report.AppendLine("DOCUMENTS LIST");
                report.AppendLine("???????????????????????????????????????????????????");
                
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
                
                // Flags Breakdown
                var flagGroups = documentsList
                    .Where(d => d.Flag != null)
                    .GroupBy(d => d.Flag!.Name)
                    .OrderBy(g => g.Key)
                    .ToList();
                
                if (flagGroups.Any())
                {
                    report.AppendLine("DOCUMENTS BY FLAG");
                    report.AppendLine("???????????????????????????????????????????????????");
                    
                    foreach (var flagGroup in flagGroups)
                    {
                        var count = flagGroup.Count();
                        var percentage = (count * 100.0) / documentsList.Count;
                        report.AppendLine($"• {flagGroup.Key}: {count} documents ({percentage:F1}%)");
                    }
                }
                
                report.AppendLine();
                report.AppendLine("???????????????????????????????????????????????????");
                report.AppendLine("         End of Report");
                report.AppendLine("???????????????????????????????????????????????????");
                
                // Create filename with timestamp
                var fileName = $"DocumentReport_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                
                // Use custom path if provided, otherwise use downloads path
                var filePath = string.IsNullOrEmpty(customPath) 
                    ? Path.Combine(GetDownloadsPath() ?? FileSystem.AppDataDirectory, fileName)
                    : Path.Combine(customPath, fileName);
                
                // Write to file
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
                if (DeviceInfo.Platform == DevicePlatform.Android)
                {
                    var externalStorage = Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath;
                    if (!string.IsNullOrEmpty(externalStorage))
                    {
                        var publicDownloads = Path.Combine(externalStorage, "Download");
                        if (Directory.Exists(publicDownloads))
                            return publicDownloads;
                    }
                }

                // Fallback to user profile Downloads
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
            if (string.IsNullOrEmpty(field))
                return "";
            
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }
            
            return field;
        }

        private double GetReadingProgress(ImportedDocument doc)
        {
            // Estimate reading progress based on last page index
            if (!doc.LastPageIndex.HasValue || doc.LastPageIndex <= 0)
                return 0.0;
            
            // Estimate total pages based on content length (rough approximation)
            var estimatedTotalPages = Math.Max(1, (doc.Content?.Length ?? 0) / 2000); // ~2000 chars per page
            var currentPage = doc.LastPageIndex.Value + 1;
            
            return Math.Min(100.0, (currentPage * 100.0) / estimatedTotalPages);
        }
    }
}