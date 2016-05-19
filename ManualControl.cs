using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using PrincetonInstruments.LightField.AddIns;

namespace FilterWheelControl.ControlPanelFunctions
{
    public partial class ControlPanel : UserControl
    {
        /////////////////////////////////////////////////////////////////
        ///
        /// Methods for Manual Filter Control
        ///
        /////////////////////////////////////////////////////////////////

        #region Buttons

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

        #endregion // Buttons

        #region Messages

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

        #endregion //Messages

    }
}
