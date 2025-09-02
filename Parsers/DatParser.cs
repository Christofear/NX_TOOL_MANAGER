// DatParsers.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NX_TOOL_MANAGER.Models;


namespace NX_TOOL_MANAGER
{
    public static class DatParsers
    {
        public static DatDocument Parse(FileKind kind, IEnumerable<string> lines) =>
            kind switch
            {
                FileKind.Tools => ToolDatParser.Parse(lines),
                FileKind.Holders => HolderDatParser.Parse(lines),
                FileKind.Shanks => ShankDatParser.Parse(lines),
                _ => ToolDatParser.Parse(lines)
            };

        // Convenience: pick kind by filename first, then sniff FORMAT fields.
        public static FileKind DetectKind(string path, IEnumerable<string> lines = null)
        {
            var name = Path.GetFileName(path)?.ToLowerInvariant() ?? "";

            if (name.Contains("holder")) return FileKind.Holders;
            if (name.Contains("shank")) return FileKind.Shanks;
            if (name.Contains("tool")) return FileKind.Tools;

            // Fallback to content sniffing (looks at FORMAT lines)
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
                // Holder FORMATs contain RTYPE + (HTYPE or MTS or MAXOFF/MINDIA/MAXDIA)
                if (body.Contains(" RTYPE") && (body.Contains(" HTYPE") || body.Contains(" MTS") || body.Contains(" MAXOFF") || body.Contains(" MINDIA")))
                    return FileKind.Holders;

                // Shank FORMATs contain RTYPE + (STYPE SNUM DESCR) or (SEQ DIAM LENGTH TAPER CRAD)
                if (body.Contains(" RTYPE") && (body.Contains(" STYPE") || (body.Contains(" SEQ") && body.Contains(" DIAM") && body.Contains(" TAPER"))))
                    return FileKind.Shanks;
            }

            // Default to tools if nothing clear is found
            return FileKind.Tools;
        }
    }
}
