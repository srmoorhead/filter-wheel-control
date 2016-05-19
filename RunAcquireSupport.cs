using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using PrincetonInstruments.LightField.AddIns;
using System.Windows.Threading;

namespace FilterWheelControl.ControlPanelFunctions
{
    public partial class ControlPanel : UserControl
    {

        ///////////////////////////////////////////////////////////////////////////////////////////
        ///
        /// Helper Methods for preview_images and acquire_images
        /// 
        /// Any methods to interact with the main Control Panel thread must invoke the dispatcher
        ///
        ///////////////////////////////////////////////////////////////////////////////////////////

        #region Instance Variables

        volatile bool _STOP;
        volatile bool _IS_RUNNING;
        int MAIN_VIEW = 0; // the integer number representing the primary view frame in the Display window -- where all captured frames are initially displayed

        #endregion // Instance Variables

        /// <summary>
        /// Lets the program know the system is ready to capture images.
        /// </summary>
        /// <returns>bool true if system is ready, false otherwise</returns>
        private bool SystemReady()
        {
            IDevice camera = null;
            foreach (IDevice device in EXPERIMENT.ExperimentDevices)
            {
                if (device.Type == DeviceType.Camera)
                    camera = device;
            }
            if (camera == null)
            {
                MessageBox.Show("Camera not found.  Please ensure there is a camera attached to the system.\nIf the camera is attached, ensure you have loaded it into this experiment.");
                return false;
            }
            return EXPERIMENT.IsReadyToRun &&
                EXPERIMENT != null &&
                EXPERIMENT.Exists(CameraSettings.ShutterTimingExposureTime) &&
                !_IS_RUNNING &&
                !EXPERIMENT.IsRunning;
        }

        /// <summary>
        /// Displays an error message and changes the run booleans to reflect a stop in acquisition.
        /// </summary>
        private void HaltAcquisition()
        {
            EXPERIMENT.Stop();
            _IS_RUNNING = false;
            _STOP = true;
            Dispatcher.BeginInvoke(new Action(ResetUI));
            MessageBox.Show("LightField has attempted to initiate capturing via the regular Run and Acquire functions.\nConcurrent capturing will cause LightField to crash.\nHalting image capturing.  Your data has been saved.");
        }

        /// <summary>
        /// Provides the max index, and string[] of filter types and a double[] of exposure times from the current FILTER_SETTINGS variable
        /// Supports both the acquire_images and preview_images threads
        /// </summary>
        /// <returns>A Tuple with Item1 = int max_index and Item2 = double[] exposure_times and Item3 = string[] filter_types</returns>
        private Tuple<int, double[], string[]> getRunVars()
        {
            int total_num_frames = totalFrames();

            int max_index = total_num_frames - 1;
            double[] exposure_times = new double[total_num_frames];
            string[] filter_types = new string[total_num_frames];

            int index = 0;
            for (int frame = 0; frame < _FILTER_SETTINGS.Count; frame++)
            {
                for (int exposure = 0; exposure < _FILTER_SETTINGS[frame].NumExposures; exposure++)
                {
                    exposure_times[index] = _FILTER_SETTINGS[frame].ExposureTime * 1000.0; // Convert from s to ms
                    filter_types[index] = _FILTER_SETTINGS[frame].FilterType;
                    index++;
                }
            }

            return new Tuple<int, double[], string[]>(max_index, exposure_times, filter_types);
        }

        /// <summary>
        /// Returns the total number of frames to be captured per loop through the Current Settings list
        /// </summary>
        /// <returns>Total number of frames per loop</returns>
        private int totalFrames()
        {
            int total_num_frames = 0;
            for (int i = 0; i < _FILTER_SETTINGS.Count; i++)
            {
                total_num_frames += _FILTER_SETTINGS[i].NumExposures;
            }
            return total_num_frames;
        }
    }
}
