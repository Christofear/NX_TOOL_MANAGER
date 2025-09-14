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
        public ObservableCollection<DatDocumentRef> FilteredMainLibraries { get; } = new();

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
            Libraries.CollectionChanged += OnLibrariesChanged;

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

        private void OnLibrariesChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Clear the filtered list
            FilteredMainLibraries.Clear();

            // Re-populate it with only the items we want to show
            var filteredItems = Libraries.Where(doc =>
                doc.Kind == FileKind.Tools ||
                doc.Kind == FileKind.Holders ||
                doc.Kind == FileKind.Shanks);

            foreach (var item in filteredItems)
            {
                FilteredMainLibraries.Add(item);
            }
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

            if (kind == FileKind.Tools)
            {
                CategoryService.LoadMappingFromDefFile();
                foreach (var toolClass in doc.Classes)
                {
                    toolClass.UIName = CategoryService.GetUINameForClass(toolClass.Name);
                }
                added.Children = CategoryService.GroupClassesIntoCategories(doc.Classes);
            }
            else if (kind == FileKind.Holders || kind == FileKind.Shanks)
            {
                string indexClassName = (kind == FileKind.Holders) ? "HOLDER_INDEX" : "SHANK_INDEX";
                var indexClass = doc.Classes.FirstOrDefault(c => c.Name.Equals(indexClassName, StringComparison.OrdinalIgnoreCase));
                added.Children = (indexClass != null) ? new List<DatClass> { indexClass } : new List<DatClass>();
            }
            else
            {
                added.Children = doc.Classes;
            }

            Libraries.Add(added);
            SelectedDocument = added;
            SelectedClass = GetFirstClass(added.Children);
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
                SelectedClass = GetFirstClass(nextDoc?.Children);
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

        // FIX: Added the segmentedToolsPath parameter to this method.
        public void ApplySelection(string toolsPath, string holdersPath, string shanksPath, string trackpointsPath, string segmentedToolsPath)
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

            // FIX: Added the logic to load/unload the segmented tools file.
            var currentSegmentedTools = Libraries.FirstOrDefault(x => x.Kind == FileKind.SegmentedTools)?.FullPath;
            if (string.IsNullOrEmpty(segmentedToolsPath) && !string.IsNullOrEmpty(currentSegmentedTools)) Unload(FileKind.SegmentedTools);
            else if (!string.IsNullOrEmpty(segmentedToolsPath)) Load(segmentedToolsPath);

            if (SelectedDocument == null && Libraries.Any())
            {
                var firstDoc = Libraries.First();
                SelectedDocument = firstDoc;
                SelectedClass = GetFirstClass(firstDoc.Children);
            }
        }

        private DatClass GetFirstClass(IEnumerable children)
        {
            if (children == null) return null;
            foreach (var item in children)
            {
                if (item is CategoryNode node)
                {
                    var found = GetFirstClass(node.Classes);
                    if (found != null) return found;
                }
                else if (item is DatClass dc)
                {
                    return dc;
                }
            }
            return null;
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

