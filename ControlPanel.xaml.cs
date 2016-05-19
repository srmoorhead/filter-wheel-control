using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using WPF.JoshSmith.ServiceProviders.UI; // For ListView DragDrop Manager
using PrincetonInstruments.LightField.AddIns;

namespace FilterWheelControl.ControlPanelFunctions
{

    /// <summary>
    /// Interaction logic for ControlPanel.xaml
    /// </summary>
    
    #region Filter Class

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
        static string[] AVAILABLE_FILTERS = { "u", "g", "r", "i", "z", "BG40", "DARK" }; // Change this array if any filters get changed
        
        // LightField Variables
        volatile IExperiment EXPERIMENT;
        volatile ILightFieldApplication _APP;

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
    }
}
