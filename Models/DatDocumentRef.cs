using NX_TOOL_MANAGER.Models;
using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NX_TOOL_MANAGER.Models
{
    public sealed class DatDocumentRef : INotifyPropertyChanged
    {
        public string FileName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public FileKind Kind { get; set; }
        public DatDocument Document { get; set; }
        public string Units { get; set; }
        public IEnumerable Children { get; set; }
        public bool IsReadOnly { get; set; }

        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                if (_isDirty == value) return;
                _isDirty = value;
                OnPropertyChanged(nameof(IsDirty));
                OnPropertyChanged(nameof(DisplayFileName));
            }
        }

        public string DisplayFileName => IsDirty ? $"{FileName} *" : FileName;

        public void SetDirty()
        {
            IsDirty = true;
        }

        public void ClearDirty()
        {
            IsDirty = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}

