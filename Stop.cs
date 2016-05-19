using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace FilterWheelControl.ControlPanelFunctions
{
    public partial class ControlPanel : UserControl
    {
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
    }
}
