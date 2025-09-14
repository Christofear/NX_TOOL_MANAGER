using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace NX_TOOL_MANAGER.Models
{
    public sealed class DatDocument
    {
        [JsonIgnore]
        public DatDocumentRef ParentRef { get; set; }

        public string Units { get; set; } = "Unknown";
        public List<string> Head { get; } = new();
        public List<DatClass> Classes { get; } = new();
    }

    public sealed class DatClass
    {
        [JsonIgnore]
        public DatDocument ParentDocument { get; set; }

        public string Name { get; set; } = string.Empty;

        // This is the new property that will hold the friendly name from the .def file.
        public string UIName { get; set; }

        // The DisplayName now prioritizes the UIName for a cleaner presentation.
        public string DisplayName => !string.IsNullOrWhiteSpace(UIName) ? UIName : Name;
        public string DisplayHeader => $"{DisplayName} ({Rows.Count})";

        public List<string> PreClassLines { get; } = new();
        public string ClassLine { get; set; }
        public List<string> PreFormatLines { get; } = new();
        public List<string> FormatLines { get; } = new();
        public List<string> PreDataLines { get; } = new();
        public ObservableCollection<DatRow> Rows { get; } = new();
        public List<string> PostDataLines { get; } = new();

        public List<string> FormatFields { get; } = new();
        public List<DatClass> Children { get; } = new();
    }

    public sealed class DatRow : INotifyPropertyChanged
    {
        [JsonIgnore]
        public DatClass ParentClass { get; set; }

        public List<string> RawLines { get; } = new();
        public List<string> Values { get; } = new();
        public Dictionary<string, string> Map { get; } = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

        public string Get(string field) => Map.TryGetValue(field, out var v) ? v : string.Empty;

        public void Set(string field, string value)
        {
            if (Map.TryGetValue(field, out var oldValue) && oldValue == value)
            {
                return;
            }

            Map[field] = value;
            OnPropertyChanged($"Map[{field}]");

            ParentClass?.ParentDocument?.ParentRef?.SetDirty();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
