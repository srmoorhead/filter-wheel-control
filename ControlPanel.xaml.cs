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

using FilterWheelControl.SettingsList;
using FilterWheelControl.ImageCapturing;
using FilterWheelControl.HardwareInterface;

namespace FilterWheelControl.ControlPanelFunctions
{

    /// <summary>
    /// Interaction logic for ControlPanel.xaml
    /// </summary>
    

    public partial class ControlPanel : UserControl
    {

        #region Instance Variables

        // Constants
        private static readonly string InputTimeTextbox_DEFAULT_TEXT = "Exposure Time (s)"; // Change this string if you wish to change the text in the InputTime textbox
        private static readonly string NumFramesTextbox_DEFAULT_TEXT = "Num"; // Change this string if you wish to change the text in the NumFrames textbox
        public static readonly int FLASH_INTERVAL = 500; // Half the period of Stop button flashing
        private static bool DELETE_ALLOWED = true;
        
        // LightField Variables
        public static IExperiment EXPERIMENT;
        private ILightFieldApplication _APP;
        private IDisplayViewerControl _VIEWER;

        // Instrument Panel Variables
        private List<TextBlock> FW_INST_LABELS; // holds the labels that make up the filter wheel instrument on the instrument panel, 0 is the current filter, moving clockwise
        private static readonly object FW_INST_LOCK = new object();

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
            
            // Set up the viewer
            _VIEWER = _APP.DisplayManager.CreateDisplayViewerControl();
            ViewerPane.Children.Add(_VIEWER.Control);
            _VIEWER.DisplayViewer.Clear();
            _VIEWER.DisplayViewer.Add(_VIEWER.DisplayViewer.LiveDisplaySource);

            new ListViewDragDropManager<Filter>(this.CurrentSettings);

            // Populate the Filter Selection Box and Jump Selection Box with the available filters
            // Set the initial state of the instrument panel
            string[] filters = WheelInteraction.getCurrentOrder();
            FW_INST_LABELS = new List<TextBlock> { F0, F1, F2, F3, F4, F5, F6, F7 };
            for (int i = 0; i < filters.Length; i++)
            {
                FilterSelectionBox.Items.Add(filters[i]);
                JumpSelectionBox.Items.Add(filters[i]);
                FW_INST_LABELS[i].Text = filters[i];
            }
        }

        #endregion // Initialize Control Panel

        #region Current Settings

        public static ObservableCollection<Filter> FilterSettings
        { get { return CurrentSettingsList.FilterSettings; } }

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
                if (!CurrentSettingsList.Add(this.FilterSelectionBox.SelectionBoxItem, this.InputTime.Text, this.NumFrames.Text, (bool)TriggerSlewAdjust.IsChecked))
                    return;
            }
            // Otherwise we are editing:
            else
            {
                // Try to edit.  If edit doesn't work, return and let the user change values
                if (!CurrentSettingsList.Edit((Filter)this.CurrentSettings.SelectedItem, this.FilterSelectionBox.SelectionBoxItem, this.InputTime.Text, this.NumFrames.Text, (bool)TriggerSlewAdjust.IsChecked))
                    return;
                
                // Refresh CurrentSettings list with updated info
                this.CurrentSettings.Items.Refresh();

                // Reset UI to initial settings
                AddButton.ClearValue(Button.BackgroundProperty);
                AddButton.ClearValue(Button.ForegroundProperty);
                this.AddButton.Content = "Add";
                this.AddFilterLabel.Content = "Add Filter:";

                DELETE_ALLOWED = true;
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
            if (DELETE_ALLOWED)
            {
                CurrentSettingsList.DeleteSelected(this.CurrentSettings.SelectedItems);
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
                DELETE_ALLOWED = false;
                
                this.AddButton.Content = "Save";
                this.AddFilterLabel.Content = "Edit Filter:";

                this.InputTime.Text = Convert.ToString(((Filter)this.CurrentSettings.SelectedItem).UserInputTime);
                this.FilterSelectionBox.SelectedItem = ((Filter)this.CurrentSettings.SelectedItem).FilterType;
                this.NumFrames.Text = Convert.ToString(((Filter)this.CurrentSettings.SelectedItem).NumExposures);
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
                foreach (Filter f in CurrentSettingsList.FilterSettings)
                    f.DisplayTime = f.SlewAdjustedTime;
            }
            else
            {
                foreach (Filter f in CurrentSettingsList.FilterSettings)
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
            string content = CurrentSettingsList.GenerateFileContent();
            FileFunctions.FileHandling.CurrentSettingsSave(content);
        }

        /// <summary>
        /// Load a filter settings list into the CurrentSettings pane
        /// Overwrite any present filters
        /// </summary>
        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            clearFilterSettings();
            
            string loaded = FileFunctions.FileHandling.CurrentSettingsLoad();

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
        /// Given a string containing the data from a filter settings file, populated the CurrentSettings list
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
                    CurrentSettingsList.Add((object)vals[0], vals[1], vals[2], (bool)TriggerSlewAdjust.IsChecked);
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
        private void disableFilterSettingsChanges()
        {
            AddButton.IsHitTestVisible = false;
            DeleteButton.IsHitTestVisible = false;
            EditButton.IsHitTestVisible = false;
            LoadButton.IsHitTestVisible = false;
            CurrentSettings.IsHitTestVisible = false;
            InputTime.KeyDown -= InputTime_KeyDown;
            NumFrames.KeyDown -= NumFrames_KeyDown;
            TriggerSlewAdjust.IsHitTestVisible = false;
            EfficientOrder.IsHitTestVisible = false;
        }

        /// <summary>
        /// Re-enables any Add, Edit, Delete, or Load functions
        /// </summary>
        private void enableFilterSettingsChanges()
        {
            AddButton.IsHitTestVisible = true;
            DeleteButton.IsHitTestVisible = true;
            EditButton.IsHitTestVisible = true;
            LoadButton.IsHitTestVisible = true;
            CurrentSettings.IsHitTestVisible = true;
            InputTime.KeyDown += InputTime_KeyDown;
            NumFrames.KeyDown +=NumFrames_KeyDown;
            TriggerSlewAdjust.IsHitTestVisible = true;
            EfficientOrder.IsHitTestVisible = true;
        }

        #endregion // Enable/Disable Changes

        #endregion // Current Settings

        #region Override Buttons

        /////////////////////////////////////////////////////////////////
        ///
        /// Methods for Override Buttons (Run, Acquire, Stop)
        ///
        /////////////////////////////////////////////////////////////////

        #region Run and Acquire

        /// <summary>
        /// Runs when the "Run" button is clicked
        /// </summary>
        private void RunOverride_Click(object sender, RoutedEventArgs e)
        {
            if ((bool)AutomatedControl.IsChecked)
            {
                if (SystemReady())
                {
                    // Update UI to reflect running
                    this.ManualControl.IsHitTestVisible = false;
                    setRunGreen();
                    disableFilterSettingsChanges();

                    // Create object to pass this and _APP
                    Tuple<ILightFieldApplication, ControlPanel, IDisplayViewerControl> args = new Tuple<ILightFieldApplication, ControlPanel, IDisplayViewerControl>(_APP, this, _VIEWER);

                    // Start capturing images
                    Thread imageCapturing = new Thread(Capturing.Run);
                    imageCapturing.Start(args);
                }
            }
            else
                AutomatedControlDisabledMessage();
        }

        /// <summary>
        /// Runs when the "Acquire" button is clicked
        /// </summary>
        private void AcquireOverride_Click(object sender, RoutedEventArgs e)
        {
            if ((bool)AutomatedControl.IsChecked)
            {
                if (SystemReady())
                {
                    // Update UI to reflect running
                    this.ManualControl.IsHitTestVisible = false;
                    setAcquireGreen();
                    disableFilterSettingsChanges();

                    // Create object to pass this and _APP
                    Tuple<ILightFieldApplication, ControlPanel, IDisplayViewerControl> args = new Tuple<ILightFieldApplication, ControlPanel, IDisplayViewerControl>(_APP, this, _VIEWER);

                    // Start capturing images
                    Thread imageCapturing = new Thread(Capturing.Acquire);
                    imageCapturing.Start(args);
                }
            }
            else
                AutomatedControlDisabledMessage();
        }

        #region Run and Acquire Color Changes

        /// <summary>
        /// Sets the background of the Run button to Green
        /// </summary>
        public void setRunGreen()
        {
            RunOverride.Background = Brushes.Green;
        }

        /// <summary>
        /// Sets the background of the Run button to Clear
        /// </summary>
        public void setRunClear()
        {
            RunOverride.ClearValue(Button.BackgroundProperty);
        }

        /// <summary>
        /// Sets the background of the Acquire button to Green
        /// </summary>
        public void setAcquireGreen()
        {
            AcquireOverride.Background = Brushes.Green;
        }

        /// <summary>
        /// Sets the background of the Acquire button to Clear
        /// </summary>
        public void setAcquireClear()
        {
            AcquireOverride.ClearValue(Button.BackgroundProperty);
        }

        #endregion // Run and Acquire Color Changes

        #endregion // Run and Acquire

        #region System Ready Checks

        /// <summary>
        /// Lets the program know the system is ready to capture images.
        /// </summary>
        /// <returns>bool true if system is ready, false otherwise</returns>
        private bool SystemReady()
        {
            // Check that a camera is attached
            IDevice camera = null;
            foreach (IDevice device in EXPERIMENT.ExperimentDevices)
            {
                if (device.Type == DeviceType.Camera)
                    camera = device;
            }
            if (camera == null)
            {
                MessageBox.Show("Camera not found.  Please ensure there is a camera attached to the system.\nIf the camera is attached, ensure you have loaded it into this experiment.");
                return false;
            }

            // Check that our camera can handle varying exposure times
            if (!EXPERIMENT.Exists(CameraSettings.ShutterTimingExposureTime))
            {
                MessageBox.Show("This camera does not support multiple exposure times.  Please ensure you are using the correct camera.");
                return false;
            }

            // Check that the user has saved the experiment
            if (EXPERIMENT == null)
            {
                MessageBox.Show("You must save the LightField experiment before beginning acquisition.");
            }

            // Check that LightField is ready to run
            if (!EXPERIMENT.IsReadyToRun)
            {
                MessageBox.Show("LightField is not ready to begin acquisition.  Please ensure all required settings have been set and there are no ongoing processes, then try again.");
                return false;
            }

            // Check that no acquisition is currently occuring
            if (EXPERIMENT.IsRunning)
            {
                MessageBox.Show("LightField is currently capturing images.  Please halt image capturing before attempting to begin a new capture session.");
                return false;
            }

            // Check that the user has entered some filter settings
            if (CurrentSettingsList.FilterSettings.Count == 0)
            {
                MessageBox.Show("To operate in multi-filter mode, you must specify some filter settings.\nIf you wish to operate manually, please use the Run and Acquire buttons at the top of the LightField window.");
                return false;
            }
            if ((string)this.AddButton.Content == "Save")
            {
                MessageBox.Show("Please finish editing the selected filter and click Save, then try again.");
                return false;
            }

            return true;
        }

        #endregion // System Ready Checks

        #region Error Message

        /// <summary>
        /// Displays an error message anytime an automated control button is pressed, but automated control is not activated.
        /// </summary>
        private static void AutomatedControlDisabledMessage()
        {
            MessageBox.Show("This control is disabled.  Please enable Automated Control.");
        }

        #endregion // Error Message

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

                Capturing._STOP = true;
                EXPERIMENT.Stop();
                Thread flashStopButton = new Thread(flashStop);
                flashStopButton.Start(FLASH_INTERVAL);

                enableFilterSettingsChanges();
            }
            else
                AutomatedControlDisabledMessage();
        }

        /// <summary>
        /// Flashes the StopOverride button between Red and Clear at interval set by 
        /// </summary>
        /// <param name="t">An int object holding half the period of the button flash.</param>
        private void flashStop(object t)
        {
            int flash_time = (int)t;
            while (Capturing._IS_RUNNING == true || Capturing._IS_ACQUIRING == true)
            {
                Application.Current.Dispatcher.Invoke(new Action(setStopRed));
                Thread.Sleep(flash_time);
                Application.Current.Dispatcher.Invoke(new Action(setStopClear));
                Thread.Sleep(flash_time);
            }
        }

        /// <summary>
        /// Sets the background of the StopOverride button to Red
        /// </summary>
        private void setStopRed()
        {
            StopOverride.Background = Brushes.Red;
        }

        /// <summary>
        /// Sets the background of the StopOverride button to clear
        /// </summary>
        private void setStopClear()
        {
            StopOverride.ClearValue(Button.BackgroundProperty);
        }
        
        /// <summary>
        /// Resets the UI after Stop
        /// </summary>
        public void ResetUI()
        {
            // Reactivate Manual Control features
            if(!Capturing._TRANSITIONING)
                ManualControl.IsHitTestVisible = true;

            // Update button colors to the user knows which features are active
            setAcquireClear();
            setRunClear();
        }

        #endregion // Stop

        #endregion // Override Buttons

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
                if (EXPERIMENT.IsRunning)
                    PleaseHaltCapturingMessage();
                else
                    ManualControlEnabledMessage();
            }
            else
                ManualControlDisabledMessage();
        }

        /// <summary>
        /// Sends a signal to the filter wheel to rotate the wheel clockwise (w.r.t. the camera)
        /// </summary>
        private void CW_Click(object sender, RoutedEventArgs e)
        {
            if (this.ManualControl.IsChecked == true)
            {
                if (EXPERIMENT.IsRunning)
                    PleaseHaltCapturingMessage();
                else
                    ManualControlEnabledMessage();
            }
            else
                ManualControlDisabledMessage();
        }

        /// <summary>
        /// Sends a signal to the filter wheel to rotate the wheel to the filter selected in the JumpSelectionBox drop down menu
        /// </summary>
        private void JumpButton_Click(object sender, RoutedEventArgs e)
        {
            if (ManualControl.IsChecked == true)
            {
                if (EXPERIMENT.IsRunning)
                    PleaseHaltCapturingMessage();
                else
                    ManualControlEnabledMessage();
            }
            else
                ManualControlDisabledMessage();
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
        public void updateFWInstrumentOrder()
        {
            string[] newOrder = WheelInteraction.getCurrentOrder();
            lock (FW_INST_LOCK)
            {
                for (int i = 0; i < FW_INST_LABELS.Count(); i++)
                    FW_INST_LABELS[i].Text = newOrder[i];
            }
        }

        /// <summary>
        /// Updates the filter wheel instrument on the instrument panel to reflect that the filter wheel is rotating
        /// </summary>
        public void updateFWInstrumentRotate()
        {
            lock (FW_INST_LOCK)
            {
                foreach (TextBlock t in FW_INST_LABELS)
                    t.Text = "...";
            }
        }

        /// <summary>
        /// Updates the Instrument Panel to show the latest frame metadata information
        /// </summary>
        /// <param name="m">The metadata object holding the new information</param>
        public void updatePanelMetaData(Metadata m, DateTime expStart, TimeSpan elapsed)
        {
            this.PrevExpSt.Text = ((DateTime)(expStart + m.ExposureStarted)).ToString("HH:mm:ss.ffff");
            this.PrevExpEnd.Text = ((DateTime)(expStart + m.ExposureEnded)).ToString("HH:mm:ss.ffff");
            this.PrevExpDur.Text = ((TimeSpan)(m.ExposureEnded - m.ExposureStarted)).ToString("c");
            this.ElapsedRunTime.Text = elapsed.ToString("c");
        }

        /// <summary>
        /// Given a new string to display, updates the current status to the new string and the previous status to the string that was in current status before the update.
        /// </summary>
        /// <param name="s">The string to change the current status to</param>
        public void updateCurrentPreviousStatus(String s)
        {
            this.PreviousStatus.Text = this.CurrentStatus.Text;
            this.CurrentStatus.Text = s;
        }

        #endregion Instrument Panel

    }
}
