﻿using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace InfoPanel
{
    [ValueConversion(typeof(object), typeof(bool))]
    public class NullToBooleanConverter: IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }


        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
