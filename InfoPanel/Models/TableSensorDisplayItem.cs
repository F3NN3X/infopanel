﻿using CommunityToolkit.Mvvm.ComponentModel;
using InfoPanel.Drawing;
using SkiaSharp;
using System;
using System.Data;

namespace InfoPanel.Models
{
    [Serializable]
    public partial class TableSensorDisplayItem: TextDisplayItem, IPluginSensorItem
    {
        [ObservableProperty]
        private string _sensorName = string.Empty;

        [ObservableProperty]
        private Enums.SensorType _sensorType = Enums.SensorType.Plugin;

        [ObservableProperty]
        public SensorValueType _valueType = SensorValueType.NOW;

        [ObservableProperty]
        private string _pluginSensorId = string.Empty;

        [ObservableProperty]
        private int _maxRows = 10;

        [ObservableProperty]
        private bool _showHeader = true;

        [ObservableProperty]
        private string _tableFormat = "0:10|1:10|2:10|3:10|4:10|5:10";

        public TableSensorDisplayItem()
        {
            SensorName = string.Empty;
        }

        public TableSensorDisplayItem(string name, Profile profile) : base(name, profile)
        {
            SensorName = name;
        }

        public TableSensorDisplayItem(string name, Profile profile, string pluginSensorId) : base(name, profile)
        {
            SensorName = name;
            SensorType = Enums.SensorType.Plugin;
            PluginSensorId = pluginSensorId;
        }

        public SensorReading? GetValue()
        {
            return SensorReader.ReadPluginSensor(PluginSensorId);
        }

        public override SKSize EvaluateSize()
        {

            var skiaGraphics = SkiaGraphics.FromEmpty(Profile.FontScale);
            var text = "A";
            var (_, height) = skiaGraphics.MeasureString(text, Font, FontStyle, FontSize, Bold, Italic, Underline, Strikeout, Wrap, Ellipsis, Width, Height);

            if (GetValue() is SensorReading sensorReading && sensorReading.ValueTable is DataTable table)
            {
                var formatParts = TableFormat.Split('|');
                var sizeF = new SKSize(0, height * (MaxRows + (ShowHeader ? 1: 0)));

                for (int i = 0; i < formatParts.Length; i++)
                {
                    var split = formatParts[i].Split(':');
                    if (split.Length == 2)
                    {
                        if (int.TryParse(split[0], out var column) && column < table.Columns.Count && int.TryParse(split[1], out var length))
                        {
                            sizeF.Width += length + 10;
                        }
                    }
                }
                return sizeF;    
            }

            return base.EvaluateSize();
        }

        public override (string, string) EvaluateTextAndColor()
        {
            return (EvaluateText(), Color);
        }

        public override string EvaluateText()
        {
            return "Invalid sensor";
        }

        public override SKRect EvaluateBounds()
        {
            var size = EvaluateSize();
            if (GetValue() is SensorReading sensorReading && sensorReading.ValueTable is not null)
            {
                return new SKRect(X, Y, X + size.Width, Y + size.Height);
            }
            else
            {
                int rectX = Width == 0 && RightAlign ? (int)(X - size.Width) : X;
                return new SKRect(rectX, Y, rectX + size.Width, Y + size.Height);
            }
        }

    }
}
