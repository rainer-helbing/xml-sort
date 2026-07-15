using System.CommandLine;

namespace XmlSort {

    internal static class Program {
        private static async Task<int> Main(string[] args) {
            var fileArgument = new Argument<FileInfo>("path") {
                Description = "Pfad zu einer vorhandenen XML-Datei"
            };
            fileArgument.AcceptExistingOnly();

            var fileCommand = new Command("file", "Sortiert eine einzelne XML-Datei") { fileArgument };
            fileCommand.SetAction(result => {
                var file = result.GetValue(fileArgument)!;
                XmlSorter.SortFile(file);
            });

            var dirArgument = new Argument<DirectoryInfo>("path") {
                Description = "Pfad zu einem vorhandenen Verzeichnis mit XML-Dateien"
            };
            dirArgument.AcceptExistingOnly();

            var dirCommand = new Command("dir", "Sortiert alle XML-Dateien in einem Verzeichnis") { dirArgument };
            dirCommand.SetAction(result => {
                var dir = result.GetValue(dirArgument)!;
                XmlSorter.SortDirectory(dir);
            });

            var rootCommand = new RootCommand("XmlSort – Sortiert Attribute und Elemente in XML-Dateien") { fileCommand, dirCommand };

            var parseResult = rootCommand.Parse(args);
            return await parseResult.InvokeAsync();
        }
    }
}