using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace XmlSort {

    internal static class XmlSorter {
        internal static void SortFile(FileInfo file, XmlSorterOptions options) {
            Console.WriteLine($"Verarbeite: {file.FullName}");
#if DEBUG
            var sw = Stopwatch.StartNew();
#endif
            XDocument doc;
            var readerSettings = new XmlReaderSettings { IgnoreWhitespace = true };
            using (var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read)) {
                using (var reader = XmlReader.Create(stream, readerSettings)) {
                    doc = XDocument.Load(reader);
                }
            }
#if DEBUG
            Console.WriteLine($"  gelesen/geparst: {sw.ElapsedMilliseconds} ms");

            sw.Restart();
#endif
            if (options.IgnoreNameSpaces && doc.Root is not null) {
                doc = new XDocument(StripNamespaces(doc.Root));
#if DEBUG
                Console.WriteLine($"  Namespaces entfernt: {sw.ElapsedMilliseconds} ms");

                sw.Restart();
#endif
            }

            Remove(doc, options);

#if DEBUG
            if (options.RemoveExpressions.Length != 0) {
                Console.WriteLine($"  Elemente entfernt: {sw.ElapsedMilliseconds} ms");

                sw.Restart();
            }
#endif

            if (doc.Root is not null)
                SortAllElements(doc.Root);
#if DEBUG
            Console.WriteLine($"  sortiert:        {sw.ElapsedMilliseconds} ms");

            sw.Restart();
#endif
            if (options.Debug) {
                Console.WriteLine($"  '--debug' gesetzt => nicht geschrieben");
            } else {
                var settings = new XmlWriterSettings {
                    Indent = true,
                    IndentChars = string.Empty,
                    OmitXmlDeclaration = true,
                    Encoding = new UTF8Encoding(false)
                };
                using (var stream = new FileStream(file.FullName, FileMode.Create, FileAccess.Write)) {
                    using (var writer = XmlWriter.Create(stream, settings)) {
                        doc.Save(writer);
                    }
                }
#if DEBUG
                Console.WriteLine($"  geschrieben:     {sw.ElapsedMilliseconds} ms");
#endif
            }

            Console.WriteLine($"Fertig:     {file.FullName}");
        }

        internal static void SortDirectory(DirectoryInfo dir, XmlSorterOptions options) {
            var xmlFiles = dir.GetFiles("*.xml", SearchOption.TopDirectoryOnly);

            if (xmlFiles.Length == 0) {
                Console.WriteLine($"Keine XML-Dateien in '{dir.FullName}' gefunden.");
                return;
            }

            foreach (var file in xmlFiles)
                SortFile(file, options);
        }

        private static XElement StripNamespaces(XElement element) =>
            new XElement(
                element.Name.LocalName,
                element.Attributes()
                    .Where(a => !a.IsNamespaceDeclaration)
                    .Select(a => new XAttribute(a.Name.LocalName, a.Value)),
                element.Nodes().Select(n => n is XElement child ? StripNamespaces(child) : n)
            );

        private static void Remove(XDocument doc, XmlSorterOptions options) {
            foreach (var xpath in options.RemoveExpressions) {
                List<XElement> nodes = doc.XPathSelectElements(xpath).ToList();
                if (options.Debug) {
                    Console.WriteLine($"  {nodes.Count} Ergebnisse für '{xpath}' gefunden");
                }
                foreach (var node in nodes)
                    node.Remove();
            }
        }

        private static void SortAllElements(XElement root) {
#if DEBUG
            var sw = Stopwatch.StartNew();
#endif
            var levels = new List<List<XElement>>();
            BuildLevels(root, 0, levels);
#if DEBUG
            Console.WriteLine($"      levels gebaut:   {sw.ElapsedMilliseconds} ms ({levels.Count} Ebenen)");
#endif

            var options = new ParallelOptions {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            // Tiefste Ebene zuerst: Kinder sind fertig, bevor Eltern umordnen.
            for (int depth = levels.Count - 1; depth >= 0; depth--)
                Parallel.ForEach(levels[depth], options, SortSingleElement);
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

    internal class XmlSorterOptions {
        public bool Debug { get; set; }
        /// <summary>
        /// XPath Expressions zum Entfernen unerw�nschter Elemente
        /// </summary>
        public string[] RemoveExpressions { get; set; } = [];
        public bool IgnoreNameSpaces { get; set; } = true;
    }

}
