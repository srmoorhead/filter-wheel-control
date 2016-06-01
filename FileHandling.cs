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
        private static int DIRECTORY = 0; // the index where the Directory information is stored
        private static int FILENAME = 1; // the index where the filename information is stored

        /// <summary>
        /// Given a frame, exports the frame as a FITS file, separating the frame into multiple fits files, one for each ROI.
        /// </summary>
        /// <param name="fnum">The current frame number.  Used to numerically order the frames and avoid overwriting.</param>
        /// <param name="frame">The frame to be saved.</param>
        /// <param name="app">The LightField Application object, used for gathering system settings.</param>
        public static bool exportFITSFrame(int fnum, IImageDataSet frame, ILightFieldApplication app)
        {
            // Get the experiment object
            IExperiment exp = app.Experiment;

            // Ensure the output file name doesn't conflict with an existing file
            string[] nameInfo = retrieveFileNameInfo(exp);
            string exportLoc = nameInfo[DIRECTORY] + "\\" + nameInfo[FILENAME] + "-Frame-" + fnum + ".fits";
            if (File.Exists(exportLoc))
            {
                MessageBox.Show("With the given file name settings, you will be overwriting existing data.\nPlease change the directory or filename, or delete the existing files, and try again.\n\nThe current export location is: " + exportLoc, "Export Error - Pre-Existing File");
                return false;
            }
            
            // Save the frame to a temporary .spe file
            IFileManager filemgr = app.FileManager;
            ImageDataFormat format = (ImageDataFormat)exp.GetValue(CameraSettings.AcquisitionPixelFormat);
            RegionOfInterest[] regions = exp.SelectedRegions;

            string tempFileName = nameInfo[DIRECTORY] + "\\" + nameInfo[FILENAME] + "_" + fnum + ".spe";
            buildTempFile(format, regions, frame, tempFileName, filemgr);
            
            // Build export settings object
            IFitsExportSettings settings = (IFitsExportSettings)filemgr.CreateExportSettings(ExportFileType.Fits);
            settings.IncludeAllExperimentInformation = true;

            settings.CustomOutputPath = nameInfo[DIRECTORY];
            
            settings.OutputPathOption = ExportOutputPathOption.CustomPath;
            settings.OutputMode = ExportOutputMode.OneFilePerRoiPerFrame;

            // Validate the output to ensure no errors
            List<string> files = new List<string>();
            files.Add(tempFileName);
            IList<IExportSelectionError> export_errors = settings.Validate(files);

            if (export_errors.Count > 0)
            {
                string errors = "There are errors in the export settings.  The errors are:\n";
                foreach (IExportSelectionError e in export_errors)
                {
                    errors += e.ToString() + "\n";
                }
                errors += "Acquisition will be halted.";
                MessageBox.Show(errors);
                return false;
            }
            else
            {
                // Export the file
                filemgr.Export((IExportSettings)settings, tempFileName);

                // Delete the temp file
                File.Delete(tempFileName);
                
                return true;
            }
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

            return new string[2]{directory, base_name};
        }
    }
}
