using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using PrincetonInstruments.LightField.AddIns;

namespace FilterWheelControl.ControlPanelFunctions
{
    public partial class ControlPanel : UserControl
    {
        ///////////////////////////////////////////////////////////////////////////////////////////
        ///
        /// The preview_images thread
        /// 
        /// Any methods to interact with the main Control Panel thread must invoke the dispatcher.
        /// This thread interacts with methods in the RunAcquireSupport.cs file.
        /// All other method calls are invoked through the dispatcher.
        ///
        ///////////////////////////////////////////////////////////////////////////////////////////
        
        /// <summary>
        /// The heart of the Run Override.
        /// Continues running until _STOP is set to true by the Stop button.
        /// </summary>
        private void preview_images()
        {
            // Initialize run varialbes
            Tuple<int, double[], string[]> runVars = getRunVars();
            int max_index = runVars.Item1;
            double[] exposure_times = runVars.Item2;
            string[] filters = runVars.Item3;

            // Capture Display functionality
            IDisplayViewer view = _APP.DisplayManager.GetDisplay(DisplayLocation.ExperimentWorkspace, MAIN_VIEW);
            IImageDataSet frame = null;

            // Read the CCD until Stop button pressed
            int current_index = 0;
            while (!_STOP)
            {
                // Rotate Filter Wheel to Correct Position
                RotateWheelToFilter(filters[current_index]);

                // Update exposure time
                EXPERIMENT.SetValue(CameraSettings.ShutterTimingExposureTime, exposure_times[current_index]);

                // Capture frame
                if (EXPERIMENT.IsReadyToRun && !EXPERIMENT.IsRunning)
                {
                    frame = EXPERIMENT.Capture(1);
                }
                else
                {
                    // LightField has attempted to initiate capturing via the regular Run or Acquire operations.
                    // Concurrent capturing will cause LightField to crash.  Save the data and halt all acquisition.
                    HaltAcquisition(CONCURRENT);

                    return;
                }

                // Display image in View tab
                view.Display("Live Multi-Filter Data", frame);

                // Update current_index
                current_index = current_index = current_index == max_index ? 0 : current_index + 1;
            }
            _IS_RUNNING = false;
        }
    }
}
