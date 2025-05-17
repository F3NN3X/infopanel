﻿using InfoPanel.Models;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace InfoPanel.Views.Components
{
    /// <summary>
    /// Interaction logic for DisplayItems.xaml
    /// </summary>
    public partial class DisplayItems : System.Windows.Controls.UserControl
    {
        private DisplayItem? SelectedItem { get { return SharedModel.Instance.SelectedItem; } }
        public DisplayItems()
        {
            InitializeComponent();
            Unloaded += DisplayItems_Unloaded;
            SharedModel.Instance.PropertyChanged += Instance_PropertyChanged;
        }

        private void DisplayItems_Unloaded(object sender, RoutedEventArgs e)
        {
            SharedModel.Instance.PropertyChanged -= Instance_PropertyChanged;
        }

        private void Instance_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SharedModel.Instance.SelectedItem))
            {
                if (SelectedItem != null)
                {
                    ListViewItems.ScrollIntoView(SelectedItem);
                }
            }
        }

        private void ButtonPushUp_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is DisplayItem item)
            {
                _isHandlingSelection = true;

                try
                {
                    if (item is GroupDisplayItem groupDisplayItem)
                    {
                        foreach (var displayItem in groupDisplayItem.DisplayItems)
                        {
                            displayItem.Selected = false;
                        }
                    }

                    SharedModel.Instance.PushDisplayItemBy(item, -1);
                    ListViewItems.ScrollIntoView(item);
                }
                finally
                {
                    _isHandlingSelection = false;
                }
            }
        }

        private void ButtonPushDown_Click(object sender, RoutedEventArgs e)
        {
            if(SelectedItem is DisplayItem item)
            {
                _isHandlingSelection = true;

                try
                {
                    if (item is GroupDisplayItem groupDisplayItem)
                    {
                        foreach (var displayItem in groupDisplayItem.DisplayItems)
                        {
                            displayItem.Selected = false;
                        }
                    }

                    SharedModel.Instance.PushDisplayItemBy(item, 1);
                    ListViewItems.ScrollIntoView(item);
                }
                finally
                {
                    _isHandlingSelection = false;
                }
            }
        }

        private void ButtonPushBack_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is DisplayItem item)
            {
                _isHandlingSelection = true;

                try
                {
                    if (item is GroupDisplayItem groupDisplayItem)
                    {
                        foreach (var displayItem in groupDisplayItem.DisplayItems)
                        {
                            displayItem.Selected = false;
                        }
                    }

                    SharedModel.Instance.PushDisplayItemToTop(item);
                    ListViewItems.ScrollIntoView(item);
                }
                finally
                {
                    _isHandlingSelection = false;
                }
            }
        }

        private void ButtonPushFront_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is DisplayItem item)
            {
                _isHandlingSelection = true;

                try
                {
                    if (item is GroupDisplayItem groupDisplayItem)
                    {
                        foreach (var displayItem in groupDisplayItem.DisplayItems)
                        {
                            displayItem.Selected = false;
                        }
                    }

                    SharedModel.Instance.PushDisplayItemToEnd(item);
                    ListViewItems.ScrollIntoView(item);
                }
                finally
                {
                    _isHandlingSelection = false;
                }
            }
        }

        private void ButtonDelete_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem != null)
            {
                SharedModel.Instance.RemoveDisplayItem(SelectedItem);
            }
        }

        private void ButtonGroup_Click(object sender, RoutedEventArgs e)
        {
            var groupDisplayItem = new GroupDisplayItem
            {
                Name = "New Group"
            };

            SharedModel.Instance.AddDisplayItem(groupDisplayItem);

            var selectedItem = SharedModel.Instance.SelectedItem;
            if (selectedItem != null)
            {
                SharedModel.Instance.PushDisplayItemTo(groupDisplayItem, selectedItem);
            }

            SharedModel.Instance.SelectedItem = groupDisplayItem;
            ListViewItems.ScrollIntoView(groupDisplayItem);
        }

        private void ButtonReload_Click(object sender, RoutedEventArgs e)
        {
            SharedModel.Instance.LoadDisplayItems();
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            SharedModel.Instance.SaveDisplayItems();
        }

        private void ButtonNewText_Click(object sender, RoutedEventArgs e)
        {
            var item = new TextDisplayItem("Custom Text")
            {
                Font = SharedModel.Instance.SelectedProfile!.Font,
                FontSize = SharedModel.Instance.SelectedProfile!.FontSize,
                Color = SharedModel.Instance.SelectedProfile!.Color
            };
            SharedModel.Instance.AddDisplayItem(item);
            SharedModel.Instance.SelectedItem = item;

        }

        private void ButtonNewImage_Click(object sender, RoutedEventArgs e)
        {
            if (SharedModel.Instance.SelectedProfile != null)
            {
                var item = new ImageDisplayItem("Image", SharedModel.Instance.SelectedProfile.Guid);
                SharedModel.Instance.AddDisplayItem(item);
                SharedModel.Instance.SelectedItem = item;
            }
        }

        private void ButtonNewClock_Click(object sender, RoutedEventArgs e)
        {
            var item = new ClockDisplayItem("Clock")
            {
                Font = SharedModel.Instance.SelectedProfile!.Font,
                FontSize = SharedModel.Instance.SelectedProfile!.FontSize,
                Color = SharedModel.Instance.SelectedProfile!.Color

            };
            SharedModel.Instance.AddDisplayItem(item);
            SharedModel.Instance.SelectedItem = item;
        }

        private void ButtonNewCalendar_Click(object sender, RoutedEventArgs e)
        {
            var item = new CalendarDisplayItem("Calendar")
            {
                Font = SharedModel.Instance.SelectedProfile!.Font,
                FontSize = SharedModel.Instance.SelectedProfile!.FontSize,
                Color = SharedModel.Instance.SelectedProfile!.Color
            };
            SharedModel.Instance.AddDisplayItem(item);
            SharedModel.Instance.SelectedItem = item;

        }

        private void ButtonDuplicate_Click(object sender, RoutedEventArgs e)
        {
            if (SharedModel.Instance.SelectedItem != null)
            {
                var item = (DisplayItem)SharedModel.Instance.SelectedItem.Clone();
                SharedModel.Instance.AddDisplayItem(item);
                SharedModel.Instance.PushDisplayItemTo(item, SharedModel.Instance.SelectedItem);
                SharedModel.Instance.SelectedItem = item;
            }
        }

        private bool _isHandlingSelection;

        private void ListViewItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isHandlingSelection || sender is not ListView listView)
                return;

            Trace.WriteLine($"ListViewItems_SelectionChanged - {listView.SelectedItems.Count} SelectedItems");

            _isHandlingSelection = true;
            try
            {

                if (listView.SelectedItems.Count == 0)
                {
                    return;
                }

                foreach (var selectedItem in listView.SelectedItems)
                {
                    if (selectedItem is GroupDisplayItem groupDisplayItem)
                    {
                        foreach (var item in groupDisplayItem.DisplayItems)
                        {
                            item.Selected = true;
                        }
                    }
                }

                var selectedItems = listView.SelectedItems.Cast<DisplayItem>().ToList();

                if (selectedItems.Count != 0)
                    listView.ScrollIntoView(selectedItems.Last());

                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) || Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    return;
                }

                foreach (var item in SharedModel.Instance.DisplayItems)
                {
                    if (item is GroupDisplayItem group && item != listView.SelectedItem)
                    {
                        foreach (var item1 in group.DisplayItems)
                        {
                            item1.Selected = false;
                        }
                    }

                }

            }
            finally
            {
                SharedModel.Instance.NotifySelectedItemChange();
                _isHandlingSelection = false;
                Trace.WriteLine("ListViewItems_SelectionChanged - finally");
            }
        }

        private void ListViewGroupItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isHandlingSelection || sender is not ListView innerListView)
                return;

            Trace.WriteLine($"ListViewGroupItems_SelectionChanged - {innerListView.SelectedItems.Count} SelectedItems");

            _isHandlingSelection = true;

            try
            {
                var selectedItems = innerListView.SelectedItems.Cast<DisplayItem>().ToList();

                if (selectedItems.Count != 0)
                    innerListView.ScrollIntoView(selectedItems.Last());

                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) || Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    return;
                }

                foreach (var item in SharedModel.Instance.SelectedItems)
                {
                    if (item != innerListView.SelectedItem)
                    {
                        item.Selected = false;
                    }
                }

            }
            finally
            {
                SharedModel.Instance.NotifySelectedItemChange();
                _isHandlingSelection = false;
                Trace.WriteLine("ListViewGroupItems_SelectionChanged - finally");
            }
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            if (e.ChangedButton != MouseButton.Left)
                return;

            if (sender is not Border border)
                return;

            var dataItem = border.DataContext;
            var listViewItem = FindAncestor<ListViewItem>(border);
            if (listViewItem == null)
                return;

            var listView = ItemsControl.ItemsControlFromItemContainer(listViewItem) as ListView;
            if (listView == null)
                return;

            // Handle modifier keys
            bool ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            bool shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            if (shift)
            {
                // Select range from last selected to current
                int currentIndex = listView.Items.IndexOf(dataItem);
                int anchorIndex = listView.SelectedIndex;

                if (anchorIndex >= 0 && currentIndex >= 0)
                {
                    int start = Math.Min(anchorIndex, currentIndex);
                    int end = Math.Max(anchorIndex, currentIndex);

                    listView.SelectedItems.Clear();
                    for (int i = start; i <= end; i++)
                    {
                        if (listView.ItemContainerGenerator.ContainerFromIndex(i) is ListViewItem item)
                            item.IsSelected = true;
                    }
                }
            }
            else if (ctrl)
            {
                // Toggle selection
                listViewItem.IsSelected = !listViewItem.IsSelected;
            }
            else
            {
                // Normal click: clear others and select this
                listView.SelectedItems.Clear();
                listViewItem.IsSelected = true;
            }

            listViewItem.BringIntoView();
        }

        public static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T target)
                    return target;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
