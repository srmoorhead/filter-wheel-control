using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows; // for MessageBox
using System.Collections.ObjectModel; // for ObservableCollection

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
        private static readonly double MS_IN_12_HRS = 3600000 * 12; // milliseconds in 12 hours, = 4.32e+7

        #endregion // Static Variables

        #region Instance Variables

        private ObservableCollection<FilterSetting> _filter_settings;
        private readonly object _current_settings_lock;

        #endregion // Instance Variables

        #region Constructors

        /// <summary>
        /// Instantiate a new CurrentSettingsList object and set the initial settings list to be empty
        /// </summary>
        public CurrentSettingsList()
        {
            this._filter_settings = new ObservableCollection<FilterSetting>();
            this._current_settings_lock = new object();
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
        /// Provides the max index, a double[] of exposure times, a string[] of filter types, the time-generated zero padding val, and an array of the filter exposure group sizes from the current FILTER_SETTINGS variable
        /// Supports both the acquire_images and preview_images threads
        /// </summary>
        /// <returns>A Tuple with Item1 = int max_index and Item2 = double[] exposure_times and Item3 = string[] filter_types and Item4 = </returns>
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
        /// Returns the total number of frames to be captured per loop through the Current Settings list
        /// </summary>
        /// <returns>Total number of frames per loop</returns>
        public int FramesPerCycle()
        {
            int total_num_frames = 0;
            lock (_current_settings_lock)
            {
                foreach (FilterSetting fs in _filter_settings)
                {
                    total_num_frames += fs.NumExposures;
                }
            }
            return total_num_frames;
        }

        /// <summary>
        /// Returns the total cycle time in milliseconds for one full settings sequence
        /// </summary>
        /// <returns>A double representing the number of milliseconds in one full setting sequence</returns>
        public double TotalCycleTime()
        {
            double time = 0;
            lock (_current_settings_lock)
            {
                foreach (FilterSetting fs in _filter_settings)
                    time += fs.DisplayTime;
            }
            return time;
        }

        /// <summary>
        /// Returns the ceiling of log10 of the number of frames possible in 12 hours
        /// </summary>
        /// <returns>Ceiling of log10 of the number of frames possible in 12 horus</returns>
        public int ZeroPaddingVal()
        {
            double cycles = MS_IN_12_HRS / TotalCycleTime();
            return Convert.ToInt32(Math.Ceiling(Math.Log10(cycles * FramesPerCycle())));
        }

        /// <summary>
        /// Retrieves all the capture settings.  More efficient than calling each setting accessor individually
        /// </summary>
        /// <returns>A Tuple holding the first FilterSetting, the number of frames in a sequence, and the zero pad value</returns>
        public Tuple<FilterSetting, int, int> GetAllCaptureSettings() 
        {
            double time;
            int frames; 

            lock (_current_settings_lock)
            {
                time = _filter_settings[0].DisplayTime;
                frames = _filter_settings[0].NumExposures;

                for (int i = 1; i < _filter_settings.Count; i++)
                {
                    _filter_settings[i - 1].Next = _filter_settings[i];
                    time += _filter_settings[i].DisplayTime * 1000.0 * _filter_settings[i].NumExposures; // convert to ms
                    frames += _filter_settings[i].NumExposures;
                }
                _filter_settings[_filter_settings.Count - 1].Next = _filter_settings[0];
            }

            double cycles = MS_IN_12_HRS / time;
            int padVal = Convert.ToInt32(Math.Ceiling(Math.Log10(cycles * frames)));

            return new Tuple<FilterSetting, int, int>(_filter_settings[0], frames, padVal);
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
    }
}
