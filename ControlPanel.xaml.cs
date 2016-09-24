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
using System.Threading;

using Filters;

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

        #endregion // Static Variables

        #region Instance Variables

        // Experiment instance variables
        private IExperiment _exp;

        // Instance variables for the instrument panel display items
        private List<TextBlock> _fw_inst_labels; // holds the labels that make up the filter wheel instrument on the instrument panel, 0 is the current filter, moving clockwise
        private readonly object _fw_inst_lock;
        private System.Windows.Threading.DispatcherTimer _elapsedTimeClock;
        private DateTime _runStart;
        
        // Instance variables for the Current Settings List
        private bool _delete_allowed;
        private CurrentSettingsList _settings_list;
        
        // Instance variables for acquisition
        private FilterSetting _current_setting;
        private bool _transitioning;
        private int _iteration;
        private WheelInterface _wi;
        private readonly object _fw_rotation_lock;

        // Custom event handlers
        EventHandler<ImageDataSetReceivedEventArgs> _IDS_received_automated;
        EventHandler<ExperimentCompletedEventArgs> _experiment_complete_automated;
        EventHandler<ExperimentStartedEventArgs> _experiment_start_automated;
        EventHandler<ExperimentCompletedEventArgs> _experiment_complete_manual;
        EventHandler<ExperimentStartedEventArgs> _experiment_start_manual;

        // Instance variables for constructor
        private bool _did_load;
        

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
            this._exp = e;
            this._delete_allowed = true;
            this._fw_inst_lock = new object();
            this._fw_rotation_lock = new object();
            this._wi = new WheelInterface(this);
            this._settings_list = new CurrentSettingsList(_wi);
            
            // Home the filter wheel
            _wi.Home();
            
            // Set up the small viewer and capture view functionality in Main View
            IDisplayViewerControl vc = dispMgr.CreateDisplayViewerControl();
            ViewerPane.Children.Add(vc.Control);
            SetUpDisplayParams(vc);

            // Assign the Drag/Drop Manager to the CurrentSettings window
            new ListViewDragDropManager<FilterSetting>(this.CurrentSettings);

            // Populate the Filter Selection Box and Jump Selection Box with the available filters
            // Set the initial state of the instrument panel
            this._fw_inst_labels = new List<TextBlock> { F0, F1, F2, F3, F4, F5, F6, F7 };
            List<string> set = _wi.GetOrderedSet();
            
            // If we were unable to contact the filter wheel, try for five seconds, then give up.
            int wait_iteration = 0;
            while (set == null && wait_iteration < 50)
            {
                Thread.Sleep(100);
                set = _wi.GetOrderedSet();
                wait_iteration++;
            }
            
            // If we have given up, abort add-in load.
            if (wait_iteration == 50)
            {
                _did_load = false;
                return;
            }

            // If we made it, start populating interface items.
            for (int i = 0; i < set.Count; i++)
            {
                FilterSelectionBox.Items.Add(set[i]);
                JumpSelectionBox.Items.Add(set[i]);
                _fw_inst_labels[i].Text = set[i];
            }
            
            // Set the initial Manual Control setting
            EnterManualControl();

            // Set up other interface properties
            SetUpEventHandlers();
            SetUpTimer();

            // Let the caller know we loaded
            _did_load = true;
        }

        /// <summary>
        /// Sets up the initial event handlers for Run and Acquire and hooks them up to the events.
        /// The initial state is in Manual Control.
        /// </summary>
        private void SetUpEventHandlers()
        {
            // Create event handlers
            _experiment_complete_manual = new EventHandler<ExperimentCompletedEventArgs>(_exp_ExperimentCompleted_ManualControl);
            _experiment_start_manual = new EventHandler<ExperimentStartedEventArgs>(_exp_ExperimentStarted_ManualControl);

            // Hook up to manual control handlers
            _exp.ExperimentStarted += _experiment_start_manual;
            _exp.ExperimentCompleted += _experiment_complete_manual;
        }

        /// <summary>
        /// Sets up the ElapsedTime timer for the instrument panel.
        /// </summary>
        private void SetUpTimer()
        {
            _elapsedTimeClock = new System.Windows.Threading.DispatcherTimer();
            _elapsedTimeClock.Tick += new EventHandler(elapsedTimeClock_Tick);
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
        /// Attaches the appropriate custom event handlers to the ExperimentCompleted, ExperimentStarted, and ImageDataSetReceived events.
        /// </summary>
        private void EnterManualControl()
        {
            // Update UI to reflect option change
            AutomatedControlDescription.BorderBrush = Brushes.Transparent;
            ManualControlDescription.BorderBrush = Brushes.Gray;
            JumpButton.IsHitTestVisible = true;
            CCW.IsHitTestVisible = true;
            CW.IsHitTestVisible = true;
            JumpSelectionBox.IsHitTestVisible = true;
            TriggerSlewAdjust.IsHitTestVisible = false;
            EfficientOrder.IsHitTestVisible = false;

            // Disconnect preview and acquire from custom event handlers
            _exp.ImageDataSetReceived -= _IDS_received_automated;
            _exp.ExperimentCompleted -= _experiment_complete_automated;
            _exp.ExperimentStarted -= _experiment_start_automated; _exp.ExperimentStarted += _experiment_start_manual;
            _exp.ExperimentCompleted -= _experiment_complete_manual;
            _exp.ExperimentStarted -= _experiment_start_manual;

            // Create new event handlers for preview and acquire
            _experiment_complete_manual = new EventHandler<ExperimentCompletedEventArgs>(_exp_ExperimentCompleted_ManualControl);
            _experiment_start_manual = new EventHandler<ExperimentStartedEventArgs>(_exp_ExperimentStarted_ManualControl);

            // Connect preview and acquire to manual event handlers
            _exp.ExperimentStarted += _experiment_start_manual;
            _exp.ExperimentCompleted += _experiment_complete_manual;
        }

        /// <summary>
        /// Updates the border indicator on the AutomatedControlDescription and ManualControlDescription labels to reflect that the system is in AutomatedControl mode.
        /// Attaches the appropriate custom event handlers to the ExperimentCompleted, ExperimentStarted, and ImageDataSetReceived events.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AutomatedControl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Update UI to reflect option change
                ManualControlDescription.BorderBrush = Brushes.Transparent;
                AutomatedControlDescription.BorderBrush = Brushes.Gray;
                JumpButton.IsHitTestVisible = false;
                CCW.IsHitTestVisible = false;
                CW.IsHitTestVisible = false;
                JumpSelectionBox.IsHitTestVisible = false;
                TriggerSlewAdjust.IsHitTestVisible = true;
                EfficientOrder.IsHitTestVisible = true;
                
                // Disconnect preview and acquire from all handlers
                _exp.ExperimentCompleted -= _experiment_complete_manual;
                _exp.ExperimentStarted -= _experiment_start_manual;
                _exp.ImageDataSetReceived -= _IDS_received_automated;
                _exp.ExperimentCompleted -= _experiment_complete_automated;
                _exp.ExperimentStarted -= _experiment_start_automated;

                // Create new event handlers for preview and acquire
                _IDS_received_automated = new EventHandler<ImageDataSetReceivedEventArgs>(_exp_ImageDataSetReceived_Automated);
                _experiment_complete_automated = new EventHandler<ExperimentCompletedEventArgs>(_exp_ExperimentCompleted_Automated);
                _experiment_start_automated = new EventHandler<ExperimentStartedEventArgs>(_exp_ExperimentStarted_Automated);

                // Hook up preview and acquire to new event handlers
                _exp.ImageDataSetReceived += _IDS_received_automated;
                _exp.ExperimentCompleted += _experiment_complete_automated;
                _exp.ExperimentStarted += _experiment_start_automated;
            }
            catch (Exception exp)
            {
                MessageBox.Show(exp.Message);
            }
        }

        #endregion // Manual/Automated Control

        #region Current Settings

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
                // Try to edit.  If edit doesn't work, return and let the user change values
                if (!_settings_list.Edit((FilterSetting)this.CurrentSettings.SelectedItem, this.FilterSelectionBox.SelectionBoxItem, this.InputTime.Text, this.NumFrames.Text, (bool)TriggerSlewAdjust.IsChecked))
                    return;
                
                // Refresh CurrentSettings list with updated info
                this.CurrentSettings.Items.Refresh();

                // Reset UI to initial settings
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
                _delete_allowed = false;
                
                this.AddButton.Content = "Save";
                this.AddFilterLabel.Content = "Edit Filter:";

                this.InputTime.Text = Convert.ToString(((FilterSetting)this.CurrentSettings.SelectedItem).UserInputTime);
                this.FilterSelectionBox.SelectedItem = ((FilterSetting)this.CurrentSettings.SelectedItem).FilterType;
                this.NumFrames.Text = Convert.ToString(((FilterSetting)this.CurrentSettings.SelectedItem).NumExposures);
                AddButton.Background = Brushes.Blue;
                AddButton.Foreground = Brushes.White;
            }
        }

        #endregion // Edit Items

        #region Options Boxes

        private void TriggerSlewAdjust_Click(object sender, RoutedEventArgs e)
        {
            if ((bool)TriggerSlewAdjust.IsChecked)
            {
                foreach (FilterSetting f in _settings_list.GetSettingsCollection())
                    f.DisplayTime = f.SlewAdjustedTime;
            }
            else
            {
                foreach (FilterSetting f in _settings_list.GetSettingsCollection())
                    f.DisplayTime = f.UserInputTime;
            }
            this.CurrentSettings.Items.Refresh();
        }

        private void EfficientOrder_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Efficient ordering feature not yet enabled.");
        }

        #endregion // Options Boxes

        #region Save and Load

        /// <summary>
        /// Save the CurrentSettings filter settings to a file for future reference
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _settings_list.CurrentSettingsSave();
        }

        /// <summary>
        /// Load a filter settings list into the CurrentSettings pane
        /// Overwrite any present filters
        /// </summary>
        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            clearFilterSettings();
            
            string loaded = _settings_list.CurrentSettingsLoad();

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
            foreach (string line in lines)
            {
                char[] tabs = { '\t' };
                string[] vals = line.Split(tabs);

                try
                {
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
            EfficientOrder.IsHitTestVisible = false;
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
            NumFrames.KeyDown +=NumFrames_KeyDown;
            TriggerSlewAdjust.IsHitTestVisible = true;
            EfficientOrder.IsHitTestVisible = true;
        }

        #endregion // Enable/Disable Changes

        #endregion // Current Settings

        #region Automated Event Handlers

        /// <summary>
        /// Sets up the system for the next exposure.  If the filter wheel was transitioning, sets up for the first exposure in the new filter.
        /// Otherwise, updates the _iteration counter.  If the current iteration is the max for the current filter, SetNextExposureTime is called.
        /// Called after every exposure.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void _exp_ImageDataSetReceived_Automated(object sender, ImageDataSetReceivedEventArgs e)
        {
            // Handle the end of a transition frame
            if (_transitioning)
            {
                _transitioning = false;
                _exp.SetValue(CameraSettings.ShutterTimingExposureTime, _current_setting.DisplayTime * 1000); // convert to ms
            }

            // Update the iteration counter and _current_setting if necessary
            if (_iteration == _current_setting.NumExposures)
            {
                _current_setting = _current_setting.Next;
                SetNextExposureTime();
            }
            else
                _iteration++;

            // Update the instrument panel
            UpdateInstrumentPanel();
        }

        /// <summary>
        /// Retrieves the first filter setting and calls SetNextExposureTime to determine how to proceed.
        /// Called every time LightField transitions from Stop to Run or Acquire.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void _exp_ExperimentStarted_Automated(object sender, ExperimentStartedEventArgs e)
        {
            // Don't start acquiring if there are no settings in the list.
            if (_settings_list.GetSettingsCollection().Count() == 0)
            {
                MessageBox.Show("Please provide some filter setting to iterate through, or switch to Manual Control.");
                _exp.Stop();
            }
            else
            {
                // Disable changes to the settings list and control system and retrieve the first setting
                Application.Current.Dispatcher.BeginInvoke(new Action(DisableFilterSettingsChanges));
                _current_setting = _settings_list.GetCaptureSettings();
                ManualControl.IsHitTestVisible = false;

                // Set up the first exposure time, wheel position, and update the instrument panel to reflect this
                _transitioning = false;
                SetNextExposureTime();
                UpdateInstrumentPanel();

                // Initialize other environment variables
                StartElapsedTimeClock();
            }  
            
            
        }

        /// <summary>
        /// Sets the next exposure time to either the transition time or the desired exposure time depending on the situation.
        /// </summary>
        private void SetNextExposureTime()
        {
            // Double check that we know where we are.
            string cur = _wi.GetCurrentFilter();
            if (cur.Equals(null) || cur != _current_setting.FilterType)
                StopAll();
            
            // If we need to rotate, set up to do so
            if (cur != _current_setting.FilterType)
            {
                _iteration = 0;
                _transitioning = true;

                double rotation_time = _wi.TimeToFilter(_current_setting.FilterType);
                rotation_time = rotation_time % 1 == 0 ? rotation_time - _settings_list.GetTriggerSlewCorrection() : rotation_time;
                _exp.SetValue(CameraSettings.ShutterTimingExposureTime, rotation_time * 1000);

                // Rotate the filter wheel
                Thread rotate = new Thread(RotateToSelectedFilter);
                rotate.Start(_current_setting.FilterType);
            }
            // Otherwise, set the iteration counter to zero and update the exposure time
            else
            {
                _iteration = 1;
                _exp.SetValue(CameraSettings.ShutterTimingExposureTime, _current_setting.DisplayTime * 1000);
            }
        }

        /// <summary>
        /// Stops the elapsed the clock and sets the final values on the instrument panel.  Allows manual control to be re-enabled.
        /// Runs when the Stop button is clicked or the desired number of frames are Acquired.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void _exp_ExperimentCompleted_Automated(object sender, ExperimentCompletedEventArgs e)
        {
            _elapsedTimeClock.Stop();
            Application.Current.Dispatcher.BeginInvoke(new Action(EnableFilterSettingsChanges));
            Application.Current.Dispatcher.BeginInvoke(new Action(() => UpdatePanelCurrentStatus("")));
            Application.Current.Dispatcher.BeginInvoke(new Action(() => UpdateFWInstrumentOrder()));
            ManualControl.IsHitTestVisible = true;
            _wi.ClosePort();
        }

        /// <summary>
        /// Updated the instrument panel to reflect the current situation.
        /// If the filter wheel is rotating, updates to show rotation.
        /// If the filter wheel is not rotating, updates the exposure counter.
        /// </summary>
        private void UpdateInstrumentPanel()
        {
            if (_transitioning)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => UpdatePanelCurrentStatus("Rotating to " + _current_setting.FilterType)));
            }
            else
            {
                String currStat = String.Format(INSTRUMENT_PANEL_DISPLAY_FORMAT, _current_setting.FilterType, _current_setting.DisplayTime, _iteration, _current_setting.NumExposures);
                Application.Current.Dispatcher.Invoke(new Action(() => UpdatePanelCurrentStatus(currStat)));
            }
        }

        /// <summary>
        /// Captures the current DateTime as the Run Start Time and begins the Elapsed Time clock on the Instrument Panel
        /// </summary>
        private void StartElapsedTimeClock()
        {
            _runStart = DateTime.Now;
            this.RunStartTime.Text = _runStart.ToString("HH:mm:ss");
            this.ElapsedRunTime.Text = "00:00:00";
            _elapsedTimeClock.Start();
        }

        /// <summary>
        /// Stops acquisition, attempts to home the filter wheel, and displays a small message to the user.
        /// </summary>
        private void StopAll()
        {
            _exp.Stop();
            _wi.Home();
            MessageBox.Show("Acquisition has been halted.  There has been an error communicating with the filter wheel.\n\nCommon causes include:\n\nBad usb/ethernet connection.\nLoss of power to filter wheel motor.\nActs of God.\n\n");
        }

        #endregion // Automated Event Handlers

        #region Manual Event Handlers

        /// <summary>
        /// Turns off the Automated Control option on the interface when acquisition is started in manual control mode.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void _exp_ExperimentStarted_ManualControl(object sender, ExperimentStartedEventArgs e)
        {
            AutomatedControl.IsHitTestVisible = false;
        }

        /// <summary>
        /// Turns on the Automated Control option on the interface when acquisition is ended in manual control mode.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void _exp_ExperimentCompleted_ManualControl(object sender, ExperimentCompletedEventArgs e)
        {
            AutomatedControl.IsHitTestVisible = true;
        }

        #endregion // Manual Event Handlers

        #region Rotation Threads

        /// <summary>
        /// A separate thread that controls clockwise filter wheel rotation.  
        /// </summary>
        private void RotateToSelectedFilter(object f)
        {
            lock (_fw_rotation_lock)
            {
                Application.Current.Dispatcher.Invoke(new Action(() => _wi.RotateToFilter((string)f)));
                Application.Current.Dispatcher.BeginInvoke(new Action(UpdateFWInstrumentRotate));
            }
        }

        /// <summary>
        /// A separate thread that controls counter clockwise filter wheel rotation.  
        /// </summary>
        private void RotateCounterClockwise()
        {
            lock (_fw_rotation_lock)
            {
                Application.Current.Dispatcher.Invoke(new Action(() => _wi.RotateCounterClockwise()));
                Application.Current.Dispatcher.BeginInvoke(new Action(UpdateFWInstrumentRotate));
            }
        }

        /// <summary>
        /// A separate thread that controls clockwise filter wheel rotation.  
        /// </summary>
        private void RotateClockwise()
        {
            lock (_fw_rotation_lock)
            {
                Application.Current.Dispatcher.Invoke(new Action(() => _wi.RotateClockwise()));
                Application.Current.Dispatcher.BeginInvoke(new Action(UpdateFWInstrumentRotate));
            }
        }

        #endregion // Rotation Threads

        #region Manual Control Buttons

        /////////////////////////////////////////////////////////////////
        ///
        /// Methods for Manual Control Buttons (Rotate CW, Rotate CCW, Jump)
        ///
        /////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sends a signal to the filter wheel to rotate the wheel counterclockwise (w.r.t. the camera)
        /// </summary>
        private void CCW_Click(object sender, RoutedEventArgs e)
        {
            if (this.ManualControl.IsChecked == true)
            {
                if (_exp.IsRunning)
                    if (!PleaseHaltCapturingMessage())
                        return;

                Thread ccw_rotate = new Thread(RotateCounterClockwise);
                ccw_rotate.Start();
            }
        }

        /// <summary>
        /// Sends a signal to the filter wheel to rotate the wheel clockwise (w.r.t. the camera)
        /// </summary>
        private void CW_Click(object sender, RoutedEventArgs e)
        {
            if (this.ManualControl.IsChecked == true)
            {
                if (_exp.IsRunning)
                    if (!PleaseHaltCapturingMessage())
                        return;

                Thread cw_rotate = new Thread(RotateClockwise);
                cw_rotate.Start();
            }
        }

        /// <summary>
        /// Sends a signal to the filter wheel to rotate the wheel to the filter selected in the JumpSelectionBox drop down menu
        /// </summary>
        private void JumpButton_Click(object sender, RoutedEventArgs e)
        {
            if (ManualControl.IsChecked == true)
            {
                if (_exp.IsRunning)
                    if (!PleaseHaltCapturingMessage())
                        return;

                if (JumpSelectionBox.SelectedIndex != -1)
                {
                    string selected = (string)JumpSelectionBox.SelectedValue;
                    Thread jump = new Thread(RotateToSelectedFilter);
                    jump.Start(selected);
                }
                else
                    MessageBox.Show("Please select a filter to jump to.");
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

        #region Instrument Panel

        /// <summary>
        /// Updates the filter wheel instrument on the instrument panel to reflect the current order
        /// </summary>
        public void UpdateFWInstrumentOrder()
        {
            List<string> newOrder = _wi.GetOrderedSet();

            lock (_fw_inst_lock)
            {
                for (int i = 0; i < _fw_inst_labels.Count(); i++)
                    _fw_inst_labels[i].Text = newOrder[i];
            }
        }

        /// <summary>
        /// Updates the filter wheel instrument on the instrument panel to reflect that the filter wheel is rotating
        /// </summary>
        public void UpdateFWInstrumentRotate()
        {
            lock (_fw_inst_lock)
            {
                foreach (TextBlock t in _fw_inst_labels)
                    t.Text = "...";
            }
        }

        /// <summary>
        /// Updates the Instrument Panel to show the latest frame metadata information
        /// </summary>
        /// <param name="m">The metadata object holding the new information</param>
        public void UpdatePanelMetaData(Metadata m, DateTime captureCalled)
        {
            this.PrevExpSt.Text = ((DateTime)(captureCalled + (TimeSpan)m.ExposureStarted)).ToString("HH:mm:ss.ffff"); // absolute time w.r.t. the computer clock
            this.PrevExpEnd.Text = ((DateTime)(captureCalled + (TimeSpan)m.ExposureEnded)).ToString("HH:mm:ss.ffff"); // absolute time w.r.t. the computer clock
            //this.PrevExpSt.Text = ((TimeSpan)m.ExposureStarted).ToString("c"); // relative time from the start of the Capture method
            //this.PrevExpEnd.Text = ((TimeSpan)m.ExposureEnded).ToString("c"); // relative time from the start of the Capture method
            this.PrevExpDur.Text = ((TimeSpan)m.ExposureEnded - (TimeSpan)m.ExposureStarted).ToString("c");
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
            MessageBox.Show(_wi.PingConnection());
        }

        #endregion Instrument Panel

        #region ShutDown/DidLoad

        /// <summary>
        /// Closes the port to the filter wheel and sets all event handlers back to defaults.
        /// </summary>
        public void ShutDown()
        {
            _wi.ClosePort();
            _exp.ExperimentCompleted -= _experiment_complete_manual;
            _exp.ExperimentStarted -= _experiment_start_manual;
            _exp.ExperimentStarted -= _experiment_start_automated;
            _exp.ExperimentCompleted -= _experiment_complete_automated;
            _exp.ImageDataSetReceived -= _IDS_received_automated;
        }

        public bool DidLoad()
        {
            return _did_load;
        }

        #endregion // ShutDown/DidLoad



    }
}
