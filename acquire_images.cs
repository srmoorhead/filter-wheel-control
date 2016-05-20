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
        /// The acquire_images thread
        /// 
        /// Any methods to interact with the main Control Panel thread must invoke the dispatcher.
        /// This thread interacts with methods in the RunAcquireSupport.cs file.
        /// All other method calls are invoked through the dispatcher.
        ///
        ///////////////////////////////////////////////////////////////////////////////////////////

        #region Acquire Images

        /// <summary>
        /// The heart of the Acquire Override.
        /// Continues running until _STOP is set to true by the Stop button or the user-provided number of frames is reached.
        /// </summary>
        private void acquire_images()
        {
            // Set up FileManager to save the data
            int num_frames = Convert.ToInt32(EXPERIMENT.GetValue(ExperimentSettings.AcquisitionFramesToStore));
            string filename = generateFileName();
            RegionOfInterest[] selectedRegions = EXPERIMENT.SelectedRegions;
            int num_regions = selectedRegions.Count();
            ImageDataFormat format = (ImageDataFormat)EXPERIMENT.GetValue(CameraSettings.AcquisitionPixelFormat);

            IFileManager filemgr = _APP.FileManager;
            IImageDataSet data = filemgr.CreateFile(filename, selectedRegions, (long)num_frames, format);

            IImageData[] separated = null;
            IImageData[] to_be_saved = null;

            // Initialize run varialbes
            Tuple<int, double[], string[]> runVars = getRunVars();
            int max_index = runVars.Item1;
            double[] exposure_times = runVars.Item2;
            string[] filters = runVars.Item3;

            // If the number of frames to capture is less than the desired filter loop sequence length, double check with user before acquiring
            if (max_index + 1 > num_frames)
            {
                MessageBoxResult okayToContinue = MessageBox.Show("With the given settings, you will not complete an entire sequence of filters.  Is this okay?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (okayToContinue == MessageBoxResult.No)
                {
                    HaltAcquisition(SELECTED);
                    return;
                }
            }

            // Capture Display functionality
            IDisplayViewer view = _APP.DisplayManager.GetDisplay(DisplayLocation.ExperimentWorkspace, MAIN_VIEW);
            IImageDataSet captured_frame = null;

            // Read the CCD and save images until Stop button pressed or Number of Frames reached
            int current_index = 0;
            int current_frame = 0;
            while (!_STOP && current_frame < num_frames)
            {
                // Rotate Filter Wheel to Correct Position
                RotateWheelToFilter(filters[current_index]);

                // Update exposure time
                EXPERIMENT.SetValue(CameraSettings.ShutterTimingExposureTime, exposure_times[current_index]);

                // Capture frame
                if (EXPERIMENT.IsReadyToRun && !EXPERIMENT.IsRunning)
                {
                    captured_frame = EXPERIMENT.Capture(1);
                }
                else
                {
                    // LightField has attempted to initiate capturing via the regular Run or Acquire operations.
                    // Concurrent capturing will cause LightField to crash.  Save the data and halt all acquisition.
                    filemgr.CloseFile(data);
                    HaltAcquisition(CONCURRENT);
                    return;
                }

                // Save the data
                separated = separateROIs(captured_frame, num_regions, 0); // 0 is for this frame (the just-captured frame)
                to_be_saved = separateROIs(data, num_regions, current_frame);
                for (int i = 0; i < num_regions; i++)
                    to_be_saved[i].SetData(separated[i].GetData());

                // Display images in View tab
                view.Display("Live Multi-Filter Data", captured_frame);

                // Update current_index and current_frame
                current_index = current_index == max_index ? 0 : current_index + 1;
                current_frame++;
            }
            filemgr.CloseFile(data);
            _IS_RUNNING = false;
        }

        #endregion // Acquire Images

        #region Acquire Support Methods

        /// <summary>
        /// Takes an IImageDataSet object and separates it into an array of IImageData objects by region of interest.
        /// </summary>
        /// <param name="exposure">An IImageDataSet object</param>
        /// <param name="num_rois">The number of regions of interest in one frame of the IImageDataSet object</param>
        /// <param name="frame">The frame number to separate by region of interest</param>
        /// <returns>IImageData[] with each element a different region of interest from the same frame</returns>
        private static IImageData[] separateROIs(IImageDataSet exposure, int num_rois, int frame)
        {
            IImageData[] separated = new IImageData[num_rois];
            for (int i = 0; i < num_rois; i++)
                separated[i] = exposure.GetFrame(i, frame);
            return separated;
        }

        /// <summary>
        /// Generates a file name to save the data in, based on the inputs from the user
        /// </summary>
        /// <returns>A string of the filename</returns>
        private string generateFileName()
        {
            string path = EXPERIMENT.GetValue(ExperimentSettings.FileNameGenerationDirectory).ToString();
            string base_name = "\\" + EXPERIMENT.GetValue(ExperimentSettings.FileNameGenerationBaseFileName).ToString();
            string file_type = ".spe";

            // TODO:  Build more complex file name structure

            return path + base_name + file_type;
        }

        #endregion // Acquire Support Methods
    }
}
