using System;
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
    public class FilterWheelInterface
    {
        #region Static Variables

        private static readonly List<string> _LOADED_FILTERS = new List<string> { "u", "g", "r", "i", "z", "BG40", "DARK", "NOFI" };

        #endregion // Static Variables

        #region Instance Variables

        private FilterWheel _fw;

        #endregion // Instance Variables

        #region Constructors

        public FilterWheelInterface()
        {
            _fw = new FilterWheel(new Filter(_LOADED_FILTERS[0]));
            for (int i = 1; i < _LOADED_FILTERS.Count(); i++)
                _fw.AddFilter(new Filter(_LOADED_FILTERS[i]));
        }

        #endregion // Constructors

        #region Modifiers

        public void RotateCounterClockwise()
        {
            _fw.MoveCCW();
        }

        public void RotateClockwise()
        {
            _fw.MoveCW();
        }

        public void RotateToFilter(object type)
        {
            _fw.MoveTo((string)type);
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

        #endregion // Accessors

    }
}
