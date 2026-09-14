using System;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace ZipSelect {
    public partial class Program {
        static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
        static void Entry(ZipArchive zip, string path, string text) {
            using (var writer = new StreamWriter(zip.CreateEntry(path).Open())) writer.Write(text);
        }
        static void TestEngine(string root) {
            string source = Path.Combine(root, "source", "nested");
            string output = Path.Combine(root, "output");
            Directory.CreateDirectory(source);
            using (var zip = ZipFile.Open(Path.Combine(source, "a.zip"), ZipArchiveMode.Create)) {
                Entry(zip, "a/bonjour.mp3", "bonjour");
                Entry(zip, "a/aurevoir.MP3", "aurevoir");
                Entry(zip, "a/docs/Démo.pdf", "document");
                Entry(zip, "b/bonjour.mp3", "autre");
                Entry(zip, "exclu/image.jpg", "image");
                Entry(zip, "sans-extension", "notice");
                zip.CreateEntry("vide/");
            }
            Action<Update> silent = u => {};
            var filters = Engine.ParseExtensions("mp3; .PDF, *.mp3");
            Assert(filters.Count == 2 && filters.Contains(".pdf"), "Normalisation des extensions");
            foreach (string bad in new[] { "", " ; ", "../mp3", ".", "mp?", "pdf/exe" }) {
                bool rejected = false;
                try { Engine.ParseExtensions(bad); } catch (ArgumentException) { rejected = true; }
                Assert(rejected, "Filtre invalide accepté : " + bad);
            }
            var first = Engine.Run(Path.Combine(root, "source"), output, "mp3, .PDF", CancellationToken.None, silent);
            Assert(first.Extracted == 4 && first.Archives == 1 && first.Issues == 0, "Plusieurs fichiers/extensions par ZIP");
            Assert(File.ReadAllText(Path.Combine(output, "a", "bonjour.mp3")) == "bonjour", "Arborescence a conservée");
            Assert(File.ReadAllText(Path.Combine(output, "b", "bonjour.mp3")) == "autre", "Homonymes dans des dossiers différents");
            Assert(File.Exists(Path.Combine(output, "a", "docs", "Démo.pdf")), "Arborescence profonde et accents");
            Assert(!Directory.Exists(Path.Combine(output, "exclu")) && !Directory.Exists(Path.Combine(output, "vide")), "Dossiers sans correspondance non créés");
            var second = Engine.Run(source, output, ".mp3 .pdf", CancellationToken.None, silent);
            Assert(second.Skipped == 4 && second.Extracted == 0, "Reprise sans doublons");
            string conflictPath = Path.Combine(output, "a", "bonjour.mp3");
            File.WriteAllText(conflictPath, "contenu différent");
            var conflict = Engine.Run(source, output, ".mp3 .pdf", CancellationToken.None, silent);
            Assert(conflict.Issues == 1 && File.ReadAllText(conflictPath) == "contenu différent", "Conflit sans écrasement");
            string allOutput = Path.Combine(root, "all");
            var all = Engine.Run(source, allOutput, "*", CancellationToken.None, silent);
            Assert(all.Extracted == 6 && File.Exists(Path.Combine(allOutput, "sans-extension")), "Tous les fichiers, y compris sans extension");
            var none = Engine.Run(source, Path.Combine(root, "none"), ".wav", CancellationToken.None, silent);
            Assert(none.Extracted == 0 && none.Issues == 0, "Aucun fichier correspondant");
            var directOnly = Engine.Run(Path.Combine(root, "source"), Path.Combine(root, "direct"), "*", new ExtractionOptions { SearchSubfolders = false }, CancellationToken.None, silent);
            Assert(directOnly.Archives == 0, "Recherche des ZIP sans sous-dossiers");
            string rootOnlyOutput = Path.Combine(root, "root-only");
            var rootOnly = Engine.Run(source, rootOnlyOutput, "*", new ExtractionOptions { IncludeArchiveSubfolders = false }, CancellationToken.None, silent);
            Assert(rootOnly.Extracted == 1 && File.Exists(Path.Combine(rootOnlyOutput, "sans-extension")) && Directory.GetDirectories(rootOnlyOutput).Length == 0, "Racine du ZIP uniquement");
            string flatOutput = Path.Combine(root, "flat");
            var flat = Engine.Run(source, flatOutput, ".mp3 .pdf", new ExtractionOptions { PreserveFolders = false }, CancellationToken.None, silent);
            Assert(flat.Extracted == 3 && flat.Issues == 1 && Directory.GetDirectories(flatOutput).Length == 0 && File.ReadAllText(Path.Combine(flatOutput, "bonjour.mp3")) == "bonjour", "Mode sans dossiers et collision sans écrasement");
            string renamedOutput = Path.Combine(root, "renamed");
            var renameOptions = new ExtractionOptions { PreserveFolders = false, RenameConflicts = true };
            var renamed = Engine.Run(source, renamedOutput, ".mp3 .pdf", renameOptions, CancellationToken.None, silent);
            Assert(renamed.Extracted == 4 && renamed.Renamed == 1 && renamed.Issues == 0 && File.ReadAllText(Path.Combine(renamedOutput, "bonjour (2).mp3")) == "autre", "Renommage automatique");
            var resumed = Engine.Run(source, renamedOutput, ".mp3 .pdf", renameOptions, CancellationToken.None, silent);
            Assert(resumed.Skipped == 4 && resumed.Extracted == 0 && Directory.GetFiles(renamedOutput).Length == 4, "Reprise des fichiers renommés sans copies supplémentaires");
            File.WriteAllText(Path.Combine(renamedOutput, "bonjour (2).mp3"), "fichier différent à conserver");
            var numbered = Engine.Run(source, renamedOutput, ".mp3 .pdf", renameOptions, CancellationToken.None, silent);
            Assert(numbered.Renamed == 1 && File.ReadAllText(Path.Combine(renamedOutput, "bonjour (3).mp3")) == "autre", "Suffixe suivant disponible");
            string separatedSource = Path.Combine(root, "separate-source");
            Directory.CreateDirectory(Path.Combine(separatedSource, "nested"));
            using (var zip = ZipFile.Open(Path.Combine(separatedSource, "musique.zip"), ZipArchiveMode.Create)) { Entry(zip, "album/bonjour.mp3", "premier"); }
            using (var zip = ZipFile.Open(Path.Combine(separatedSource, "nested", "musique.zip"), ZipArchiveMode.Create)) { Entry(zip, "album/bonjour.mp3", "second"); }
            string separatedOutput = Path.Combine(root, "separated");
            var perZipOptions = new ExtractionOptions { FolderPerArchive = true };
            var separated = Engine.Run(separatedSource, separatedOutput, ".mp3", perZipOptions, CancellationToken.None, silent);
            Assert(separated.Extracted == 2 && separated.Issues == 0 && File.ReadAllText(Path.Combine(separatedOutput, "musique", "album", "bonjour.mp3")) == "premier" && File.ReadAllText(Path.Combine(separatedOutput, "musique (2)", "album", "bonjour.mp3")) == "second", "Dossier par ZIP et archives homonymes");
            var separatedAgain = Engine.Run(separatedSource, separatedOutput, ".mp3", perZipOptions, CancellationToken.None, silent);
            Assert(separatedAgain.Skipped == 2 && separatedAgain.Extracted == 0, "Reprise avec un dossier par ZIP");
            perZipOptions.PreserveFolders = false;
            string separatedFlat = Path.Combine(root, "separated-flat");
            var separatedFiles = Engine.Run(separatedSource, separatedFlat, ".mp3", perZipOptions, CancellationToken.None, silent);
            Assert(separatedFiles.Extracted == 2 && File.Exists(Path.Combine(separatedFlat, "musique", "bonjour.mp3")) && !Directory.Exists(Path.Combine(separatedFlat, "musique", "album")), "Un dossier par ZIP avec fichiers seuls");
            using (var zip = ZipFile.Open(Path.Combine(source, "unsafe.zip"), ZipArchiveMode.Create)) {
                Entry(zip, "../escape.mp3", "bad");
                Entry(zip, "/absolute.mp3", "bad");
                Entry(zip, "C:/outside.mp3", "bad");
                Entry(zip, "a/../../outside.mp3", "bad");
                Entry(zip, "a/NUL.mp3", "bad");
                Entry(zip, "valid/ok.mp3", "ok");
            }
            string guarded = Path.Combine(root, "guarded");
            var safe = Engine.Run(source, guarded, ".mp3", CancellationToken.None, silent);
            Assert(safe.Issues == (OperatingSystem.IsWindows() ? 5 : 4) && safe.Extracted == (OperatingSystem.IsWindows() ? 4 : 5) && !File.Exists(Path.Combine(root, "escape.mp3")), "Chemins malveillants rejetés, extraction valide poursuivie");
            File.WriteAllText(Path.Combine(source, "broken.zip"), "not-a-zip");
            var corrupt = Engine.Run(source, Path.Combine(root, "corrupt"), ".pdf", CancellationToken.None, silent);
            Assert(corrupt.Issues == 1 && corrupt.Extracted == 1, "ZIP illisible sans interrompre les autres");
            using (var cts = new CancellationTokenSource()) {
                bool cancelled = false;
                string cancelOutput = Path.Combine(root, "cancel");
                try {
                    Engine.Run(source, cancelOutput, "*", cts.Token, u => { if (u.Status != null && u.Status.StartsWith(Texts.Get("Extracting"))) cts.Cancel(); });
                } catch (OperationCanceledException) { cancelled = true; }
                Assert(cancelled && Directory.GetFiles(cancelOutput, "*", SearchOption.AllDirectories).Length == 0, "Annulation et suppression du fichier temporaire");
            }
        }
    }
}
