using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Threading;
using System.Text.RegularExpressions;

using Filters;
using System.IO.Ports;

namespace FilterWheelControl
{
    public class WheelInterface
    {
        #region Static Variables

        public static readonly List<string> _LOADED_FILTERS = new List<string> { "u '", "g '", "r '", "i '", "z '", "EMPTY", "BLOCK", "BG40" };
        public static readonly double _TIME_BETWEEN_ADJACENT_FILTERS = 1.49; // in seconds

        private static readonly string _PORT_NAME = "COM82"; // This must be set when the filter wheel is attached to the computer.
        private static readonly int _BAUD_RATE = 9600;
        private static readonly string _NEWLINE = "\r\n";

        private static readonly string _MOVE = "mv";
        private static readonly string _HOME = "hm";
        private static readonly string _INQUIRE = "?";
        //private static readonly string _CHECK_STATUS = "CS";
        private static readonly string _SUBMIT = "\r";
        //private static readonly string _PROMPT = ">";
        private static readonly int _CW = 9999;
        private static readonly int _CCW = 8888;

        private static readonly char[] _DELIMITERS = { '=', ' ', '>', '\r', '\n' };

        private static volatile Queue<string> _QUEUE; // the instruction queue for the filter wheel
        private static readonly object _connection_lock = new object();

        public static readonly string _TRANSIT = "Transitioning";

        #endregion // Static Variables

        #region Instance Variables

        private SerialPort _fw;
        private SerialDataReceivedEventHandler _data_received;
        private SerialErrorReceivedEventHandler _error_received;
        private ControlPanel _panel;
        private System.Windows.Threading.DispatcherTimer _send_command_timer;
        private System.Windows.Threading.DispatcherTimer _timeout_timer;
        private volatile bool _is_free;
        private volatile string _current_filter;
        private volatile bool _connected;


        #endregion // Instance Variables 

        #region Constructors

        /// <summary>
        /// Opens a connection to the filter wheel via a serial port.
        /// </summary>
        public WheelInterface(ControlPanel p)
        {
            try
            {
                OpenPortConnection();
                
                // Save the control panel object and create the command queue
                this._panel = p;
                _QUEUE = new Queue<string>();

                // Create event handler for DataRecieved ErrorReceived events
                this._data_received = new SerialDataReceivedEventHandler(_fw_DataReceived);
                this._error_received = new SerialErrorReceivedEventHandler(_fw_ErrorReceived);
                _fw.ErrorReceived += _error_received;
                

                // Create timer for queue sending
                _send_command_timer = new System.Windows.Threading.DispatcherTimer();
                _send_command_timer.Tick += new EventHandler(_send_command_Tick);
                _send_command_timer.Interval = new TimeSpan(0, 0, 0, 0, 10); // try to send a new command every 10ms

                // Create time for timeout
                _timeout_timer = new System.Windows.Threading.DispatcherTimer();
                _timeout_timer.Interval = new TimeSpan(0, 0, 6);

                _connected = true;
                Application.Current.Dispatcher.BeginInvoke(new Action(() => _panel.PingStatusConnected()));
            }
            catch (Exception e)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => _panel.PingStatusDisconnected()));
                ProvideErrorInformation(e.Message);
            }
        }

        private void ProvideErrorInformation(string s) 
        {
            MessageBox.Show("There was an error establishing a connection to the filter wheel.  Please see this (possibly helpful) info:\n\n" + s);
        }

        #endregion // Constructors

        #region Port Communications

        /// <summary>
        /// Opens a connection to _PORT_NAME with a Baud Rate of _BAUD_RATE.
        /// Sets the newline character to _NEWLINE and the timeout time to _TIMEOUT.
        /// </summary>
        private void OpenPortConnection() 
        {
            // Open the port connection
            this._fw = new SerialPort(_PORT_NAME, _BAUD_RATE);
            this._fw.NewLine = _NEWLINE; // newline character
            _connected = true;
        }

        /// <summary>
        /// Adds a command to the command queue for the filter wheel.
        /// </summary>
        /// <param name="s">The string holding the command to be added.  The string must be formatted correctly.</param>
        private void AddToQueue(string s)
        {
            lock (_connection_lock)
            {
                _QUEUE.Enqueue(s);
                _send_command_timer.Start();
                
            }
        }

        /// <summary>
        /// Sends a command to the filter wheel on every timer tick.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _send_command_Tick(object sender,  EventArgs e)
        {
            if (_is_free && _QUEUE.Count > 0)
            {
                try
                {
                    _is_free = false;

                    _fw.Open();
                    _fw.DataReceived += _data_received;

                    string command = _QUEUE.Dequeue();
                    if (command.Contains(_MOVE) || command.Contains(_HOME))
                    {
                        if (command.Contains(Convert.ToString(_CW)))
                        {
                            int cur = _LOADED_FILTERS.IndexOf(_current_filter);
                            command = cur == 0 ? _MOVE + (_LOADED_FILTERS.Count - 1) + _SUBMIT : _MOVE + (_LOADED_FILTERS.IndexOf(_current_filter) - 1) + _SUBMIT;
                            _timeout_timer.Interval = new TimeSpan(0, 0, 3);
                        }
                        else if (command.Contains(Convert.ToString(_CCW)))
                        {
                            int cur = _LOADED_FILTERS.IndexOf(_current_filter);
                            command = cur == _LOADED_FILTERS.Count - 1 ? _MOVE + 0 + _SUBMIT : _MOVE + (_LOADED_FILTERS.IndexOf(_current_filter) + 1) + _SUBMIT;
                            _timeout_timer.Interval = new TimeSpan(0, 0, 3);
                        }

                        Application.Current.Dispatcher.BeginInvoke(new Action(_panel.UpdateFWInstrumentRotate));
                        _current_filter = _TRANSIT;
                    }

                    _fw.WriteLine(command);
                    _timeout_timer.Tick += _timeout_timer_Tick;
                    _timeout_timer.Start();
                }
                catch (Exception ex)
                {
                    _send_command_timer.Stop();
                    _QUEUE.Clear();
                    _connected = false;
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => _panel.PingStatusDisconnected()));
                    MessageBox.Show("The connection to the filter wheel has been lost.  Please attempt to restore a connection.\n\nNo more filter movements will occur.  You may want to halt data acquisition until the problem is resolved.\n\nHere is some more information:\n" + ex.Message);
                }
            }
            else if (_QUEUE.Count == 0)
                _send_command_timer.Stop();
        }

        /// <summary>
        /// Processes the output from the commands sent to the filter wheel.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _fw_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            lock (_connection_lock)
            {
                _timeout_timer.Stop();
                _timeout_timer.Tick -= _timeout_timer_Tick;
                _timeout_timer.Interval = new TimeSpan(0, 0, 6);
                _fw.DataReceived -= _data_received;
                string output = _fw.ReadExisting();

                // Handle the inquiry output
                if (output.Contains("W1 = "))
                    ProcessInquiry(output);
                
                if(_fw.IsOpen)
                    _fw.Close();
                _is_free = true;
            }
        }

        /// <summary>
        /// Stops command processing, alerts the user, and clears the queue if an error is received.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _fw_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            _send_command_timer.Stop();
            _timeout_timer.Stop();
            Application.Current.Dispatcher.BeginInvoke(new Action(() => _panel.PingStatusDisconnected()));
            
            ProvideErrorInformation("An unknown connection error with the filter wheel was recieved during normal operation.  Please double check the connection before continuing.");
            if (_fw.IsOpen)
            {
                _fw.Close();
                _is_free = true;
            }
            else
                _is_free = true;
            _QUEUE.Clear();
        }

        /// <summary>
        /// Attempts to open a port to the filter wheel.
        /// </summary>
        /// <returns>0 if a port is already open or is opened and then closed successfully, 1 otherwise.</returns>
        public int PingConnection()
        {
            lock (_connection_lock)
            {
                bool last_status = _connected;
                int result;
                
                if (_fw.IsOpen)
                {
                    _connected = true;
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => _panel.PingStatusConnected()));
                    result = 0;
                }
                else
                {
                    try
                    {
                        _is_free = false;
                        _fw.Open();
                        if (_fw.IsOpen)
                        {
                            _fw.Close();
                            _is_free = true;
                            _connected = true;
                            Application.Current.Dispatcher.BeginInvoke(new Action(() => _panel.PingStatusConnected()));
                            result = 0;
                        }
                        else
                        {
                            _is_free = true;
                            _connected = false;
                            Application.Current.Dispatcher.BeginInvoke(new Action(() => _panel.PingStatusDisconnected()));
                            result = 1;
                        }
                    }
                    catch (Exception e)
                    {
                        if (e.Message == "Port is already open.")
                        {
                            _connected = true;
                            Application.Current.Dispatcher.BeginInvoke(new Action(() => _panel.PingStatusConnected()));
                            result = 0;
                        }
                        else
                        {
                            _connected = false;
                            Application.Current.Dispatcher.BeginInvoke(new Action(() => _panel.PingStatusDisconnected()));
                            result = 1;
                        }
                    }
                }

                if (last_status == false && _connected == true)
                    Home();
                
                return result;
            }
        }

        /// <summary>
        /// Handles the case of a timeout with the filter wheel.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _timeout_timer_Tick(object sender, EventArgs e)
        {
            _connected = false;
            _send_command_timer.Stop();
            _timeout_timer.Stop();
            _timeout_timer.Tick -= _timeout_timer_Tick;
            _QUEUE.Clear();

            _fw.DataReceived -= _fw_DataReceived;

            try
            {
                if (_fw.IsOpen)
                {
                    _fw.Close();
                    _is_free = true;
                }
            }
            catch (Exception)
            {
                InformDisconnect();
                return;
            }
            Application.Current.Dispatcher.BeginInvoke(new Action(() => _panel.PingStatusDisconnected()));
            MessageBox.Show("Commands to the filter wheel have timed out.\nPlease check all hardware connections, including filter wheel motor power, and try again.", "Timeout Error");
        }

        /// <summary>
        /// Resets the connection to the port.
        /// Clears all commands from the Queue.
        /// </summary>
        public void ResetConnection()
        {
            try
            {
                // Stop all current command processing, clear the queue, and close the port
                _send_command_timer.Stop();
                _QUEUE.Clear();
                if (_fw.IsOpen)
                    _fw.Close();
                _is_free = true;
                _connected = false;

                // Dispose the current serial port connection
                _fw.Dispose();
                _fw = null;

                // Attempt to reconnect to the filter wheel
                OpenPortConnection();
                Application.Current.Dispatcher.BeginInvoke(new Action(() => _panel.PingStatusConnected()));
            }
            catch (Exception e)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => _panel.PingStatusDisconnected()));
                MessageBox.Show("An error occurred while attempting to reset the connection.  Here is some more information:\n\n" + e.Message);
            }
        }

        /// <summary>
        /// Closes all connections to the filter wheel.
        /// Discards both buffers.
        /// </summary>
        public void ShutDown()
        {
            _send_command_timer.Stop();
            _timeout_timer.Stop();
            _fw.ErrorReceived -= _error_received;
            _QUEUE.Clear();
            if (_fw.IsOpen)
                _fw.Close();
            _is_free = true;
            _fw.DiscardInBuffer();
            _fw.DiscardOutBuffer();
            _fw.Dispose();
        }

        /// <summary>
        /// Stops the queue command timer and informs the user of a disconnect.
        /// </summary>
        public void InformDisconnect()
        {
            _send_command_timer.Stop();
            Application.Current.Dispatcher.BeginInvoke(new Action(() => _panel.PingStatusDisconnected()));
            MessageBox.Show("The filter wheel is not connected.  Please Ping the connection and try again.\n\nIf the problem persists, please check all hardware components.");
        }

        #endregion // Port Communications

        #region Output Processing

        /// <summary>
        /// Processes the result of an inquiry command.
        /// Updates the Filter Wheel instrument on the instrument panel.
        /// Does nothing if the result is unknown (?).
        /// </summary>
        /// <param name="inq">A string holding the output of the inquire command.</param>
        private void ProcessInquiry(string inq)
        {
            string[] values = inq.Split(_DELIMITERS, StringSplitOptions.RemoveEmptyEntries);

            if (values[1] == "?")
            {
                return;
            }
            else
            {
                int cur = Convert.ToInt16(values[1]);
                Application.Current.Dispatcher.Invoke(new Action(() => _panel.UpdateFWInstrumentOrder(BuildOrderedSet(cur))));
                _current_filter = _LOADED_FILTERS[cur];
            }
        }

        /// <summary>
        /// Provides the filters currently in the wheel, ordered with the 0th element being the filter in the prime position, moving clockwise.
        /// </summary>
        /// <returns>A list of strings holding the filter types.</returns>
        private List<string> BuildOrderedSet(int cur)
        {
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

        #endregion // Output Processing

        #region Input Processing

        /// <summary>
        /// Rotates the filter wheel to the specified filter.  
        /// Does nothing if the input is not a filter type currently in the filter wheel.
        /// </summary>
        /// <param name="type">A string representing the filter type to rotate to.  Must be included in _LOADED_FILTERS</param>
        public void RotateToFilter(object type)
        {
            // Scrub input
            string ftype;
            try
            {
                ftype = (string)type;
            }
            catch (FormatException)
            {
                return;
            }

            // If type is in _LOADED_FILTERS, rotate to it.
            ftype = (string)type;
            if (_LOADED_FILTERS.Contains(ftype))
            {
                int loc = _LOADED_FILTERS.IndexOf(ftype);
                MoveTo(loc); 
            }
        }

        /// <summary>
        /// Adds the mv command to the filter wheel command queue, followed by the inquire command.
        /// </summary>
        /// <param name="loc">The location to move the filter wheel to.</param>
        private void MoveTo(int loc)
        {
            if (_connected)
            {
                AddToQueue(loc + _MOVE + _SUBMIT);
                Inquire();
            }
            else
                InformDisconnect();
        }

        /// <summary>
        /// Adds the hm command to the filter wheel command queue, followed by the inquire command.
        /// </summary>
        public void Home()
        {
            if (_connected)
            {
                AddToQueue(_HOME + _SUBMIT);
                Inquire();
            }
            else
                InformDisconnect();
        }

        /// <summary>
        /// Clears the queue and adds the hm command to the filter wheel command queue, followed by the inquire command.
        /// </summary>
        public void EmergencyHome()
        {
            if (_connected)
            {
                _QUEUE.Clear();
                AddToQueue(_HOME + _SUBMIT);
                Inquire();
            }
            else
                InformDisconnect();
        }

        /// <summary>
        /// Add the ? command to the filter wheel command queue.
        /// </summary>
        public void Inquire()
        {
            if (_connected)
            {
                AddToQueue(_INQUIRE + _SUBMIT);
            }
            else
                InformDisconnect();
        }

        /// <summary>
        /// Sets up the system for a single movement of the wheel in the clockwise direction w.r.t. the camera.
        /// </summary>
        public void Clockwise()
        {
            if (_connected)
            {
                Inquire();
                MoveTo(_CW);
            }
            else
                InformDisconnect();
        }

        /// <summary>
        /// Sets up the system for a single movement of the wheel in the counterclockwise direction w.r.t. the camera.
        /// </summary>
        public void CounterClockwise()
        {
            if (_connected)
            {
                Inquire();
                MoveTo(_CCW);
            }
            else
                InformDisconnect();
        }

        #endregion // Input Processing

        #region Accessors

        /// <summary>
        /// Returns the time, in seconds, between the two provided filters, assuming a constant time between adjacent filters of _TIME_BETWEEN_ADJACENT_FILTERS
        /// </summary>
        /// <param name="f1">One of the filters to calculate time between.</param>
        /// <param name="f2">The other filter to calculate time between.</param>
        /// <returns>The time, in seconds, between the two provided filters.</returns>
        public static double TimeBetweenFilters(string f1, string f2)
        {
            int stop = _LOADED_FILTERS.Count;

            int pos1 = 0;
            while((_LOADED_FILTERS[pos1] != f1 && _LOADED_FILTERS[pos1] != f2) && pos1 < stop)
            {
                pos1++;
            }

            int pos2 = pos1 + 1;
            while ((_LOADED_FILTERS[pos2] != f1 && _LOADED_FILTERS[pos2] != f2) && pos2 < stop)
            {
                pos2++;
            }

            return Math.Min((pos2 - pos1), stop - (pos2 - pos1)) * _TIME_BETWEEN_ADJACENT_FILTERS;
        }

        /// <summary>
        /// Access the value of _TIME_BETWEEN_ADJACENT_FILTERS
        /// </summary>
        /// <returns>The value of _TIME_BETWEEN_ADJACENT_FILTERS</returns>
        public double TimeBetweenAdjacentFilters()
        {
            return _TIME_BETWEEN_ADJACENT_FILTERS;
        }

        /// <summary>
        /// Access the number of filters in the wheel.
        /// </summary>
        /// <returns>The number of filters in the _LOADED_FILTERS list (8).</returns>
        public int NumFilters()
        {
            return _LOADED_FILTERS.Count;
        }

        #endregion // Accessors

    }
}
