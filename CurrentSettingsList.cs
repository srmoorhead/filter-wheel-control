using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows; // for MessageBox
using System.Collections.ObjectModel; // for ObservableCollection

namespace FilterWheelControl.SettingsList
{
    public class Filter
    {
        public string FilterType { get; set; }
        public double DisplayTime { get; set; }
        public double UserInputTime { get; set; }
        public double SlewAdjustedTime { get; set; }
        public int NumExposures { get; set; }
        public int OrderLocation { get; set; }
    }
    
    public class CurrentSettingsList
    {
        #region Instance Variables

        private static volatile ObservableCollection<Filter> _FILTER_SETTINGS = new ObservableCollection<Filter>();
        private static readonly object CURRENT_SETTINGS_LOCK = new object();

        private static double TRIGGER_SLEW_CORRECTION = 0.002; // seconds

        #endregion // Instance Variables

        public static ObservableCollection<Filter> FilterSettings
        { get { return _FILTER_SETTINGS; } }

        #region Add and Edit

        /// <summary>
        /// Adds a Filter with the specified settings to the ObservableCollection _FILTER_SETTINGS
        /// </summary>
        /// <param name="filterType">The string holding the type of filter</param>
        /// <param name="time">A string holding the time, in seconds, of the exposure duration</param>
        /// <param name="frames">A string holding the number of consecutive frames of this exposure time and filter to take</param>
        /// <param name="slewAdjust">True if the times should be adjusted to account for trigger timing slew, false otherwise</param>
        /// <returns>true if the object was added, false otherwise</returns>
        public static bool Add(object filterType, string time, string frames, bool slewAdjust)
        {
            // Calculate slew adjusted time if necessary
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
                lock (CURRENT_SETTINGS_LOCK)
                {
                    int newIndex = _FILTER_SETTINGS.Count + 1;
                    _FILTER_SETTINGS.Add(new Filter
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
        public static bool Edit(Filter toBeChanged, object filterType, string time, string frames, bool slewAdjust)
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
                lock (CURRENT_SETTINGS_LOCK)
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
            if (f == null)
            {
                MessageBox.Show("You must select a filter.\nPlease ensure you have selected a filter from the drop down menu.");
                return false;
            }

            return true;
        }

        #endregion // Validate Inputs

        #endregion // Add and Edit

        #region Delete

        public static void DeleteSelected(System.Collections.IList selected)
        {
            lock (CURRENT_SETTINGS_LOCK)
            {
                int numSelected = selected.Count;
                for (int i = 0; i < numSelected; i++)
                {
                    _FILTER_SETTINGS.Remove(((Filter)selected[0]));
                }

                UpdateLocVals();
            }
        }

        /// <summary>
        /// Update the indices in the list to reflect changes in ordering, additions, or deletions
        /// </summary>
        private static void UpdateLocVals()
        {
            for (int i = 0; i < _FILTER_SETTINGS.Count; i++)
            {
                _FILTER_SETTINGS[i].OrderLocation = i + 1;
            }
        }

        #endregion //Delete

        #region Generate Runtime Variables

        /// <summary>
        /// Provides the max index, and string[] of filter types and a double[] of exposure times from the current FILTER_SETTINGS variable
        /// Supports both the acquire_images and preview_images threads
        /// </summary>
        /// <returns>A Tuple with Item1 = int max_index and Item2 = double[] exposure_times and Item3 = string[] filter_types</returns>
        public static Tuple<int, double[], string[]> getRunVars()
        {
            int max_index;
            double[] exposure_times;
            string[] filter_types;
            
            lock (CURRENT_SETTINGS_LOCK)
            {
                int total_num_frames = totalFrames(_FILTER_SETTINGS);

                max_index = total_num_frames - 1;
                exposure_times = new double[total_num_frames];
                filter_types = new string[total_num_frames];

                int index = 0;
                for (int frame = 0; frame < _FILTER_SETTINGS.Count; frame++)
                {
                    for (int exposure = 0; exposure < _FILTER_SETTINGS[frame].NumExposures; exposure++)
                    {
                        exposure_times[index] = _FILTER_SETTINGS[frame].UserInputTime * 1000.0; // Convert from s to ms
                        filter_types[index] = _FILTER_SETTINGS[frame].FilterType;
                        index++;
                    }
                }
            }

            return new Tuple<int, double[], string[]>(max_index, exposure_times, filter_types);
        }

        /// <summary>
        /// Returns the total number of frames to be captured per loop through the Current Settings list
        /// </summary>
        /// <param name="settingsList">An ObservableCollection of Filter objects holding the User's desired filter settings</param>
        /// <returns>Total number of frames per loop</returns>
        private static int totalFrames(ObservableCollection<Filter> settingsList)
        {
            int total_num_frames = 0;
            for (int i = 0; i < settingsList.Count; i++)
            {
                total_num_frames += settingsList[i].NumExposures;
            }
            return total_num_frames;
        }

        #endregion // Generate Runtime Variables

        #region Generate File Content

        /// <summary>
        /// Build the file contents of a filter settings file
        /// </summary>
        /// <returns>A string holding the contents of the file</returns>
        public static string GenerateFileContent()
        {
            string content = "";
            lock (CURRENT_SETTINGS_LOCK)
            {
                foreach (Filter f in FilterSettings)
                {
                    content += f.FilterType + '\t' + f.UserInputTime + '\t' + f.NumExposures + "\r\n";
                }
            }

            return content;
        }

        #endregion // Generate File Content
    }
}
