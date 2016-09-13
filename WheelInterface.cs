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
        private static readonly double _TIME_BETWEEN_ADJACENT_FILTERS = 0.5; // in seconds

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

        /// <summary>
        /// Returns the time, in seconds, between the two provided filters, assuming a constant time between adjacent filters of _TIME_BETWEEN_ADJACENT_FILTERS
        /// </summary>
        /// <param name="f1">One of the filters to calculate time between.</param>
        /// <param name="f2">The other filter to calculate time between.</param>
        /// <returns>The time, in seconds, between the two provided filters.</returns>
        public static double TimeBetweenFilters(string f1, string f2)
        {
            int i = 0;
            while ((_LOADED_FILTERS[i] != f1 && _LOADED_FILTERS[i] != f2) && i < _LOADED_FILTERS.Count)
                i++;

            int j = i + 1;
            while ((_LOADED_FILTERS[j] != f1 && _LOADED_FILTERS[j] != f2) && j < _LOADED_FILTERS.Count)
                j++;

            return Math.Min((j - i) * _TIME_BETWEEN_ADJACENT_FILTERS, (_LOADED_FILTERS.Count - (j - i)) * _TIME_BETWEEN_ADJACENT_FILTERS);
        }

        /// <summary>
        /// Returns the time, in seconds, from the current filter to the provided filter, assuming a constant time between adjacent filters of _TIME_BETWEEN_ADJACENT_FILTERS
        /// </summary>
        /// <param name="f">The filter to rotate to</param>
        /// <returns>The time, in seconds, from the current filter to the provided filter</returns>
        public double TimeToFilter(string f)
        {
            return TimeBetweenFilters(this.GetCurrentFilter().ToString(), f);
        }

        #endregion // Accessors

    }
}
