using Microsoft.Win32;
using NX_TOOL_MANAGER.Helpers;
using NX_TOOL_MANAGER.Models;
using NX_TOOL_MANAGER.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace NX_TOOL_MANAGER.Services
{
    public class LibraryManager : INotifyPropertyChanged
    {
        public static LibraryManager Instance { get; } = new LibraryManager();
        public ObservableCollection<DatDocumentRef> Libraries { get; } = new();

        private DatDocumentRef _selectedDocument;
        public DatDocumentRef SelectedDocument
        {
            get => _selectedDocument;
            set { if (_selectedDocument == value) return; _selectedDocument = value; Raise(); Raise(nameof(SelectedDocumentUnits)); }
        }

        public string SelectedDocumentUnits => SelectedDocument?.Units ?? "N/A";

        private DatClass _selectedClass;
        public DatClass SelectedClass
        {
            get => _selectedClass;
            set { if (_selectedClass == value) return; _selectedClass = value; Raise(); }
        }

        private LibraryManager()
        {
            UnloadLibraryCommand = new RelayCommand<DatDocumentRef>(doc => { if (doc != null) Unload(doc.Kind); });

            SaveLibraryCommand = new RelayCommand<DatDocumentRef>(doc =>
            {
                if (doc == null || string.IsNullOrEmpty(doc.FullPath)) return;
                try
                {
                    DatWriter.Write(doc.FullPath, doc.Document);
                    doc.ClearDirty();
                    MessageBox.Show($"Successfully saved '{doc.FileName}'.", "Save Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not save file.\nError: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });

            SaveAsLibraryCommand = new RelayCommand<DatDocumentRef>(doc =>
            {
                if (doc == null) return;
                var dlg = new SaveFileDialog { FileName = doc.FileName, Filter = "NX ASCII Tool DB (*.dat)|*.dat" };
                if (dlg.ShowDialog() == true)
                {
                    try
                    {
                        DatWriter.Write(dlg.FileName, doc.Document);
                        doc.FullPath = dlg.FileName;
                        doc.FileName = Path.GetFileName(dlg.FileName);
                        doc.ClearDirty();
                        MessageBox.Show($"Successfully saved to '{Path.GetFileName(dlg.FileName)}'.", "Save As Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Could not save file.\nError: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            });
        }

        public ICommand UnloadLibraryCommand { get; }
        public ICommand SaveLibraryCommand { get; }
        public ICommand SaveAsLibraryCommand { get; }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        public void Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

            bool isReadOnly = false;
            try
            {
                var fileInfo = new FileInfo(path);
                isReadOnly = fileInfo.IsReadOnly;
            }
            catch (Exception) { isReadOnly = true; }

            var kind = DatParsers.DetectKind(path);
            var lines = File.ReadLines(path);
            var doc = DatParsers.Parse(lines, kind);

            var existing = Libraries.FirstOrDefault(x => x.Kind == kind);
            if (existing != null) Unload(existing.Kind);

            var added = new DatDocumentRef
            {
                Kind = kind,
                FullPath = path,
                FileName = Path.GetFileName(path),
                Document = doc,
                Units = doc.Units,
                IsReadOnly = isReadOnly
            };

            added.Document.ParentRef = added;

            // --- THIS IS THE NEW, SMARTER LOGIC ---
            if (kind == FileKind.Tools)
            {
                // For tools, group the classes into categories.
                added.Children = CategoryService.GroupClassesIntoCategories(doc.Classes);
            }
            else if (kind == FileKind.Holders || kind == FileKind.Shanks)
            {
                // For holders and shanks, we ONLY show the INDEX class in the tree.
                string indexClassName = (kind == FileKind.Holders) ? "HOLDER_INDEX" : "SHANK_INDEX";
                var indexClass = doc.Classes.FirstOrDefault(c => c.Name.Equals(indexClassName, StringComparison.OrdinalIgnoreCase));

                // The tree will now only have one node under the holder/shank file.
                added.Children = (indexClass != null) ? new List<DatClass> { indexClass } : new List<DatClass>();
            }
            else
            {
                // For other types (like Trackpoints), show the flat list of classes.
                added.Children = doc.Classes;
            }

            Libraries.Add(added);
            SelectedDocument = added;

            if (added.Children is List<CategoryNode> categories)
            {
                SelectedClass = categories.FirstOrDefault()?.Classes.FirstOrDefault();
            }
            else if (added.Children is IEnumerable<DatClass> classes)
            {
                SelectedClass = classes.FirstOrDefault();
            }
        }

        public bool Unload(FileKind kind)
        {
            var existing = Libraries.FirstOrDefault(x => x.Kind == kind);
            if (existing == null) return true;

            if (existing.IsDirty)
            {
                var result = MessageBox.Show(
                    $"The file '{existing.FileName}' has unsaved changes.\n\nDo you want to save the changes?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    if (SaveLibraryCommand.CanExecute(existing))
                    {
                        SaveLibraryCommand.Execute(existing);
                    }
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    return false;
                }
            }

            bool wasSelectedDoc = (SelectedDocument == existing);
            Libraries.Remove(existing);
            if (wasSelectedDoc)
            {
                var nextDoc = Libraries.FirstOrDefault();
                SelectedDocument = nextDoc;
                if (nextDoc?.Children is List<CategoryNode> categories) { SelectedClass = categories.FirstOrDefault()?.Classes.FirstOrDefault(); }
                else if (nextDoc?.Children is IEnumerable<DatClass> classes) { SelectedClass = classes.FirstOrDefault(); }
            }
            else if (SelectedClass != null && ReferenceEquals(existing, FindDocForClass(SelectedClass)))
            {
                SelectedClass = null;
            }
            return true;
        }

        public void UnloadAll()
        {
            var kindsToUnload = Libraries.Select(l => l.Kind).ToList();
            foreach (var kind in kindsToUnload)
            {
                if (!Unload(kind))
                {
                    return;
                }
            }
        }

        public void ApplySelection(string toolsPath, string holdersPath, string shanksPath, string trackpointsPath)
        {
            var currentTools = Libraries.FirstOrDefault(x => x.Kind == FileKind.Tools)?.FullPath;
            if (string.IsNullOrEmpty(toolsPath) && !string.IsNullOrEmpty(currentTools)) Unload(FileKind.Tools);
            else if (!string.IsNullOrEmpty(toolsPath)) Load(toolsPath);

            var currentHolders = Libraries.FirstOrDefault(x => x.Kind == FileKind.Holders)?.FullPath;
            if (string.IsNullOrEmpty(holdersPath) && !string.IsNullOrEmpty(currentHolders)) Unload(FileKind.Holders);
            else if (!string.IsNullOrEmpty(holdersPath)) Load(holdersPath);

            var currentShanks = Libraries.FirstOrDefault(x => x.Kind == FileKind.Shanks)?.FullPath;
            if (string.IsNullOrEmpty(shanksPath) && !string.IsNullOrEmpty(currentShanks)) Unload(FileKind.Shanks);
            else if (!string.IsNullOrEmpty(shanksPath)) Load(shanksPath);

            var currentTrackpoints = Libraries.FirstOrDefault(x => x.Kind == FileKind.Trackpoints)?.FullPath;
            if (string.IsNullOrEmpty(trackpointsPath) && !string.IsNullOrEmpty(currentTrackpoints)) Unload(FileKind.Trackpoints);
            else if (!string.IsNullOrEmpty(trackpointsPath)) Load(trackpointsPath);

            if (SelectedDocument == null && Libraries.Any())
            {
                var firstDoc = Libraries.First();
                SelectedDocument = firstDoc;
                if (firstDoc.Children is List<CategoryNode> categories) { SelectedClass = categories.FirstOrDefault()?.Classes.FirstOrDefault(); }
                else if (firstDoc.Children is IEnumerable<DatClass> classes) { SelectedClass = classes.FirstOrDefault(); }
            }
        }

        private DatDocumentRef FindDocForClass(DatClass cls)
        {
            if (cls == null) return null;
            return Libraries.FirstOrDefault(d => d.Document?.Classes?.Contains(cls) == true);
        }

        public void AddLog(LogType type, string message)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.AddLogEntry(type, message);
            }
        }
    }
}

