using System.Xml.Linq;

namespace XmlSort {

    internal sealed class ElementWorkItem {
        private int _pendingChildren;
        private readonly TaskCompletionSource _childrenReady =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public XElement Element { get; }
        public Task ChildrenReady => _childrenReady.Task;

        public ElementWorkItem(XElement element) {
            Element = element;
            _pendingChildren = element.Elements().Count();
            if (_pendingChildren == 0)
                _childrenReady.SetResult();
        }

        public void MarkChildSorted() {
            if (Interlocked.Decrement(ref _pendingChildren) == 0)
                _childrenReady.SetResult();
        }
    }
}
