using System.CommandLine;

namespace XmlSort {

    internal static class Program {
        private static async Task<int> Main(string[] args) {
            var debugOption = new Option<bool>("--debug") { Description = "Sortiert ohne die Dateien zurückzuschreiben. Vorgabe: false" };
            var removeOption = new Option<string?>("--remove") { Description = "Semikolon-getrennte Liste von XPath-Ausdrücken; passende Elemente werden entfernt. Bsp: //IAttribute[ident='crb_Display']" };
            var ignoreNamespacesOption = new Option<bool>("--ignoreSpaces") { Description = "Legt fest, dass Namespaces ignoriert werden.", DefaultValueFactory = (_) => true };

            var fileArgument = new Argument<FileInfo>("path") {
                Description = "Pfad zu einer vorhandenen XML-Datei"
            };
            fileArgument.AcceptExistingOnly();

            var fileCommand = new Command("file", "Sortiert eine einzelne XML-Datei") { fileArgument };
            fileCommand.SetAction(result => {
                var file = result.GetValue(fileArgument)!;

                XmlSorterOptions opt = new() {
                    Debug = result.GetValue(debugOption),
                    RemoveExpressions = result.GetValue(removeOption)?.Split(';') ?? Array.Empty<string>(),
                    IgnoreNameSpaces = result.GetValue(ignoreNamespacesOption),
                };
                XmlSorter.SortFile(file, opt);
            });

            var dirArgument = new Argument<DirectoryInfo>("path") {
                Description = "Pfad zu einem vorhandenen Verzeichnis mit XML-Dateien"
            };
            dirArgument.AcceptExistingOnly();

            var dirCommand = new Command("dir", "Sortiert alle XML-Dateien in einem Verzeichnis") { dirArgument };
            dirCommand.SetAction(result => {
                var dir = result.GetValue(dirArgument)!;
                XmlSorterOptions opt = new() {
                    Debug = result.GetValue(debugOption),
                    RemoveExpressions = result.GetValue(removeOption)?.Split(';') ?? Array.Empty<string>(),
                    IgnoreNameSpaces = result.GetValue(ignoreNamespacesOption),
                };
                XmlSorter.SortDirectory(dir, opt);
            });

            var rootCommand = new RootCommand("XmlSort – Sortiert Attribute und Elemente in XML-Dateien") { fileCommand, dirCommand };
            rootCommand.Add(debugOption);
            rootCommand.Add(removeOption);
            rootCommand.Add(ignoreNamespacesOption);

            var parseResult = rootCommand.Parse(args);
            return await parseResult.InvokeAsync();
        }
    }
}