using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Assertions;

namespace AutoStreamer
{
    [Serializable]
    public class AsTextureTreeDataItem : TreeDataItem
    {
        public string assetPath
        {
            get { return name; }
        }
        public int runtimeMemory;
        public int width;
        public int height;
        public List<int> refScenes;

        public bool usePlaceholder;
        public int placeholderImportMaxSize;
        public string placehoderAssetPath;
        public int assetBundleSize;

        public AsTextureTreeDataItem(string name, int depth, int id)
            : base(name, depth, id)
        {
            refScenes = new List<int>();
            usePlaceholder = false;
            assetBundleSize = 0;
        }
    }

    public class AsTexturesTreeView : TreeViewBaseT<AsTextureTreeDataItem>
    {
        const float kRowHeights = 20f;
        const float kToggleWidth = 18f;

        enum MyColumns
        {
            AssetPath,
            Size,
            Width,
            Height,
            Placeholder,
            AssetBundle,
            RefScenes,
        }

        enum SortOption
        {
            AssetPath,
            Size,
            Width,
            Height,
            Placeholder,
            AssetBundle,
            RefScenes,
        }

        SortOption[] m_SortOptions =
        {
            SortOption.AssetPath,
            SortOption.Size,
            SortOption.Width,
            SortOption.Height,
            SortOption.Placeholder,
            SortOption.AssetBundle,
            SortOption.RefScenes,
        };

	    public AsTexturesTreeView(TreeViewState state, MultiColumnHeader multicolumnHeader, TreeModelT<AsTextureTreeDataItem> model) : base(state, multicolumnHeader, model)
        {
            // Custom setup
            rowHeight = kRowHeights;

            // lz: modified
            columnIndexForTreeFoldouts = 0;
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            customFoldoutYOffset = (kRowHeights - EditorGUIUtility.singleLineHeight) * 0.5f; // center foldout in the row since we also center content. See RowGUI
            multicolumnHeader.sortingChanged += OnSortingChanged;

            Reload();
        }

        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            var rows = base.BuildRows(root);
            SortIfNeeded(root, rows);
            return rows;
        }

        void OnSortingChanged(MultiColumnHeader multiColumnHeader)
        {
            SortIfNeeded(rootItem, GetRows());
        }

        void SortIfNeeded(TreeViewItem root, IList<TreeViewItem> rows)
        {
            if (rows.Count <= 1)
                return;

            if (multiColumnHeader.sortedColumnIndex == -1)
            {
                return; // No column to sort for (just use the order the data are in)
            }

            // Sort the roots of the existing tree items
            SortByMultipleColumns();
            TreeToList(root, rows);
            Repaint();
        }

        public static void TreeToList(TreeViewItem root, IList<TreeViewItem> result)
        {
            if (root == null)
                throw new NullReferenceException("root");
            if (result == null)
                throw new NullReferenceException("result");

            result.Clear();

            if (root.children == null)
                return;

            Stack<TreeViewItem> stack = new Stack<TreeViewItem>();
            for (int i = root.children.Count - 1; i >= 0; i--)
                stack.Push(root.children[i]);

            while (stack.Count > 0)
            {
                TreeViewItem current = stack.Pop();
                result.Add(current);

                if (current.hasChildren && current.children[0] != null)
                {
                    for (int i = current.children.Count - 1; i >= 0; i--)
                    {
                        stack.Push(current.children[i]);
                    }
                }
            }
        }

        void SortByMultipleColumns()
        {
            var sortedColumns = multiColumnHeader.state.sortedColumns;

            if (sortedColumns.Length == 0)
                return;

            var myTypes = rootItem.children.Cast<TreeViewItemBaseT<AsTextureTreeDataItem>>();
            var orderedQuery = InitialOrder(myTypes, sortedColumns);
            for (int i = 1; i < sortedColumns.Length; i++)
            {
                SortOption sortOption = m_SortOptions[sortedColumns[i]];
                bool ascending = multiColumnHeader.IsSortedAscending(sortedColumns[i]);

                switch (sortOption)
                {
                    case SortOption.AssetPath:
                        orderedQuery = orderedQuery.ThenBy(l => l.data.name, ascending);
                        break;
                    case SortOption.Size:
                        orderedQuery = orderedQuery.ThenBy(l => l.data.runtimeMemory, ascending);
                        break;
                    case SortOption.Width:
                        orderedQuery = orderedQuery.ThenBy(l => l.data.width, ascending);
                        break;
                    case SortOption.Height:
                        orderedQuery = orderedQuery.ThenBy(l => l.data.height, ascending);
                        break;
                    case SortOption.Placeholder:
                        orderedQuery = orderedQuery.ThenBy(l => l.data.usePlaceholder, ascending);
                        break;
                    case SortOption.AssetBundle:
                        orderedQuery = orderedQuery.ThenBy(l => l.data.assetBundleSize, ascending);
                        break;
                    case SortOption.RefScenes:
                        orderedQuery = orderedQuery.ThenBy(l => l.data.refScenes.Count, ascending);
                        break;
                }
            }

            rootItem.children = orderedQuery.Cast<TreeViewItem>().ToList();
        }

        IOrderedEnumerable<TreeViewItemBaseT<AsTextureTreeDataItem>> InitialOrder(IEnumerable<TreeViewItemBaseT<AsTextureTreeDataItem>> myTypes, int[] history)
        {
            SortOption sortOption = m_SortOptions[history[0]];
            bool ascending = multiColumnHeader.IsSortedAscending(history[0]);
            switch (sortOption)
            {
                case SortOption.AssetPath:
                    return myTypes.Order(l => l.data.name, ascending);
                case SortOption.Size:
                    return myTypes.Order(l => l.data.runtimeMemory, ascending);
                case SortOption.Width:
                    return myTypes.Order(l => l.data.width, ascending);
                case SortOption.Height:
                    return myTypes.Order(l => l.data.height, ascending);
                case SortOption.Placeholder:
                    return myTypes.Order(l => l.data.usePlaceholder, ascending);
                case SortOption.AssetBundle:
                    return myTypes.Order(l => l.data.assetBundleSize, ascending);
                case SortOption.RefScenes:
                    return myTypes.Order(l => l.data.refScenes.Count, ascending);
                default:
                    Assert.IsTrue(false, "Unhandled enum");
                    break;
            }

            // default
            return myTypes.Order(l => l.data.name, ascending);
        }

        public static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState(float treeViewWidth)
        {
            var columns = new[]
            {
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("AssetPath"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Center,
                    width = 500,
                    minWidth = 100,
                    autoResize = false,
                    allowToggleVisibility = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("RT Mem"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Center,
                    width = 70,
                    minWidth = 50,
                    autoResize = false,
                    allowToggleVisibility = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Width"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Center,
                    width = 50,
                    minWidth = 50,
                    autoResize = false,
                    allowToggleVisibility = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Height"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Center,
                    width = 50,
                    minWidth = 50,
                    autoResize = false,
                    allowToggleVisibility = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Placeholder"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Center,
                    width = 50,
                    minWidth = 50,
                    autoResize = false,
                    allowToggleVisibility = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("AB"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Center,
                    width = 80,
                    minWidth = 50,
                    autoResize = false,
                    allowToggleVisibility = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("RefScenes"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Center,
                    width = 100,
                    minWidth = 100,
                    autoResize = false,
                    allowToggleVisibility = false
                },
            };

            // Assert.AreEqual(columns.Length, Enum.GetValues(typeof(MyColumns)).Length, "Number of columns should match number of enum values: You probably forgot to update one of them.");

            var state = new MultiColumnHeaderState(columns);
            return state;
        }

        override protected void SelectionChanged(IList<int> ids)
        {
            if (ids.Count == 1)
            {
                var textureItems = EditorWindow.GetWindow<AsTextureWindow>().GetTextureData();
                int id = ids[0];

                if (id < textureItems.Count)
                {
                    var textureItem = textureItems[id];
                    Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(textureItem.assetPath);
                }
            }
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = (TreeViewItemBaseT<AsTextureTreeDataItem>)args.item;

            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                CellGUI(args.GetCellRect(i), item, (MyColumns)args.GetColumn(i), ref args);
            }
        }

        void CellGUI(Rect cellRect, TreeViewItemBaseT<AsTextureTreeDataItem> item, MyColumns column, ref RowGUIArgs args)
        {
            // Center cell rect vertically (makes it easier to place controls, icons etc in the cells)
            CenterRectUsingSingleLineHeight(ref cellRect);

            switch (column)
            {
                case MyColumns.AssetPath:
                    {
                        string value = item.data.assetPath;
                        DefaultGUI.Label(cellRect, value, args.selected, args.focused);
                    }
                    break;

                case MyColumns.Size:
                    {
                        string value = EditorUtility.FormatBytes(item.data.runtimeMemory);
                        DefaultGUI.Label(cellRect, value, args.selected, args.focused);
                    }
                    break;

                case MyColumns.Width:
                    {
                        string value = item.data.width.ToString();
                        DefaultGUI.Label(cellRect, value, args.selected, args.focused);
                    }
                    break;
                case MyColumns.Height:
                    {
                        string value = item.data.height.ToString();
                        DefaultGUI.Label(cellRect, value, args.selected, args.focused);
                    }
                    break;
                case MyColumns.Placeholder:
                    {
                        #if true
                        // Do toggle
                        Rect toggleRect = cellRect;
                        toggleRect.width = kToggleWidth;
                        if (toggleRect.xMax < cellRect.xMax)
                        {
                            bool isEnabled = EditorGUI.Toggle(toggleRect, item.data.usePlaceholder);
                            if (isEnabled != item.data.usePlaceholder)
                            {
                                IList<int> ids = GetSelection();
                                if (!ids.Contains(item.id))
                                {
                                    item.data.usePlaceholder = isEnabled;
                                }
                                else
                                {
                                    List<AsTextureTreeDataItem> elems = EditorWindow.GetWindow<AsTextureWindow>().GetTextureData();
                                    foreach (int id in ids)
                                        elems[id].usePlaceholder = isEnabled;
                                }

                                EditorWindow.GetWindow<AsTextureWindow>().UpdateAndSaveData();
                            }

                            if (item.data.placehoderAssetPath != null && item.data.placehoderAssetPath.Length > 5)
                            {
                                Rect placeholderRect = cellRect;
                                placeholderRect.x = toggleRect.xMax;
                                placeholderRect.width = 20;
                                if (GUI.Button(placeholderRect, "F"))
                                {
                                    Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(item.data.placehoderAssetPath);
                                }
                            }
                        }
                        #endif
                    }
                    break;
                case MyColumns.AssetBundle:
                    {
                        string value = EditorUtility.FormatBytes(item.data.assetBundleSize);
                        DefaultGUI.Label(cellRect, value, args.selected, args.focused);
                    }
                    break;
                case MyColumns.RefScenes:
                    {
                        string value = String.Join(",", item.data.refScenes.Select(i=>i.ToString()).ToArray());
                        DefaultGUI.Label(cellRect, value, args.selected, args.focused);
                    }
                    break;
            }
        }

    }
}
