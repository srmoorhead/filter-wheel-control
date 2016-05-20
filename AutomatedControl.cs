using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading;
using PrincetonInstruments.LightField.AddIns;

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
            if(!EXPERIMENT.Exists(CameraSettings.ShutterTimingExposureTime))
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
            if (_IS_RUNNING || EXPERIMENT.IsRunning)
            {
                MessageBox.Show("LightField is currently acquiring images.  Please halt image capturing before attempting to begin a new capture session.");
                return false;
            }

            // Check that the user has entered some filter settings
            if (_FILTER_SETTINGS.Count == 0)
            {
                MessageBox.Show("To operate in multi-filter mode, you must specify some filter settings.\nIf you wish to operate manually, please use the Run and Acquire buttons at the top of the LightField window.");
                return false;
            }

            return true;
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
