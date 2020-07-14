using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System;
using System.Linq;
using UnityEngine.Assertions;

namespace AutoStreamer
{
    public class AsSceneTreeDataItem : TreeDataItem
    {
        public string assetPath
        {
            get { return name;}
        }
        public int assetBundleSize;
        public AsSceneTreeDataItem(string name, int depth, int id)
            : base(name, depth, id)
        {
            assetBundleSize = 0;
        }
    }

    public class AsScenesTreeView : TreeViewBaseT<AsSceneTreeDataItem>
    {
        const float kRowHeights = 20f;

	    enum MyColumns
        {
            AssetPath,
            AssetBundle,
        }
        enum SortOption
        {
            AssetPath,
            AssetBundle,
        }

        SortOption[] m_SortOptions =
        {
            SortOption.AssetPath,
            SortOption.AssetBundle,
        };

        public AsScenesTreeView(TreeViewState state, MultiColumnHeader multicolumnHeader, TreeModelT<AsSceneTreeDataItem> model) : base(state, multicolumnHeader, model)
        {
            // Custom setup
            rowHeight = kRowHeights;

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

            var myTypes = rootItem.children.Cast<TreeViewItemBaseT<AsSceneTreeDataItem>>();
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
                    case SortOption.AssetBundle:
                        orderedQuery = orderedQuery.ThenBy(l => l.data.assetBundleSize, ascending);
                        break;
                }
            }

            rootItem.children = orderedQuery.Cast<TreeViewItem>().ToList();
        }

        IOrderedEnumerable<TreeViewItemBaseT<AsSceneTreeDataItem>> InitialOrder(IEnumerable<TreeViewItemBaseT<AsSceneTreeDataItem>> myTypes, int[] history)
        {
            SortOption sortOption = m_SortOptions[history[0]];
            bool ascending = multiColumnHeader.IsSortedAscending(history[0]);
            switch (sortOption)
            {
                case SortOption.AssetPath:
                    return myTypes.Order(l => l.data.name, ascending);
                case SortOption.AssetBundle:
                    return myTypes.Order(l => l.data.assetBundleSize, ascending);
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
                    headerContent = new GUIContent("AB"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Center,
                    width = 80,
                    minWidth = 50,
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
            #if false
            if (ids.Count == 1)
            {
                var textureItems = EditorWindow.GetWindow<AsTextureWindow>().GetData();
                int id = ids[0];

                if (id < textureItems.Count)
                {
                    var textureItem = textureItems[id];
                    Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(textureItem.assetPath);
                }
            }
            #endif
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = (TreeViewItemBaseT<AsSceneTreeDataItem>)args.item;

            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                CellGUI(args.GetCellRect(i), item, (MyColumns)args.GetColumn(i), ref args);
            }
        }

        void CellGUI(Rect cellRect, TreeViewItemBaseT<AsSceneTreeDataItem> item, MyColumns column, ref RowGUIArgs args)
        {
            // Center cell rect vertically (makes it easier to place controls, icons etc in the cells)
            CenterRectUsingSingleLineHeight(ref cellRect);

            switch (column)
            {
                case MyColumns.AssetPath:
                    {
                        string value = item.data.assetPath.Substring(7);
                        DefaultGUI.Label(cellRect, value, args.selected, args.focused);
                    }
                    break;
                case MyColumns.AssetBundle:
                    {
                        string value = EditorUtility.FormatBytes(item.data.assetBundleSize);
                        DefaultGUI.Label(cellRect, value, args.selected, args.focused);
                    }
                    break;
            }
        }
    }
}


