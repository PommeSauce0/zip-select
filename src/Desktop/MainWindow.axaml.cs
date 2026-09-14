using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using System.Diagnostics;

namespace ZipSelect.Desktop;

// Logo généré par IA (OpenAI image_gen) : assets/branding/logo.png.
// La provenance et le prompt sont conservés dans assets/branding/Generation.md.
public partial class MainWindow : Window
{
    readonly string settingsPath;
    readonly Settings settings;
    CancellationTokenSource? cancellation;
    Update? lastUpdate;
    public bool IsRunning { get; private set; }
    public MainWindow() : this(Settings.DefaultPath) { }
    public MainWindow(string path)
    {
        InitializeComponent(); settingsPath = path; settings = Settings.Load(path);
        Source.Text = settings.Source; Destination.Text = settings.Destination; Extensions.Text = settings.Extensions;
        ApplyLanguage(settings.Language, false);
        Log.Text = Texts.Get("ChooseYourFoldersAndFileExtensionsTo");
        LanguageFrench.Click += (_, _) => ApplyLanguage("fr");
        LanguageEnglish.Click += (_, _) => ApplyLanguage("en");
        BrowseSource.Click += async (_, _) => await Browse(Source);
        BrowseDestination.Click += async (_, _) => await Browse(Destination);
        MenuSource.Click += async (_, _) => await Browse(Source);
        MenuDestination.Click += async (_, _) => await Browse(Destination);
        Start.Click += async (_, _) => await RunExtractionAsync();
        Cancel.Click += (_, _) => { cancellation?.Cancel(); Cancel.IsEnabled = false; Status.Text = Texts.Get("Stopping"); };
        ThemeLight.Click += (_, _) => { ApplyTheme(false); Save(); };
        ThemeDark.Click += (_, _) => { ApplyTheme(true); Save(); };
        MenuClear.Click += (_, _) => Log.Text = "";
        MenuQuit.Click += (_, _) => Close();
        ConfigureOptions.Click += async (_, _) => {
            var result = await new OptionsWindow(settings.Options).ShowDialog<ExtractionOptions?>(this);
            if (result != null) { settings.Options = result; UpdateSummary(); Save(); }
        };
        MenuOpen.Click += async (_, _) => {
            try {
                string folder = Path.GetFullPath(Destination.Text?.Trim().Trim('"') ?? "");
                if (!Directory.Exists(folder)) throw new IOException(Texts.Get("ThisFolderWillBeCreatedDuringExtraction"));
                if (OperatingSystem.IsLinux()) { var info = new ProcessStartInfo("xdg-open") { UseShellExecute = false }; info.ArgumentList.Add(folder); Process.Start(info); }
                else Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
            } catch (Exception e) { await Message(e.Message); }
        };
        MenuExport.Click += async (_, _) => {
            try {
                var target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = Texts.Get("SaveLog55"), SuggestedFileName = "extraction-zip.txt", DefaultExtension = "txt" });
                if (target != null) { await using var stream = await target.OpenWriteAsync(); using var writer = new StreamWriter(stream); await writer.WriteAsync(Log.Text); }
            } catch (Exception e) { await Message(e.Message); }
        };
        MenuHelp.Click += async (_, _) => await Message(Texts.Get("ChooseTheSourceAndDestinationFoldersEnter"), Texts.Get("UserGuide"));
        MenuAbout.Click += async (_, _) => {
            var version = typeof(MainWindow).Assembly.GetName().Version?.ToString(3);
            await Message(Texts.Format("ZipSelectWindowsExperimentalLinuxSupportLogo", version), Texts.Get("About"));
        };
        Closing += (_, e) => { if (IsRunning) { e.Cancel = true; cancellation?.Cancel(); Status.Text = Texts.Get("StoppingYouCanCloseTheWindowOnce"); } else Save(); };
    }
    public void ApplyTheme(bool dark)
    {
        settings.Dark = dark;
        Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        ThemeDark.IsChecked = dark; ThemeLight.IsChecked = !dark; ThemeLabel.Text = dark ? Texts.Get("DarkTheme") : Texts.Get("LightTheme");
    }
    public void ApplyLanguage(string language, bool save = true)
    {
        // A run keeps one language so its log remains consistent.
        if (IsRunning) return;
        Texts.SetLanguage(language);
        settings.Language = Texts.Language;
        foreach (var key in Texts.Entries.Keys) Application.Current!.Resources[key] = Texts.Get(key);
        LanguageFrench.IsChecked = Texts.Language == "fr";
        LanguageEnglish.IsChecked = Texts.Language == "en";
        ApplyTheme(settings.Dark);
        UpdateSummary();
        Counters.Text = Texts.Format("ZipsFoundExtractedAlreadyPresentToReview105", lastUpdate?.Archives ?? 0,
            lastUpdate?.Extracted ?? 0, lastUpdate?.Skipped ?? 0, lastUpdate?.Issues ?? 0);
        Status.Text = Texts.Get("Ready");
        var welcome = Texts.Entries["ChooseYourFoldersAndFileExtensionsTo"];
        if (Log.Text == welcome.fr || Log.Text == welcome.en) Log.Text = Texts.Get("ChooseYourFoldersAndFileExtensionsTo");
        if (save) Save();
    }
    void Save()
    {
        settings.Source = Source.Text ?? ""; settings.Destination = Destination.Text ?? ""; settings.Extensions = Extensions.Text ?? "";
        try { settings.Save(settingsPath); } catch (Exception e) { Append(Texts.Get("CouldNotSavePreferences") + e.Message); }
    }
    void UpdateSummary() => OptionsSummary.Text =
        (settings.Options.SearchSubfolders ? Texts.Get("RecursiveSearch") : Texts.Get("SourceFolderOnly")) + " · " +
        (settings.Options.IncludeArchiveSubfolders ? Texts.Get("EntireZip") : Texts.Get("ZipRootOnly")) + " · " +
        (settings.Options.PreserveFolders ? Texts.Get("KeepFolders") : Texts.Get("FilesOnly")) + " · " +
        (settings.Options.FolderPerArchive ? Texts.Get("OneFolderPerZip") : Texts.Get("SharedDestination")) + " · " +
        (settings.Options.RenameConflicts ? Texts.Get("RenameConflicts") : Texts.Get("ReportConflicts"));
    async Task Browse(TextBox field)
    {
        try {
            var selected = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = Texts.Get("ChooseAFolder"), AllowMultiple = false });
            if (selected.Count > 0) field.Text = selected[0].TryGetLocalPath() ?? field.Text;
        } catch (Exception e) { await Message(e.Message); }
    }
    void Append(string text)
    {
        Log.Text = (Log.Text ?? "") + text + Environment.NewLine;
        if (Log.Text.Length > 100000) Log.Text = Log.Text[^80000..];
        Log.CaretIndex = Log.Text.Length;
    }
    public async Task RunExtractionAsync()
    {
        if (IsRunning) return;
        string source = Source.Text?.Trim().Trim('"') ?? "";
        string destination = Destination.Text?.Trim().Trim('"') ?? "";
        string extensions = Extensions.Text ?? "";
        try { Engine.ParseExtensions(extensions); if (!Directory.Exists(source) || string.IsNullOrWhiteSpace(destination)) throw new ArgumentException(Texts.Get("ChooseAnExistingSourceFolderAndA")); }
        catch (Exception e) { await Message(e.Message); return; }
        Save(); IsRunning = true; cancellation = new(); SetBusy(true); Log.Text = "";
        var options = Settings.Copy(settings.Options);
        try {
            await Task.Run(() => Engine.Run(source, destination, extensions, options, cancellation.Token, u => {
                // Synchronous dispatch limits queued updates when processing thousands of small entries.
                Dispatcher.UIThread.Invoke(() => {
                    lastUpdate = u;
                    Counters.Text = Texts.Format("ZipsFoundExtractedAlreadyPresentToReview105", u.Archives, u.Extracted, u.Skipped, u.Issues);
                    Status.Text = u.Status; Progress.IsIndeterminate = u.Scanning;
                    Progress.Value = u.Archives == 0 ? 0 : 100.0 * u.Done / u.Archives;
                    if (u.Message != null) Append(u.Message);
                });
            }));
        } catch (OperationCanceledException) { Status.Text = Texts.Get("ExtractionStopped"); Append(Texts.Get("CompletedFilesHaveBeenKeptStartAgain")); }
        catch (Exception e) { Status.Text = Texts.Get("ExtractionInterrupted"); Append(Texts.Get("Error") + e.Message); }
        finally { IsRunning = false; cancellation.Dispose(); cancellation = null; SetBusy(false); Progress.IsIndeterminate = false; }
    }
    void SetBusy(bool busy)
    {
        foreach (var control in new Control[] { Source, Destination, Extensions, BrowseSource, BrowseDestination, Start, MenuSource, MenuDestination, OptionsMenu, LanguageMenu }) control.IsEnabled = !busy;
        Cancel.IsVisible = busy; Cancel.IsEnabled = busy;
    }
    async Task Message(string text, string title = "ZIP Select")
    {
        var close = new Button { Content = Texts.Get("Close"), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        var dialog = new Window { Title = title, Width = 520, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = new StackPanel { Margin = new Thickness(24), Spacing = 20, Children = { new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, close } } };
        close.Click += (_, _) => dialog.Close(); await dialog.ShowDialog(this);
    }
}
