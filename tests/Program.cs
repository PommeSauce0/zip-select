using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ZipSelect.Desktop;

namespace ZipSelect;
public partial class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        string root = Path.Combine(Path.GetTempPath(), "zip-select-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try {
            TestEngine(root);
            Texts.SetLanguage("en");
            TestEngine(Path.Combine(root, "english-engine"));
            try { Engine.ParseExtensions(""); throw new Exception("Filtre vide accepté"); }
            catch (ArgumentException e) { Assert(e.Message.StartsWith("Enter at least"), "Validation traduite en anglais"); }
            foreach (var entry in Texts.Entries.Values) {
                Assert(!string.IsNullOrWhiteSpace(entry.fr) && !string.IsNullOrWhiteSpace(entry.en), "Traduction complète");
                Assert(System.Text.CompositeFormat.Parse(entry.fr).MinimumArgumentCount == System.Text.CompositeFormat.Parse(entry.en).MinimumArgumentCount, "Paramètres de traduction cohérents");
            }
            Texts.SetLanguage("fr");
            string portableSource = Path.Combine(root, "portable-source");
            Directory.CreateDirectory(portableSource);
            using (var zip = System.IO.Compression.ZipFile.Open(Path.Combine(portableSource, "UPPER.ZIP"), System.IO.Compression.ZipArchiveMode.Create)) {
                Entry(zip, "album\\track.mp3", "music");
            }
            string portableOutput = Path.Combine(root, "portable-output");
            var portable = Engine.Run(portableSource, portableOutput, "mp3", new ExtractionOptions { PreserveFolders = false }, CancellationToken.None, _ => {});
            Assert(portable.Extracted == 1 && File.Exists(Path.Combine(portableOutput, "track.mp3")), "ZIP majuscule et séparateurs Windows sur chaque plateforme");
            if (OperatingSystem.IsLinux()) {
                string outside = Path.Combine(root, "outside"); Directory.CreateDirectory(outside);
                string link = Path.Combine(root, "linked-output"); Directory.CreateSymbolicLink(link, outside);
                bool rejected = false;
                try { Engine.Run(portableSource, link, "*", CancellationToken.None, _ => {}); } catch (IOException) { rejected = true; }
                Assert(rejected && Directory.GetFiles(outside).Length == 0, "Destination lien symbolique refusée");
            }
            AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
            string preferences = Path.Combine(root, "settings.json");
            var window = new MainWindow(preferences);
            window.Show();
            var output = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/tests");
            Directory.CreateDirectory(output);
            foreach (bool dark in new[] { false, true }) {
                window.ApplyTheme(dark);
                Dispatcher.UIThread.RunJobs();
                using (var frame = window.CaptureRenderedFrame()) frame!.Save(Path.Combine(output, dark ? "sombre.png" : "clair.png"));
                var options = new OptionsWindow(new ExtractionOptions());
                options.Show(window);
                options.FindControl<RadioButton>("PerZip")!.IsChecked = true;
                options.FindControl<RadioButton>("FilesOnly")!.IsChecked = true;
                options.FindControl<RadioButton>("RenameExisting")!.IsChecked = true;
                Assert(options.ReadOptions().FolderPerArchive && !options.ReadOptions().PreserveFolders && options.ReadOptions().RenameConflicts, "Choix des options");
                Dispatcher.UIThread.RunJobs();
                using (var frame = options.CaptureRenderedFrame()) frame!.Save(Path.Combine(output, dark ? "options-sombre.png" : "options-clair.png"));
                options.FindControl<Button>("SaveOptions")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(options.SelectedOptions?.RenameConflicts == true, "Enregistrement des options");
            }
            window.FindControl<MenuItem>("LanguageEnglish")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert(window.FindControl<Button>("Start")!.Content?.ToString() == "Extract", "Bouton traduit sans redémarrage");
            Assert(window.FindControl<TextBox>("Log")!.Text!.StartsWith("Choose your folders"), "Accueil traduit");
            using (var frame = window.CaptureRenderedFrame()) frame!.Save(Path.Combine(output, "english-dark.png"));
            var englishOptions = new OptionsWindow(new ExtractionOptions());
            englishOptions.Show(window);
            Dispatcher.UIThread.RunJobs();
            Assert(englishOptions.Title == "Extraction options" && englishOptions.FindControl<TextBlock>("Example")!.Text!.Contains("music.zip contains"), "Options et exemple traduits");
            using (var frame = englishOptions.CaptureRenderedFrame()) frame!.Save(Path.Combine(output, "english-options.png"));
            englishOptions.Close();
            window.FindControl<TextBox>("Source")!.Text = Path.Combine(root, "separate-source");
            window.FindControl<TextBox>("Destination")!.Text = Path.Combine(root, "ui-output");
            window.FindControl<TextBox>("Extensions")!.Text = ".mp3";
            var run = window.RunExtractionAsync();
            window.ApplyLanguage("fr");
            Assert(Texts.Language == "en" && !window.FindControl<MenuItem>("LanguageMenu")!.IsEnabled, "Langue stable pendant l’extraction");
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!run.IsCompleted && DateTime.UtcNow < deadline) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); }
            Assert(run.IsCompleted, "Extraction UI terminée"); run.GetAwaiter().GetResult();
            Assert(!window.IsRunning && File.Exists(Path.Combine(root, "ui-output", "album", "bonjour.mp3")), "Extraction depuis la fenêtre");
            Assert(window.FindControl<TextBox>("Log")!.Text!.Contains("[EXTRACTED] album/bonjour.mp3") && window.FindControl<TextBlock>("Status")!.Text == "Extraction complete", "Journal anglais et nom de fichier inchangé");
            window.Close();
            Assert(Settings.Load(preferences).Extensions == ".mp3" && Settings.Load(preferences).Dark && Settings.Load(preferences).Language == "en", "Préférences persistantes");
            var reopened = new MainWindow(preferences); reopened.Show(); Dispatcher.UIThread.RunJobs();
            Assert(reopened.FindControl<Button>("Start")!.Content?.ToString() == "Extract", "Langue restaurée au démarrage");
            reopened.ApplyLanguage("fr"); Dispatcher.UIThread.RunJobs();
            Assert(reopened.FindControl<Button>("Start")!.Content?.ToString() == "Extraire", "Retour au français");
            reopened.Close();
            string oldSettings = Path.Combine(root, "old-settings.json"); File.WriteAllText(oldSettings, "{\"Dark\":true}");
            Assert(Settings.Load(oldSettings).Language == "fr", "Anciennes préférences compatibles");
            Console.WriteLine("OK — moteur, extraction depuis l’interface, options, thèmes et préférences.");
            // Only remove the unique fixture directory created by this run.
            var temporaryRoot = Path.GetFullPath(Path.GetTempPath());
            if (!Path.GetFullPath(root).StartsWith(temporaryRoot, StringComparison.Ordinal) || !Path.GetFileName(root).StartsWith("zip-select-tests-", StringComparison.Ordinal))
                throw new InvalidOperationException("Dossier de test inattendu.");
            Directory.Delete(root, true);
            return 0;
        } catch (Exception e) { Console.Error.WriteLine(e); Console.Error.WriteLine("Données de diagnostic : " + root); return 1; }
    }
}
