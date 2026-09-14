using Avalonia.Controls;

namespace ZipSelect.Desktop;

public partial class OptionsWindow : Window
{
    public ExtractionOptions? SelectedOptions { get; private set; }
    public OptionsWindow() : this(new ExtractionOptions()) { }
    public OptionsWindow(ExtractionOptions options)
    {
        InitializeComponent();
        foreach (var toggle in new Avalonia.Controls.Primitives.ToggleButton[] { Together, PerZip, KeepFolders, FilesOnly, SkipExisting, RenameExisting, DeepSearch, DeepContents })
            toggle.IsCheckedChanged += (_, _) => Refresh();
        Populate(options);
        Reset.Click += (_, _) => Populate(new());
        CancelOptions.Click += (_, _) => Close(null);
        SaveOptions.Click += (_, _) => { SelectedOptions = ReadOptions(); Close(SelectedOptions); };
        Opened += (_, _) => { if (Screens.ScreenFromWindow(this) is { } screen) MaxHeight = screen.WorkingArea.Height / screen.Scaling; };
    }
    public ExtractionOptions ReadOptions() => new() {
        FolderPerArchive = PerZip.IsChecked == true, PreserveFolders = KeepFolders.IsChecked == true,
        RenameConflicts = RenameExisting.IsChecked == true, SearchSubfolders = DeepSearch.IsChecked == true,
        IncludeArchiveSubfolders = DeepContents.IsChecked == true
    };
    void Populate(ExtractionOptions o)
    {
        Together.IsChecked = !o.FolderPerArchive; PerZip.IsChecked = o.FolderPerArchive;
        KeepFolders.IsChecked = o.PreserveFolders; FilesOnly.IsChecked = !o.PreserveFolders;
        RenameExisting.IsChecked = o.RenameConflicts; SkipExisting.IsChecked = !o.RenameConflicts;
        DeepSearch.IsChecked = o.SearchSubfolders; DeepContents.IsChecked = o.IncludeArchiveSubfolders;
        Refresh();
    }
    void Refresh()
    {
        var o = ReadOptions();
        Example.Text = Texts.Format("MusicZipContainsHelloMpDestinationHello",
            o.IncludeArchiveSubfolders ? "album/" : "",
            o.FolderPerArchive ? Texts.Get("Music") : "",
            o.PreserveFolders && o.IncludeArchiveSubfolders ? "album/" : "");
        KeepFolders.IsEnabled = FilesOnly.IsEnabled = o.IncludeArchiveSubfolders;
    }
}
