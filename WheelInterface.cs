using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Threading;

using FilterWheelSimulator;
using Filters;
using System.IO.Ports;

namespace FilterWheelControl
{
    public class WheelInterface
    {
        #region Static Variables

        private static readonly List<string> _LOADED_FILTERS = new List<string> { "u", "g", "r", "i", "z", "BG40", "DARK", "NOFI" };
        public static readonly double _TIME_BETWEEN_ADJACENT_FILTERS = 1.5; // in seconds

        private static readonly string _PORT_NAME = "COM4";
        private static readonly int _BAUD_RATE = 9600;

        private static readonly string _MOVE = "mv";
        private static readonly string _INQUIRE = "?";

        private static readonly char[] _DELIMITERS = { '=', ' ', '>', '\r', '\n' };

        #endregion // Static Variables

        #region Instance Variables

        private SerialPort _fw;

        #endregion // Instance Variables 

        #region Constructors

        /// <summary>
        /// Opens a connection to the filter wheel via a serial port.
        /// </summary>
        public WheelInterface()
        {
            try
            {
                _fw = new SerialPort(_PORT_NAME, _BAUD_RATE);
                _fw.NewLine = "\r\n"; // newline character
                _fw.ReadTimeout = 1000; // ms until we give up reading
                try
                {
                    _fw.Open();
                }
                catch (Exception e)
                {
                    ProvideErrorInformation(e.Message);
                }
                _fw.Close();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }

        #endregion // Constructors

        #region Modifiers

        /// <summary>
        /// Rotates the filter wheel counter clockwise w.r.t. the camera.
        /// </summary>
        public void RotateCounterClockwise()
        {
            try
            {
                int cur = GetCurrentPosition();
                if (cur == -1)
                    return;

                int move_to;

                if (cur == 0)
                    move_to = _LOADED_FILTERS.Count - 1;
                else
                    move_to = GetCurrentPosition() - 1;

                OpenPort();
                _fw.WriteLine(move_to + _MOVE + "\r");
                ClosePort();
            }
            catch (Exception e)
            {
                ProvideErrorInformation(e.Message);
            }
        }

        /// <summary>
        /// Rotates the filter wheel clockwise w.r.t. the camera.
        /// </summary>
        public void RotateClockwise()
        {
            try
            {
                int cur = GetCurrentPosition();
                if (cur == -1)
                    return;

                int move_to;

                if (cur == _LOADED_FILTERS.Count - 1)
                    move_to = 0;
                else
                    move_to = GetCurrentPosition() + 1;

                OpenPort();
                _fw.WriteLine(move_to + _MOVE + "\r");
                ClosePort();
            }
            catch (Exception e)
            {
                ProvideErrorInformation(e.Message);
            }
        }

        /// <summary>
        /// Rotates the filter wheel to the specified filter
        /// </summary>
        /// <param name="type">A string representing the filter type to rotate to.  Must be included in _LOADED_FILTERS</param>
        public void RotateToFilter(object type)
        {
            try
            {
                OpenPort();
                _fw.WriteLine(_LOADED_FILTERS.IndexOf((string)type) + _MOVE + "\r");
                ClosePort();
            }
            catch (Exception e)
            {
                ProvideErrorInformation(e.Message);
            }
        }

        private void OpenPort()
        {
            if (!_fw.IsOpen)
                _fw.Open();
        }

        private void ClosePort()
        {
            if (_fw.IsOpen)
                _fw.Close();
        }

        /// <summary>
        /// Displays a MessageBox with some information should a try/catch block fail.
        /// </summary>
        private void ProvideErrorInformation(string message)
        {
            MessageBox.Show("There has been an error communicating with the filter wheel.\nHere is some more (possibly helpful) info:  " + message);
        }

        #endregion // Modifiers

        #region Accessors

        /// <summary>
        /// Provides the filters currently in the wheel, ordered with the 0th element being the filter in the prime position, moving clockwise.
        /// </summary>
        /// <returns>A list of strings holding the filter types.</returns>
        public List<string> GetOrderedSet()
        {
            int cur = GetCurrentPosition();
            if (cur == -1)
                return null;
            List<string> ordered = new List<string>();

            int i = cur;
            while (i < _LOADED_FILTERS.Count)
            {
                ordered.Add(_LOADED_FILTERS[i]);
                i++;
            }

            i = 0;
            while (i < cur)
            {
                ordered.Add(_LOADED_FILTERS[i]);
                i++;
            }

            return ordered;
        }

        /// <summary>
        /// Retrieves the current position of the filter and associates that back to the type of filter in that position.
        /// </summary>
        /// <returns>A string holding the type of the filter in the prime position (in front of the camera).</returns>
        public string GetCurrentFilter()
        {
            return _LOADED_FILTERS[Convert.ToInt16(GetCurrentPosition())];
        }

        /// <summary>
        /// Opens a port to the filter wheel and inquires what position is currently in front of the camera.
        /// </summary>
        /// <returns>The integer of the position in front of the camera. -1 if the wheel is currently rotating.</returns>
        public int GetCurrentPosition()
        {
            OpenPort();

            _fw.WriteLine(_INQUIRE + "\r");
            Thread.Sleep(100);
            string report = _fw.ReadExisting();

            ClosePort();

            string[] values = report.Split(_DELIMITERS, StringSplitOptions.RemoveEmptyEntries);

            if (values[1] == "?")
                return -1;
            else
                return Convert.ToInt16(values[1]);
        }

        /// <summary>
        /// Determines if the filter wheel must rotate to reach the filter type represented by f.
        /// </summary>
        /// <param name="f">The filter desired.</param>
        /// <returns>True if the current prime filter type is not the same as the desired filter type, False otherwise.</returns>
        public bool MustRotate(string f)
        {
            return f != GetCurrentFilter();
        }

        /// <summary>
        /// Returns the time, in seconds, between the two provided filters, assuming a constant time between adjacent filters of _TIME_BETWEEN_ADJACENT_FILTERS
        /// </summary>
        /// <param name="f1">One of the filters to calculate time between.</param>
        /// <param name="f2">The other filter to calculate time between.</param>
        /// <returns>The time, in seconds, between the two provided filters.</returns>
        public static double TimeBetweenFilters(string f1, string f2)
        {
            int f1_index = _LOADED_FILTERS.IndexOf(f1);
            int f2_index = _LOADED_FILTERS.IndexOf(f2);
            int distance = Math.Abs(f2_index - f1_index);

            return Math.Min(distance, _LOADED_FILTERS.Count - distance) * _TIME_BETWEEN_ADJACENT_FILTERS;
        }

        /// <summary>
        /// Returns the time, in seconds, from the current filter to the provided filter, assuming a constant time between adjacent filters of _TIME_BETWEEN_ADJACENT_FILTERS
        /// </summary>
        /// <param name="f">The filter to rotate to</param>
        /// <returns>The time, in seconds, from the current filter to the provided filter</returns>
        public double TimeToFilter(string f)
        {
            return TimeBetweenFilters(this.GetCurrentFilter(), f);
        }

        #endregion // Accessors

    }
}
