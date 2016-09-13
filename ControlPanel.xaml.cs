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
        public static readonly int FLASH_INTERVAL = 500; // Half the period of Stop button flashing
        private static string INSTRUMENT_PANEL_DISPLAY_FORMAT = "{0}\t|  {1}\t|  {2} of {3}";  // {0} = filter type, {1} = exposure time, {2} = this iteration, {3} = total iterations

        #endregion // Static Variables

        #region Instance Variables

        // Experiment instance variables
        private IExperiment _exp;
        private IFileManager _file_mgr;

        // Instance variables for the instrument panel display items
        private List<TextBlock> _fw_inst_labels; // holds the labels that make up the filter wheel instrument on the instrument panel, 0 is the current filter, moving clockwise
        private readonly object _fw_inst_lock;
        private readonly object _fw_movement_lock;
        System.Windows.Threading.DispatcherTimer _elapsedTimeClock;
        DateTime _runStart;
        
        // Instance variables for the Current Settings List
        private bool _delete_allowed;
        private CurrentSettingsList _settings_list;
        
        // Instance variables for acquisition
        private FilterSetting _current_setting;
        private bool _transitioning;
        private int _iteration;
        WheelInterface _wi;
        

        #endregion // Instance Variables

        #region Initialize Control Panel

        /////////////////////////////////////////////////////////////////
        ///
        /// Control Panel Initialization
        ///
        /////////////////////////////////////////////////////////////////
        
        public ControlPanel(IExperiment e, IDisplay dispMgr, IFileManager fileMgr)
        {      
            InitializeComponent();

            // Initialize instance variables
            this._exp = e;
            this._delete_allowed = true;
            this._fw_inst_lock = new object();
            this._fw_movement_lock = new object();
            this._wi = new WheelInterface();
            this._settings_list = new CurrentSettingsList(_wi);
            this._file_mgr = fileMgr;
            
            // Set up the small viewer and capture view functionality in Main View
            IDisplayViewerControl vc = dispMgr.CreateDisplayViewerControl();
            ViewerPane.Children.Add(vc.Control);
            vc.DisplayViewer.Clear();
            vc.DisplayViewer.Add(vc.DisplayViewer.LiveDisplaySource);

            // Assign the Drag/Drop Manager to the CurrentSettings window
            new ListViewDragDropManager<FilterSetting>(this.CurrentSettings);

            // Populate the Filter Selection Box and Jump Selection Box with the available filters
            // Set the initial state of the instrument pane
            this._fw_inst_labels = new List<TextBlock> { F0, F1, F2, F3, F4, F5, F6, F7 };
            List<Filter> set = _wi.GetOrderedSet();
            for(int i = 0; i < set.Count; i++)
            {
                FilterSelectionBox.Items.Add(set[i].ToString());
                JumpSelectionBox.Items.Add(set[i].ToString());
                _fw_inst_labels[i].Text = set[i].ToString();
            }

            // Set the initial Manual Control setting indicator
            AutomatedControlDescription.BorderBrush = Brushes.Transparent;
            ManualControlDescription.BorderBrush = Brushes.Gray;

            // Set up the elapsed time timer
            _elapsedTimeClock = new System.Windows.Threading.DispatcherTimer();
            _elapsedTimeClock.Tick += new EventHandler(elapsedTimeClock_Tick);
            _elapsedTimeClock.Interval = new TimeSpan(0,0,1); // updates every 1 second
        }

        #endregion // Initialize Control Panel

        #region Manual/Automated Control

        /// <summary>
        /// Updates the border indicator on the AutomatedControlDescription and ManualControlDescription labels to reflect that the system is in ManualControl mode
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ManualControl_Click(object sender, RoutedEventArgs e)
        {
            AutomatedControlDescription.BorderBrush = Brushes.Transparent;
            ManualControlDescription.BorderBrush = Brushes.Gray;
        }

        /// <summary>
        /// Updates the border indicator on the AutomatedControlDescription and ManualControlDescription labels to reflect that the system is in AutomatedControl mode
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AutomatedControl_Click(object sender, RoutedEventArgs e)
        {
            // Update UI to reflect option change
            ManualControlDescription.BorderBrush = Brushes.Transparent;
            AutomatedControlDescription.BorderBrush = Brushes.Gray;

            // Hook up preview and acquire to new event handlers
            EventHandler<ImageDataSetReceivedEventArgs> IDSReceived = new EventHandler<ImageDataSetReceivedEventArgs>(_exp_ImageDataSetReceived);
            EventHandler<ExperimentCompletedEventArgs> ExperimentComplete = new EventHandler<ExperimentCompletedEventArgs>(_exp_ExperimentCompleted);
            EventHandler<ExperimentStartedEventArgs> ExperimentStart = new EventHandler<ExperimentStartedEventArgs>(_exp_ExperimentStarted);
            _exp.ImageDataSetReceived += IDSReceived;
            _exp.ExperimentCompleted += ExperimentComplete;
            _exp.ExperimentStarted += ExperimentStart;
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
                    _settings_list.Add((object)vals[0], vals[1], vals[2], (bool)TriggerSlewAdjust.IsChecked);
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
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void _exp_ImageDataSetReceived(object sender, ImageDataSetReceivedEventArgs e)
        {
            if (_transitioning)
            {
                _transitioning = false;
                Application.Current.Dispatcher.BeginInvoke(new Action(UpdateFWInstrumentOrder));
                _exp.SetValue(CameraSettings.ShutterTimingExposureTime, _current_setting.DisplayTime * 1000); // convert to ms
                _iteration = 0;
            }
            
            // Update the iteration counter and _current_setting if necessary
            if (_iteration == _current_setting.NumExposures)
            {
                _current_setting = _current_setting.Next;
                SetNextExposureTime();
            }
            else
                _iteration++;
        }

        /// <summary>
        /// Retrieves the first filter setting and calls SetNextExposureTime to determine how to proceed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void _exp_ExperimentStarted(object sender, ExperimentStartedEventArgs e)
        {
            _current_setting = _settings_list.GetAllCaptureSettings().Item1;
            StartElapsedTimeClock();
            SetNextExposureTime();
        }

        /// <summary>
        /// Sets the next exposure time to either the transition time or the desired exposure time depending on the situation.
        /// </summary>
        private void SetNextExposureTime()
        {
            if (_wi.GetCurrentFilter().ToString() != _current_setting.FilterType)
            {
                _transitioning = true;

                double rotation_time = _wi.TimeToFilter(_current_setting.FilterType);
                _exp.SetValue(CameraSettings.ShutterTimingExposureTime, rotation_time * 1000);

                Application.Current.Dispatcher.BeginInvoke(new Action(UpdateFWInstrumentRotate));

                Thread rotate = new Thread(_wi.RotateToFilter);
                rotate.Start(_current_setting.FilterType);
            }
            else
            {
                _iteration = 1;
                _exp.SetValue(CameraSettings.ShutterTimingExposureTime, _current_setting.DisplayTime * 1000);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void _exp_ExperimentCompleted(object sender, ExperimentCompletedEventArgs e)
        {
            _elapsedTimeClock.Stop();
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
        /// Displays a message after waiting 500 ms to inform the observer that there has been an error.
        /// </summary>
        private void FilterRotationErrorMessage()
        {
            Thread.Sleep(500);
            MessageBox.Show("There has been an error with the filter wheel rotation.  The Filter Wheel is not on the expected filter.  Please try restarting your acquisition.", "Filter Wheel Rotation Error");
        }

        #endregion // Automated Event Handlers

        #region Automated Control Message

        /// <summary>
        /// Displays an error message anytime an automated control button is pressed, but automated control is not activated.
        /// </summary>
        private static void AutomatedControlDisabledMessage()
        {
            MessageBox.Show("This control is disabled.  Please enable Automated Control.");
        }

        #endregion // Automated Control Message

        #region Stop

        /// <summary>
        /// Launches the thread that awaits capturing to finish
        /// </summary>
        public void LaunchFinishCapturing()
        {
            Thread launch = new Thread(FinishCapturing);
            launch.Start();
        }

        /// <summary>
        /// Flashes the Stop button until capturing is halted.  Resets the current CaptureSession
        /// </summary>
        private void FinishCapturing()
        {
            _elapsedTimeClock.Stop();
            Application.Current.Dispatcher.BeginInvoke(new Action(EnableFilterSettingsChanges));
        }

        /// <summary>
        /// Resets the UI after Stop
        /// </summary>
        public void ResetUI()
        {
            ManualControl.IsHitTestVisible = true;
        }

        #endregion // Stop

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
                    PleaseHaltCapturingMessage();
                else
                {
                    Thread rotate_ccw = new Thread(CCWRotate);
                    rotate_ccw.Start();
                }
            }
            else
                ManualControlDisabledMessage();
        }

        /// <summary>
        /// Handles the wheel rotation and instrument panel updating for the manual control counterclockwise rotation button
        /// </summary>
        private void CCWRotate()
        {
            lock (_fw_movement_lock)
            {
                Application.Current.Dispatcher.Invoke(new Action(UpdateFWInstrumentRotate));
                _wi.RotateCounterClockwise();
                Application.Current.Dispatcher.Invoke(new Action(UpdateFWInstrumentOrder)); 
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
                    PleaseHaltCapturingMessage();
                else
                {
                    Thread rotate_cw = new Thread(CWRotate);
                    rotate_cw.Start();
                }
            }
            else
                ManualControlDisabledMessage();
        }

        /// <summary>
        /// Handles the wheel rotation and instrument panel updating for the manual control clockwise rotation button
        /// </summary>
        private void CWRotate()
        {
            lock (_fw_movement_lock)
            {
                Application.Current.Dispatcher.Invoke(new Action(UpdateFWInstrumentRotate));
                _wi.RotateClockwise();
                Application.Current.Dispatcher.Invoke(new Action(UpdateFWInstrumentOrder)); 
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
                    PleaseHaltCapturingMessage();
                else
                {
                    if (JumpSelectionBox.SelectedIndex != -1)
                    {
                        string selected = (string)JumpSelectionBox.SelectedValue;
                        Thread jump_to = new Thread(Jump);
                        jump_to.Start(selected);
                    }
                    else
                        MessageBox.Show("Please select a filter to jump to.");

                }
            }
            else
                ManualControlDisabledMessage();
        }

        /// <summary>
        /// Handles the wheel rotation and instrument panel updating for the manual control clockwise rotation button
        /// </summary>
        private void Jump(object f_type)
        {
            lock (_fw_movement_lock)
            {
                Application.Current.Dispatcher.Invoke(new Action(UpdateFWInstrumentRotate));
                _wi.RotateToFilter((string)f_type);
                Application.Current.Dispatcher.Invoke(new Action(UpdateFWInstrumentOrder));
            }
        }

        #region Manual Control Messages

        /// <summary>
        /// Displays a message letting the user know that the Manual Control functions are enabled.
        /// For build purposes only.  Will be removed for final version when these buttons actually do something.
        /// </summary>
        private static void ManualControlEnabledMessage()
        {
            MessageBox.Show("This control is enabled");
        }

        /// <summary>
        /// Displays an error message anytime LightField is capturing images and the user attempts to rotate the filter wheel.
        /// </summary>
        private static void PleaseHaltCapturingMessage()
        {
            MessageBox.Show("Please halt current frame capturing before attempting to rotate the filter wheel.");
        }

        /// <summary>
        /// Displays an error message anytime a manual control button is pressed, but manual control is disabled.
        /// </summary>
        private static void ManualControlDisabledMessage()
        {
            MessageBox.Show("This control is disabled.  Please enable Manual Control.");
        }

        #endregion //Manual Control Messages

        #endregion // Manual Control Buttons

        #region Instrument Panel

        /// <summary>
        /// Updates the filter wheel instrument on the instrument panel to reflect the current order
        /// </summary>
        public void UpdateFWInstrumentOrder()
        {
            List<Filter> newOrder = _wi.GetOrderedSet();
            lock (_fw_inst_lock)
            {
                for (int i = 0; i < _fw_inst_labels.Count(); i++)
                    _fw_inst_labels[i].Text = newOrder[i].GetFilterType();
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

        #endregion Instrument Panel

        

    }
}
