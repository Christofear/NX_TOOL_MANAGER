namespace NX_TOOL_MANAGER.Models
{
    public sealed class DatDocumentRef
    {
        public string FileName { get; set; } = string.Empty;   // e.g., Tool_Database.dat
        public string FullPath { get; set; } = string.Empty;   // full path on disk
        public FileKind Kind { get; set; }                     // Tools / Holders / Shanks
        public DatDocument Document { get; set; }
    }
}
