using System.Diagnostics;
using System.Xml.Linq;

namespace XmlSort {

    internal static class XmlSorter {
        internal static async Task SortFileAsync(FileInfo file, CancellationToken ct) {
            Console.WriteLine($"Verarbeite: {file.FullName}");

            var sw = Stopwatch.StartNew();
            var content = await File.ReadAllTextAsync(file.FullName, ct);
            var doc = XDocument.Parse(content);
            Console.WriteLine($"  gelesen/geparst: {sw.ElapsedMilliseconds} ms");

            sw.Restart();
            if (doc.Root is not null)
                await SortAllElementsAsync(doc.Root, ct);
            Console.WriteLine($"  sortiert:        {sw.ElapsedMilliseconds} ms");

            sw.Restart();
            await File.WriteAllTextAsync(file.FullName, doc.ToString(), ct);
            Console.WriteLine($"  geschrieben:     {sw.ElapsedMilliseconds} ms");

            Console.WriteLine($"Fertig:     {file.FullName}");
        }

        internal static async Task SortDirectoryAsync(DirectoryInfo dir, CancellationToken ct) {
            var xmlFiles = dir.GetFiles("*.xml", SearchOption.TopDirectoryOnly);

            if (xmlFiles.Length == 0) {
                Console.WriteLine($"Keine XML-Dateien in '{dir.FullName}' gefunden.");
                return;
            }

            foreach (var file in xmlFiles)
                await SortFileAsync(file, ct);
        }

        private static Task SortAllElementsAsync(XElement root, CancellationToken ct) {
            var sw = Stopwatch.StartNew();
            var levels = new List<List<XElement>>();
            BuildLevels(root, 0, levels);
            Console.WriteLine($"      levels gebaut:   {sw.ElapsedMilliseconds} ms ({levels.Count} Ebenen)");

            var options = new ParallelOptions {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = ct
            };

            // Tiefste Ebene zuerst: Kinder sind fertig, bevor Eltern umordnen.
            for (int depth = levels.Count - 1; depth >= 0; depth--)
                Parallel.ForEach(levels[depth], options, SortSingleElement);

            return Task.CompletedTask;
        }

        private static void BuildLevels(XElement element, int depth, List<List<XElement>> levels) {
            if (levels.Count == depth)
                levels.Add(new List<XElement>());
            levels[depth].Add(element);

            foreach (var child in element.Elements())
                BuildLevels(child, depth + 1, levels);
        }

        private static void SortSingleElement(XElement element) {
            SortAttributes(element);

            var children = element.Elements().ToList();
            if (children.Count == 0)
                return;

            foreach (var child in children)
                child.Remove();

            foreach (var child in children
                .OrderBy(e => e.Name.NamespaceName)
                .ThenBy(e => e.Name.LocalName)
                .ThenBy(AttributesSortKey))
                element.Add(child);
        }

        private static void SortAttributes(XElement element) {
            var sorted = element.Attributes()
                .OrderBy(a => a.Name.NamespaceName)
                .ThenBy(a => a.Name.LocalName)
                .ThenBy(a => a.Value)
                .ToList();

            element.RemoveAttributes();

            foreach (var attr in sorted)
                element.Add(attr);
        }

        private static string AttributesSortKey(XElement element) =>
            string.Join("\0", element.Attributes()
                .Select(a => $"{a.Name.NamespaceName}{a.Name.LocalName}={a.Value}"));
    }
}
