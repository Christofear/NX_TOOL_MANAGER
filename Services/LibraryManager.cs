using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Win32;
using NX_TOOL_MANAGER.Helpers;   // RelayCommand<T>
using NX_TOOL_MANAGER.Models;

namespace NX_TOOL_MANAGER.Services
{
    public class LibraryManager : INotifyPropertyChanged
    {
        // Singleton-ish (simple)
        public static LibraryManager Instance { get; } = new LibraryManager();

        public ObservableCollection<DatDocumentRef> Libraries { get; } = new();

        private DatDocumentRef _selectedDocument;
        public DatDocumentRef SelectedDocument
        {
            get => _selectedDocument;
            set
            {
                if (_selectedDocument == value) return;
                _selectedDocument = value;
                Raise();
            }
        }

        private DatClass _selectedClass;
        public DatClass SelectedClass
        {
            get => _selectedClass;
            set
            {
                if (_selectedClass == value) return;
                _selectedClass = value;
                Raise();
            }
        }

        private LibraryManager()
        {
            // Commands available to views (context menus, buttons, etc.)
            UnloadLibraryCommand = new RelayCommand<DatDocumentRef>(doc =>
            {
                if (doc != null) Unload(doc.Kind);
            });

            SaveLibraryCommand = new RelayCommand<DatDocumentRef>(doc =>
            {
                if (doc == null) return;
                // TODO: implement actual write-back
                // WriteLibrary(doc, doc.FullPath);
            });

            SaveAsLibraryCommand = new RelayCommand<DatDocumentRef>(doc =>
            {
                if (doc == null) return;
                var dlg = new SaveFileDialog { FileName = doc.FileName, Filter = "NX ASCII Tool DB (*.dat)|*.dat" };
                if (dlg.ShowDialog() == true)
                {
                    // TODO: implement actual write-back
                    // WriteLibrary(doc, dlg.FileName);
                }
            });
        }

        // Commands exposed to XAML
        public ICommand UnloadLibraryCommand { get; }
        public ICommand SaveLibraryCommand { get; }
        public ICommand SaveAsLibraryCommand { get; }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        public void Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

            var kind = DatParsers.DetectKind(path);
            var lines = File.ReadLines(path);
            var doc = DatParsers.Parse(kind, lines);

            // Only one per kind
            var existing = Libraries.FirstOrDefault(x => x.Kind == kind);
            if (existing != null) Libraries.Remove(existing);

            var added = new DatDocumentRef
            {
                Kind = kind,
                FullPath = path,
                FileName = Path.GetFileName(path),
                Document = doc
            };

            Libraries.Add(added);

            // === Auto-select the newly loaded document and its first class ===
            SelectedDocument = added;
            SelectedClass = added.Document?.Classes?.FirstOrDefault();
        }

        public void Unload(FileKind kind)
        {
            var existing = Libraries.FirstOrDefault(x => x.Kind == kind);
            if (existing == null) return;

            bool wasSelectedDoc = (SelectedDocument == existing);
            Libraries.Remove(existing);

            // Clear selection if we removed the selected stuff
            if (wasSelectedDoc)
            {
                // Prefer another library if present
                var nextDoc = Libraries.FirstOrDefault();
                SelectedDocument = nextDoc;
                SelectedClass = nextDoc?.Document?.Classes?.FirstOrDefault();
            }
            else if (SelectedClass != null && ReferenceEquals(existing, FindDocForClass(SelectedClass)))
            {
                // SelectedClass belonged to the removed doc
                SelectedClass = null;
            }
        }

        public void UnloadAll()
        {
            Libraries.Clear();
            SelectedDocument = null;
            SelectedClass = null;
        }

        /// Apply dialog result atomically on OK
        public void ApplySelection(string toolsPath, string holdersPath, string shanksPath)
        {
            // Tools
            var currentTools = Libraries.FirstOrDefault(x => x.Kind == FileKind.Tools)?.FullPath;
            if (string.IsNullOrEmpty(toolsPath) && !string.IsNullOrEmpty(currentTools))
                Unload(FileKind.Tools);
            else if (!string.IsNullOrEmpty(toolsPath))
                Load(toolsPath);

            // Holders
            var currentHolders = Libraries.FirstOrDefault(x => x.Kind == FileKind.Holders)?.FullPath;
            if (string.IsNullOrEmpty(holdersPath) && !string.IsNullOrEmpty(currentHolders))
                Unload(FileKind.Holders);
            else if (!string.IsNullOrEmpty(holdersPath))
                Load(holdersPath);

            // Shanks
            var currentShanks = Libraries.FirstOrDefault(x => x.Kind == FileKind.Shanks)?.FullPath;
            if (string.IsNullOrEmpty(shanksPath) && !string.IsNullOrEmpty(currentShanks))
                Unload(FileKind.Shanks);
            else if (!string.IsNullOrEmpty(shanksPath))
                Load(shanksPath);

            // If nothing selected yet but we have libraries, pick the first doc/class
            if (SelectedDocument == null && Libraries.Any())
            {
                var firstDoc = Libraries.First();
                SelectedDocument = firstDoc;
                SelectedClass = firstDoc.Document?.Classes?.FirstOrDefault();
            }
        }

        private DatDocumentRef FindDocForClass(DatClass cls)
        {
            if (cls == null) return null;
            return Libraries.FirstOrDefault(d => d.Document?.Classes?.Contains(cls) == true);
        }

        // (Optional) Write-back stub:
        // private void WriteLibrary(DatDocumentRef doc, string path) { ... }
    }
}
