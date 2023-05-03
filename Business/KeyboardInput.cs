﻿// <copyright file="KeyboardInput.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace SystemTrayMenu.Handler
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Windows.Controls;
    using System.Windows.Input;
    using SystemTrayMenu.DataClasses;
    using SystemTrayMenu.Helpers;
    using SystemTrayMenu.Utilities;
    using static SystemTrayMenu.UserInterface.Menu;
    using Menu = SystemTrayMenu.UserInterface.Menu;

    internal class KeyboardInput : IDisposable
    {
        private readonly Menu?[] menus;
        private readonly KeyboardHook hook = new();

        private Menu? focussedMenu;
        private ListViewItemData? focussedRow;

        public KeyboardInput(Menu?[] menus)
        {
            this.menus = menus;
        }

        internal event Action? HotKeyPressed;

        internal event Action? ClosePressed;

        internal event Action<ListView, ListViewItemData>? RowSelected;

        internal event Action<int, ListView?>? RowDeselected;

        internal event Action<ListView, ListViewItemData>? EnterPressed;

        internal bool InUse { get; set; }

        public void Dispose()
        {
            hook.Dispose();
        }

        internal void RegisterHotKey()
        {
            if (!string.IsNullOrEmpty(Properties.Settings.Default.HotKey))
            {
                try
                {
                    hook.RegisterHotKey();
                    hook.KeyPressed += (sender, e) => HotKeyPressed?.Invoke();
                }
                catch (InvalidOperationException ex)
                {
                    Log.Warn($"key:'{Properties.Settings.Default.HotKey}'", ex);
                    Properties.Settings.Default.HotKey = string.Empty;
                    Properties.Settings.Default.Save();
                }
            }
        }

        internal void ResetSelectedByKey()
        {
            focussedMenu = null;
            focussedRow = null;
        }

        internal void CmdKeyProcessed(Menu sender, Key key, ModifierKeys modifiers)
        {
            switch (key)
            {
                case Key.Enter:
                    if (modifiers == ModifierKeys.None)
                    {
                        SelectByKey(key, modifiers);
                        focussedMenu?.FocusTextBox();
                    }

                    break;
                case Key.Left:
                case Key.Right:
                case Key.Home:
                case Key.End:
                case Key.Up:
                case Key.Down:
                case Key.Escape:
                    if (modifiers == ModifierKeys.None)
                    {
                        SelectByKey(key, modifiers);
                    }

                    break;
                case Key.F4:
                    if (modifiers == ModifierKeys.Alt)
                    {
                        SelectByKey(key, modifiers);
                    }

                    break;
                case Key.F:
                    if (modifiers == ModifierKeys.Control)
                    {
                        focussedMenu?.FocusTextBox();
                    }

                    break;
                case Key.Tab:
                    if (modifiers == ModifierKeys.None)
                    {
                        int indexOfTheCurrentMenu = GetMenuIndex(sender);
                        int indexMax = menus.Where(m => m != null).Count() - 1;
                        int indexNew = 0;
                        if (indexOfTheCurrentMenu > 0)
                        {
                            indexNew = indexOfTheCurrentMenu - 1;
                        }
                        else
                        {
                            indexNew = indexMax;
                        }

                        menus[indexNew]?.FocusTextBox();
                    }
                    else if (modifiers == ModifierKeys.Shift)
                    {
                        int indexOfTheCurrentMenu = GetMenuIndex(sender);
                        int indexMax = menus.Where(m => m != null).Count() - 1;
                        int indexNew = 0;
                        if (indexOfTheCurrentMenu < indexMax)
                        {
                            indexNew = indexOfTheCurrentMenu + 1;
                        }
                        else
                        {
                            indexNew = 0;
                        }

                        menus[indexNew]?.FocusTextBox();
                    }

                    break;
                case Key.Apps:
                    if (modifiers == ModifierKeys.None)
                    {
                        ListView? dgv = focussedMenu?.GetDataGridView();
                        if (dgv != null)
                        {
                            if (focussedRow != null)
                            {
#if TODO // WPF: Better way to open context menu (as it looks like this is the code's intention)
                                Point point = dgv.GetCellDisplayRectangle(2, iRowKey, false).Location;
                                RowData trigger = (RowData)dgv.Rows[iRowKey].Cells[2].Value;
                                MouseEventArgs mouseEventArgs = new(MouseButtons.Right, 1, point.X, point.Y, 0);
                                trigger.MouseDown(dgv, mouseEventArgs);
#endif
                            }
                        }
                    }

                    break;
                default:
                    break;
            }

            int GetMenuIndex(in Menu? currentMenu)
            {
                int index = 0;
                foreach (Menu? menuFindIndex in menus.Where(m => m != null))
                {
                    if (currentMenu == menuFindIndex)
                    {
                        break;
                    }

                    index++;
                }

                return index;
            }
        }

        internal void SearchTextChanging()
        {
            ClearIsSelectedByKey();
        }

        internal void SearchTextChanged(Menu menu, bool isSearchStringEmpty)
        {
            if (isSearchStringEmpty)
            {
                ClearIsSelectedByKey();
            }
            else
            {
                ListView? dgv = menu.GetDataGridView();
                if (dgv != null)
                {
                    if (dgv.Items.Count > 0)
                    {
                        Select(dgv, (ListViewItemData)dgv.Items[0], true);
                    }
                }
            }
        }

        internal void ClearIsSelectedByKey()
        {
            ClearIsSelectedByKey(focussedMenu, focussedRow);
        }

        internal void Select(ListView dgv, ListViewItemData itemData, bool refreshview)
        {
            Menu menu = (Menu)dgv.GetParentWindow();
            if (itemData != focussedRow || menu != focussedMenu)
            {
                ClearIsSelectedByKey();
            }

            focussedMenu = menu;
            focussedRow = itemData;

            itemData.data.IsSelected = true;

            if (refreshview)
            {
                if (dgv.SelectedItems.Contains(itemData))
                {
                    dgv.SelectedItems.Remove(itemData);
                }

                dgv.SelectedItems.Add(itemData);
            }
        }

        private static void ClearIsSelectedByKey(Menu? menu, ListViewItemData? itemData)
        {
            if (menu != null && itemData != null)
            {
                ListView? dgv = menu?.GetDataGridView();
                if (dgv != null)
                {
                    if (dgv.SelectedItems.Contains(itemData))
                    {
                        dgv.SelectedItems.Remove(itemData);
                    }

                    itemData.data.IsSelected = false;
                    itemData.data.IsClicking = false;
                }
            }
        }

        private bool IsAnyMenuSelectedByKey(
            ref Menu? subMenu,
            ref string textSelected)
        {
            bool isStillSelected = false;
            if (focussedRow != null)
            {
                ListViewItemData itemData = focussedRow;
                RowData rowData = itemData.data;
                if (rowData.IsSelected)
                {
                    isStillSelected = true;
                    subMenu = rowData.SubMenu;
                    textSelected = itemData.ColumnText;
                }
            }

            return isStillSelected;
        }

        private void SelectByKey(Key key, ModifierKeys modifiers, string keyInput = "", bool keepSelection = false)
        {
            int iRowBefore = focussedMenu?.GetDataGridView()?.Items.IndexOf(focussedRow) ?? -1;
            Menu? menuBefore = focussedMenu;
            ListViewItemData? rowBefore = focussedRow;

            Menu? menu;
            ListView? dgv;
            ListView? dgvBefore;
            Menu? menuFromSelected = null;
            string textselected = string.Empty;
            bool isStillSelected = IsAnyMenuSelectedByKey(ref menuFromSelected, ref textselected);
            if (isStillSelected)
            {
                if (keepSelection)
                {
                    // If current selection is still valid for this search then skip selecting different item
                    if (textselected.StartsWith(keyInput, true, CultureInfo.InvariantCulture))
                    {
                        return;
                    }
                }

                menu = focussedMenu;
                dgv = menu?.GetDataGridView();
            }
            else
            {
                ResetSelectedByKey();
                menu = null;
                dgv = null;
            }

            dgvBefore = dgv;

            bool toClear = false;
            bool handled = false;
            switch (key)
            {
                case Key.Enter:
                    if ((modifiers == ModifierKeys.None) && focussedRow != null && dgv != null)
                    {
                        ListViewItemData itemData = focussedRow;
                        RowData trigger = itemData.data;
                        if (trigger.IsMenuOpen || !trigger.IsPointingToFolder)
                        {
                            trigger.OpenItem(out bool doCloseAfterOpen);
                            if (doCloseAfterOpen)
                            {
                                ClosePressed?.Invoke();
                            }
                        }
                        else
                        {
                            RowDeselected?.Invoke(iRowBefore, dgvBefore);
                            SelectRow(dgv, focussedRow);
                            EnterPressed?.Invoke(dgv, itemData);
                        }

                        handled = true;
                    }

                    break;
                case Key.Up:
                    if ((modifiers == ModifierKeys.None) &&
                        dgv != null &&
                        (SelectMatchedReverse(dgv, focussedRow) ||
                        SelectMatchedReverse(dgv, dgv.Items.Count - 1)))
                    {
                        RowDeselected?.Invoke(iRowBefore, dgvBefore);
                        SelectRow(dgv, focussedRow);
                        toClear = true;
                        handled = true;
                    }

                    break;
                case Key.Down:
                    if ((modifiers == ModifierKeys.None) &&
                        (SelectMatched(dgv, focussedRow) ||
                        SelectMatched(dgv, 0)))
                    {
                        RowDeselected?.Invoke(iRowBefore, dgvBefore);
                        SelectRow(dgv, focussedRow);
                        toClear = true;
                        handled = true;
                    }

                    break;
                case Key.Home:
                    if ((modifiers == ModifierKeys.None) && SelectMatched(dgv, 0))
                    {
                        RowDeselected?.Invoke(iRowBefore, dgvBefore);
                        SelectRow(dgv, focussedRow);
                        toClear = true;
                        handled = true;
                    }

                    break;
                case Key.End:
                    if ((modifiers == ModifierKeys.None) &&
                        dgv != null &&
                        SelectMatchedReverse(dgv, dgv.Items.Count - 1))
                    {
                        RowDeselected?.Invoke(iRowBefore, dgvBefore);
                        SelectRow(dgv, focussedRow);
                        toClear = true;
                        handled = true;
                    }

                    break;
                case Key.Left:
                    if (modifiers == ModifierKeys.None &&
                        dgv != null &&
                        dgvBefore != null)
                    {
                        Menu? nextMenu = focussedMenu?.SubMenu;
                        bool nextMenuLocationIsLeft = nextMenu != null && menu != null && nextMenu.Location.X < menu.Location.X;
                        Menu? previousMenu = focussedMenu?.ParentMenu;
                        bool previousMenuLocationIsRight = previousMenu != null && menu != null && menu.Location.X < previousMenu.Location.X;
                        if (nextMenuLocationIsLeft || previousMenuLocationIsRight)
                        {
                            SelectNextMenu(iRowBefore, ref dgv, dgvBefore, menuFromSelected, isStillSelected, ref toClear);
                        }
                        else if (focussedMenu?.Level > 0)
                        {
                            SelectPreviousMenu(iRowBefore, ref menu, ref dgv, dgvBefore, ref toClear);
                        }

                        handled = true;
                    }

                    break;
                case Key.Right:
                    if (modifiers == ModifierKeys.None &&
                        dgv != null &&
                        dgvBefore != null)
                    {
                        bool nextMenuLocationIsRight = focussedMenu?.SubMenu?.Location.X > focussedMenu?.Location.X;
                        bool previousMenuLocationIsLeft = focussedMenu?.Location.X > focussedMenu?.ParentMenu?.Location.X;
                        if (nextMenuLocationIsRight || previousMenuLocationIsLeft)
                        {
                            SelectNextMenu(iRowBefore, ref dgv, dgvBefore, menuFromSelected, isStillSelected, ref toClear);
                        }
                        else if (focussedMenu?.Level > 0)
                        {
                            SelectPreviousMenu(iRowBefore, ref menu, ref dgv, dgvBefore, ref toClear);
                        }

                        handled = true;
                    }

                    break;
                case Key.Escape:
                case Key.F4:
                    if ((key == Key.Escape && modifiers == ModifierKeys.None) ||
                        (key == Key.F4 && modifiers == ModifierKeys.Alt))
                    {
                        RowDeselected?.Invoke(iRowBefore, dgvBefore);
                        ResetSelectedByKey();
                        toClear = true;
                        ClosePressed?.Invoke();

                        handled = true;
                    }

                    break;
                default:
                    break;
            }

            if (!handled)
            {
                if (!string.IsNullOrEmpty(keyInput))
                {
                    if (SelectMatched(dgv, focussedRow, keyInput) ||
                        SelectMatched(dgv, 0, keyInput))
                    {
                        RowDeselected?.Invoke(iRowBefore, null);
                        SelectRow(dgv, focussedRow);
                        toClear = true;
                    }
                    else if (isStillSelected)
                    {
                        int prevRowIndex = focussedRow == null ? -1 : menuBefore?.GetDataGridView()?.Items.IndexOf(focussedRow) - 1 ?? -1;
                        focussedRow = prevRowIndex > 0 && menuBefore?.GetDataGridView()?.Items.Count > prevRowIndex ? (ListViewItemData?)menuBefore?.GetDataGridView()?.Items[prevRowIndex] : null;
                        if (SelectMatched(dgv, focussedRow, keyInput) ||
                            SelectMatched(dgv, 0, keyInput))
                        {
                            RowDeselected?.Invoke(iRowBefore, null);
                            SelectRow(dgv, focussedRow);
                        }
                        else
                        {
                            focussedRow = rowBefore;
                        }
                    }
                }
            }

            if (isStillSelected && toClear)
            {
                ClearIsSelectedByKey(menuBefore, rowBefore);
            }
        }

        private void SelectPreviousMenu(int iRowBefore, ref Menu? menu, ref ListView? dgv, ListView? dgvBefore, ref bool toClear)
        {
            if (focussedMenu?.Level > 0)
            {
                if (focussedMenu.ParentMenu != null)
                {
                    menu = focussedMenu = focussedMenu.ParentMenu;
                    focussedRow = null;
                    dgv = menu?.GetDataGridView();
                    if (dgv != null)
                    {
                        if (SelectMatched(dgv, dgv.Items.IndexOf(dgv.SelectedItems.Count > 0 ? dgv.SelectedItems[0] : null)) ||
                            SelectMatched(dgv, 0))
                        {
                            RowDeselected?.Invoke(iRowBefore, dgvBefore);
                            SelectRow(dgv, focussedRow);
                            toClear = true;
                        }
                    }
                }
            }
            else
            {
                RowDeselected?.Invoke(iRowBefore, dgvBefore);
                ResetSelectedByKey();
                toClear = true;
            }
        }

        private void SelectNextMenu(int iRowBefore, ref ListView? dgv, ListView dgvBefore, Menu? menuFromSelected, bool isStillSelected, ref bool toClear)
        {
            if (isStillSelected)
            {
                if (menuFromSelected != null &&
                    menuFromSelected == focussedMenu?.SubMenu)
                {
                    dgv = menuFromSelected?.GetDataGridView();
                    if (dgv != null && dgv.Items.Count > 0)
                    {
                        focussedMenu = menuFromSelected;
                        focussedRow = null;
                        if (SelectMatched(dgv, focussedRow) ||
                            SelectMatched(dgv, 0))
                        {
                            RowDeselected?.Invoke(iRowBefore, dgvBefore);
                            SelectRow(dgv, focussedRow);
                            toClear = true;
                        }
                    }
                }
            }
            else
            {
                focussedMenu = menus[0];
                while (focussedMenu?.SubMenu != null)
                {
                    focussedMenu = focussedMenu.SubMenu;
                }

                focussedRow = null;
                Menu? lastMenu = focussedMenu;
                if (lastMenu != null)
                {
                    dgv = lastMenu?.GetDataGridView();
                    if (SelectMatched(dgv, focussedRow) ||
                        SelectMatched(dgv, 0))
                    {
                        RowDeselected?.Invoke(iRowBefore, dgvBefore);
                        SelectRow(dgv, focussedRow);
                        toClear = true;
                    }
                }
            }
        }

        private void SelectRow(ListView? dgv, ListViewItemData? itemData)
        {
            if (dgv != null && itemData != null)
            {
                InUse = true;
                RowSelected?.Invoke(dgv, itemData);
            }
        }

        private bool SelectMatched(ListView? dgv, ListViewItemData? start, string keyInput = "") =>
            start != null && dgv != null && SelectMatched(dgv, dgv.Items.IndexOf(start), keyInput);

        private bool SelectMatched(ListView? dgv, int indexStart, string keyInput = "")
        {
            bool found = false;
            if (dgv != null && indexStart >= 0)
            {
                for (uint i = (uint)indexStart; i < dgv.Items.Count; i++)
                {
                    if (Select(dgv, i, keyInput))
                    {
                        found = true;
                        break;
                    }
                }
            }

            return found;
        }

        private bool SelectMatchedReverse(ListView dgv, ListViewItemData? start, string keyInput = "") =>
            start != null && SelectMatchedReverse(dgv, dgv.Items.IndexOf(start), keyInput);

        private bool SelectMatchedReverse(ListView dgv, int indexStart, string keyInput = "")
        {
            bool found = false;
            if (indexStart > 0)
            {
                for (int i = indexStart; i > -1; i--)
                {
                    if (Select(dgv, (uint)i, keyInput))
                    {
                        found = true;
                        break;
                    }
                }
            }

            return found;
        }

        private bool Select(ListView dgv, uint i, string keyInput = "")
        {
            bool found = false;
            if (dgv.Items.Count > i && dgv.Items[(int)i] != focussedRow)
            {
                ListViewItemData itemData = (ListViewItemData)dgv.Items[(int)i];
                if (itemData.ColumnText.StartsWith(keyInput, true, CultureInfo.InvariantCulture))
                {
                    focussedRow = itemData;
                    itemData.data.IsSelected = true;
                    if (dgv.SelectedItems.Contains(itemData))
                    {
                        dgv.SelectedItems.Remove(itemData);
                    }

                    dgv.SelectedItems.Add(itemData);
                    dgv.ScrollIntoView(itemData);

                    found = true;
                }
            }

            return found;
        }
    }
}
