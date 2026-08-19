using RightEdge.Core;
using RightEdge.Core.Helpers;
using RightEdge.Device;
using RightEdge.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MLM2PRO_BT_APP.RightEdge.Controls
{
    /// <summary>
    /// Interaction logic for KnownDeviceSelector.xaml
    /// </summary>
    public partial class KnownDeviceSelector : UserControl
    {
        ManagedPuttTrackerDevice? lastCreatedDeviceObj = null;
        bool _initialized = false;

        public KnownDeviceSelector()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            InitiateDeviceSelectionControls();
        }

        public String CurrentDeviceNickname
        {
            get
            {
                Init();
                String retVal = RESettings.CurrentAppSettings.defaultDeviceHostname;
                try
                {
                    KnownDevice kd = (KnownDevice)DeviceSelectorCurrentSelection.Tag;
                    retVal = kd.nickname;
                }
                catch { }

                return (retVal);
            }
        }

        public String CurrentDeviceHostName
        {
            get
            {
                Init();
                String retVal = RESettings.CurrentAppSettings.defaultDeviceHostname;
                try
                {
                    KnownDevice kd = (KnownDevice)DeviceSelectorCurrentSelection.Tag;
                    retVal = kd.name;
                }
                catch { }

                return (retVal);
            }
        }

        public String CurrentDeviceIPAddress
        {
            get
            {
                Init();
                String retVal = RESettings.CurrentAppSettings.defaultDeviceHostIP;
                try
                {
                    KnownDevice kd = (KnownDevice)DeviceSelectorCurrentSelection.Tag;
                    retVal = kd.ipAddress;
                }
                catch { }

                return (retVal);
            }
        }

        public String CurrentDeviceModel
        {
            get
            {
                Init();
                String retVal = "REPT2401";
                try
                {
                    KnownDevice kd = (KnownDevice)DeviceSelectorCurrentSelection.Tag;
                    retVal = kd.modelNum;
                }
                catch { }

                return (retVal);
            }
        }

        public bool HasCurrentDevice
        {
            get
            {
                return (!CurrentDeviceHostName.Trim().Equals(String.Empty));
            }
        }

        public event Action<KnownDevice>? SelectedDeviceChanged;

        public void Init()
        {
            if (!_initialized)
                InitiateDeviceSelectionControls();

            return;
        }

        public void ReInit()
        {
            InitiateDeviceSelectionControls();

            return;
        }

        public ManagedPuttTrackerDevice CreateSelectedDeviceObject()
        {
            //
            // Only create new if the last one is different than what is being requested now...
            //
            if (lastCreatedDeviceObj == null)
            {
                lastCreatedDeviceObj = new ManagedPuttTrackerDevice(CurrentDeviceHostName, CurrentDeviceIPAddress, "WIFI");
            }
            else
            {
                if ((lastCreatedDeviceObj.HostName != CurrentDeviceHostName) || (lastCreatedDeviceObj.IPAddress != CurrentDeviceIPAddress))
                {
                    lastCreatedDeviceObj = new ManagedPuttTrackerDevice(CurrentDeviceHostName, CurrentDeviceIPAddress, "WIFI");
                }
            }

            if (CurrentDeviceNickname.IndexOf("USB", StringComparison.OrdinalIgnoreCase) > (-1))
                lastCreatedDeviceObj.updatePreferredConnectionType(ConnectionType.USB);

            return (lastCreatedDeviceObj);
        }

        private void InitiateDeviceSelectionControls()
        {
            DeviceSelectorMenuFlyout.Items.Clear();
            DeviceSelectorCurrentSelection.Tag = null;
            DeviceSelectorCurrentSelection.Text = String.Empty;

            // Make sure we're working with the latest app configs...
            RESettings.Reload();

            List<KnownDevice> knownDevices = KnownDevices.Get();
            foreach (KnownDevice kd in knownDevices)
            {
                MenuItem kdItem = new MenuItem();
                kdItem.Tag = kd;
                kdItem.Header = !kd.nickname.Equals(String.Empty) ? kd.nickname : kd.name;
                kdItem.Click += DeviceSelectorMenuFlyoutItem_Click;

                DeviceSelectorMenuFlyout.Items.Add(kdItem);

                if ((DeviceSelectorCurrentSelection.Text == String.Empty) || kd.name.Equals(RESettings.CurrentAppSettings.defaultDeviceName))
                {
                    DeviceSelectorCurrentSelection.Tag = kd;
                    DeviceSelectorCurrentSelection.Text = kdItem.Header.ToString();
                }
            }

            if (DeviceSelectorMenuFlyout.Items.Count < 1)
            {
                KnownDevice kd = DefaultConnectionAsKnownDevice();
                if (kd.DisplayName != String.Empty)
                {
                    MenuItem kdItem = new MenuItem();
                    kdItem.Tag = kd;
                    kdItem.Header = !kd.nickname.Equals(String.Empty) ? kd.nickname : kd.name;
                    kdItem.Click += DeviceSelectorMenuFlyoutItem_Click;

                    // Add to the drop down and make it the current selection....
                    DeviceSelectorMenuFlyout.Items.Add(kdItem);

                    DeviceSelectorCurrentSelection.Tag = kd;
                    DeviceSelectorCurrentSelection.Text = kdItem.Header.ToString();
                }
            }

            // Add a Wired/USB option; and if the current preferred connection method is USB, make it the current selection...
            MenuItem usbWiredItem = UsbWiredDefaultDeviceMenuFlyoutItem();
            if (usbWiredItem != null)
            {
                DeviceSelectorMenuFlyout.Items.Add(UsbWiredDefaultDeviceMenuFlyoutItem());
                if (RESettings.CurrentAppSettings.preferredConnectionType == "USB")
                {
                    MenuItem usbFlyoutItem = (MenuItem)DeviceSelectorMenuFlyout.Items[DeviceSelectorMenuFlyout.Items.Count - 1];
                    DeviceSelectorCurrentSelection.Tag = usbFlyoutItem.Tag;
                    DeviceSelectorCurrentSelection.Text = usbFlyoutItem.Header.ToString();
                }
            }

            if (DeviceSelectorMenuFlyout.Items.Count < 1)
            {
                NoDevicesPanel.Visibility = Visibility.Visible;
                DeviceSelector.Visibility = Visibility.Collapsed;
            }
            else
            {
                NoDevicesPanel.Visibility = Visibility.Collapsed;
                DeviceSelector.Visibility = Visibility.Visible;
            }


            _initialized = true;
        }

        private KnownDevice DefaultConnectionAsKnownDevice()
        {
            KnownDevice retObj = new KnownDevice(RESettings.CurrentAppSettings.defaultDeviceHostname, "", "", RESettings.CurrentAppSettings.defaultDeviceHostIP, RESettings.CurrentAppSettings.defaultDeviceName);

            return (retObj);
        }

        private MenuItem? UsbWiredDefaultDeviceMenuFlyoutItem()
        {
            MenuItem? retObj = null;
            String defaultDeviceName = RESettings.CurrentAppSettings.defaultDeviceName;

            KnownDevice defaultKDevice = new KnownDevice();
            if (!KnownDevices.GetKnownDevice(defaultDeviceName, ref defaultKDevice))
                defaultKDevice = DefaultConnectionAsKnownDevice();

            if (!defaultKDevice.DisplayName.Equals(String.Empty))
            {
                retObj = new MenuItem();

                retObj.Header = "USB/Wired [" + defaultKDevice.DisplayName + "]";
                retObj.Tag = new KnownDevice(defaultKDevice.name, defaultKDevice.serialNum, defaultKDevice.modelNum, defaultKDevice.ipAddress, retObj.Header.ToString());
                retObj.Click += WiredUsbDeviceSelectorMenuFlyoutItem_Click;
            }

            return (retObj);
        }

        private void DeviceSelectorMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            object selectedObj = ((MenuItem)sender).Tag;

            DeviceSelectorCurrentSelection.Tag = ((MenuItem)sender).Tag;
            DeviceSelectorCurrentSelection.Text = ((MenuItem)sender).Header.ToString();

            // Get the newly selected KnownDevice and bubble up SelectedDeviceChanged event... 
            KnownDevice currentSelectedKd;
            try
            {
                currentSelectedKd = (KnownDevice)selectedObj;
            }
            catch (Exception)
            {
                currentSelectedKd = KnownDevices.GetLast();
            }

            SelectedDeviceChanged?.Invoke(currentSelectedKd);
        }

        private void WiredUsbDeviceSelectorMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            object selectedObj = ((MenuItem)sender).Tag;

            DeviceSelectorCurrentSelection.Tag = ((MenuItem)sender).Tag;
            DeviceSelectorCurrentSelection.Text = ((MenuItem)sender).Header.ToString();

            // Get the newly selected KnownDevice and bubble up SelectedDeviceChanged event... 
            KnownDevice currentSelectedKd;
            try
            {
                currentSelectedKd = (KnownDevice)selectedObj;
            }
            catch (Exception)
            {
                currentSelectedKd = KnownDevices.GetLast();
            }

            SelectedDeviceChanged?.Invoke(currentSelectedKd);

            return;
        }

        private void DeviceSelector_Click(object sender, RoutedEventArgs e)
        {
            if (DeviceSelector.ContextMenu != null)
            {
                DeviceSelector.ContextMenu.PlacementTarget = DeviceSelector;
                DeviceSelector.ContextMenu.IsOpen = true;
            }
        }
    }
}
