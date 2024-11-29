using InfoPanel.Models;
using InfoPanel.Monitors;
using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using static HWHash;
using static InfoPanel.Monitors.PluginMonitor;

namespace InfoPanel.Views.Components
{
    /// <summary>
    /// Interaction logic for HWiNFOSensors.xaml
    /// </summary>
    public partial class PluginSensors : System.Windows.Controls.UserControl
    {
        private PluginSensorsVM ViewModel { get; set; }

        private Timer? UpdateTimer;

        public PluginSensors()
        {
            ViewModel = new PluginSensorsVM();
            DataContext = ViewModel;

            InitializeComponent();

            Loaded += HWiNFOSensors_Loaded;
            Unloaded += HWiNFOSensors_Unloaded;

            UpdateTimer = new Timer
            {
                Interval = 1000
            };
            UpdateTimer.Tick += Timer_Tick;

            //tick once
            Timer_Tick(this, null);
            UpdateTimer.Start();
        }

        private void HWiNFOSensors_Loaded(object sender, RoutedEventArgs e)
        {
            if (UpdateTimer != null)
            {
                UpdateTimer.Tick += Timer_Tick;
                UpdateTimer.Start();
            }
        }

        private void HWiNFOSensors_Unloaded(object sender, RoutedEventArgs e)
        {
            if (UpdateTimer != null)
            {
                UpdateTimer.Stop();
                UpdateTimer.Tick -= Timer_Tick;
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (TreeViewInfo.Items.Count > 0)
            {
                UpdateSensorDetails();
            }
            else
            {
                LoadHWiNFO();
            }
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = (ScrollViewer)sender;
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        public void LoadHWiNFO()
        {
            TreeViewInfo.Items.Clear();

            var parentDict = new Dictionary<Guid, TreeViewItem>();

            foreach (PluginData hash in PluginMonitor.GetOrderedList())
            {
                TreeViewItem item;

                var identifier = hash.PluginId;

                if (parentDict.ContainsKey(identifier))
                {
                    item = parentDict[identifier];
                }
                else
                {
                    item = new TreeViewItem();
                    item.SetResourceReference(TreeViewItem.ForegroundProperty, "TextFillColorSecondaryBrush");
                    item.Focusable = false;
                    item.Header = hash.PluginName;
                    //item.Tag = hash.Index;
                    item.Selected += delegate (object sender, RoutedEventArgs e)
                    {
                        TreeViewItem selectedItem = (TreeViewItem)TreeViewInfo.SelectedItem;
                        Trace.WriteLine("Selected " + selectedItem.Tag);

                        if (selectedItem.Parent is TreeViewItem)
                        {
                            GridActions.IsEnabled = true;
                        }
                        else
                        {
                            GridActions.IsEnabled = false;
                        }
                    };

                    parentDict.Add(identifier, item);
                    TreeViewInfo.Items.Add(item);
                }

                TreeViewItem subItem = new();
                subItem.SetResourceReference(TreeViewItem.ForegroundProperty, "TextFillColorTertiaryBrush");
                subItem.Focusable = false;
                subItem.PreviewMouseDown += SubItem_PreviewMouseDown;
                subItem.Header = hash.Name;
                subItem.Tag = hash.EntryId;

                bool added = false;
                foreach(TreeViewItem group in item.Items)
                {
                    if(group.Header == hash.CollectionName)
                    {
                        group.Items.Add(subItem);
                        added = true;
                        break;
                    }
                }

                if(!added)
                {
                    TreeViewItem group = new();
                    group.SetResourceReference(TreeViewItem.ForegroundProperty, "TextFillColorTertiaryBrush");
                    group.Focusable = false;
                    group.Header = hash.CollectionName;
                    group.Items.Add(subItem);
                    item.Items.Add(group);
                }


            }
        }

        private void SubItem_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {   
            ((TreeViewItem) sender).IsSelected = true;
        }

        private void UpdateSensorDetails()
        {
            TreeViewItem? selectedTreeViewItem = (TreeViewItem)TreeViewInfo.SelectedItem;

            if (selectedTreeViewItem?.Tag is Guid identifier)
            {
                if(PluginMonitor.SENSORHASH.TryGetValue(identifier, out PluginData data))
                {
                    //var item = new SensorDisplayItem(sensor.Name, sensor.Identifier.ToString());

                    ViewModel.SensorName = data.Name;
                    ViewModel.SensorId = data.EntryId.ToString();
                    ViewModel.SensorValue = data.Value;
                }
            }
            else
            {
                //ViewModel.SensorName = "No sensor selected";
                //ViewModel.SensorId = String.Empty;
                //ViewModel.SensorValue = String.Empty;
            }
        }

        private void TreeViewInfo_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            UpdateSensorDetails();
        }

        private void ButtonSelect_Click(object sender, RoutedEventArgs e)
        {
            TreeViewItem? selectedTreeViewItem = (TreeViewItem)TreeViewInfo.SelectedItem;
            if (selectedTreeViewItem?.Tag is Identifier identifier)
            {
                if (LibreMonitor.SENSORHASH.TryGetValue(identifier.ToString(), out ISensor? sensor))
                {
                    var item = new SensorDisplayItem(sensor.Name, sensor.Identifier.ToString())
                    {
                        SensorName = sensor.Name,
                        Font = SharedModel.Instance.SelectedProfile!.Font,
                        FontSize = SharedModel.Instance.SelectedProfile!.FontSize,
                        Color = SharedModel.Instance.SelectedProfile!.Color,
                        Unit = sensor.GetUnit(),
                    };
                    
                    SharedModel.Instance.AddDisplayItem(item);
                    SharedModel.Instance.SelectedItem = item;
                }
            }
        }

        private void ButtonReplace_Click(object sender, RoutedEventArgs e)
        {
            TreeViewItem? selectedTreeViewItem = (TreeViewItem)TreeViewInfo.SelectedItem;
            if (selectedTreeViewItem?.Tag is Identifier identifier)
            {
                if (LibreMonitor.SENSORHASH.TryGetValue(identifier.ToString(), out ISensor? sensor))
                {
                    if (SharedModel.Instance.SelectedItem is SensorDisplayItem sensorDisplayItem)
                    {
                        sensorDisplayItem.Name = (string)selectedTreeViewItem.Header;
                        sensorDisplayItem.SensorName = (string)selectedTreeViewItem.Header;
                        sensorDisplayItem.SensorType = Models.SensorType.Libre;
                        sensorDisplayItem.LibreSensorId = sensor.Identifier.ToString();
                        sensorDisplayItem.Unit = sensor.GetUnit();
                    }
                    else if (SharedModel.Instance.SelectedItem is ChartDisplayItem chartDisplayItem)
                    {
                        chartDisplayItem.Name = (string)selectedTreeViewItem.Header;
                        chartDisplayItem.SensorName = (string)selectedTreeViewItem.Header;
                        chartDisplayItem.SensorType = Models.SensorType.Libre;
                        chartDisplayItem.LibreSensorId = sensor.Identifier.ToString();
                    }
                    else if (SharedModel.Instance.SelectedItem is GaugeDisplayItem gaugeDisplayItem)
                    {
                        gaugeDisplayItem.Name = (string)selectedTreeViewItem.Header;
                        gaugeDisplayItem.SensorName = (string)selectedTreeViewItem.Header;
                        gaugeDisplayItem.SensorType = Models.SensorType.Libre;
                        gaugeDisplayItem.LibreSensorId = sensor.Identifier.ToString();
                    }
                }
            }
        }

        private void ButtonAddGraph_Click(object sender, RoutedEventArgs e)
        {
            TreeViewItem? selectedTreeViewItem = (TreeViewItem)TreeViewInfo.SelectedItem;
            if (selectedTreeViewItem?.Tag is Identifier identifier)
            {
                var item = new GraphDisplayItem((string)selectedTreeViewItem.Header, GraphDisplayItem.GraphType.LINE, identifier.ToString());
                SharedModel.Instance.AddDisplayItem(item);
                SharedModel.Instance.SelectedItem = item;
            }
        }

        private void ButtonAddBar_Click(object sender, RoutedEventArgs e)
        {
            TreeViewItem? selectedTreeViewItem = (TreeViewItem)TreeViewInfo.SelectedItem;
            if (selectedTreeViewItem?.Tag is Identifier identifier)
            {
                var item = new BarDisplayItem((string)selectedTreeViewItem.Header, identifier.ToString());
                SharedModel.Instance.AddDisplayItem(item);
                SharedModel.Instance.SelectedItem = item;
            }
        }

        private void ButtonAddDonut_Click(object sender, RoutedEventArgs e)
        {
            TreeViewItem? selectedTreeViewItem = (TreeViewItem)TreeViewInfo.SelectedItem;
            if (selectedTreeViewItem?.Tag is Identifier identifier)
            {
                var item = new DonutDisplayItem((string)selectedTreeViewItem.Header, identifier.ToString());
                SharedModel.Instance.AddDisplayItem(item);
                SharedModel.Instance.SelectedItem = item;
            }
        }

        private void ButtonAddCustom_Click(object sender, RoutedEventArgs e)
        {
            TreeViewItem? selectedTreeViewItem = (TreeViewItem)TreeViewInfo.SelectedItem;
            if (selectedTreeViewItem?.Tag is Identifier identifier)
            {
                var item = new GaugeDisplayItem((string)selectedTreeViewItem.Header, identifier.ToString());
                SharedModel.Instance.AddDisplayItem(item);
                SharedModel.Instance.SelectedItem = item;
            }
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
    }
}
