﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Threading;

using FilterWheelSimulator;
using Filters;

namespace FilterWheelControl
{
    public class WheelInterface
    {
        #region Static Variables

        private static readonly List<string> _LOADED_FILTERS = new List<string> { "u", "g", "r", "i", "z", "BG40", "DARK", "NOFI" };

        #endregion // Static Variables

        #region Instance Variables

        private FilterWheel _fw;
        private volatile bool _is_rotating;

        #endregion // Instance Variables

        #region Constructors

        public WheelInterface()
        {
            _fw = new FilterWheel(new Filter(_LOADED_FILTERS[0]));
            for (int i = 1; i < _LOADED_FILTERS.Count(); i++)
                _fw.AddFilter(new Filter(_LOADED_FILTERS[i]));
        }

        #endregion // Constructors

        #region Modifiers

        public void RotateCounterClockwise()
        {
            _is_rotating = true;
            _fw.MoveCCW();
            _is_rotating = false;
        }

        public void RotateClockwise()
        {
            _is_rotating = true;
            _fw.MoveCW();
            _is_rotating = false;
        }

        public void RotateToFilter(object type)
        {
            _is_rotating = true;
            _fw.MoveTo((string)type);
            _is_rotating = false;
        }

        #endregion // Modifiers

        #region Accessors

        public string GetStringRepresentation()
        {
            return _fw.ToString();
        }

        public List<Filter> GetOrderedSet()
        {
            return _fw.GetOrderedFilterSet();
        }

        public Filter GetCurrentFilter()
        {
            return _fw.GetCurrent();
        }

        public bool MustRotate(string f)
        {
            return f != _fw.GetCurrent().ToString();
        }

        public bool IsRotating()
        {
            return _is_rotating;
        }

        #endregion // Accessors

    }
}
