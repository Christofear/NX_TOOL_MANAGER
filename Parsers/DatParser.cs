using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NX_TOOL_MANAGER.Models;

namespace NX_TOOL_MANAGER.Services
{
    public static class DatParsers
    {
        public static DatDocument Parse(IEnumerable<string> lines, FileKind kind) =>
            kind switch
            {
                FileKind.Tools => ToolDatParser.Parse(lines),
                FileKind.Holders => HolderDatParser.Parse(lines),
                FileKind.Shanks => ShankDatParser.Parse(lines),
                // THE FIX: Now calls the new, dedicated parser for Trackpoint files.
                FileKind.Trackpoints => TrackpointDatParser.Parse(lines),
                _ => ToolDatParser.Parse(lines) // Default fallback
            };

        // This method overload is preserved for convenience where the kind is not yet known.
        public static DatDocument Parse(FileKind kind, IEnumerable<string> lines) => Parse(lines, kind);

        public static FileKind DetectKind(string path, IEnumerable<string> lines = null)
        {
            var name = Path.GetFileName(path)?.ToLowerInvariant() ?? "";

            // First, try to identify the file by its name.
            if (name.Contains("holder")) return FileKind.Holders;
            if (name.Contains("shank")) return FileKind.Shanks;
            if (name.Contains("trackpoint")) return FileKind.Trackpoints;
            if (name.Contains("tool")) return FileKind.Tools;

            // If the name is ambiguous, fall back to content sniffing.
            lines ??= File.ReadLines(path).Take(400);
            return DetectKindFromContent(lines);
        }

        private static FileKind DetectKindFromContent(IEnumerable<string> lines)
        {
            // Look only at FORMAT lines; they carry the schema
            foreach (var ln in lines)
            {
                var t = ln.TrimStart('#', ' ', '\t');
                if (!t.StartsWith("FORMAT", StringComparison.OrdinalIgnoreCase)) continue;

                var body = t.Substring(6).ToUpperInvariant();

                if (body.Contains(" RTYPE") && (body.Contains(" HTYPE") || body.Contains(" MTS") || body.Contains(" MAXOFF") || body.Contains(" MINDIA")))
                    return FileKind.Holders;

                if (body.Contains(" RTYPE") && (body.Contains(" STYPE") || (body.Contains(" SEQ") && body.Contains(" DIAM") && body.Contains(" TAPER"))))
                    return FileKind.Shanks;

                if (body.Contains("DEFTYPE"))
                    return FileKind.Trackpoints;
            }

            // Default to tools if nothing clear is found
            return FileKind.Tools;
        }
    }
}

