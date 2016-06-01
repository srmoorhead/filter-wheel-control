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

        public static volatile bool _STOP;
        public static volatile bool _IS_RUNNING;
        
        private static int MAIN_VIEW = 0; // the primary view in which captured images are displayed
        private static int CONCURRENT = 0; // denotes the case in which concurrent capturing halts acquisition
        private static int SELECTED = 1; // denotes the case in which the user selects to halt acquisition
        private static int EXPORT = 2;  // denotes the case in which there is an export error to halt acquisition

        #endregion // Instance Variables

        /// <summary>
        /// Overrides the Run button in Automated Multi-Filter mode.
        /// Displays captured images in the main view of the Display window.
        /// </summary>
        /// <param name="args">A tuple containig the ILightFieldApplication app and the ControlPanel instance currently running.  Caution:  No check is made before casting!</param>
        public static void Run(object args)
        {
            // Reflect that the system is running
            _STOP = false;
            _IS_RUNNING = true;
            
            // Gather run variables
            Tuple<int, double[], string[]> runVars = CurrentSettingsList.getRunVars();
            int max_index = runVars.Item1;
            double[] exposure_times = runVars.Item2;
            string[] filters = runVars.Item3;

            // Read in the args
            Tuple<ILightFieldApplication, ControlPanel> arguments = (Tuple<ILightFieldApplication, ControlPanel>)args;

            // Set up first exposure
            IExperiment exp = arguments.Item1.Experiment;
            int current_index = 0;

            // Start rotating the filter wheel to the first position
            Thread rotate = new Thread(WheelInteraction.RotateWheelToFilter);
            rotate.Start(filters[current_index]);

            // Set the exposure value to the first val and capture display fuctionality
            exp.SetValue(CameraSettings.ShutterTimingExposureTime, exposure_times[current_index]);
            IDisplayViewer view = (arguments.Item1.DisplayManager.GetDisplay(DisplayLocation.ExperimentWorkspace, MAIN_VIEW));

            // Wait for the filter wheel to finish rotating
            rotate.Join();

            // Begin capture loop:
            while (!_STOP)
            {
                // Capture frame
                IImageDataSet frame;
                if (exp.IsReadyToRun && !exp.IsRunning)
                {
                    frame = exp.Capture(1);
                }
                else
                {
                    // LightField has attempted to initiate capturing via the regular Run or Acquire operations.
                    // Concurrent capturing will cause LightField to crash.  Save the data and halt all acquisition.
                    exp.Stop();
                    HaltAcquisition(arguments.Item2, CONCURRENT);
                    return;
                }

                if (!_STOP)
                {
                    // Begin rotating the filter wheel
                    current_index = current_index = current_index == max_index ? 0 : current_index + 1;

                    rotate = new Thread(WheelInteraction.RotateWheelToFilter);
                    rotate.Start(filters[current_index]);

                    // Display the current frame
                    view.Display("Live Multi-Filter Data", frame);

                    // Change the exposure value
                    exp.SetValue(CameraSettings.ShutterTimingExposureTime, exposure_times[current_index]);

                    // Wait for the filter wheel to finish rotating
                    rotate.Join();
                }
                else
                {
                    // Display the last frame
                    view.Display("Live Multi-Filter Data", frame);
                }
            }
            wrapUp(arguments.Item2);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>
        public static void Acquire(object args)
        {
            // Reflect that the system is running
            _STOP = false;
            _IS_RUNNING = true;

            // Read in the args
            Tuple<ILightFieldApplication, ControlPanel> arguments = (Tuple<ILightFieldApplication, ControlPanel>)args;
            IExperiment exp = arguments.Item1.Experiment;

            // Gather run variables
            Tuple<int, double[], string[]> runVars = CurrentSettingsList.getRunVars();
            int max_index = runVars.Item1;
            double[] exposure_times = runVars.Item2;
            string[] filters = runVars.Item3;

            // Check that the user will complete a full filter sequence
            int frames_to_store = Convert.ToInt32(exp.GetValue(ExperimentSettings.AcquisitionFramesToStore));
            if (max_index + 1 > frames_to_store)
            {
                MessageBoxResult okayToContinue = MessageBox.Show("With the given settings, you will not complete an entire sequence of filters.  Is this okay?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (okayToContinue == MessageBoxResult.No)
                {
                    HaltAcquisition(arguments.Item2, SELECTED);
                    return;
                }
            }

            // Set up first exposure
            int current_index = 0;

            // Start rotating the filter wheel to the first position
            Thread rotate = new Thread(WheelInteraction.RotateWheelToFilter);
            rotate.Start(filters[current_index]);

            // Set the exposure value to the first val and capture display fuctionality
            exp.SetValue(CameraSettings.ShutterTimingExposureTime, exposure_times[current_index]);
            IDisplayViewer view = (arguments.Item1.DisplayManager.GetDisplay(DisplayLocation.ExperimentWorkspace, MAIN_VIEW));

            // Wait for the filter wheel to finish rotating
            rotate.Join();

            // Begin capture loop:
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
                    // LightField has attempted to initiate capturing via the regular Run or Acquire operations.
                    // Concurrent capturing will cause LightField to crash.  Save the data and halt all acquisition.
                    exp.Stop();
                    HaltAcquisition(arguments.Item2, CONCURRENT);
                    return;
                }

                if (!_STOP)
                {
                    // Begin rotating the filter wheel
                    current_index = current_index = current_index == max_index ? 0 : current_index + 1;
                    rotate = new Thread(WheelInteraction.RotateWheelToFilter);
                    rotate.Start(filters[current_index]);

                    // Export the current frame as a FITS file
                    if (!FileHandling.exportFITSFrame(frame_num, frame, arguments.Item1))
                    {
                        // there has been an export error
                        exp.Stop();
                        HaltAcquisition(arguments.Item2, EXPORT);
                    }

                    // Display the current frame
                    view.Display("Live Multi-Filter Data", frame);

                    // Change the exposure value
                    exp.SetValue(CameraSettings.ShutterTimingExposureTime, exposure_times[current_index]);

                    frame_num++;

                    // Wait for the filter wheel to finish rotating
                    rotate.Join();
                }
                else
                {
                    // Export the last frame as a FITS file
                    if (!FileHandling.exportFITSFrame(frame_num, frame, arguments.Item1))
                    {
                        // there has been an export error
                        exp.Stop();
                        HaltAcquisition(arguments.Item2, EXPORT);
                    }
                    
                    // Display the last frame
                    view.Display("Live Multi-Filter Data", frame);
                }
            }
            wrapUp(arguments.Item2);
        }

        /// <summary>
        /// Displays an error message and changes the run booleans to reflect a stop in acquisition.
        /// </summary>
        private static void HaltAcquisition(ControlPanel panel, int reason)
        {
            _IS_RUNNING = false;
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
            _STOP = true;
            Application.Current.Dispatcher.BeginInvoke(new Action(panel.ResetUI));
            MessageBox.Show("Acquisition Complete!");
        }

    }
}
