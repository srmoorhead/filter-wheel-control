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

using System.Collections.ObjectModel;
using PrincetonInstruments.LightField.AddIns;

namespace FilterWheelControl.ControlPanelFunctions
{
    public partial class ControlPanel : UserControl
    {
        /////////////////////////////////////////////////////////////////
        ///
        /// Methods for Populating and Editing the Current Settings List
        ///
        /////////////////////////////////////////////////////////////////

        #region Instance Variables

        static string InputTimeTextbox_DEFAULT_TEXT = "Exposure Time (s)"; // Change this string if you wish to change the text in the InputTime textbox
        static string NumFramesTextbox_DEFAULT_TEXT = "Num"; // Change this string if you wish to change the text in the NumFrames textbox
        volatile ObservableCollection<Filter> _FILTER_SETTINGS = new ObservableCollection<Filter>();

        public ObservableCollection<Filter> FilterSettings
        { get { return _FILTER_SETTINGS; } }

        #endregion // Instance Variables

        #region Add

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

        #endregion // Add

        #region Check User Inputs

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

        #endregion // Check User Inputs

        #region TextBox Default Text Checks

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

        #endregion // TextBox Default Text Checks

        #region Delete Items

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
            for (int i = 0; i < _FILTER_SETTINGS.Count; i++)
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

        #endregion // Delete Items

        #region Edit Items

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

        #endregion // Edit Items
    }
}
