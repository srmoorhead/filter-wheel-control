# filter-wheel-control
The files for the Filter Wheel Control add-in in LightField.

MouseUtilities.cs, DragAdorner.cs, and ListViewDragDropManager.cs are copyright (2007) Josh Smith.  See:  http://www.codeproject.com/Articles/17266/Drag-and-Drop-Items-in-a-WPF-ListView

ControlPanel.xaml.cs - Primary interaction logic for the control panel.
ControlPanel.xaml - Layout and design of control panel.
FilterWheelControl.cs - Launch file that loads add-in into LightField.
CaptureSession.cs - Handles capturing images in a synchronous manner with the filter wheel rotation.  Controls most parts of image acquisition.
CurrentSettingsList.cs - Handles all functionality involving the current settings list, including file IO.
FileHandler.cs - Handles all image file IO for .spe and .fits files.
Filters.cs - An object representing a filter in a filter wheel.  Closely mirrors a singly-linked list.
FilterWheelSim.cs - A simulator for a filter wheel.  Used while the physical wheel was not present for development.
WheelInterface.cs - Handles all interactions with the filter wheel hardware device.

All LightField methods and libraries are copyright protected by Princeton Instruments.  For information on LightField, see:  http://www.princetoninstruments.com/products/LightField

For questions on this add-in, contact Sean Moorhead at sean[dot]moorhead[at]utexas[dot]edu .
