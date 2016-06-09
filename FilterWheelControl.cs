using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.AddIn;

using PrincetonInstruments.LightField.AddIns;

namespace FilterWheelControl
{
    [AddIn("Filter Wheel Control",
            Version = "0.2.2",
            Publisher = "University of Texas Dept. of Astronomy - S. R. Moorhead",
            Description = "Allows the observer to manipulate parameters relating to the control of a filter wheel.")]
    
    public class FilterWheelControlPanel : AddInBase, ILightFieldAddIn
    {
        private ControlPanelFunctions.ControlPanel control_;
        
        public UISupport UISupport { get { return UISupport.ExperimentView; } }
        
        public void Activate(ILightFieldApplication app)
        {
            // Capture Interface
            LightFieldApplication = app;

            ScrollViewer sv = new ScrollViewer();            
            // Build controls
            control_ = new ControlPanelFunctions.ControlPanel(app);

            sv.Content = control_;
            ExperimentViewElement = sv;//control_;

            // Initialize The Base with the controls dispatcher            
            Initialize(control_.Dispatcher, "Filter Wheel Control");
        }
        
        public void Deactivate() { }        
        
        public override string UIExperimentViewTitle { get { return "Filter Wheel Control"; } }
        
    }
}
