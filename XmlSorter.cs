using System.Xml.Linq;

namespace XmlSort {

    internal static class XmlSorter {
        internal static async Task SortFileAsync(FileInfo file, CancellationToken ct) {
            Console.WriteLine($"Verarbeite: {file.FullName}");

            var content = await File.ReadAllTextAsync(file.FullName, ct);
            var doc = XDocument.Parse(content);

            if (doc.Root is not null)
                await SortElementsAsync(doc.Root, ct);

            await File.WriteAllTextAsync(file.FullName, doc.ToString(), ct);
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

        private static async Task SortElementsAsync(XElement root, CancellationToken ct) {
            using var listLock = new AsyncLock();
            var workList = new List<ElementWorkItem>();
            var index = new Dictionary<XElement, ElementWorkItem>();

            BuildWorkList(root, index, workList);

            await Task.WhenAll(workList
                .ToList()
                .Select(item => SortElementAsync(item, index, workList, listLock, ct)));
        }

        private static void BuildWorkList(
            XElement element,
            Dictionary<XElement, ElementWorkItem> index,
            List<ElementWorkItem> list) {
            foreach (var child in element.Elements())
                BuildWorkList(child, index, list);

            var item = new ElementWorkItem(element);
            list.Add(item);
            index[element] = item;
        }

        private static async Task SortElementAsync(
            ElementWorkItem workItem,
            Dictionary<XElement, ElementWorkItem> index,
            List<ElementWorkItem> workList,
            AsyncLock listLock,
            CancellationToken ct) {
            await workItem.ChildrenReady.WaitAsync(ct);

            SortAttributes(workItem.Element);

            var children = workItem.Element.Elements().ToList();
            foreach (var child in children)
                child.Remove();

            foreach (var child in children
                .OrderBy(e => e.Name.NamespaceName)
                .ThenBy(e => e.Name.LocalName)
                .ThenBy(AttributesSortKey))
                workItem.Element.Add(child);

            using (await listLock.LockAsync(ct))
                workList.Remove(workItem);

            var parentXml = workItem.Element.Parent;
            if (parentXml is not null && index.TryGetValue(parentXml, out var parentWorkItem))
                parentWorkItem.MarkChildSorted();
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