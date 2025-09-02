using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

// UPDATED: Namespace reflects the "Models" folder
namespace NX_TOOL_MANAGER.Models
{
    public sealed class DatDocument
    {
        public List<string> Head { get; } = new();
        public List<DatClass> Classes { get; } = new();
    }

    public sealed class DatClass
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "(Unnamed Class)" : Name;

        // New: handy read-only string for the tree header
        public string DisplayHeader => $"{DisplayName} ({Rows.Count})";

        public List<string> FormatFields { get; } = new();
        public ObservableCollection<DatRow> Rows { get; } = new();
        public List<DatClass> Children { get; } = new();
    }


    // UPDATED: DatRow now notifies the UI when its data changes
    public sealed class DatRow : INotifyPropertyChanged
    {
        public List<string> RawLines { get; } = new();
        public List<string> Values { get; } = new();
        public Dictionary<string, string> Map { get; } = new();

        public string Get(string field) => Map.TryGetValue(field, out var v) ? v : string.Empty;

        // ADDED: A method to set values that also notifies the UI to refresh
        public void Set(string field, string value)
        {
            Map[field] = value;
            // This tells the DataGrid that the specific cell's value has changed
            OnPropertyChanged($"Map[{field}]");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

    }



}

