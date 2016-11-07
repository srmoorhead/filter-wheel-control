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
using WPF.JoshSmith.ServiceProviders.UI; // for ListView DragDrop Manager
using PrincetonInstruments.LightField.AddIns; // for all LightField interactions
using System.Collections.ObjectModel; // for Observable Collection
using System.Collections.Specialized;
using System.Threading;

namespace FilterWheelControl
{

    /// <summary>
    /// Interaction logic for ControlPanel.xaml
    /// </summary>
    

    public partial class ControlPanel : UserControl
    {
        #region Static Variables

        private static readonly string InputTimeTextbox_DEFAULT_TEXT = "Exposure Time (s)"; // Change this string if you wish to change the text in the InputTime textbox
        private static readonly string NumFramesTextbox_DEFAULT_TEXT = "Num"; // Change this string if you wish to change the text in the NumFrames textbox
        private static string INSTRUMENT_PANEL_DISPLAY_FORMAT = "{0}\t|  {1}\t|  {2} of {3}";  // {0} = filter type, {1} = exposure time, {2} = this iteration, {3} = total iterations

        public static readonly string _TRIGGER_ADJUSTED_STRING = "Trigger Slew Adjusted"; // String to put at the top of the output file when saving CurrentSettings if the Adjust times for trigger option is selected
        public static readonly string _TRIGGER_UNALTERED_STRING = "Exposure Times Unaltered"; // String to put at the top of the output file when saving CurrentSettings if the Adjust times for trigger option is deselected

        private static readonly string _CONNECTED = "Last Ping Status:  Connected"; // String to display when the last ping was successful
        private static readonly string _DISCONNECTED = "Last Ping Status: Disconnected"; // String to display when the last ping was unsuccessful

        #endregion // Static Variables

        #region Instance Variables

        // Experiment instance variables
        private IExperiment _exp;

        // Instance variables for the instrument panel display items
        private List<TextBlock> _fw_inst_labels; // holds the labels that make up the filter wheel instrument on the instrument panel, 0 is the current filter, moving clockwise
        private readonly object _fw_inst_lock; // Lock for the filter wheel instrument panel display to prevent concurrent access
        private System.Windows.Threading.DispatcherTimer _elapsedTimeClock; // Timer for the elapsed time value on the instrument panel
        private DateTime _runStart; // The time the runs started
        
        // Instance variables for the Current Settings List
        private bool _delete_allowed; // Bool denoting whether we can delete from the current settings list or not
        private CurrentSettingsList _settings_list; // The CurrentSettingsList object associated with this interface
        
        // Instance variables for acquisition
        private volatile FilterSetting _current_setting; // The setting currently being acquired
        private volatile string _current_filter; // The filter currently in front of the camera
        private int _iteration; // The iteration within the current setting (e.g. 3 of 5 frames in r')
        WheelInterface _wheel_interface; // The wheel interface instance associated with this panel
        private readonly object _fw_rotation_lock; // Lock for sending commands to the filter wheel
        private volatile bool _on_manual; // Bool denoting whether or not the system is in Manual Control Mode
        private volatile bool _rotate; // Bool noting if we need to rotate next, or not.
        private volatile int _frames_acquired; // The number of frames acquired since Run/Acquire was last clicked
        private volatile int _frames_per_cycle; // The number of frames per one fully cycle of filter settings

        #endregion // Instance Variables

        #region Initialize Control Panel

        /////////////////////////////////////////////////////////////////
        ///
        /// Control Panel Initialization
        ///
        /////////////////////////////////////////////////////////////////
        
        public ControlPanel(IExperiment e, IDisplay dispMgr)
        {      
            InitializeComponent();

            // Initialize instance variables
            this._exp = e; // save the experiment
            this._delete_allowed = true; // set the inital value on _delete_allowed
            this._fw_inst_lock = new object(); // create the object to lock on for _fw_inst_lock
            this._fw_rotation_lock = new object(); // create the object to lock on for _fw_rotation_lock
            this._wheel_interface = new WheelInterface(this); // create the wheel interface
            this._settings_list = new CurrentSettingsList(_wheel_interface); // create the current settings list
            
            // Home the filter wheel if the ping is successful
            if (_wheel_interface.PingConnection() == 0)
            {
                _wheel_interface.Home();
            }

            // Set up the small viewer and capture view functionality in Main View
            IDisplayViewerControl vc = dispMgr.CreateDisplayViewerControl();
            ViewerPane.Children.Add(vc.Control);
            SetUpDisplayParams(vc);

            // Assign the Drag/Drop Manager to the CurrentSettings window
            new ListViewDragDropManager<FilterSetting>(this.CurrentSettings);

            // Populate the Filter Selection Box and Jump Selection Box with the available filters
            // Set the initial state of the instrument panel
            this._fw_inst_labels = new List<TextBlock> { F0, F1, F2, F3, F4, F5, F6, F7 };
            for (int i = 0; i < WheelInterface._LOADED_FILTERS.Count; i++)
            {
                FilterSelectionBox.Items.Add(WheelInterface._LOADED_FILTERS[i]);
                JumpSelectionBox.Items.Add(WheelInterface._LOADED_FILTERS[i]);
            }
            
            // Attach the acquisition events to their handlers
            _exp.ExperimentStarted += _exp_ExperimentStarted;
            _exp.ExperimentCompleted += _exp_ExperimentCompleted;
            _exp.ImageDataSetReceived += _exp_ImageDataSetReceived;
            EnterManualControl(); // begin in manual control

            // Set up the elapsed run timer
            SetUpTimer();

            // Hook up the CollectionChanged event from the _settings_list to the ClearSeqTimeVals handler
            _settings_list.GetSettingsCollection().CollectionChanged += CollectionChangedHandler;
        }

        /// <summary>
        /// Sets up the ElapsedTime timer for the instrument panel.
        /// </summary>
        private void SetUpTimer()
        {
            _elapsedTimeClock = new System.Windows.Threading.DispatcherTimer();
            _elapsedTimeClock.Interval = new TimeSpan(0, 0, 1); // updates every 1 second
        }

        /// <summary>
        /// Sets the initial values for display properties in the IDisplayViewer on the FilterWheelControl panel.
        /// </summary>
        /// <param name="vc">The IDisplayViewerControl associated with the IDisplayViewer on the FilterWheelControl panel.</param>
        private void SetUpDisplayParams(IDisplayViewerControl vc)
        {
            vc.DisplayViewer.Clear();
            vc.DisplayViewer.Add(vc.DisplayViewer.LiveDisplaySource);
            vc.DisplayViewer.AlwaysAutoScaleIntensity = true;
            vc.DisplayViewer.ShowExposureEndedTimeStamp = false;
            vc.DisplayViewer.ShowExposureStartedTimeStamp = false;
            vc.DisplayViewer.ShowFrameTrackingNumber = false;
            vc.DisplayViewer.ShowGateTrackingDelay = false;
            vc.DisplayViewer.ShowGateTrackingWidth = false;
            vc.DisplayViewer.ShowModulationTrackingPhase = false;
            vc.DisplayViewer.ShowStampedExposureDuration = false;
        }

        #endregion // Initialize Control Panel

        #region Manual/Automated Control

        /// <summary>
        /// Calls EnterManualControl
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ManualControl_Click(object sender, RoutedEventArgs e)
        {
            EnterManualControl();
        }

        /// <summary>
        /// Updates the border indicator on the AutomatedControlDescription and ManualControlDescription labels to reflect that the system is in ManualControl mode.
        /// Blocks all changes to the CurrentSettings list and enables a mask over the list as a visual cue.
        /// Changes the IsHitTestVisible option on the JumpButton, JumpSelectionBox, CW button, and CCW button to true.
        /// Calls DisableFilterSettingsChanges to update the current settings list.
        /// Changes the _on_manual bool to true.
        /// </summary>
        private void EnterManualControl()
        {
            // Update UI to reflect manual control change
            AutomatedControlDescription.BorderBrush = Brushes.Transparent;
            ManualControlDescription.BorderBrush = Brushes.Gray;

            // Block any changes to filter settings
            CurrentSettingsMask.Visibility = System.Windows.Visibility.Visible;
            DisableFilterSettingsChanges();

            JumpButton.IsHitTestVisible = true;
            JumpSelectionBox.IsHitTestVisible = true;
            CW.IsHitTestVisible = true;
            CCW.IsHitTestVisible = true;
            _on_manual = true;
        }

        /// <summary>
        /// Updates the border indicator on the AutomatedControlDescription and ManualControlDescription labels to reflect that the system is in AutomatedControl mode.
        /// Removes the mask from the CurrentSettingsList view and enables edits to the list.
        /// Changes the IsHitTestVisible option on the JumpButton, JumpSelectionBox, CW button, and CCW button to false.
        /// Calls EnableFilterSettingsChanges to update the current settings list.
        /// Changes the _on_manual bool to false.
        /// </summary>
        private void AutomatedControl_Click(object sender, RoutedEventArgs e)
        {
            // Update UI to reflect automated control change
            ManualControlDescription.BorderBrush = Brushes.Transparent;
            AutomatedControlDescription.BorderBrush = Brushes.Gray;

            CurrentSettingsMask.Visibility = System.Windows.Visibility.Collapsed;
            EnableFilterSettingsChanges();

            JumpButton.IsHitTestVisible = false;
            JumpSelectionBox.IsHitTestVisible = false;
            CW.IsHitTestVisible = false;
            CCW.IsHitTestVisible = false;
            _on_manual = false;
        }

        #endregion // Manual/Automated Control

        #region Current Settings

        /// <summary>
        /// Returns the ObservableCollection holding the filter settings
        /// </summary>
        public ObservableCollection<FilterSetting> FilterSettings
        { get { return _settings_list.GetSettingsCollection(); } }

        /////////////////////////////////////////////////////////////////
        ///
        /// Methods for Populating and Editing the Current Settings List
        ///
        /////////////////////////////////////////////////////////////////

        #region Add

        /// <summary>
        /// Runs when Add or Edit button is clicked (they are the same button)
        /// </summary>
        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            Add_Edit();
        }

        /// <summary>
        /// Triggers an Add or Edit when Enter or Return are hit and the InputTime box has focus
        /// </summary>
        private void InputTime_KeyDown(object sender, KeyEventArgs e)
        {
            if (Key.Enter == e.Key || Key.Return == e.Key)
            {
                Add_Edit();
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
                Add_Edit();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Determines whether to call Add() or Edit() based on current system settings, then calls the respective function
        /// </summary>
        private void Add_Edit()
        {   
            // If the button is set to Add:
            if (this.AddButton.Content.ToString() == "Add")
            {
                // If Add doesn't doesn't work, return and let the user change values
                if (!_settings_list.Add(this.FilterSelectionBox.SelectionBoxItem, this.InputTime.Text, this.NumFrames.Text, (bool)TriggerSlewAdjust.IsChecked))
                    return;
            }
            // Otherwise we are editing:
            else
            {
                // Remove the current sequence time values because they have changed
                ClearSeqTimeVals();
                
                // Try to edit.  If edit doesn't work, return and let the user change values
                if (!_settings_list.Edit((FilterSetting)this.CurrentSettings.SelectedItem, this.FilterSelectionBox.SelectionBoxItem, this.InputTime.Text, this.NumFrames.Text, (bool)TriggerSlewAdjust.IsChecked))
                    return;
                
                // Refresh CurrentSettings list with updated info
                this.CurrentSettings.Items.Refresh();

                // Reset buttons and labels to show Add
                AddButton.ClearValue(Button.BackgroundProperty);
                AddButton.ClearValue(Button.ForegroundProperty);
                this.AddButton.Content = "Add";
                this.AddFilterLabel.Content = "Add Filter:";

                _delete_allowed = true;
            }

            // Reset input boxes
            this.InputTime.Text = InputTimeTextbox_DEFAULT_TEXT;
            this.FilterSelectionBox.SelectedIndex = -1;
            this.NumFrames.Text = NumFramesTextbox_DEFAULT_TEXT;
        }

        #endregion // Add

        #region Default Text Checks

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

        #endregion // Default Text Checks

        #region Delete Items

        /// <summary>
        /// Recognizes when the CurrentSettings window has focus and the backspace or delete keys are pressed
        /// </summary>
        private void CurrentSettings_KeyDown(object sender, KeyEventArgs e)
        {
            if (Key.Back == e.Key || Key.Delete == e.Key)
            {
                Delete();
            }
            e.Handled = true;
        }

        /// <summary>
        /// Runs when the Delete menu item is selected from the right-click menu in CurrentSettings
        /// </summary>
        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Delete();
        }
        
        /// <summary>
        /// Runs when the Delete button beneath the CurrentSettings list is pressed
        /// </summary>
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            Delete();
        }

        /// <summary>
        /// Deletes the selected items from the CurrentSettings list
        /// </summary>
        private void Delete()
        {
            if (_delete_allowed)
            {
                _settings_list.DeleteSelected(this.CurrentSettings.SelectedItems);
                this.CurrentSettings.Items.Refresh();
            }
        }

        #endregion // Delete Items

        #region Edit Items

        /// <summary>
        /// Runs when the Edit menu item is selected in the R-Click menu on the CurrentSettings list
        /// </summary>
        private void EditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            EditSetUp();
        }

        /// <summary>
        /// Runs when a Current Settings List item is double clicked
        /// </summary>
        private void CurrentSettings_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            EditSetUp();
        }

        /// <summary>
        /// Runs when the Edit button beneath the CurrentSettings list is clicked
        /// </summary>
        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            EditSetUp();
        }

        /// <summary>
        /// Sets up the UI to handle an Edit as selected from the CurrentSettings list R-Click menu or double-click
        /// </summary>
        private void EditSetUp()
        {
            if (this.CurrentSettings.SelectedItem != null)
            {
                // Block deletions from the list until the edit has finished
                _delete_allowed = false;
                
                // Change the button text and label to reflect that an edit is occuring
                this.AddButton.Content = "Save";
                this.AddFilterLabel.Content = "Edit Filter:";
                AddButton.Background = Brushes.Blue;
                AddButton.Foreground = Brushes.White;

                // Set the input time box, filter selection box, and num frames box values to reflect the settings of the filter to be edited
                this.InputTime.Text = Convert.ToString(((FilterSetting)this.CurrentSettings.SelectedItem).UserInputTime);
                this.FilterSelectionBox.SelectedItem = ((FilterSetting)this.CurrentSettings.SelectedItem).FilterType;
                this.NumFrames.Text = Convert.ToString(((FilterSetting)this.CurrentSettings.SelectedItem).NumExposures);
            }
        }

        #endregion // Edit Items

        #region Options Boxes

        /// <summary>
        /// Changes the display time value for all filter settings when the TriggerSlewAdjust check box is selected or deselcted.
        /// If selected, adjusts all display times to become SlewAdjustedTime.
        /// If deselected, adjusts all display times to be UserInputTime.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TriggerSlewAdjust_Click(object sender, RoutedEventArgs e)
        {
            if ((bool)TriggerSlewAdjust.IsChecked)
            {
                // We are entering the slew adjusted mode
                foreach (FilterSetting f in _settings_list.GetSettingsCollection())
                    f.DisplayTime = f.SlewAdjustedTime;
            }
            else
            {
                // We are exiting slew adjusted mode
                foreach (FilterSetting f in _settings_list.GetSettingsCollection())
                    f.DisplayTime = f.UserInputTime;
            }
            // Refresh the list to ensure the view updates
            this.CurrentSettings.Items.Refresh();
        }

        #endregion // Options Boxes

        #region Save and Load

        /// <summary>
        /// Save the CurrentSettings filter settings to a file for future reference
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _settings_list.CurrentSettingsSave((bool)TriggerSlewAdjust.IsChecked);
        }

        /// <summary>
        /// Load a filter settings list into the CurrentSettings pane
        /// Overwrite any present filters
        /// </summary>
        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            // Empty the current list
            clearFilterSettings();
            
            // Retrieve the contents from the file to be loaded
            string loaded = _settings_list.CurrentSettingsLoad();

            // As long as the string has information in it, read it into the list
            if (loaded != null)
            {
                readFileIntoList(loaded);
            }
        }

        /// <summary>
        /// Removes all filters from the FilterSettings list
        /// </summary>
        private void clearFilterSettings()
        {
            for (int i = FilterSettings.Count - 1; i >= 0; i--)
                FilterSettings.RemoveAt(i);
        }

        /// <summary>
        /// Given a string containing the data from a filter settings file, populate the CurrentSettings list
        /// </summary>
        /// <param name="toBeRead">The string to be read into the CurrentSettings list</param>
        private void readFileIntoList(string toBeRead)
        {
            string[] lineBreaks = { "\r\n" };
            string[] lines = toBeRead.Split(lineBreaks, StringSplitOptions.RemoveEmptyEntries);

            int i;

            // Deterime how to interpret the first line.  If it has trigger information, start reading filter settings on lines[1]
            if (lines[0] == _TRIGGER_ADJUSTED_STRING)
            {
                TriggerSlewAdjust.IsChecked = true;
                i = 1;
            }
            else if (lines[0] == _TRIGGER_UNALTERED_STRING)
            {
                TriggerSlewAdjust.IsChecked = false;
                i = 1;
            }
            else
                i = 0;

            // While there are still lines in the file, add a filter settings with the specified parameters to the list
            while (i < lines.Length)
            {
                char[] tabs = { '\t' };
                string[] vals = lines[i].Split(tabs);

                try
                {
                    // If the listed filter is not in the wheel, do not load the filter setting.  Otherwise, add the filter setting.
                    if (!WheelInterface._LOADED_FILTERS.Contains(vals[0]))
                    {
                        MessageBox.Show("This list contains filters that are no longer in the wheel.  Please rebuild your filter settings using the current filter set.");
                        return;
                    }
                    else
                    {
                        _settings_list.Add((object)vals[0], vals[1], vals[2], (bool)TriggerSlewAdjust.IsChecked);
                    }
                }
                catch (IndexOutOfRangeException)
                {
                    MessageBox.Show("There was a problem reading in your file.  Please ensure the file hasn't been corrupted or edited.");
                }
                i++;
            }
        }

        #endregion // Save and Load

        #region Enable/Disable Changes

        /// <summary>
        /// Disables any Add, Edit, Delete, or Load functions
        /// </summary>
        private void DisableFilterSettingsChanges()
        {
            AddButton.IsHitTestVisible = false;
            DeleteButton.IsHitTestVisible = false;
            EditButton.IsHitTestVisible = false;
            LoadButton.IsHitTestVisible = false;
            SaveButton.IsHitTestVisible = false;
            CurrentSettings.IsHitTestVisible = false;
            InputTime.KeyDown -= InputTime_KeyDown;
            NumFrames.KeyDown -= NumFrames_KeyDown;
            TriggerSlewAdjust.IsHitTestVisible = false;
        }

        /// <summary>
        /// Re-enables any Add, Edit, Delete, or Load functions
        /// </summary>
        private void EnableFilterSettingsChanges()
        {
            AddButton.IsHitTestVisible = true;
            DeleteButton.IsHitTestVisible = true;
            EditButton.IsHitTestVisible = true;
            LoadButton.IsHitTestVisible = true;
            SaveButton.IsHitTestVisible = true;
            CurrentSettings.IsHitTestVisible = true;
            InputTime.KeyDown += InputTime_KeyDown;
            NumFrames.KeyDown += NumFrames_KeyDown;
            TriggerSlewAdjust.IsHitTestVisible = true;
        }

        #endregion // Enable/Disable Changes

        #endregion // Current Settings

        #region Manual Control Buttons

        /////////////////////////////////////////////////////////////////
        ///
        /// Methods for Manual Control Buttons
        ///
        /////////////////////////////////////////////////////////////////
        
        /// <summary>
        /// Sends a signal to the filter wheel to rotate the wheel to the filter selected in the JumpSelectionBox drop down menu
        /// </summary>
        private void JumpButton_Click(object sender, RoutedEventArgs e)
        {
            // If we're in manual control mode, handle the jump button click
            if (ManualControl.IsChecked == true)
            {
                // If the experiment is running, check with the user before rotating the filter wheel.
                // If the user is okay with it, continue.  Otherwise, return.
                if (_exp.IsRunning)
                    if (!PleaseHaltCapturingMessage())
                        return;

                // If the user has selected a filter to jump to, tell the filter wheel to rotate to that filter
                if (JumpSelectionBox.SelectedIndex != -1)
                {
                    string selected = (string)JumpSelectionBox.SelectedValue;
                    _wheel_interface.RotateToFilter(selected);
                    JumpSelectionBox.SelectedIndex = -1; // reset box to empty
                }
                else
                    MessageBox.Show("Please select a filter to jump to.");
            }
        }

        /// <summary>
        /// Sends a signal to the filter wheel to rotate one position clockwise w.r.t. the wheel.
        /// </summary>
        private void CW_Click(object sender, RoutedEventArgs e)
        {
            // If we're in manual control, continue
            if (ManualControl.IsChecked == true)
            {
                // If the experiment is running, check with the user before making changes to the filter wheel
                if (_exp.IsRunning)
                    if (!PleaseHaltCapturingMessage())
                        return;

                // If the user is okay with it, rotate the filter wheel Clockwise 1 position
                _wheel_interface.Clockwise();
            }
        }

        /// <summary>
        /// Sends a signal to the filter wheel to rotate one position counterclockwise w.r.t. the wheel.
        /// </summary>
        private void CCW_Click(object sender, RoutedEventArgs e)
        {
            // If we're in manual control, continue
            if (ManualControl.IsChecked == true)
            {
                // If the experiment is running, check with the user on how to proceed
                if (_exp.IsRunning)
                    if (!PleaseHaltCapturingMessage())
                        return;

                // If the user is okay with it, rotate the filter wheel one position counterclockwise
                _wheel_interface.CounterClockwise();
            }
        }

        /// <summary>
        /// Displays an error message anytime LightField is capturing images and the user attempts to rotate the filter wheel.
        /// Asks the observer if they would like to continue
        /// </summary>
        /// <returns>True if the observer would like to continue, false otherwise.</returns>
        private static bool PleaseHaltCapturingMessage()
        {
            MessageBoxResult rotate = MessageBox.Show("You are currently acquiring images.  If you rotate the filter wheel, you will have one (or more) bad frames while the wheel is rotating.  Are you sure you want to do this?", "Really?", MessageBoxButton.YesNo);
            return rotate == MessageBoxResult.Yes;
        }

        #endregion // Manual Control Buttons

        #region Event Handlers

        /// <summary>
        /// Handles the ExperimentCompleted event for both Manual Control and Automated Control
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void _exp_ExperimentCompleted(object sender, ExperimentCompletedEventArgs e)
        {
            // If we're in manual control mode, route the ExperimentCompleted event to the manual control hander.  Otherwise, route the event to the automated control handler.
            if (_on_manual)
            {
                _exp_ExperimentCompleted_ManualControl();
            }
            else
            {
                _exp_ExperimentCompleted_Automated();
            }
        }

        /// <summary>
        /// Handles the ExperimentStarted event for both Manual Control and Automated Control
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void _exp_ExperimentStarted(object sender, ExperimentStartedEventArgs e)
        {
            // If we're in manual control mode, route the ExperimentStarted event to the manual control hander.  Otherwise, route the event to the automated control handler.
            if (_on_manual)
            {
                _exp_ExperimentStarted_ManualControl();
            }
            else
            {
                _exp_ExperimentStarted_Automated();
            } 
        }

        /// <summary>
        /// Handles the ImageDataSetReceived event for both Manual Control and Automated Control
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void _exp_ImageDataSetReceived(object sender, ImageDataSetReceivedEventArgs e)
        {
            // If we're in manual control mode, route the ImageDataSetReceived event to the manual control hander.  Otherwise, route the event to the automated control handler.
            if (_on_manual)
            {
                _exp_ImageDataSetReceived_ManualControl();
            }
            else
            {
                _exp_ImageDataSetReceived_Automated();
            }
        }

        #endregion // Event Handlers

        #region Automated Event Methods

        /// <summary>
        /// Handles the completion of an experiment while the filter wheel control panel is in automated control mode.
        /// </summary>
        private void _exp_ExperimentCompleted_Automated()
        {
            // Stop the elapsed time clock and remove the tick event handler
            _elapsedTimeClock.Stop();
            _elapsedTimeClock.Tick -= elapsedTimeClock_Tick;

            // Re-enable filter settings changes and update the current status display
            Application.Current.Dispatcher.BeginInvoke(new Action(EnableFilterSettingsChanges));
            Application.Current.Dispatcher.BeginInvoke(new Action(() => UpdatePanelCurrentStatus("Acquisition Complete")));
            Application.Current.Dispatcher.BeginInvoke(new Action(() => UpdatePanelCurrentStatus("")));
            
            // Re-enable the manual control option and home the filter wheel.
            ManualControl.IsHitTestVisible = true;
            _wheel_interface.Home();
        }

        /// <summary>
        /// Handles the start of an experiment while the filter wheel control panel is in automated control mode.
        /// </summary>
        private void _exp_ExperimentStarted_Automated()
        {
            // Don't start acquiring if there are no settings in the list.
            if (_settings_list.GetSettingsCollection().Count == 0)
            {
                MessageBox.Show("Please provide some filter setting to iterate through, or switch to Manual Control.", "No Filter Settings");
                _exp.Stop();
            }
            else
            {
                // Disable changes to the settings list and manual control system and retrieve the first setting
                ManualControl.IsHitTestVisible = false;
                Application.Current.Dispatcher.BeginInvoke(new Action(DisableFilterSettingsChanges));
                _current_setting = _settings_list.GetCaptureSettings();

                // Save the filter settings for this acquisition session
                //Application.Current.Dispatcher.BeginInvoke(new Action(() => _settings_list.CurrentSettingsSave((bool)TriggerSlewAdjust.IsChecked, RetrieveFileNameInfo())));

                // Deterime the settings for the first exposure time
                // If the filter currently in front of the camera is not the filter type for the first exposure, generate a transition frame for the first exposure
                if (_current_setting.FilterType != _current_filter)
                {
                    // calculate the transition frame to the first filter and set this frame as the _current_setting.
                    // Attach the Next value of the frame to the first frame in the sequence
                    FilterSetting transit = new FilterSetting
                    {
                        FilterType = _current_setting.FilterType,
                        DisplayTime = WheelInterface.TimeBetweenFilters(_current_filter, _current_setting.FilterType),
                        UserInputTime = 0,
                        SlewAdjustedTime = 0,
                        NumExposures = 1,
                        OrderLocation = -1
                    };
                    transit.Next = _current_setting;
                    _current_setting = transit;

                    // Begin rotating the filter wheel to the first filter
                    _wheel_interface.RotateToFilter(_current_setting.FilterType);

                    // Update the instrument panel to reflect the rotation
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => UpdatePanelCurrentStatus("Rotating to " + transit.FilterType)));
                }
                else
                {
                    // Update the exposure time to reflect the first exposure
                    String curStat = String.Format(INSTRUMENT_PANEL_DISPLAY_FORMAT, _current_setting.FilterType, _current_setting.DisplayTime, _iteration, _current_setting.NumExposures);
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => UpdatePanelCurrentStatus(curStat)));
                }
                // Set the LF Exposure Time value to the exposure time of the first frame and set the iteration num to 1
                _exp.SetValue(CameraSettings.ShutterTimingExposureTime, _current_setting.DisplayTime * 1000);
                _iteration = 1;

                // Update frames per cycle value, set the _frames_acquired value to 0, and update the NumCyclesComplete display on the instrument panel
                Application.Current.Dispatcher.BeginInvoke(new Action(() => UpdateFramesPerCycle()));
                _frames_acquired = 0;
                Application.Current.Dispatcher.BeginInvoke(new Action(() => NumCyclesCompleted.Text = Convert.ToString(_frames_acquired)));

                // Start the elapsed time clock
                Application.Current.Dispatcher.BeginInvoke(new Action(() => StartElapsedTimeClock()));
                
                // Begin thread to set values for second exposure
                Thread next_exp = new Thread(SetNextExpTime);
                next_exp.Start();
            }
        }

        /// <summary>
        /// Does the heavy lifting during an exposure so that between-exposure processing time is minimized.
        /// Updates the exposure time, _iteration counter, and determines if the wheel needs to rotate next exposure.
        /// </summary>
        private void SetNextExpTime()
        {
            // Give LF a little time to get into the next exposure (This is assuming we're only taking 1 exp/s maximum).
            if (_frames_acquired == 0)
            {
                Thread.Sleep(500);
            }
            else
            {
                Thread.Sleep(300);
            }

            // If the frame currently being exposed is the final frame in this filter setting, move to the next one.
            // Else, just update the iterator.
            if (_iteration == _current_setting.NumExposures)
            {
                // Move to the next filter setting
                _current_setting = _current_setting.Next;
                _exp.SetValue(CameraSettings.ShutterTimingExposureTime, _current_setting.DisplayTime * 1000);
                _iteration = 1;

                // If this next frame is a transition frame (i.e. orderlocation = -1), we must rotate
                if (_current_setting.OrderLocation == -1)
                {
                    // We must rotate
                    _rotate = true;
                }
            }
            else
            {
                // We are staying in the same filter setting.  Update the iteration
                _iteration++;
                _rotate = false;
            }
        }

        /// <summary>
        /// Handles the ImageDataSetReceived event when the filter wheel control panel is in automated control mode.
        /// </summary>
        private void _exp_ImageDataSetReceived_Automated() 
        {
            // Update the cycles completed count
            _frames_acquired++;
            Application.Current.Dispatcher.BeginInvoke(new Action(() => NumCyclesCompleted.Text = Convert.ToString(_frames_acquired / _frames_per_cycle)));
            
            if (_rotate)
            {
                // Tell the filter wheel to start rotating
                _wheel_interface.RotateToFilter(_current_setting.FilterType);
                Application.Current.Dispatcher.BeginInvoke(new Action(() => UpdatePanelCurrentStatus("Rotating to " + _current_setting.FilterType))); // "Rotating to f", f in _LOADED_FILTERS
                _rotate = false;
            }
            else
            {
                // We are staying in the same filter setting.  Update the instrument panel.
                String curStat = String.Format(INSTRUMENT_PANEL_DISPLAY_FORMAT, _current_setting.FilterType, _current_setting.DisplayTime, _iteration, _current_setting.NumExposures);
                Application.Current.Dispatcher.BeginInvoke(new Action(() => UpdatePanelCurrentStatus(curStat)));
            }

            // Begin the thread to set values for the next exposure
            Thread next_exp = new Thread(SetNextExpTime);
            next_exp.Start();
        }

        /// <summary>
        /// Captures the current DateTime as the Run Start Time and begins the Elapsed Time clock on the Instrument Panel
        /// </summary>
        private void StartElapsedTimeClock()
        {
            _runStart = DateTime.Now;
            this.RunStartTime.Text = _runStart.ToString("HH:mm:ss");
            this.ElapsedRunTime.Text = "00:00:00";
            _elapsedTimeClock.Tick += elapsedTimeClock_Tick;
            _elapsedTimeClock.Start();
        }

        /// <summary>
        /// Calls the FramesPerCycle method on the _settings_list to update the _frames_per_cycle instance variable used to calculate progress
        /// </summary>
        private void UpdateFramesPerCycle()
        {
            _frames_per_cycle = _settings_list.FramesPerCycle();
        }

        /// <summary>
        /// Stops acquisition, attempts to home the filter wheel, and displays a small message to the user.
        /// </summary>
        private void StopAll()
        {
            _exp.Stop();
            _elapsedTimeClock.Tick -= elapsedTimeClock_Tick;
            _wheel_interface.PingConnection();
            MessageBox.Show("Acquisition has been halted.  There has been an error communicating with the filter wheel.\n\nCommon causes include:\n\nBad usb/ethernet connection.\nLoss of power to filter wheel motor.\n", "Wheel Connection Error");
        }

        #endregion // Automated Event Methods

        #region Manual Event Methods

        /// <summary>
        /// Turns off the Automated Control option on the interface when acquisition is started in manual control mode.
        /// </summary>
        private void _exp_ExperimentStarted_ManualControl()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() => AutomatedControl.IsHitTestVisible = false));
        }

        /// <summary>
        /// Does nothing currently.
        /// </summary>
        private void _exp_ImageDataSetReceived_ManualControl()
        {
            return;
        }

        /// <summary>
        /// Turns on the Automated Control option on the interface when acquisition is ended in manual control mode.
        /// </summary>
        private void _exp_ExperimentCompleted_ManualControl()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() => AutomatedControl.IsHitTestVisible = true));
        }

        #endregion // Manual Event Methods

        #region Filename Info

        /// <summary>
        /// Returns the string representing the user-specified file name as entered in the LightField settings pane.
        /// Note that this DOES NOT include the file type extension (i.e. .spe, .fits, etc.).
        /// </summary>
        /// <returns>The string, including directory, of the file name</returns>
        private string RetrieveFileNameInfo()
        {
            // Get the file director and base name (without any date, time additions)
            string directory = _exp.GetValue(ExperimentSettings.FileNameGenerationDirectory).ToString();
            string base_name = _exp.GetValue(ExperimentSettings.FileNameGenerationBaseFileName).ToString();

            string space = " ";
            // If the AttachDate parameter has been set, handle the date attachment
            if (_exp.GetValue(ExperimentSettings.FileNameGenerationAttachDate) != null)
            {
                // If the AttachDate parameter is a bool AND true, attach the date in the format provided in ExperimentSettings.FileNameGenerationDateFormat
                if ((bool)_exp.GetValue(ExperimentSettings.FileNameGenerationAttachDate))
                {
                    // If we attach as a prefix, do so.  Otherwise attach as a suffix.
                    if ((FileFormatLocation)_exp.GetValue(ExperimentSettings.FileNameGenerationFileFormatLocation) == FileFormatLocation.Prefix)
                        base_name = GetFormattedDate((DateFormat)_exp.GetValue(ExperimentSettings.FileNameGenerationDateFormat)) + space + base_name;
                    else
                        base_name += space + GetFormattedDate((DateFormat)_exp.GetValue(ExperimentSettings.FileNameGenerationDateFormat));
                }
            }

            // If the AttachTime parameter has been set, handle the time attachment
            if (_exp.GetValue(ExperimentSettings.FileNameGenerationAttachTime) != null)
            {
                // If the AttachTime parameter is a bool AND true, attach the time in the format provided in ExperimentSettings.FileNameGenerationTimeFormat
                if ((bool)_exp.GetValue(ExperimentSettings.FileNameGenerationAttachTime))
                {
                    // If we attach as a prefix, do so.  Otherwise, attach as a suffix.
                    if ((FileFormatLocation)_exp.GetValue(ExperimentSettings.FileNameGenerationFileFormatLocation) == FileFormatLocation.Prefix)
                        base_name = GetFormattedTime((TimeFormat)_exp.GetValue(ExperimentSettings.FileNameGenerationTimeFormat)) + space + base_name;
                    else
                        base_name += space + GetFormattedTime((TimeFormat)_exp.GetValue(ExperimentSettings.FileNameGenerationTimeFormat));
                }
            }
            // Return the directory concatenated with the base name (where base_name has been updated to include date/time info if required)
            return directory + "\\" + base_name;
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
                    return today.ToString("dd-MM-yyyy");
                case DateFormat.dd_Month_yyyy:
                    return today.ToString("dd-MMMM-yyyy");
                case DateFormat.mm_dd_yyyy:
                    return today.ToString("MM-dd-yyyy");
                case DateFormat.Month_dd_yyyy:
                    return today.ToString("MMMM-dd-yyyy");
                case DateFormat.yyyy_mm_dd:
                    return today.ToString("yyyy-MM-dd");
                case DateFormat.yyyy_Month_dd:
                    return today.ToString("yyyy-MMMM-dd");
                default:
                    return today.ToString("yyyy-MM-dd");
            }
        }

        #endregion // Filename Info

        #region Instrument Panel

        /// <summary>
        /// Updates the filter wheel instrument on the instrument panel to reflect the current order.
        /// Updates the _current_filter variable.
        /// </summary>
        public void UpdateFWInstrumentOrder(List<string> newOrder)
        {
            // Lock the instrument
            lock (_fw_inst_lock)
            {
                // Update the label text to reflect the new filter order
                _current_filter = newOrder[0];
                for (int i = 0; i < _fw_inst_labels.Count(); i++)
                    _fw_inst_labels[i].Text = newOrder[i];
            }
        }

        /// <summary>
        /// Updates the filter wheel instrument on the instrument panel to reflect that the filter wheel is rotating
        /// </summary>
        public void UpdateFWInstrumentRotate()
        {
            // Lock the instrument
            lock (_fw_inst_lock)
            {
                // Set each label to ... to reflect rotation/unknown position
                foreach (TextBlock t in _fw_inst_labels)
                    t.Text = "...";
            }
        }

        /// <summary>
        /// Updates the PingStatusBox Text to show that the wheel is disconnected.
        /// </summary>
        public void PingStatusDisconnected()
        {
            PingStatusBox.Text = _DISCONNECTED;
            PingStatusBox.Foreground = Brushes.Red; // Set the text color to red
        }

        /// <summary>
        /// Updates the PingStatusBox Text to show that the wheel is connected.
        /// </summary>
        public void PingStatusConnected()
        {
            PingStatusBox.Text = _CONNECTED;
            PingStatusBox.ClearValue(TextBlock.ForegroundProperty); // Set the text color to default
        }

        /// <summary>
        /// Given a new string to display, updates the current status to the new string and the previous status to the string that was in current status before the update.
        /// </summary>
        /// <param name="s">The string to change the current status to</param>
        public void UpdatePanelCurrentStatus(String s)
        {
            this.PreviousStatus.Text = this.CurrentStatus.Text;
            this.CurrentStatus.Text = s;
        }

        /// <summary>
        /// Updates the elapsed time display on every timer tick.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void elapsedTimeClock_Tick(object sender, EventArgs e)
        {
            this.ElapsedRunTime.Text = (DateTime.Now - _runStart).ToString(@"hh\:mm\:ss");
        }

        /// <summary>
        /// Runs when the ping button on the instrument panel is clicked.
        /// Calls _wi.PingConnection and displays the result in a MessageBox
        /// </summary>
        /// <param name="senter"></param>
        /// <param name="e"></param>
        public void PingButton_Click(object sender, RoutedEventArgs e)
        {
            string message = _wheel_interface.PingConnection() == 0 ? "The connection seems good." : "A connection was not made.  Please check the connection.";
            MessageBox.Show(message, "Ping Status");
        }

        /// <summary>
        /// Calculates the sequence cycle time and transition time values from the current settings list.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void TimeCalc_Click(object sender, RoutedEventArgs e)
        {
            UpdateSeqTimeVals();
        }

        /// <summary>
        /// Updates the SeqExposeTime and SeqTransitTime text boxes on the instrument panel to reflect the corresponding values.
        /// </summary>
        private void UpdateSeqTimeVals()
        {
            SeqExposeTime.Text = Convert.ToString(_settings_list.CalculateExposedTime()) + " s";
            SeqTransitTime.Text = Convert.ToString(_settings_list.CalculateTransitionTime()) + " s";
        }


        /// <summary>
        /// Handles the CollectionChanged event from the Observable Collection _settings_list.
        /// Calls ClearSeqTimeVals();
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CollectionChangedHandler(object sender, NotifyCollectionChangedEventArgs e)
        {
            ClearSeqTimeVals();
        }

        /// <summary>
        /// Removes the text from the SeqExposeTime and SeqTransitTime text boxes.
        /// </summary>
        public void ClearSeqTimeVals()
        {
            SeqExposeTime.Text = "";
            SeqTransitTime.Text = "";
        }

        #endregion Instrument Panel

        #region ShutDown

        /// <summary>
        /// Closes the port to the filter wheel and sets all event handlers back to defaults.
        /// </summary>
        public void ShutDown()
        {
            _wheel_interface.ShutDown();
            _exp.ExperimentStarted -= _exp_ExperimentStarted;
            _exp.ExperimentCompleted -= _exp_ExperimentCompleted;
            _exp.ImageDataSetReceived -= _exp_ImageDataSetReceived;
        }

        #endregion // ShutDown
    }
}
