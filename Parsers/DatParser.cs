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
                FileKind.Trackpoints => TrackpointDatParser.Parse(lines),
                FileKind.SegmentedTools => SegmentedToolDatParser.Parse(lines),
                _ => ToolDatParser.Parse(lines) // Default fallback
            };

        public static FileKind DetectKind(string path, IEnumerable<string> lines = null)
        {
            var name = Path.GetFileName(path)?.ToLowerInvariant() ?? "";

            // First, try to identify the file by its name.
            if (name.Contains("holder")) return FileKind.Holders;
            if (name.Contains("shank")) return FileKind.Shanks;
            if (name.Contains("trackpoint")) return FileKind.Trackpoints;
            if (name.Contains("segmented_tool")) return FileKind.SegmentedTools;
            if (name.Contains("tool")) return FileKind.Tools;

            // If the name is ambiguous, fall back to content sniffing.
            lines ??= File.ReadLines(path).Take(400);
            return DetectKindFromContent(lines);
        }

        private static FileKind DetectKindFromContent(IEnumerable<string> lines)
        {
            var lineList = lines.ToList();

            // First, check for header comments which are very reliable identifiers.
            var content = string.Join("\n", lineList).ToUpperInvariant();
            if (content.Contains("HOLDER_DATABASE.DAT")) return FileKind.Holders;
            if (content.Contains("SHANK_DATABASE.DAT")) return FileKind.Shanks;
            if (content.Contains("TRACKPOINT_DATABASE.DAT")) return FileKind.Trackpoints;
            if (content.Contains("SEGMENTED_TOOL_DATABASE.DAT")) return FileKind.SegmentedTools;

            // If no header comment is found, fall back to sniffing FORMAT lines.
            foreach (var ln in lineList)
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

                // THE FIX: Added content sniffing for segmented tools based on a unique keyword.
                if (body.Contains("SWEEP"))
                    return FileKind.SegmentedTools;
            }

            // Default to tools if nothing clear is found
            return FileKind.Tools;
        }
    }
}

