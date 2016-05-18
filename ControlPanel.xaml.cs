using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WPF.JoshSmith.ServiceProviders.UI; // For ListView DragDrop Manager

using System.Collections.ObjectModel;
using PrincetonInstruments.LightField.AddIns;
using System.Threading;
using System.Windows.Threading;


namespace FilterWheelControl.ControlPanelFunctions
{
    #region Filter Class

    /// <summary>
    /// Interaction logic for ControlPanel.xaml
    /// </summary>
    public class Filter
    {
        public string FilterType { get; set; }
        public double ExposureTime { get; set; }
        public int NumExposures { get; set; }
        public int OrderLocation { get; set; }
    }

    #endregion // Filter Class

    public partial class ControlPanel : UserControl
    {

        #region Instance Variables

        // Constants
        static string InputTimeTextbox_DEFAULT_TEXT = "Exposure Time (s)"; // Change this string if you wish to change the text in the InputTime textbox
        static string NumFramesTextbox_DEFAULT_TEXT = "Num"; // Change this string if you wish to change the text in the NumFrames textbox
        static string[] AVAILABLE_FILTERS = { "u", "g", "r", "i", "z", "BG40", "DARK" }; // Change this array if any filters get changed
        
        // LightField Variables
        IExperiment EXPERIMENT;
        ILightFieldApplication _APP;

        // User-Input Filter Parameters
        ObservableCollection<Filter> _FILTER_SETTINGS = new ObservableCollection<Filter>();
        
        // Instance Variables for Run and Acquire Threads
        volatile bool _STOP;
        volatile bool _IS_RUNNING;

        #endregion // Instance Variables

        #region Initialize Control Panel

        /////////////////////////////////////////////////////////////////
        ///
        /// Control Panel Initialization
        ///
        /////////////////////////////////////////////////////////////////
        
        public ControlPanel(ILightFieldApplication app)
        {      
            InitializeComponent();

            EXPERIMENT = app.Experiment;
            _APP = app;

            new ListViewDragDropManager<Filter>(this.CurrentSettings);

            // Populate the Filter Selection Box and Jump Selection Box with the available filters
            for (int i = 0; i < AVAILABLE_FILTERS.Length; i++)
            {
                FilterSelectionBox.Items.Add(AVAILABLE_FILTERS[i]);
                JumpSelectionBox.Items.Add(AVAILABLE_FILTERS[i]);
            }
        }

        #endregion // Initialize Control Panel

        public ObservableCollection<Filter> FilterSettings
        { get { return _FILTER_SETTINGS; } }

        #region Manual Control

        /////////////////////////////////////////////////////////////////
        ///
        /// Methods for Manual Filter Control
        ///
        /////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sends a signal to the filter wheel to rotate the wheel counterclockwise (w.r.t. the camera)
        /// </summary>
        private void CCW_Click(object sender, RoutedEventArgs e)
        {
            if (this.ManualControl.IsChecked == true)
                MessageBox.Show("This control is enabled");
            else
                ManualControlDisabledMessage();
        }

        /// <summary>
        /// Sends a signal to the filter wheel to rotate the wheel clockwise (w.r.t. the camera)
        /// </summary>
        private void CW_Click(object sender, RoutedEventArgs e)
        {
            if (ManualControl.IsChecked == true)
                MessageBox.Show("This control is enabled");
            else
                ManualControlDisabledMessage();
        }

        /// <summary>
        /// Sends a signal to the filter wheel to rotate the wheel to the filter selected in the JumpSelectionBox drop down menu
        /// </summary>
        private void JumpButton_Click(object sender, RoutedEventArgs e)
        {
            if (ManualControl.IsChecked == true)
                MessageBox.Show("I cannot jump.  I am a computer.\n(This control is enabled).");
            else
                ManualControlDisabledMessage();
        }

        /// <summary>
        /// Displays an error message anytime a manual control button is pressed, but manual control is disabled.
        /// </summary>
        private static void ManualControlDisabledMessage()
        {
            MessageBox.Show("This control is disabled.  Please enable Manual Control.");
        }

        #endregion // Manual Control

        #region Automated Control

        /////////////////////////////////////////////////////////////////
        ///
        /// Methods for Automated Control of the Filter Wheel
        ///
        /////////////////////////////////////////////////////////////////

        #region Current Settings List

        /////////////////////////////////////////////////////////////////
        ///
        /// Methods for Populating and Editing the Current Settings List
        ///
        /////////////////////////////////////////////////////////////////

        /// <summary>
        /// Runs when Add or Edit button is clicked (they are the same button)
        /// </summary>
        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            Add();
        }

        /// <summary>
        /// Triggers an Add or Edit when Enter or Return are hit and the InputTime box has focus
        /// </summary>
        private void InputTime_KeyDown(object sender, KeyEventArgs e)
        {
            if (Key.Enter == e.Key || Key.Return == e.Key)
            {
                Add();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Triggers and Add or Edit when Enter or Return are hit and the NumFrames box has focus
        /// </summary>
        private void NumFrames_KeyDown(object sender, KeyEventArgs e)
        {
            if (Key.Enter == e.Key || Key.Return == e.Key)
            {
                Add();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Adds a filter to the Current Settings List based on the parameters input by the user
        /// </summary>
        private void Add()
        {
            // Ensure user selected a filter
            if (this.FilterSelectionBox.SelectedValue == null)
            {
                MessageBox.Show("You must select a filter.\nPlease ensure you have selected a filter from the drop down menu.");
                return;
            }
            
            // Ensure user entered valid values in the exposure time input box and number of frames input box
            if (!ValidInputTime() || !ValidNumFrames())
                return;

            if (this.AddButton.Content.ToString() == "Add")
            {
                // We are adding a new filter to the list
                int newIndex = _FILTER_SETTINGS.Count + 1;
                _FILTER_SETTINGS.Add(new Filter
                {
                    FilterType = this.FilterSelectionBox.SelectionBoxItem.ToString(),
                    ExposureTime = Convert.ToDouble(this.InputTime.Text),
                    NumExposures = Convert.ToInt16(this.NumFrames.Text),
                    OrderLocation = newIndex
                });
            }
            else
            {
                // We are changing a selected filter
                ((Filter)this.CurrentSettings.SelectedItem).FilterType = this.FilterSelectionBox.SelectionBoxItem.ToString();
                ((Filter)this.CurrentSettings.SelectedItem).ExposureTime = Convert.ToDouble(this.InputTime.Text);
                ((Filter)this.CurrentSettings.SelectedItem).NumExposures = Convert.ToInt16(this.NumFrames.Text);
                // Order Location remains the same

                this.CurrentSettings.Items.Refresh();

                //Reset button labels
                this.AddButton.Content = "Add";
                this.AddFilterLabel.Content = "Add Filter:";
            }

            // Reset the input buttons
            this.InputTime.Text = InputTimeTextbox_DEFAULT_TEXT;
            this.FilterSelectionBox.SelectedIndex = -1;
            this.NumFrames.Text = NumFramesTextbox_DEFAULT_TEXT;
        }

        /// <summary>
        /// Checks if the entered exposure time in the ExposureTime textbox is valid.
        /// </summary>
        /// <returns>true if the entered value is a valid time, false otherwise</returns>
        private bool ValidInputTime()
        {
            double UserInputTime;
            try
            {
                UserInputTime = Convert.ToDouble(this.InputTime.Text);
            }
            catch (FormatException)
            {
                MessageBox.Show("Exposure Time must be a number.\nPlease ensure the entered value is a number zero or greater.");
                return false;
            }
            if (Convert.ToDouble(this.InputTime.Text) < 0)
            {
                MessageBox.Show("Exposure Time must be 0 seconds or greater.\nPlease ensure the entered value is a number zero or greater.");
                return false;
            }
            if (this.InputTime.Text == "NaN")
            {
                MessageBox.Show("Nice try.  Please enter a number 0 seconds or greater.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Checks if the entered number of frames in the NumFrames textbox is valid.
        /// </summary>
        /// <returns>true if the entered value is a valid number, false otherwise</returns>
        private bool ValidNumFrames()
        {
            double UserNumFrames;
            try
            {
                UserNumFrames = Convert.ToDouble(this.NumFrames.Text);
            }
            catch (FormatException)
            {
                MessageBox.Show("The number of frames must be a number.\nPlease ensure the entered value is a number greater than zero.");
                return false;
            }
            if (Convert.ToDouble(this.NumFrames.Text) <= 0)
            {
                MessageBox.Show("The number of frames must be greater than zero.\nPlease ensure the entered value is a number greater than zero.");
                return false;
            }
            if (this.NumFrames.Text == "NaN")
            {
                MessageBox.Show("Nice try.  Please enter a number greater than zero.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Clears the text from the InputTime textbox when the box is selected and there is no pre-existing input
        /// </summary>
        private void InputTime_GotFocus(object sender, RoutedEventArgs e)
        {
            InputTime.Text = InputTime.Text == InputTimeTextbox_DEFAULT_TEXT ? string.Empty : InputTime.Text;
        }

        /// <summary>
        /// Resets the InputTime textbox if no text is entered, otherwise keeps the existing entered text
        /// </summary>
        private void InputTime_LostFocus(object sender, RoutedEventArgs e)
        {
            InputTime.Text = InputTime.Text == string.Empty ? InputTimeTextbox_DEFAULT_TEXT : InputTime.Text;
        }

        /// <summary>
        /// Clears the text from the NumFrames textbox when the box is selected and there is no pre-existing input
        /// </summary>
        private void NumFrames_GotFocus(object sender, RoutedEventArgs e)
        {
            NumFrames.Text = NumFrames.Text == NumFramesTextbox_DEFAULT_TEXT ? string.Empty : NumFrames.Text;
        }

        /// <summary>
        /// Resets the InputTime textbox if no text is entered, otherwise keeps teh existing entered text
        /// </summary>
        private void NumFrames_LostFocus(object sender, RoutedEventArgs e)
        {
            NumFrames.Text = NumFrames.Text == string.Empty ? NumFramesTextbox_DEFAULT_TEXT : NumFrames.Text;
        }

        /// <summary>
        /// Recognizes when the CurrentSettings window has focus and the backspace or delete keys are pressed
        /// </summary>
        private void CurrentSettings_KeyDown(object sender, KeyEventArgs e)
        {
            if (Key.Back == e.Key || Key.Delete == e.Key)
            {
                DeleteSelectedItems();
                UpdateLocVals();
                this.CurrentSettings.Items.Refresh();
                e.Handled = true;
            }
            e.Handled = true;
        }

        /// <summary>
        /// Deletes any selected items from the CurrentSettings items
        /// </summary>
        private void DeleteSelectedItems()
        {
            int numSelected = this.CurrentSettings.SelectedItems.Count;
            for (int i = 0; i < numSelected; i++)
            {
                _FILTER_SETTINGS.Remove(((Filter)this.CurrentSettings.SelectedItems[0]));
            }
        }

        /// <summary>
        /// Update the indices in the list to reflect changes in ordering
        /// </summary>
        private void UpdateLocVals()
        {
            for(int i = 0; i < _FILTER_SETTINGS.Count; i++)
            {
                _FILTER_SETTINGS[i].OrderLocation = i + 1;
            }
        }

        /// <summary>
        /// Runs when the Delete menu item is selected from the right-click menu in CurrentSettings
        /// </summary>
        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedItems();
            UpdateLocVals();
            this.CurrentSettings.Items.Refresh();
        }

        /// <summary>
        /// Changes the Add Filter settings to allow editing of a filter
        /// Triggered by clicking the Edit option in the R-Click menu on the Current Settings box
        /// </summary>
        private void EditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            this.AddButton.Content = "Save";
            this.AddFilterLabel.Content = "Edit Filter:";

            this.InputTime.Text = Convert.ToString(((Filter)this.CurrentSettings.SelectedItem).ExposureTime);
            this.FilterSelectionBox.SelectedItem = ((Filter)this.CurrentSettings.SelectedItem).FilterType;
            this.NumFrames.Text = Convert.ToString(((Filter)this.CurrentSettings.SelectedItem).NumExposures);
        }

        #endregion // Current Settings List

        /////////////////////////////////////////////////////////////////
        ///
        /// Methods for Override Buttons (Run, Acquire, Stop)
        ///
        /////////////////////////////////////////////////////////////////

        #region Run

        /// <summary>
        /// Runs when the "Run" button is clicked
        /// </summary>
        private void RunOverride_Click(object sender, RoutedEventArgs e)
        {
            if ((bool)AutomatedControl.IsChecked)
            {
                if (SystemReady())
                {
                    // Disable manual controls
                    this.ManualControl.IsHitTestVisible = false;

                    // Change the button's color so the user knows this feature is activated
                    this.AcquireOverride.ClearValue(Button.BackgroundProperty);
                    this.RunOverride.Background = Brushes.Green;
                    
                    // Begin running the preview_images thread until the user clicks Stop
                    _IS_RUNNING = true;
                    _STOP = false;
                    Thread preview = new Thread(preview_images);
                    preview.Start();
                }
            }
            else
                AutomatedControlDisabledMessage();
        }

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
            IDisplayViewer view = _APP.DisplayManager.GetDisplay(DisplayLocation.ExperimentWorkspace, 0);
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
                    // Something bad has happened.  Quit running.
                    HaltAcquisition();
                    Dispatcher.BeginInvoke(new Action(ResetUI));
                    return;
                }

                // Display image in View tab
                view.Display("Live Multi-Filter Data", frame);

                // Update current_index
                current_index = updateCurrentIndex(current_index, max_index);
            }
            _IS_RUNNING = false;
        }

        #endregion // Run

        #region Acquire

        /// <summary>
        /// Runs when the "Acquire" button is clicked
        /// </summary>
        private void AcquireOverride_Click(object sender, RoutedEventArgs e)
        {
            if ((bool)AutomatedControl.IsChecked)
            {
                if (SystemReady())
                {
                    // Disable manual controls
                    this.ManualControl.IsHitTestVisible = false;

                    // Update button colors so the user knows which features are active
                    this.AcquireOverride.Background = Brushes.Green;
                    this.RunOverride.ClearValue(Button.BackgroundProperty);

                    // Begin acquiring images
                    _IS_RUNNING = true;
                    _STOP = false;

                    // Run the capture sequence until Stop button is pressed (and _STOP bool changed) or max number of frames reached
                    Thread acquisition = new Thread(acquire_images);
                    acquisition.Start();
                }
            }
            else
                AutomatedControlDisabledMessage();
        }

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
            int THIS_FRAME = 0;

            // Capture Display functionality
            IDisplayViewer view = _APP.DisplayManager.GetDisplay(DisplayLocation.ExperimentWorkspace, 0);
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
                    // Something bad has happened.  Save the data and quit acquisition.
                    filemgr.CloseFile(data);
                    HaltAcquisition();
                    Dispatcher.BeginInvoke(new Action(ResetUI));
                    return;
                }

                // Save the data
                separated = separateROIs(captured_frame, num_regions, THIS_FRAME);
                to_be_saved = separateROIs(data, num_regions, current_frame);
                for (int i = 0; i < num_regions; i++)
                    to_be_saved[i].SetData(separated[i].GetData());

                // Display images in View tab
                view.Display("Live Multi-Filter Data", captured_frame);

                // Update current_index and current_frame
                current_index = updateCurrentIndex(current_index, max_index);
                current_frame++;
            }
            filemgr.CloseFile(data);
            _IS_RUNNING = false;
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

        #endregion //Acquire

        #region Support Methods for Run and Acquire
        
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
                MessageBox.Show("Camera not found.  Please ensure there is a camera attached to the system.");
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
            MessageBox.Show("There has been an error.  Halting Acquisition.\nYour data has been saved.");
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

        /// <summary>
        /// Updates the current exposure time and filter array index.
        /// Sets to zero if the current index is equal to the max index.
        /// Increments by 1 otherwise.
        /// </summary>
        /// <param name="i">The current index.</param>
        /// <param name="max">The maximum index in the array.</param>
        /// <returns>0 if i == max, i+1 otherwise</returns>
        private static int updateCurrentIndex(int i, int max)
        {
            if (i == max)
                return 0;
            else
                return i + 1;
        }

        /// <summary>
        /// Sends a trigger to the Filter Wheel to move the wheel to the specified filter
        /// </summary>
        /// <param name="f"></param>
        private static void RotateWheelToFilter(string f)
        {

        }

        #endregion // Support Methods for Run and Acquire

        #region Stop

        /// <summary>
        /// Runs when the "Stop" button is clicked
        /// </summary>
        private void StopOverride_Click(object sender, RoutedEventArgs e)
        {
            if ((bool)AutomatedControl.IsChecked)
            {
                if (EXPERIMENT == null)
                    return;

                _STOP = true;
                EXPERIMENT.Stop();

                ResetUI(); 
            }
            else
                AutomatedControlDisabledMessage();
        }

        /// <summary>
        /// Resets the UI after Stop
        /// </summary>
        public void ResetUI()
        {
            // Reactivate Manual Control features
            ManualControl.IsHitTestVisible = true;

            // Update button colors to the user knows which features are active
            AcquireOverride.ClearValue(Button.BackgroundProperty);
            RunOverride.ClearValue(Button.BackgroundProperty);
        }

        #endregion // Stop

        /// <summary>
        /// Displays an error message anytime an automated control button is pressed, but automated control is not activated.
        /// </summary>
        private static void AutomatedControlDisabledMessage()
        {
            MessageBox.Show("This control is disabled.  Please enable Automated Control.");
        }

        #endregion // Automated Control
    }
}
