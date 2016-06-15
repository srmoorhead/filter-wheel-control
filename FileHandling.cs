using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PrincetonInstruments.LightField.AddIns;
using System.Windows;
using System.IO;

namespace FilterWheelControl.FileFunctions
{
    class FileHandling
    {
        #region FITS exporting

        private static readonly int DIRECTORY_INDEX = 0; // the index where the Directory information is stored
        private static readonly int FILENAME_INDEX = 1; // the index where the filename information is stored
        private static readonly string FITS_EXTENSION = ".fits"; // the file extension for a fits file
        private static readonly string SPE_EXTENSION = ".spe"; // the file extension for an spe file

        /// <summary>
        /// Given a frame, exports the frame as a FITS file, separating the frame into multiple fits files, one for each ROI.
        /// </summary>
        /// <param name="fnum">The current frame number.  Used to numerically order the frames and avoid overwriting.</param>
        /// <param name="frame">The frame to be saved.</param>
        /// <param name="app">The LightField Application object, used for gathering system settings.</param>
        public static bool exportFITSFrame(int fnum, IImageDataSet frame, ILightFieldApplication app, int padSize)
        {
            // Get the experiment object
            IExperiment exp = app.Experiment;

            // Ensure the output file name doesn't conflict with an existing file
            string[] nameInfo = retrieveFileNameInfo(exp);
            string baseFile = nameInfo[DIRECTORY_INDEX] + "\\" + nameInfo[FILENAME_INDEX] + "-Frame-" + fnum.ToString().PadLeft(padSize, '0'); ;
            string exportLoc =  baseFile + FITS_EXTENSION;
            if (File.Exists(exportLoc))
            {
                MessageBox.Show("With the given file name settings, you will be overwriting existing data.\nPlease change the directory or filename, or delete the existing files, and try again.\n\nThe current export location is: " + exportLoc, "Export Error - Pre-Existing File");
                return false;
            }
            
            // Save the frame to a temporary .spe file
            IFileManager filemgr = app.FileManager;
            ImageDataFormat format = (ImageDataFormat)exp.GetValue(CameraSettings.AcquisitionPixelFormat);
            RegionOfInterest[] regions = exp.SelectedRegions;

            string tempFileName = baseFile + SPE_EXTENSION;
            buildTempFile(format, regions, frame, tempFileName, filemgr);
            
            // Build export settings object
            IFitsExportSettings settings = (IFitsExportSettings)filemgr.CreateExportSettings(ExportFileType.Fits);
            settings.IncludeAllExperimentInformation = true;

            settings.CustomOutputPath = nameInfo[DIRECTORY_INDEX];
            
            settings.OutputPathOption = ExportOutputPathOption.CustomPath;
            settings.OutputMode = ExportOutputMode.OneFilePerRoiPerFrame;

            // Validate the output to ensure no errors
            
            if(validateOutput(settings, tempFileName))
            {
                // Export the file
                filemgr.Export((IExportSettings)settings, tempFileName);

                // Delete the temp file
                File.Delete(tempFileName);
                
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Saves one frame of data to an .spe file with filename name
        /// </summary>
        /// <param name="app">The current LightField Application</param>
        /// <param name="filemgr">The FileManager associated with the LightField Application</param>
        /// <param name="frame">The frame to be saved.</param>
        /// <param name="name">The name of the file to save.</param>
        private static void buildTempFile(ImageDataFormat format, RegionOfInterest[] regions, IImageDataSet frame, string name, IFileManager filemgr)
        {
            if (File.Exists(name))
                File.Delete(name);

            IImageDataSet data = filemgr.CreateFile(name, regions, 1, format);

            // Copy the data over to the file
            int numROIs = regions.Count();
            IImageData[] split_frame = separateROIs(frame, numROIs, 0);
            IImageData[] split_file = separateROIs(data, numROIs, 0);
            for (int i = 0; i < numROIs; i++)
                split_file[i].SetData(split_frame[i].GetData());
        }

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
        /// Returns the string representing the user-specified file name as entered in the LightField settings pane.
        /// Note that this DOES NOT include the file type (i.e. .spe, .fits, etc.).
        /// </summary>
        /// <param name="exp">The current LightField Experiment</param>
        /// <returns>The string, including directory, of the file name</returns>
        private static string[] retrieveFileNameInfo(IExperiment exp)
        {
            string directory = exp.GetValue(ExperimentSettings.FileNameGenerationDirectory).ToString();
            string base_name = exp.GetValue(ExperimentSettings.FileNameGenerationBaseFileName).ToString();

            // TODO:  Finish the date and time and increment handling
            /*
            DateTime thisDay = DateTime.Today;
            if ((bool)exp.GetValue(ExperimentSettings.FileNameGenerationAttachDate))
            {
                base_name += ' ' + thisDay.Date.ToString();
            }
            */

            return new string[2]{directory, base_name};
        }

        #region Validate Fits Export

        /// <summary>
        /// Validates the settings of an export and handles any errors
        /// </summary>
        /// <param name="settings">The IFitsExportSettings to be validated</param>
        /// <param name="fname">The temporary file where the data is currently stored</param>
        /// <returns>false if there are export errors as generated by IFitsExportSettings.Validate(), true otherwise</returns>
        private static bool validateOutput(IFitsExportSettings settings, string fname)
        {
            List<string> files = new List<string>();
            files.Add(fname);
            IList<IExportSelectionError> export_errors = settings.Validate(files);

            if (export_errors.Count > 0)
            {
                displayExportErrors(export_errors);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Displays export errors to the user
        /// </summary>
        /// <param name="export_errors">An IList of type IExportSelectionError - the IList returned by the IFitsExportSettings.Validate() method</param>
        private static void displayExportErrors(IList<IExportSelectionError> export_errors)
        {
            string errors = "There are errors in the export settings.  The errors are:\n";
            foreach (IExportSelectionError e in export_errors)
            {
                errors += e.ToString() + "\n";
            }
            errors += "Acquisition will be halted.";
            MessageBox.Show(errors);
        }

        #endregion // Validate Fits Export

        #endregion // FITS Exporting

        #region CurrentSettings IO

        /// <summary>
        /// Writes a string of content to a .dat file
        /// </summary>
        /// <param name="content">The string of information to be written to the file</param>
        public static void CurrentSettingsSave(string content)
        {
            try
            {

                // Configure save file dialog box
                Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                dlg.FileName = "FilterSettings"; // Default file name
                dlg.DefaultExt = ".dat"; // Default file extension
                dlg.Filter = "Filter data files (.dat)|*.dat"; // Filter files by extension

                // Show save file dialog box
                Nullable<bool> result = dlg.ShowDialog();

                // Process save file dialog box results
                if (result == true)
                {
                    // Save document
                    string filename = dlg.FileName;

                    FileStream output = File.Create(filename);
                    Byte[] info = new UTF8Encoding(true).GetBytes(content);

                    output.Write(info, 0, info.Length);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("There was an error saving your file.  See info here:\n\n" + ex.ToString());
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public static string CurrentSettingsLoad()
        {
            try
            {
                // Configure open file dialog box
                Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
                dlg.FileName = "FilterSettings"; // Default file name
                dlg.DefaultExt = ".dat"; // Default file extension
                dlg.Filter = "Filter data files (.dat)|*.dat"; // Filter files by extension

                // Show open file dialog box
                Nullable<bool> result = dlg.ShowDialog();

                // Process open file dialog box results
                if (result == true)
                {
                    // Open document
                    string filename = dlg.FileName;

                    byte[] bytes;

                    using (FileStream fsSource = new FileStream(filename, FileMode.Open, FileAccess.Read))
                    {

                        // Read the source file into a byte array.
                        bytes = new byte[fsSource.Length];
                        int numBytesToRead = (int)fsSource.Length;
                        int numBytesRead = 0;
                        while (numBytesToRead > 0)
                        {
                            // Read may return anything from 0 to numBytesToRead.
                            int n = fsSource.Read(bytes, numBytesRead, numBytesToRead);

                            // Break when the end of the file is reached.
                            if (n == 0)
                                break;

                            numBytesRead += n;
                            numBytesToRead -= n;
                        }
                    }

                    string return_val = System.Text.Encoding.UTF8.GetString(bytes, 0, bytes.Length);

                    return return_val;
                }
            }
            catch (FileNotFoundException e)
            {
                MessageBox.Show("There was an error reading in the file.  See info here:\n\n" + e.Message);
            }
            return null;
        }

        #endregion // CurrentSettings IO
    }
}
