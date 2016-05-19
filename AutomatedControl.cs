using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading;

namespace FilterWheelControl.ControlPanelFunctions
{
    public partial class ControlPanel : UserControl
    {
        /////////////////////////////////////////////////////////////////
        ///
        /// Methods for Override Buttons (Run, Acquire, Stop)
        ///
        /////////////////////////////////////////////////////////////////

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
        /// Runs when the "Acquire" button is clicked
        /// </summary>
        private void AcquireOverride_Click(object sender, RoutedEventArgs e)
        {
            if ((bool)AutomatedControl.IsChecked)
            {
                if (SystemReady())
                {
                    // Update UI to reflect acquiring
                    this.ManualControl.IsHitTestVisible = false;
                    this.AcquireOverride.Background = Brushes.Green;

                    // Begin running the acquire_images thread until the user clicks stop
                    _IS_RUNNING = true;
                    _STOP = false;
                    Thread acquisition = new Thread(acquire_images);
                    acquisition.Start();
                }
            }
            else
                AutomatedControlDisabledMessage();
        }

        /// <summary>
        /// Displays an error message anytime an automated control button is pressed, but automated control is not activated.
        /// </summary>
        private static void AutomatedControlDisabledMessage()
        {
            MessageBox.Show("This control is disabled.  Please enable Automated Control.");
        }

    }
}
