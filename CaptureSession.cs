using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using PrincetonInstruments.LightField.AddIns;
using System.Collections.ObjectModel; // for ObservableCollection
using System.Windows; // for MessageBox
using System.Windows.Threading;
//using System.Diagnostics;

namespace FilterWheelControl
{
    class CaptureSession
    {
        #region Static Variables

        private static readonly int CONCURRENT = 0; // denotes the case in which concurrent capturing halts acquisition
        private static readonly int SELECTED = 1; // denotes the case in which the user selects to halt acquisition
        private static readonly int EXPORT = 2;  // denotes the case in which there is an export error to halt acquisition
        private static string INSTRUMENT_PANEL_DISPLAY_FORMAT = "{0}\t|  {1}\t|  {2} of {3}";  // {0} = filter type, {1} = exposure time, {2} = this iteration, {3} = total iterations

        #endregion // Static Variables

        #region Instance Variables

        private volatile bool _is_running;
        private volatile bool _is_acquiring;
        private volatile bool _stop;
        private volatile bool _save;

        private List<IDisplayViewer> _views;
        private ControlPanel _panel;
        private IExperiment _exp;
        private FilterWheelInterface _fw;
        private FilterSetting _current_setting;
        
        private FileHandler _file_handler;
        private int _zero_pad;
        private int _frames;

        private int _fnum;

        #endregion // Instance Variables

        #region Constructors

        /// <summary>
        /// Instantiates a new capture session, in which the user can take images.
        /// </summary>
        /// <param name="panel">The ControlPanel object making the call</param>
        /// <param name="views">A list of views in which to display any captured images</param>
        /// <param name="exp">The current LightField experiment</param>
        /// <param name="fmgr">The LightField application's IFileManager object</param>
        /// <param name="f">The FilterWheelInterface object</param>
        /// <param name="settings">The current filter settings</param>
        public CaptureSession(ControlPanel panel, List<IDisplayViewer> views, IExperiment exp, IFileManager fmgr, FilterWheelInterface f, CurrentSettingsList settings)
        {
            this._panel = panel;
            this._views = views;
            this._exp = exp;
            this._fw = f;

            Tuple<FilterSetting, int, int> vars = settings.GetAllCaptureSettings();
            this._current_setting = vars.Item1;
            this._zero_pad = vars.Item3;

            this._file_handler = new FileHandler(exp, fmgr);
            _fnum = 1;

            this._is_running = false;
            this._is_acquiring = false;
            this._stop = true;
            this._save = false;
        }

        #endregion // Constructors

        #region Accessors

        /// <summary>
        /// Accessor for _is_running
        /// </summary>
        /// <returns>The value of _is_running</returns>
        public bool IsRunning() { return _is_running; }

        /// <summary>
        /// Accessor for _is_acquiring
        /// </summary>
        /// <returns>The value of _is_acquiring</returns>
        public bool IsAcquiring() { return _is_acquiring; }

        /// <summary>
        /// Accessor for _stop
        /// </summary>
        /// <returns>The value of _stop</returns>
        public bool IsStopped() { return _stop; }

        /// <summary>
        /// Accessor for _save
        /// </summary>
        /// <returns>The value of _save</returns>
        public bool IsSaving() { return _save; }

        #endregion // Accessors

        #region Run and Acquire

        /// <summary>
        /// Sets up the capture loop to mimic the Run functionality in LightField
        /// </summary>
        public void Run()
        {
            if (_is_running)
                return;
            else if (_is_acquiring)
            {
                _save = false;
                _is_acquiring = false;
            }
            else
            {
                _stop = false;
                _save = false;
                Thread capturing = new Thread(StartCapturing);
                capturing.Start();
            }
            _is_running = true;
        }

        /// <summary>
        /// Sets up the capture loop to mimic the Acquire functionality in LightField
        /// </summary>
        public void Acquire(int frames)
        {
            this._frames = frames;
            this._zero_pad = Math.Min(this._zero_pad, Convert.ToInt16(Math.Ceiling(Math.Log10(frames))));
            if (_is_acquiring)
                return;
            else if (_is_running)
            {
                _save = true;
                _is_running = false;
            }
            else
            {
                _stop = false;
                _save = true;
                Thread capturing = new Thread(StartCapturing);
                capturing.Start();
            }
            _is_acquiring = true;
        }

        /// <summary>
        /// Set up the first exposure and start the capture loop
        /// </summary>
        private void StartCapturing()
        {
            SetUpExposure();
            CaptureLoop();
        }

        /// <summary>
        /// Sets all exposure settings of _current_setting in LightField and rotates the filter wheel
        /// </summary>
        private void SetUpExposure()
        {
            Thread rotate_fw = new Thread(_fw.RotateToFilter);

            bool rotate = _fw.MustRotate(_current_setting.FilterType);
            if (rotate)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(_panel.UpdateFWInstrumentRotate));
                rotate_fw.Start(_current_setting.FilterType);
            }

            _exp.SetValue(CameraSettings.ShutterTimingExposureTime, _current_setting.DisplayTime * 1000.0); // convert to ms

            if (rotate)
            {
                rotate_fw.Join();
                Application.Current.Dispatcher.BeginInvoke(new Action(_panel.UpdateFWInstrumentOrder));
            }
        }

        /// <summary>
        /// Captures images until _stop is set to true.
        /// Saves images if _save is set to true, otherwise only displays.
        /// </summary>
        private void CaptureLoop()
        {
            IImageDataSet data;
            DateTime captureCallTime;
            int iteration = 1;

            while (!_stop)
            {
                String currStat = String.Format(INSTRUMENT_PANEL_DISPLAY_FORMAT, _current_setting.FilterType, _current_setting.DisplayTime, iteration, _current_setting.NumExposures);
                Application.Current.Dispatcher.BeginInvoke(new Action(() => _panel.UpdatePanelCurrentStatus(currStat)));
                
                if (_exp.IsReadyToRun && !_exp.IsRunning)
                {
                    captureCallTime = DateTime.Now;
                    data = _exp.Capture(1);
                }
                else
                {
                    // LightField has attempted to take control of capture
                    _exp.Stop();
                    HaltAcquisition(CONCURRENT);
                    return;
                }

                if (_save && (_fnum == _frames))
                {
                    _stop = true;
                    Application.Current.Dispatcher.BeginInvoke(new Action(_panel.LaunchFinishCapturing));
                }

                if (!_stop)
                {
                    if (iteration == _current_setting.NumExposures)
                    {
                        _current_setting = _current_setting.Next; // get next setting
                        iteration = 1;

                        Thread rotate_fw = new Thread(_fw.RotateToFilter);
                        bool rotate = _fw.MustRotate(_current_setting.FilterType);
                        if (rotate)
                        {
                            Application.Current.Dispatcher.BeginInvoke(new Action(_panel.UpdateFWInstrumentRotate));
                            rotate_fw.Start(_current_setting.FilterType);
                        }

                        // Save frame if required
                        if (_save)
                        {
                            if (!_file_handler.ExportFITSFrame(data, _fnum, _zero_pad))
                            {
                                // there has been an export error
                                _exp.Stop();
                                HaltAcquisition(EXPORT);
                                return;
                            }
                            _fnum++;
                        }

                        DisplayFrame(data);
                        Application.Current.Dispatcher.BeginInvoke(new Action(() => _panel.UpdatePanelMetaData(data.GetFrameMetaData(0), captureCallTime)));
                        _exp.SetValue(CameraSettings.ShutterTimingExposureTime, _current_setting.DisplayTime * 1000.0); // convert to ms

                        if (rotate)
                        {
                            rotate_fw.Join();
                            Application.Current.Dispatcher.BeginInvoke(new Action(_panel.UpdateFWInstrumentOrder));
                        }
                    }
                    else
                    {
                        // Save frame if required
                        if (_save)
                        {
                            if (!_file_handler.ExportFITSFrame(data, _fnum, _zero_pad))
                            {
                                // there has been an export error
                                _exp.Stop();
                                HaltAcquisition(EXPORT);
                                return;
                            }
                            _fnum++;
                        }

                        DisplayFrame(data);
                        Application.Current.Dispatcher.BeginInvoke(new Action(() => _panel.UpdatePanelMetaData(data.GetFrameMetaData(0), captureCallTime)));
                        iteration++;
                    }
                }
                else
                {
                    // Save frame if required
                    if (_save)
                    {
                        if (!_file_handler.ExportFITSFrame(data, _fnum, _zero_pad))
                        {
                            // there has been an export error
                            _exp.Stop();
                            HaltAcquisition(EXPORT);
                            return;
                        }
                        _fnum++;
                    }

                    Tuple<IImageDataSet, IDisplayViewer> disp1Args = new Tuple<IImageDataSet, IDisplayViewer>(data, _views[0]);
                    Tuple<IImageDataSet, IDisplayViewer> disp2Args = new Tuple<IImageDataSet, IDisplayViewer>(data, _views[1]);
                    DisplayFrameInView(disp1Args);
                    DisplayFrameInView(disp2Args);

                    Application.Current.Dispatcher.BeginInvoke(new Action(() => _panel.UpdatePanelMetaData(data.GetFrameMetaData(0), captureCallTime)));
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => _panel.UpdatePanelCurrentStatus("")));
                }
            }
            wrapUp();
        }

        #endregion // Run and Acquire

        #region Stop and Pause

        /// <summary>
        /// Stops capturing of images
        /// </summary>
        public void Stop()
        {
            _stop = true;
        }

        #endregion // Stop and Pause

        #region Display

        /// <summary>
        /// Asynchronously displays a frame in two views
        /// </summary>
        /// <param name="frame">the IImageDataSet to display</param>
        /// <param name="view1">the first IDisplayViewer</param>
        /// <param name="view2">the second IDisplayViewer</param>
        private void DisplayFrame(IImageDataSet frame)
        {
            Thread disp1 = new Thread(DisplayFrameInView);
            Tuple<IImageDataSet, IDisplayViewer> disp1Args = new Tuple<IImageDataSet, IDisplayViewer>(frame, _views[0]);

            Thread disp2 = new Thread(DisplayFrameInView);
            Tuple<IImageDataSet, IDisplayViewer> disp2Args = new Tuple<IImageDataSet, IDisplayViewer>(frame, _views[1]);
            
            disp1.Start(disp1Args);
            disp2.Start(disp2Args);
        }

        /// <summary>
        /// Given a frame and a view object, displays the frame in the view
        /// Maintains most view window settings
        /// </summary>
        /// <param name="frame">An IImageDataSet object to be displayed</param>
        /// <param name="view">The IDisplayViewer object in which to display the frame</param>
        private void DisplayFrameInView(object args)
        {
            Tuple<IImageDataSet, IDisplayViewer> arguments = (Tuple<IImageDataSet, IDisplayViewer>)args;
            IDisplayViewer view = arguments.Item2;
            IImageDataSet frame = arguments.Item1;
            
            object selectedRegion = null;
            object selectedPosition = null;
            
            if (view.DataSelection != null)
                selectedRegion = view.DataSelection;
            if (view.CursorPosition != null)
                selectedPosition = view.CursorPosition;

            view.Display("Live Multi-Filter Data", frame);
            
            if (selectedRegion != null)
                view.DataSelection = (System.Windows.Rect)selectedRegion;
            if (selectedPosition != null)
                view.CursorPosition = (Nullable<System.Windows.Point>)selectedPosition;
        }

        #endregion // Display

        #region Halting and Wrap Up

        /// <summary>
        /// Displays an error message and changes the run booleans to reflect a stop in acquisition.
        /// </summary>
        private void HaltAcquisition(int reason)
        {
            _is_running = false;
            _is_acquiring = false;
            _stop = true;
            Application.Current.Dispatcher.BeginInvoke(new Action(_panel.ResetUI));
            Application.Current.Dispatcher.BeginInvoke(new Action(_panel.LaunchFinishCapturing));
            if (reason == CONCURRENT)
                MessageBox.Show("LightField has attempted to initiate capturing via the regular Run and Acquire functions.  Concurrent capturing will cause LightField to crash.\n\nHalting acquisition.  If you were acquiring, your data has been saved.");
            if (reason == SELECTED || reason == EXPORT)
                MessageBox.Show("Acqusition has been halted.");
        }

        /// <summary>
        /// Wraps up the capturing.  Resets bool vals and the UI.
        /// </summary>
        private void wrapUp()
        {
            _is_running = false;
            _is_acquiring = false;
            Application.Current.Dispatcher.BeginInvoke(new Action(_panel.ResetUI));
            _views[0].Clear();
            _views[0].Add(_views[0].LiveDisplaySource);
        }

        #endregion // Halting and Wrap Up

    }
}
