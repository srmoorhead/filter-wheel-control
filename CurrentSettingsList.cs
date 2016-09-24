using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows; // for MessageBox
using System.Collections.ObjectModel; // for ObservableCollection
using System.IO;

using PrincetonInstruments.LightField.AddIns;

namespace FilterWheelControl
{
    public class FilterSetting
    {
        public string FilterType { get; set; }
        public double DisplayTime { get; set; }
        public double UserInputTime { get; set; }
        public double SlewAdjustedTime { get; set; }
        public int NumExposures { get; set; }
        public int OrderLocation { get; set; }
        public FilterSetting Next { get; set; }
    }
    
    public class CurrentSettingsList
    {
        #region Static Variables

        private static readonly double TRIGGER_SLEW_CORRECTION = 0.002; // seconds

        #endregion // Static Variables

        #region Instance Variables

        private ObservableCollection<FilterSetting> _filter_settings;
        private readonly object _current_settings_lock;
        private WheelInterface _wheel_interface;

        #endregion // Instance Variables

        #region Constructors

        /// <summary>
        /// Instantiate a new CurrentSettingsList object and set the initial settings list to be empty
        /// </summary>
        public CurrentSettingsList(WheelInterface wi)
        {
            this._filter_settings = new ObservableCollection<FilterSetting>();
            this._current_settings_lock = new object();
            this._wheel_interface = wi;
        }

        /// <summary>
        /// Instantiate a new CurrentSettingsList object and set the initial settings list to a pre-existing list
        /// </summary>
        /// <param name="settings">The settings to set as the CurrentSettingsList filter settings</param>
        public CurrentSettingsList(ObservableCollection<FilterSetting> settings)
        {
            this._filter_settings = settings;
            this._current_settings_lock = new object();
        }


        #endregion // Constructors

        #region Accessors

        /// <summary>
        /// Accessor for the filter settings ObservableCollection
        /// </summary>
        /// <returns>The ObservableCollection holding all of the Filter objects</returns>
        public ObservableCollection<FilterSetting> GetSettingsCollection() { return _filter_settings; }

        /// <summary>
        /// Build the file contents of a filter settings file
        /// </summary>
        /// <returns>A string holding the contents of the file</returns>
        public string GenerateFileContent()
        {
            string content = "";
            lock (_current_settings_lock)
            {
                foreach (FilterSetting f in _filter_settings)
                {
                    content += f.FilterType + '\t' + f.UserInputTime + '\t' + f.NumExposures + "\r\n";
                }
            }

            return content;
        }
   
        
        /// <summary>
        /// Retrieves all the capture settings.  More efficient than calling each setting accessor individually
        /// </summary>
        /// <returns>A Tuple holding the first FilterSetting and the number of frames in a sequence (not including transitions)</returns>
        public FilterSetting GetCaptureSettings() 
        {
            lock (_current_settings_lock)
            {
                for (int i = 1; i < _filter_settings.Count; i++)
                {
                    _filter_settings[i - 1].Next = _filter_settings[i];
                }
                _filter_settings[_filter_settings.Count - 1].Next = _filter_settings[0];
            }

            return _filter_settings[0];
        }

        /// <summary>
        /// Retrieves the value of the TRIGGER_SLEW_CORRECTION variable.
        /// </summary>
        /// <returns>TRIGGER_SLEW_CORRECTION</returns>
        public double GetTriggerSlewCorrection()
        {
            return TRIGGER_SLEW_CORRECTION;
        }

        #endregion // Accessors

        #region Modifiers

        /// <summary>
        /// Adds a Filter with the specified settings to the ObservableCollection _FILTER_SETTINGS
        /// </summary>
        /// <param name="filterType">The string holding the type of filter</param>
        /// <param name="time">A string holding the time, in seconds, of the exposure duration</param>
        /// <param name="frames">A string holding the number of consecutive frames of this exposure time and filter to take</param>
        /// <param name="slewAdjust">True if the times should be adjusted to account for trigger timing slew, false otherwise</param>
        /// <returns>true if the object was added, false otherwise</returns>
        public bool Add(object filterType, string time, string frames, bool slewAdjust)
        {
            // Calculate slew adjusted time
            double inputTime;
            double slewTime;
            if (ValidInputTime(time))
            {
                inputTime = Convert.ToDouble(time);
                if ((inputTime % 1 == 0) && (inputTime > 0))
                    slewTime = inputTime - TRIGGER_SLEW_CORRECTION;
                else if ((inputTime % 1 > .998) && (inputTime % 1 < 1))
                    slewTime = inputTime + (1 - (inputTime % 1)) - TRIGGER_SLEW_CORRECTION;
                else
                    slewTime = inputTime;
            }
            else
                return false;

            // Validate other inputs and add filter
            if (ValidNumFrames(frames) && ValidFilter(filterType))
            {
                lock (_current_settings_lock)
                {
                    int newIndex = _filter_settings.Count + 1;
                    _filter_settings.Add(new FilterSetting
                    {
                        FilterType = filterType.ToString(),
                        DisplayTime = slewAdjust ? slewTime : inputTime,
                        UserInputTime = inputTime,
                        SlewAdjustedTime = slewTime,
                        NumExposures = Convert.ToInt16(frames),
                        OrderLocation = newIndex
                    });
                }

                return true;
            }
            return false;
        }

        /// <summary>
        /// Edits a filter with the specified settings in the ObservableCollection _FILTER_SETTINGS
        /// </summary>
        /// <param name="toBeChanged">The object in _FILTER_SETTINGS to be changed </param>
        /// <param name="filterType">The new type of the filter</param>
        /// <param name="time">The new exposure time for the filter (in seconds)</param>
        /// <param name="frames">The new number of frames to consecutively capture with these settings</param>
        /// <param name="slewAdjust">True if the times should be adjusted to account for trigger timing slew, false otherwise</param>
        /// <returns>true if the edit occurred, false otherwise</returns>
        public bool Edit(FilterSetting toBeChanged, object filterType, string time, string frames, bool slewAdjust)
        {
            // Calculate slew adjusted time, if necessary
            double inputTime;
            double slewTime;
            if (ValidInputTime(time))
            {
                inputTime = Convert.ToDouble(time);
                if ((inputTime % 1 == 0) && (inputTime > 0))
                    slewTime = inputTime - TRIGGER_SLEW_CORRECTION;
                else
                    slewTime = inputTime;
            }
            else
                return false;

            // Validate other inputs and edit filter
            if (ValidNumFrames(frames) && ValidFilter(filterType))
            {
                lock (_current_settings_lock)
                {
                    toBeChanged.FilterType = filterType.ToString();
                    toBeChanged.DisplayTime = slewAdjust ? slewTime : inputTime;
                    toBeChanged.UserInputTime = inputTime;
                    toBeChanged.SlewAdjustedTime = slewTime;
                    toBeChanged.NumExposures = Convert.ToInt16(frames);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Delete the selected items from the FilterSettingsList
        /// </summary>
        /// <param name="selected">The selected items to delete</param>
        public void DeleteSelected(System.Collections.IList selected)
        {
            lock (_current_settings_lock)
            {
                int numSelected = selected.Count;
                for (int i = 0; i < numSelected; i++)
                {
                    _filter_settings.Remove(((FilterSetting)selected[0]));
                }

                UpdateLocVals();
            }
        }

        /// <summary>
        /// Update the indices in the list to reflect changes in ordering, additions, or deletions
        /// </summary>
        private void UpdateLocVals()
        {
            for (int i = 0; i < _filter_settings.Count; i++)
            {
                _filter_settings[i].OrderLocation = i + 1;
            }
        }

        #region Validate Inputs

        /// <summary>
        /// Checks if the entered exposure time in the ExposureTime textbox is valid.
        /// </summary>
        /// <param name="input">The text entered in the InputTime textbox</param>
        /// <returns>true if the entered value is a valid time, false otherwise</returns>
        private static bool ValidInputTime(string input)
        {
            double UserInputTime;
            try
            {
                UserInputTime = Convert.ToDouble(input);
            }
            catch (FormatException)
            {
                MessageBox.Show("Exposure Time must be a number.\nPlease ensure the entered value is a number zero or greater.");
                return false;
            }
            if (Convert.ToDouble(input) < 0)
            {
                MessageBox.Show("Exposure Time must be 0 seconds or greater.\nPlease ensure the entered value is a number zero or greater.");
                return false;
            }
            if (input == "NaN")
            {
                MessageBox.Show("Nice try.  Please enter a number 0 seconds or greater.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Checks if the entered number of frames in the NumFrames textbox is valid.
        /// </summary>
        /// <param name="input">The text entered in the NumFrames textbox</param>
        /// <returns>true if the entered value is a valid number, false otherwise</returns>
        private static bool ValidNumFrames(string input)
        {
            double UserNumFrames;
            try
            {
                UserNumFrames = Convert.ToInt16(input);
            }
            catch (FormatException)
            {
                MessageBox.Show("The number of frames must be an integer number.\nPlease ensure the entered value is an integer number greater than zero.");
                return false;
            }
            if (Convert.ToInt16(input) <= 0)
            {
                MessageBox.Show("The number of frames must be greater than zero.\nPlease ensure the entered value is a number greater than zero.");
                return false;
            }
            if (input == "NaN")
            {
                MessageBox.Show("Nice try.  Please enter a number greater than zero.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Checks that the user has selected a filter in the FilterSelectionBox
        /// </summary>
        /// <param name="f">The combo box object representing the selected choice in the FilterSelectionBox</param>
        /// <returns>false if the object is null, true otherwise</returns>
        private static bool ValidFilter(object f)
        {
            // Ensure user selected a filter
            if (f == null || f.ToString() == "")
            {
                MessageBox.Show("You must select a filter.\nPlease ensure you have selected a filter from the drop down menu.");
                return false;
            }
            return true;
        }

        #endregion // Validate Inputs

        #endregion // Modifiers

        #region File IO

        /// <summary>
        /// Writes a string of content to a .dat file
        /// </summary>
        /// <param name="content">The string of information to be written to the file</param>
        /// <param name="filename">The location to save the file.  Can be provided or null.  If null, the user will be prompted.</param>
        public void CurrentSettingsSave(bool adjusted, string filename = null)
        {
            string content = this.GenerateFileContent();

            // Add the adjusted flag
            if (adjusted)
                content = ControlPanel._TRIGGER_ADJUSTED_STRING + "\r\n" + content;
            else
                content = ControlPanel._TRIGGER_UNALTERED_STRING + "\r\n" + content;
            
            try
            {
                // If no filename was given, ask the user to provide one
                if (filename == null)
                {
                    // Configure save file dialog box
                    Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                    dlg.FileName = "FilterSettings"; // Default file name
                    dlg.DefaultExt = ".dat"; // Default file extension
                    dlg.Filter = "Filter data files (.dat)|*.dat"; // Filter files by extension

                    Nullable<bool> result = dlg.ShowDialog();

                    if (result == true)
                        filename = dlg.FileName;
                }
                else
                    filename += "_FilterSettings.dat";
                
                // Now, if we've got a filename, save the file.
                if (filename != null)
                {
                    FileStream output = File.Create(filename.ToString());
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
        /// Loads a file containing filter and timing information into the current settings list.
        /// </summary>
        public string CurrentSettingsLoad()
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

        #endregion // File IO
    }
}
