using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PrincetonInstruments.LightField.AddIns;
using System.Windows;
using System.IO;
using System.Threading;

namespace FilterWheelControl
{
    class FileHandler
    {   
        #region Static Variables

        private static readonly int DIRECTORY_INDEX = 0;
        private static readonly int FILENAME_INDEX = 1;
        private static readonly string FITS_EXTENSION = ".fits"; // the file extension for a fits file
        private static readonly string SPE_EXTENSION = ".spe"; // the file extension for an spe file

        #endregion // Static Variables

        #region Instance Variables

        IExperiment _exp;
        IFileManager _file_mgr;

        #endregion // Instance Variables

        #region Constructors

        public FileHandler(IExperiment e, IFileManager fmgr)
        {
            this._exp = e;
            this._file_mgr = fmgr;
        }

        #endregion // Constructors

        #region Export Fits Frame

        /// <summary>
        /// Given a frame, exports the frame as a FITS file, separating the frame into multiple fits files, one for each ROI.
        /// </summary>
        /// <param name="fnum">The current frame number.  Used to numerically order the frames and avoid overwriting.</param>
        /// <param name="frame">The frame to be saved.</param>
        /// <param name="app">The LightField Application object, used for gathering system settings.</param>
        public bool ExportFITSFrame(IImageDataSet frame, int fnum, int padSize)
        {
            // Ensure the output file name doesn't conflict with an existing file
            string[] nameInfo = RetrieveFileNameInfo();
            string baseFile;
            if ((baseFile = CreateFileName(nameInfo, fnum, padSize)) == "ERROR")
                return false;

            // Save the frame to a temporary .spe file
            ImageDataFormat format = (ImageDataFormat)_exp.GetValue(CameraSettings.AcquisitionPixelFormat);
            RegionOfInterest[] regions = _exp.SelectedRegions;

            string tempFileName = baseFile + SPE_EXTENSION;
            BuildTempFile(format, regions, frame, tempFileName);
            
            // Build export settings object
            IFitsExportSettings settings = GenerateExportSettings(nameInfo);

            // Validate the output to ensure no errors
            return WriteFile(settings, tempFileName, _file_mgr);
        }

        /// <summary>
        /// Validates the output and exports the file
        /// </summary>
        /// <param name="settings">The IFitsExportSettings pertaining to this file</param>
        /// <param name="name">The name of the file after export</param>
        /// <param name="filemgr">The IFileManager object handling exporting</param>
        /// <returns>true if the export was a success, false otherwise</returns>
        private bool WriteFile(IFitsExportSettings settings, string name, IFileManager filemgr)
        {
            if (ValidateOutput(settings, name))
            {
                // Export the file
                filemgr.Export((IExportSettings)settings, name);

                // Delete the temp file
                try
                {
                    File.Delete(name);
                }
                catch (IOException)
                {
                    return false;
                }
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Builds the IFitsExportSettings object.
        /// IncludeAllExperimentInformation is set to true
        /// OutputPathOption is set to ExportOutputPathOption.CustomPath
        /// OutputMode is set to ExportOutputMode.OneFilePerRoiPerFrame
        /// CustomOutputPath is set to the directory the user specified
        /// </summary>
        /// <param name="nameInfo">An array containing the directory and the base file name</param>
        /// <param name="filemgr">The IFileManager object to use for exporting</param>
        /// <returns>The IFitsExportSettings generated with the given input</returns>
        private IFitsExportSettings GenerateExportSettings(string[] nameInfo)
        {
            IFitsExportSettings settings = (IFitsExportSettings)_file_mgr.CreateExportSettings(ExportFileType.Fits);
            settings.IncludeAllExperimentInformation = true;
            settings.CustomOutputPath = nameInfo[DIRECTORY_INDEX];
            settings.OutputPathOption = ExportOutputPathOption.CustomPath;
            settings.OutputMode = ExportOutputMode.OneFilePerRoiPerFrame;
            return settings;
        }

        /// <summary>
        /// Generate the filename for the exported file.
        /// </summary>
        /// <param name="nameInfo">An array containing the directory and the base file name</param>
        /// <param name="fnum">The frame number</param>
        /// <param name="padSize">The amount of zero padding to include before the frame number</param>
        /// <returns>The updated baseFileName</returns>
        private string CreateFileName(string[] nameInfo, int fnum, int padSize)
        {
            string baseFile = nameInfo[DIRECTORY_INDEX] + "\\" + nameInfo[FILENAME_INDEX] + "-Frame-" + fnum.ToString().PadLeft(padSize, '0'); ;
            string exportLoc = baseFile + FITS_EXTENSION;
            bool edited = false;
            string append = "";
            while (File.Exists(exportLoc))
            {
                append += "+";
                exportLoc = baseFile + append + FITS_EXTENSION;
                edited = true;
            }
            if (edited && fnum == 1)
            {
                MessageBoxResult useNewName = MessageBox.Show("A file already exists with the name " + baseFile + FITS_EXTENSION + "\n\nYour data will be saved as: " + exportLoc + "\n\nIs this okay?", "Export Error - Pre-Existing File", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (useNewName == MessageBoxResult.No)
                    return "ERROR";
            }
            return baseFile + append;
        }

        /// <summary>
        /// Saves one frame of data to an .spe file with filename name
        /// </summary>
        /// <param name="app">The current LightField Application</param>
        /// <param name="filemgr">The FileManager associated with the LightField Application</param>
        /// <param name="frame">The frame to be saved.</param>
        /// <param name="name">The name of the file to save.</param>
        private void BuildTempFile(ImageDataFormat format, RegionOfInterest[] regions, IImageDataSet frame, string name)
        {
            if (File.Exists(name))
                File.Delete(name);

            IImageDataSet data = _file_mgr.CreateFile(name, regions, 1, format);

            // Copy the data over to the file
            int numROIs = regions.Count();
            IImageData[] split_frame = SeparateROIs(frame, numROIs, 0);
            IImageData[] split_file = SeparateROIs(data, numROIs, 0);
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
        private IImageData[] SeparateROIs(IImageDataSet exposure, int num_rois, int frame)
        {
            IImageData[] separated = new IImageData[num_rois];
            for (int i = 0; i < num_rois; i++)
                separated[i] = exposure.GetFrame(i, frame);
            return separated;
        }

        #region Generate File Name

        /// <summary>
        /// Returns the string representing the user-specified file name as entered in the LightField settings pane.
        /// Note that this DOES NOT include the file type (i.e. .spe, .fits, etc.).
        /// </summary>
        /// <param name="_exp">The current LightField Experiment</param>
        /// <returns>The string, including directory, of the file name</returns>
        private string[] RetrieveFileNameInfo()
        {
            string directory = _exp.GetValue(ExperimentSettings.FileNameGenerationDirectory).ToString();
            string base_name = _exp.GetValue(ExperimentSettings.FileNameGenerationBaseFileName).ToString();

            string space = " ";
            if (_exp.GetValue(ExperimentSettings.FileNameGenerationAttachDate) != null)
            {
                if ((bool)_exp.GetValue(ExperimentSettings.FileNameGenerationAttachDate))
                {
                    if ((FileFormatLocation)_exp.GetValue(ExperimentSettings.FileNameGenerationFileFormatLocation) == FileFormatLocation.Prefix)
                        base_name = GetFormattedDate((DateFormat)_exp.GetValue(ExperimentSettings.FileNameGenerationDateFormat)) + space + base_name;
                    else
                        base_name += space + GetFormattedDate((DateFormat)_exp.GetValue(ExperimentSettings.FileNameGenerationDateFormat));
                }
            }

            if (_exp.GetValue(ExperimentSettings.FileNameGenerationAttachTime) != null)
            {
                if ((bool)_exp.GetValue(ExperimentSettings.FileNameGenerationAttachTime))
                {
                    if ((FileFormatLocation)_exp.GetValue(ExperimentSettings.FileNameGenerationFileFormatLocation) == FileFormatLocation.Prefix)
                        base_name = GetFormattedTime((TimeFormat)_exp.GetValue(ExperimentSettings.FileNameGenerationTimeFormat)) + space + base_name;
                    else
                        base_name += space + GetFormattedTime((TimeFormat)_exp.GetValue(ExperimentSettings.FileNameGenerationTimeFormat));
                }
            }
            return new string[2]{directory, base_name};
        }

        /// <summary>
        /// Given a time format, returns the current time in that format
        /// </summary>
        /// <param name="format">The TimeFormat object representing the desired format</param>
        /// <returns>The current time in the given format</returns>
        private string GetFormattedTime(TimeFormat format)
        {
            DateTime now = DateTime.Now;

            switch (format)
            {
                case TimeFormat.hh_mm_ss_24hr:
                    return now.ToString("HH_mm_ss");
                default:
                    return now.ToString("hh_mm_ss_tt", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Given a DateFormat, returns the date formatted in that manner.
        /// </summary>
        /// <param name="format">A DateFormat object representing how the date should be formatted</param>
        /// <returns>A string of the date represented in the given format</returns>
        private string GetFormattedDate(DateFormat format)
        {
            DateTime today = DateTime.Today;
                
            switch (format)
            {
                case DateFormat.dd_mm_yyyy:
                    return today.ToString("dd MM yyyy");
                case DateFormat.dd_Month_yyyy: 
                    return today.ToString("dd MMMM yyyy");
                case DateFormat.mm_dd_yyyy:
                    return today.ToString("MM dd yyyy");
                case DateFormat.Month_dd_yyyy:
                    return today.ToString("MMMM dd yyyy");
                case DateFormat.yyyy_mm_dd:
                    return today.ToString("yyyy MM dd");
                case DateFormat.yyyy_Month_dd:
                    return today.ToString("yyyy MMMM dd");
                default:
                    return today.ToString("yyyy MM dd");
            }
        }

        #endregion // Generate File Name

        #region Validate Fits Export

        /// <summary>
        /// Validates the settings of an export and handles any errors
        /// </summary>
        /// <param name="settings">The IFitsExportSettings to be validated</param>
        /// <param name="fname">The temporary file where the data is currently stored</param>
        /// <returns>false if there are export errors as generated by IFitsExportSettings.Validate(), true otherwise</returns>
        private bool ValidateOutput(IFitsExportSettings settings, string fname)
        {
            List<string> files = new List<string>();
            files.Add(fname);
            IList<IExportSelectionError> export_errors = settings.Validate(files);

            if (export_errors.Count > 0)
            {
                DisplayExportErrors(export_errors);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Displays export errors to the user
        /// </summary>
        /// <param name="export_errors">An IList of type IExportSelectionError - the IList returned by the IFitsExportSettings.Validate() method</param>
        private void DisplayExportErrors(IList<IExportSelectionError> export_errors)
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

        #endregion // Export Fits File

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
