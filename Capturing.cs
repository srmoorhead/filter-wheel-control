using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using PrincetonInstruments.LightField.AddIns;
using System.Collections.ObjectModel; // for ObservableCollection
using System.Windows; // for MessageBox
using System.Windows.Threading;

using FilterWheelControl.SettingsList;
using FilterWheelControl.ControlPanelFunctions;
using FilterWheelControl.HardwareInterface;
using FilterWheelControl.FileFunctions;

namespace FilterWheelControl.ImageCapturing
{
    class Capturing
    {
        #region Instance Variables

        public static volatile bool _STOP = true;
        public static volatile bool _IS_RUNNING = false;
        public static volatile bool _IS_ACQUIRING = false;
        
        private static readonly int MAIN_VIEW = 0; // the primary view in which captured images are displayed
        private static readonly int CONCURRENT = 0; // denotes the case in which concurrent capturing halts acquisition
        private static readonly int SELECTED = 1; // denotes the case in which the user selects to halt acquisition
        private static readonly int EXPORT = 2;  // denotes the case in which there is an export error to halt acquisition

        #endregion // Instance Variables

        #region Run

        /// <summary>
        /// Overrides the Run button in Automated Multi-Filter mode.
        /// Displays captured images in the main view of the Display window.
        /// </summary>
        /// <param name="args">A tuple containig the ILightFieldApplication app and the ControlPanel instance currently running.  Caution:  No check is made before casting!</param>
        public static void Run(object args)
        {
            if (_IS_RUNNING)
                return;

            Tuple<ILightFieldApplication, ControlPanel> arguments = (Tuple<ILightFieldApplication, ControlPanel>)args;
            if (_IS_ACQUIRING)
                transitionToRun(arguments.Item2);
            
            _STOP = false;
            _IS_RUNNING = true;
            
            // Gather run variables
            Tuple<int, double[], string[], int> runVars = CurrentSettingsList.getRunVars();
            int max_index = runVars.Item1;
            double[] exposure_times = runVars.Item2;
            string[] filters = runVars.Item3;

            // Set up the first exposure
            Thread rotate = new Thread(WheelInteraction.RotateWheelToFilter);
            int current_index = 0;
            rotate.Start(filters[current_index]);

            IExperiment exp = arguments.Item1.Experiment;
            exp.SetValue(CameraSettings.ShutterTimingExposureTime, exposure_times[current_index]);

            // Capture display functionality
            IDisplayViewer view = arguments.Item1.DisplayManager.GetDisplay(DisplayLocation.ExperimentWorkspace, MAIN_VIEW);
            IImageDataSet frame;

            rotate.Join();

            ////////////////////////////////
            ////  Begin the capture loop ///
            ////////////////////////////////

            while (!_STOP)
            {
                // Capture frame
                if (exp.IsReadyToRun && !exp.IsRunning)
                    frame = exp.Capture(1);
                else
                {
                    // Stop acquisition
                    exp.Stop();
                    HaltAcquisition(arguments.Item2, CONCURRENT);
                    return;
                }

                if (!_STOP)
                {
                    // Set up the next exposure
                    current_index = current_index == max_index ? 0 : current_index + 1;

                    rotate = new Thread(WheelInteraction.RotateWheelToFilter);
                    rotate.Start(filters[current_index]);

                    displayFrameInView(frame, view);

                    exp.SetValue(CameraSettings.ShutterTimingExposureTime, exposure_times[current_index]);

                    rotate.Join();
                }
                else
                    displayFrameInView(frame, view);
            }
            wrapUp(arguments.Item2);
        }

        #endregion // Run

        #region Acquire

        /// <summary>
        /// Overrides the Acquire Button in Automated Multi-Filter Mode.
        /// Displays captured images in the main view of the Display window.
        /// Saves captured images as fits files, separated by frame and ROI.
        /// </summary>
        /// <param name="args">A Tuple containing Item 1: the LightField Application object and Item 2: The ControlPanel object</param>
        public static void Acquire(object args)
        {
            if (_IS_ACQUIRING)
                return;

            Tuple<ILightFieldApplication, ControlPanel> arguments = (Tuple<ILightFieldApplication, ControlPanel>)args;
            if (_IS_RUNNING)
                transitionToAcquire(arguments.Item2);
            
            _STOP = false;
            _IS_ACQUIRING = true;

            // Gather run variables
            Tuple<int, double[], string[], int> runVars = CurrentSettingsList.getRunVars();

            // Check that the user will complete a full filter sequence
            int max_index = runVars.Item1;
            IExperiment exp = arguments.Item1.Experiment;
            int frames_to_store = Convert.ToInt32(exp.GetValue(ExperimentSettings.AcquisitionFramesToStore));
            if (!checkForFullSequence(max_index, frames_to_store))
            {
                HaltAcquisition(arguments.Item2, SELECTED);
                return;
            }

            // Set up first exposure
            int current_index = 0;
            string[] filters = runVars.Item3;
            Thread rotate = new Thread(WheelInteraction.RotateWheelToFilter);
            rotate.Start(filters[current_index]);

            double[] exposure_times = runVars.Item2;
            exp.SetValue(CameraSettings.ShutterTimingExposureTime, exposure_times[current_index]);
            IDisplayViewer view = (arguments.Item1.DisplayManager.GetDisplay(DisplayLocation.ExperimentWorkspace, MAIN_VIEW));

            int padVal = Math.Min(runVars.Item4, Convert.ToInt16(Math.Ceiling(Math.Log10(frames_to_store))));

            rotate.Join();

            ////////////////////////////////
            ////  Begin the capture loop ///
            ////////////////////////////////

            int frame_num = 1;
            while (!_STOP && (frame_num <= frames_to_store))
            {
                // Capture frame
                IImageDataSet frame;
                if (exp.IsReadyToRun && !exp.IsRunning)
                {
                    frame = exp.Capture(1);
                }
                else
                {
                    // Stop acquisition
                    exp.Stop();
                    HaltAcquisition(arguments.Item2, CONCURRENT);
                    return;
                }

                if (!_STOP)
                {
                    // Set up the next frame
                    current_index = current_index = current_index == max_index ? 0 : current_index + 1;
                    rotate = new Thread(WheelInteraction.RotateWheelToFilter);
                    rotate.Start(filters[current_index]);

                    // Export the current frame as a FITS file and display it
                    if (!FileHandling.exportFITSFrame(frame_num, frame, arguments.Item1, padVal))
                    {
                        // there has been an export error
                        exp.Stop();
                        HaltAcquisition(arguments.Item2, EXPORT);
                    }
                    displayFrameInView(frame, view);

                    // Finish set up for next frame
                    exp.SetValue(CameraSettings.ShutterTimingExposureTime, exposure_times[current_index]);
                    frame_num++;
                    rotate.Join();
                }
                else
                {
                    // Export the last frame as a FITS file
                    if (!FileHandling.exportFITSFrame(frame_num, frame, arguments.Item1, padVal))
                    {
                        // there has been an export error
                        exp.Stop();
                        HaltAcquisition(arguments.Item2, EXPORT);
                    }
                    
                    // Display the last frame
                    displayFrameInView(frame, view);
                }
            }
            wrapUp(arguments.Item2);
        }

        /// <summary>
        /// Check if the user will complete a full filter sequence
        /// If not, asks the user what to do
        /// </summary>
        /// <param name="max">The maximum index in the sequence (e.g. with 4 filters, max = 3</param>
        /// <param name="total">The total number of frames to capture</param>
        /// <returns>true if acquisition should continue, false otherwise</returns>
        private static bool checkForFullSequence(int max, int total)
        {
            MessageBoxResult okayToContinue = MessageBoxResult.Yes;
            if (max + 1 > total)
                okayToContinue = MessageBox.Show("With the given settings, you will not complete an entire sequence of filters.  Is this okay?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            return okayToContinue == MessageBoxResult.Yes;
        }

        #endregion // Acquire

        #region Transitions

        /// <summary>
        /// Stops current Acquisition, flashes the Run button until Run can commence.
        /// Set the UI to reflect Running
        /// </summary>
        /// <param name="p">The current ControlPanel object</param>
        private static void transitionToRun(ControlPanel p)
        {
            _STOP = true;
            
            // Flash the Run button until Acquiring is halted
            while (_IS_ACQUIRING)
            {
                Application.Current.Dispatcher.Invoke(new Action(p.setRunGreen));
                Thread.Sleep(ControlPanel.FLASH_INTERVAL);
                Application.Current.Dispatcher.Invoke(new Action(p.setRunClear));
                Thread.Sleep(ControlPanel.FLASH_INTERVAL);
            }

            // Set the run button to Green and the Acquire button to Clear to reflect Running
            Application.Current.Dispatcher.Invoke(new Action(p.setRunGreen));
            Application.Current.Dispatcher.Invoke(new Action(p.setAcquireClear));
        }

        /// <summary>
        /// Stops current Running, flashes the Acquire button until Acquisition can commence.
        /// Set the UI to reflect Acquiring
        /// </summary>
        /// <param name="p">The current ControlPanel object</param>
        private static void transitionToAcquire(ControlPanel p)
        {
            _STOP = true;

            // Flash the Acquire button until Running is halted
            while (_IS_RUNNING)
            {
                Application.Current.Dispatcher.Invoke(new Action(p.setAcquireGreen));
                Thread.Sleep(ControlPanel.FLASH_INTERVAL);
                Application.Current.Dispatcher.Invoke(new Action(p.setAcquireClear));
                Thread.Sleep(ControlPanel.FLASH_INTERVAL);
            }

            // Set the Acquire button to Green and the Run button to Clear to reflect Acquiring
            Application.Current.Dispatcher.Invoke(new Action(p.setAcquireGreen));
            Application.Current.Dispatcher.Invoke(new Action(p.setRunClear));
        }

        #endregion // Transitions

        #region Display

        /// <summary>
        /// Given a frame and a view object, displays the frame in the view
        /// Maintains most view window settings
        /// </summary>
        /// <param name="frame">An IImageDataSet object to be displayed</param>
        /// <param name="view">The IDisplayViewer object in which to display the frame</param>
        private static void displayFrameInView(IImageDataSet frame, IDisplayViewer view)
        {
            object selectedRegion = null;
            object selectedPosition = null;
            
            if (view.DataSelection != null)
                selectedRegion = view.DataSelection;
            if (view.CursorPosition != null)
                selectedPosition = view.CursorPosition;

            double zoom = view.Zoom;

            view.Display("Live Multi-Filter Data", frame);

            if (selectedRegion != null)
                view.DataSelection = (System.Windows.Rect)selectedRegion;
            if (selectedPosition != null)
                view.CursorPosition = (Nullable<System.Windows.Point>)selectedPosition;
            
            view.Zoom = zoom;
        }

        #endregion // Display

        #region Halting and Wrap Up

        /// <summary>
        /// Displays an error message and changes the run booleans to reflect a stop in acquisition.
        /// </summary>
        private static void HaltAcquisition(ControlPanel panel, int reason)
        {
            _IS_RUNNING = false;
            _IS_ACQUIRING = false;
            _STOP = true;
            Application.Current.Dispatcher.BeginInvoke(new Action(panel.ResetUI));
            if (reason == CONCURRENT)
                MessageBox.Show("LightField has attempted to initiate capturing via the regular Run and Acquire functions.  Concurrent capturing will cause LightField to crash.\n\nHalting acquisition.  If you were acquiring, your data has been saved.");
            if (reason == SELECTED || reason == EXPORT)
                MessageBox.Show("Acqusition has been halted.");
        }

        /// <summary>
        /// Wraps up the capturing.  Resets bool vals and the UI.
        /// </summary>
        private static void wrapUp(ControlPanel panel)
        {
            _IS_RUNNING = false;
            _IS_ACQUIRING = false;
            Application.Current.Dispatcher.BeginInvoke(new Action(panel.ResetUI));
        }

        #endregion // Halting and Wrap Up

    }
}
