using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace AutoStreamer
{
    public class TreeViewItemBaseT<T> : TreeViewItem where T : TreeDataItem
    {
        public T data { get; set; }

        public TreeViewItemBaseT(int id, int depth, string displayName, T data) : base(id, depth, displayName)
        {
            this.data = data;
        }
    }

    public class TreeViewBaseT<T> : TreeView where T : TreeDataItem
    {
        TreeModelT<T> m_TreeModel;
        readonly List<TreeViewItem> m_Rows = new List<TreeViewItem>(100);
        public event Action treeChanged;

        public TreeModelT<T> treeModel { get { return m_TreeModel; } }
        public event Action<IList<TreeViewItem>> beforeDroppingDraggedItems;


        public TreeViewBaseT(TreeViewState state, TreeModelT<T> model) : base(state)
        {
            Init(model);
        }

        public TreeViewBaseT(TreeViewState state, MultiColumnHeader multiColumnHeader, TreeModelT<T> model)
            : base(state, multiColumnHeader)
        {
            Init(model);
        }

        void Init(TreeModelT<T> model)
        {
            m_TreeModel = model;
            m_TreeModel.modelChanged += ModelChanged;
        }

        void ModelChanged()
        {
            if (treeChanged != null)
                treeChanged();

            Reload();
        }

        protected override TreeViewItem BuildRoot()
        {
            int depthForHiddenRoot = -1;
            return new TreeViewItemBaseT<T>(m_TreeModel.root.id, depthForHiddenRoot, m_TreeModel.root.name, m_TreeModel.root);
        }

        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            if (m_TreeModel.root == null)
            {
                Debug.LogError("tree model root is null. did you call SetData()?");
            }

            m_Rows.Clear();
            if (!string.IsNullOrEmpty(searchString) && m_TreeModel.root.hasChildren)
            {
                Search(m_TreeModel.root, searchString, m_Rows);
            }
            else
            {
                if (m_TreeModel.root.hasChildren)
                    AddChildrenRecursive(m_TreeModel.root, 0, m_Rows);
            }

            // We still need to setup the child parent information for the rows since this 
            // information is used by the TreeView internal logic (navigation, dragging etc)
            SetupParentsAndChildrenFromDepths(root, m_Rows);

            return m_Rows;
        }

        void AddChildrenRecursive(T parent, int depth, IList<TreeViewItem> newRows)
        {
            foreach (T child in parent.children)
            {
                var item = new TreeViewItemBaseT<T>(child.id, depth, child.name, child);
                newRows.Add(item);

                if (child.hasChildren)
                {
                    if (IsExpanded(child.id))
                    {
                        AddChildrenRecursive(child, depth + 1, newRows);
                    }
                    else
                    {
                        item.children = CreateChildListForCollapsedParent();
                    }
                }
            }
        }

        void Search(T searchFromThis, string search, List<TreeViewItem> result)
        {
            if (string.IsNullOrEmpty(search))
                throw new ArgumentException("Invalid search: cannot be null or empty", "search");

            const int kItemDepth = 0; // tree is flattened when searching

            Stack<T> stack = new Stack<T>();
            foreach (var element in searchFromThis.children)
                stack.Push((T)element);
            while (stack.Count > 0)
            {
                T current = stack.Pop();
                // Matches search?
                if (current.MatchesSearch(search))
                // if (current.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.Add(new TreeViewItemBaseT<T>(current.id, kItemDepth, current.name, current));
                }

                if (current.children != null && current.children.Count > 0)
                {
                    foreach (var element in current.children)
                    {
                        stack.Push((T)element);
                    }
                }
            }
            SortSearchResult(result);
        }

        protected virtual void SortSearchResult(List<TreeViewItem> rows)
        {
            rows.Sort((x, y) => EditorUtility.NaturalCompare(x.displayName, y.displayName)); // sort by displayName by default, can be overriden for multicolumn solutions
        }

        protected override IList<int> GetAncestors(int id)
        {
            return m_TreeModel.GetAncestors(id);
        }

        protected override IList<int> GetDescendantsThatHaveChildren(int id)
        {
            return m_TreeModel.GetDescendantsThatHaveChildren(id);
        }


        // Dragging
        //-----------

        const string k_GenericDragID = "GenericDragColumnDragging";

        protected override bool CanStartDrag(CanStartDragArgs args)
        {
            return true;
        }

        protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
        {
            if (hasSearch)
                return;

            DragAndDrop.PrepareStartDrag();
            var draggedRows = GetRows().Where(item => args.draggedItemIDs.Contains(item.id)).ToList();
            DragAndDrop.SetGenericData(k_GenericDragID, draggedRows);
            DragAndDrop.objectReferences = new UnityEngine.Object[] { }; // this IS required for dragging to work
            string title = draggedRows.Count == 1 ? draggedRows[0].displayName : "< Multiple >";
            DragAndDrop.StartDrag(title);
        }

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            // Check if we can handle the current drag data (could be dragged in from other areas/windows in the editor)
            var draggedRows = DragAndDrop.GetGenericData(k_GenericDragID) as List<TreeViewItem>;
            if (draggedRows == null)
                return DragAndDropVisualMode.None;

            // Parent item is null when dragging outside any tree view items.
            switch (args.dragAndDropPosition)
            {
                case DragAndDropPosition.UponItem:
                case DragAndDropPosition.BetweenItems:
                    {
                        bool validDrag = ValidDrag(args.parentItem, draggedRows);
                        if (args.performDrop && validDrag)
                        {
                            T parentData = ((TreeViewItemBaseT<T>)args.parentItem).data;
                            OnDropDraggedElementsAtIndex(draggedRows, parentData, args.insertAtIndex == -1 ? 0 : args.insertAtIndex);
                        }
                        return validDrag ? DragAndDropVisualMode.Move : DragAndDropVisualMode.None;
                    }

                case DragAndDropPosition.OutsideItems:
                    {
                        if (args.performDrop)
                            OnDropDraggedElementsAtIndex(draggedRows, m_TreeModel.root, m_TreeModel.root.children.Count);

                        return DragAndDropVisualMode.Move;
                    }
                default:
                    Debug.LogError("Unhandled enum " + args.dragAndDropPosition);
                    return DragAndDropVisualMode.None;
            }
        }

        public virtual void OnDropDraggedElementsAtIndex(List<TreeViewItem> draggedRows, T parent, int insertIndex)
        {
            if (beforeDroppingDraggedItems != null)
                beforeDroppingDraggedItems(draggedRows);

            var draggedElements = new List<TreeDataItem>();
            foreach (var x in draggedRows)
                draggedElements.Add(((TreeViewItemBaseT<T>)x).data);

            var selectedIDs = draggedElements.Select(x => x.id).ToArray();
            m_TreeModel.MoveElements(parent, insertIndex, draggedElements);
            SetSelection(selectedIDs, TreeViewSelectionOptions.RevealAndFrame);
        }


        bool ValidDrag(TreeViewItem parent, List<TreeViewItem> draggedItems)
        {
            TreeViewItem currentParent = parent;
            while (currentParent != null)
            {
                if (draggedItems.Contains(currentParent))
                    return false;
                currentParent = currentParent.parent;
            }
            return true;
        }

    }

    public static class TreeElementUtility
    {
        public static void TreeToList<T>(T root, IList<T> result) where T : TreeDataItem
        {
            if (result == null)
                throw new NullReferenceException("The input 'IList<T> result' list is null");
            result.Clear();

            Stack<T> stack = new Stack<T>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                T current = stack.Pop();
                result.Add(current);

                if (current.children != null && current.children.Count > 0)
                {
                    for (int i = current.children.Count - 1; i >= 0; i--)
                    {
                        stack.Push((T)current.children[i]);
                    }
                }
            }
        }

        // Returns the root of the tree parsed from the list (always the first element).
        // Important: the first item and is required to have a depth value of -1. 
        // The rest of the items should have depth >= 0. 
        public static T ListToTree<T>(IList<T> list) where T : TreeDataItem
        {
            // Validate input
            ValidateDepthValues(list);

            // Clear old states
            foreach (var element in list)
            {
                element.parent = null;
                element.children = null;
            }

            // Set child and parent references using depth info
            for (int parentIndex = 0; parentIndex < list.Count; parentIndex++)
            {
                var parent = list[parentIndex];
                bool alreadyHasValidChildren = parent.children != null;
                if (alreadyHasValidChildren)
                    continue;

                int parentDepth = parent.depth;
                int childCount = 0;

                // Count children based depth value, we are looking at children until it's the same depth as this object
                for (int i = parentIndex + 1; i < list.Count; i++)
                {
                    if (list[i].depth == parentDepth + 1)
                        childCount++;
                    if (list[i].depth <= parentDepth)
                        break;
                }

                // Fill child array
                List<TreeDataItem> childList = null;
                if (childCount != 0)
                {
                    childList = new List<TreeDataItem>(childCount); // Allocate once
                    childCount = 0;
                    for (int i = parentIndex + 1; i < list.Count; i++)
                    {
                        if (list[i].depth == parentDepth + 1)
                        {
                            list[i].parent = parent;
                            childList.Add(list[i]);
                            childCount++;
                        }

                        if (list[i].depth <= parentDepth)
                            break;
                    }
                }

                parent.children = childList;
            }

            return list[0];
        }

        // Check state of input list
        public static void ValidateDepthValues<T>(IList<T> list) where T : TreeDataItem
        {
            if (list.Count == 0)
                throw new ArgumentException("list should have items, count is 0, check before calling ValidateDepthValues", "list");

            if (list[0].depth != -1)
                throw new ArgumentException("list item at index 0 should have a depth of -1 (since this should be the hidden root of the tree). Depth is: " + list[0].depth, "list");

            for (int i = 0; i < list.Count - 1; i++)
            {
                int depth = list[i].depth;
                int nextDepth = list[i + 1].depth;
                if (nextDepth > depth && nextDepth - depth > 1)
                    throw new ArgumentException(string.Format("Invalid depth info in input list. Depth cannot increase more than 1 per row. Index {0} has depth {1} while index {2} has depth {3}", i, depth, i + 1, nextDepth));
            }

            for (int i = 1; i < list.Count; ++i)
                if (list[i].depth < 0)
                    throw new ArgumentException("Invalid depth value for item at index " + i + ". Only the first item (the root) should have depth below 0.");

            if (list.Count > 1 && list[1].depth != 0)
                throw new ArgumentException("Input list item at index 1 is assumed to have a depth of 0", "list");
        }


        // For updating depth values below any given element e.g after reparenting elements
        public static void UpdateDepthValues<T>(T root) where T : TreeDataItem
        {
            if (root == null)
                throw new ArgumentNullException("root", "The root is null");

            if (!root.hasChildren)
                return;

            Stack<TreeDataItem> stack = new Stack<TreeDataItem>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                TreeDataItem current = stack.Pop();
                if (current.children != null)
                {
                    foreach (var child in current.children)
                    {
                        child.depth = current.depth + 1;
                        stack.Push(child);
                    }
                }
            }
        }

        // Returns true if there is an ancestor of child in the elements list
        static bool IsChildOf<T>(T child, IList<T> elements) where T : TreeDataItem
        {
            while (child != null)
            {
                child = (T)child.parent;
                if (elements.Contains(child))
                    return true;
            }
            return false;
        }

        public static IList<T> FindCommonAncestorsWithinList<T>(IList<T> elements) where T : TreeDataItem
        {
            if (elements.Count == 1)
                return new List<T>(elements);

            List<T> result = new List<T>(elements);
            result.RemoveAll(g => IsChildOf(g, elements));
            return result;
        }
    }

    public static class MyExtensionMethods
    {
        public static IOrderedEnumerable<T> Order<T, TKey>(this IEnumerable<T> source, Func<T, TKey> selector, bool ascending)
        {
            if (ascending)
            {
                return source.OrderBy(selector);
            }
            else
            {
                return source.OrderByDescending(selector);
            }
        }

        public static IOrderedEnumerable<T> ThenBy<T, TKey>(this IOrderedEnumerable<T> source, Func<T, TKey> selector, bool ascending)
        {
            if (ascending)
            {
                return source.ThenBy(selector);
            }
            else
            {
                return source.ThenByDescending(selector);
            }
        }
    }

    public class MyMultiColumnHeader : MultiColumnHeader
    {
        Mode m_Mode;

        public enum Mode
        {
            LargeHeader,
            DefaultHeader,
            MinimumHeaderWithoutSorting
        }

        public MyMultiColumnHeader(MultiColumnHeaderState state)
            : base(state)
        {
            mode = Mode.DefaultHeader;
        }

        public Mode mode
        {
            get
            {
                return m_Mode;
            }
            set
            {
                m_Mode = value;
                switch (m_Mode)
                {
                    case Mode.LargeHeader:
                        canSort = true;
                        height = 37f;
                        break;
                    case Mode.DefaultHeader:
                        canSort = true;
                        height = DefaultGUI.defaultHeight;
                        break;
                    case Mode.MinimumHeaderWithoutSorting:
                        canSort = false;
                        height = DefaultGUI.minimumHeight;
                        break;
                }
            }
        }

        protected override void ColumnHeaderGUI(MultiColumnHeaderState.Column column, Rect headerRect, int columnIndex)
        {
            // Default column header gui
            base.ColumnHeaderGUI(column, headerRect, columnIndex);

            // Add additional info for large header
            if (mode == Mode.LargeHeader)
            {
                // Show example overlay stuff on some of the columns
                if (columnIndex > 2)
                {
                    headerRect.xMax -= 3f;
                    var oldAlignment = EditorStyles.largeLabel.alignment;
                    EditorStyles.largeLabel.alignment = TextAnchor.UpperRight;
                    GUI.Label(headerRect, 36 + columnIndex + "%", EditorStyles.largeLabel);
                    EditorStyles.largeLabel.alignment = oldAlignment;
                }
            }
        }
    }

}

