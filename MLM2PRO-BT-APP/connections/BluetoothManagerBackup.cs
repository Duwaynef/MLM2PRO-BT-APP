using MLM2PRO_BT_APP.util;
using InTheHand.Bluetooth;

namespace MLM2PRO_BT_APP.connections;

public class BluetoothManagerBackup : BluetoothBase<BluetoothDevice>
{
    private GattService? _primaryService;
    private GattCharacteristic? _gaTTeventsCharacteristicUuid;
    private GattCharacteristic? _gaTTheartbeatCharacteristicUuid;
    private GattCharacteristic? _gaTTwriteResponseCharacteristicUuid;
    private GattCharacteristic? _gaTTmeasurementCharacteristic;
    private Timer? _deviceDiscoveryTimer;
    private bool _currentlySearching;
    private bool _cleaningUp;

    public BluetoothManagerBackup()
    {

        Logger.Log("BackupBluetooth: Running");
        if (!(SettingsManager.Instance.Settings?.LaunchMonitor?.AutoStartLaunchMonitor ?? true)) return;
        Logger.Log("BackupBluetooth: initialized");
        if (!string.IsNullOrEmpty(SettingsManager.Instance.Settings?.WebApiSettings?.WebApiSecret))
        {
            StartDeviceDiscoveryTimer();
        }

    }

    private void StartDeviceDiscoveryTimer()
    {
        _deviceDiscoveryTimer?.Dispose();
        _deviceDiscoveryTimer = new Timer(DeviceDiscoveryTimerSignal, null, 0, 10000);
    }
    private async void DeviceDiscoveryTimerSignal(object? state)
    {
        if (_currentlySearching) return;
        if (_cleaningUp) return;
        if (BluetoothDevice != null)
        {
            if (_deviceDiscoveryTimer != null) await _deviceDiscoveryTimer.DisposeAsync();
        }
        else if (BluetoothDevice == null && !_currentlySearching)
        {
            await DiscoverDevicesAsync();
        }
    }
    protected override async Task TriggerDeviceDiscovery()
    {
        _cleaningUp = false;
        if (_currentlySearching) return;
        if (App.SharedVm != null) App.SharedVm.LmStatus = "TRIGGERING DISCOVERY";
        Logger.Log("Device Discovery Triggered.");
        await DiscoverDevicesAsync();
    }
    private async Task DiscoverDevicesAsync()
    {
        try
        {
            if (_currentlySearching || _cleaningUp) return;
            if (App.SharedVm != null) App.SharedVm.LmStatus = "LOOKING FOR DEVICES";
            Logger.Log("BackupBluetooth: Looking for devices");
            _currentlySearching = true;
            
            
            await CheckForKnownBtDevices();
            // await Task.Delay(TimeSpan.FromSeconds(20));

            var pairedDevices = Bluetooth.GetPairedDevicesAsync().Result;

            foreach (var pairedDevice in pairedDevices)
            {
                Logger.Log("Found Paired Device: " +  pairedDevice.Name);
                if (pairedDevice.Name.Contains("MLM2-") || pairedDevice.Name.Contains("BlueZ ")|| pairedDevice.Name.Contains("MLM2_BT_"))
                {
                    if (App.SharedVm != null) App.SharedVm.LmStatus = "FOUND PAIRED DEVICE: " + pairedDevice.Name;
                    Logger.Log("BackupBluetooth: Found device in Bluetooth cache: " + pairedDevice.Name);
                    await ConnectToDeviceAsync(pairedDevice);
                    if (BluetoothDevice != null) return;
                }
                else
                {
                    if (App.SharedVm != null) App.SharedVm.LmStatus = "BLUETOOTH MANAGER FOUND NO MATCHES";
                    Logger.Log("BackupBluetooth: Found no match for device: " + pairedDevice.Name);
                    Logger.Log(pairedDevice.Id);
                }
            }
            //Logger.Log("BackupBluetooth: No paired devices found, checking for known devices...");

        }
        catch (Exception ex)
        {
            Logger.Log($"Error during device discovery: {ex.Message}");
        }
        await Task.Delay(TimeSpan.FromSeconds(10));
        _currentlySearching = false;
    }
    private async Task CheckForKnownBtDevices()
    {
        try
        {
            if (SettingsManager.Instance.Settings?.LaunchMonitor?.KnownBluetoothIDs != null)
            {
                foreach (string device in SettingsManager.Instance.Settings.LaunchMonitor.KnownBluetoothIDs)
                {
                    Logger.Log("BackupBluetooth: KnownDevices: Searching: " + device);
                    var btDevice = await GetDeviceFromIdAsync(device);
                    Logger.Log("BackupBluetooth: KnownDevices: Found device: " + btDevice?.Name);
                    if (btDevice == null) continue;
                    Logger.Log("BackupBluetooth: KnownDevices: isConnected? : " + btDevice.Gatt.IsConnected);
                    Logger.Log("BackupBluetooth: KnownDevices: trying to connect...: " + btDevice.Name);
                    await ConnectToDeviceKnownAsync(btDevice);
                    Logger.Log("BackupBluetooth: KnownDevices: returning. Device connected? " + BluetoothDevice?.Gatt.IsConnected);
                    if (BluetoothDevice is { Gatt.IsConnected: true }) return;
                }
            }
            Logger.Log("BackupBluetooth: No known devices found.");
        }
        catch (Exception ex)
        {
            Logger.Log($"Error during device discovery: {ex.Message}");
        }        
    }
    private static async Task<BluetoothDevice?> GetDeviceFromIdAsync(string deviceId)
    {
        try
        {
            return await BluetoothDevice.FromIdAsync(deviceId);
        }
        catch (Exception ex)
        {
            Logger.Log($"Failed to connect to device: {ex.Message}");
            return null;
        }
    }    
    private async Task ConnectToDeviceKnownAsync(BluetoothDevice device)
    {
        try
        {
            if (App.SharedVm != null) App.SharedVm.LmStatus = "ATTEMPTING TO CONNECT: : " + device.Name;
            device.GattServerDisconnected += Device_GattServerDisconnected!; //@TODO: Sometimes this crashes, need ot figure that out
            Logger.Log("Connect Async Starting: " + device.Name);
            Logger.Log("Connect Async attempting to connect to : " + device.Name);
            await device.Gatt.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Logger.Log("Device Name     : " + device.Name);
            Logger.Log("Device Connected: " + device.Gatt.IsConnected);
            Logger.Log("Device ID       : " + device.Id);
            Logger.Log("Device MTU      : " + device.Gatt.Mtu);
            _primaryService = await device.Gatt.GetPrimaryServiceAsync(ServiceUuid).WaitAsync(TimeSpan.FromSeconds(10));
            Logger.Log("Got Primary Service: " + device.Name);
            
            // device.Gatt.AutoConnect = true;
            if (_primaryService == null)
            {
                Logger.Log("Primary service not found.");
                return;
            }
            BluetoothDevice = device;
            if (App.SharedVm != null) App.SharedVm.LmStatus = "CONNECTION ESTABLISHED: " + device.Name ;
            await SetupBluetoothDevice();
            _currentlySearching = false;
        }
        catch (Exception ex)
        {
            Logger.Log($"Error connecting to device: {ex.Message}");
        }
    }
    private async Task ConnectToDeviceAsync(BluetoothDevice device)
    {
        try
        {
            if (App.SharedVm != null) App.SharedVm.LmStatus = "ATTEMPTING TO CONNECT: : " + device.Name;
            // device.GattServerDisconnected += Device_GattServerDisconnected!; //@TODO: Sometimes this crashes, need ot figure that out
            Logger.Log("Connect Async Starting: " + device.Name);
            Logger.Log("Connect Async attempting to connect to : " + device.Name);
            await device.Gatt.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(20));
            Logger.Log("Device Name     : " + device.Name);
            Logger.Log("Device Connected: " + device.Gatt.IsConnected);
            Logger.Log("Device ID       : " + device.Id);
            Logger.Log("Device MTU      : " + device.Gatt.Mtu);
            _primaryService = await device.Gatt.GetPrimaryServiceAsync(ServiceUuid).WaitAsync(TimeSpan.FromSeconds(20));
            Logger.Log("Got Primary Service: " + device.Name);

            // device.Gatt.AutoConnect = true;
            if (_primaryService == null)
            {
               Logger.Log("Primary service not found.");
                return;
            }
            BluetoothDevice = device;
            if (App.SharedVm != null) App.SharedVm.LmStatus = "CONNECTION ESTABLISHED: " + device.Name ;
            await SetupBluetoothDevice();
            _currentlySearching = false;
        }
        catch (Exception ex)
        {
            Logger.Log($"Error connecting to device: {ex.Message}");
        }
    }
    private async void Device_GattServerDisconnected(object sender, EventArgs e)
    {
        if (sender is not BluetoothDevice _ || _cleaningUp || _currentlySearching) return;
        Logger.Log("Device disconnected. Attempting to reconnect...");
        await TriggerDeviceDiscovery();
    }
    protected override async Task<bool> SubscribeToCharacteristicsAsync()
    {
        try
        {
            if (_primaryService == null) return false;
            _gaTTeventsCharacteristicUuid = await _primaryService.GetCharacteristicAsync(EventsCharacteristicUuid).WaitAsync(TimeSpan.FromSeconds(5));
            if (_gaTTeventsCharacteristicUuid == null) return false;
            _gaTTeventsCharacteristicUuid.CharacteristicValueChanged += Characteristic_ValueChanged;
            await _gaTTeventsCharacteristicUuid.StartNotificationsAsync();

            _gaTTheartbeatCharacteristicUuid = await _primaryService.GetCharacteristicAsync(HeartbeatCharacteristicUuid).WaitAsync(TimeSpan.FromSeconds(5));
            if (_gaTTheartbeatCharacteristicUuid == null) return false;
            _gaTTheartbeatCharacteristicUuid.CharacteristicValueChanged += Characteristic_ValueChanged;
            await _gaTTheartbeatCharacteristicUuid.StartNotificationsAsync();

            _gaTTwriteResponseCharacteristicUuid = await _primaryService.GetCharacteristicAsync(WriteResponseCharacteristicUuid).WaitAsync(TimeSpan.FromSeconds(5));
            if (_gaTTwriteResponseCharacteristicUuid == null) return false;
            _gaTTwriteResponseCharacteristicUuid.CharacteristicValueChanged += Characteristic_ValueChanged;
            await _gaTTwriteResponseCharacteristicUuid.StartNotificationsAsync();

            _gaTTmeasurementCharacteristic = await _primaryService.GetCharacteristicAsync(MeasurementCharacteristicUuid).WaitAsync(TimeSpan.FromSeconds(5));
            if (_gaTTmeasurementCharacteristic == null) return false;
            _gaTTmeasurementCharacteristic.CharacteristicValueChanged += Characteristic_ValueChanged;
            await _gaTTmeasurementCharacteristic.StartNotificationsAsync();
            Logger.Log("Successfully Subscribed to all notifications");
        } 
        catch (Exception ex)
        {
            Logger.Log($"Error subscribing to characteristics: {ex.Message}");
            return false;
        }
        return true;
    }
    protected override byte[] GetCharacteristicValueAsync(object args)
    {
        return (args as GattCharacteristicValueChangedEventArgs)?.Value?.ToArray() ?? Array.Empty<byte>();
    }
    protected override Guid GetSenderUuidAsync(object sender)
    {
        return (sender as GattCharacteristic)?.Uuid ?? Guid.Empty;
    }
    protected override async Task<bool> WriteCharacteristic(Guid serviceUuid, Guid characteristicUuid, byte[]? data)
    {
        try
        {
            if (serviceUuid == Guid.Empty || characteristicUuid == Guid.Empty || data == null)
            {
                Logger.Log("Invalid input provided.");
                return false;
            }

            if (BluetoothDevice == null)
            {
                Logger.Log("Bluetooth device not connected.");
                return false;
            }

            _ = await BluetoothDevice.Gatt.GetPrimaryServiceAsync(ServiceUuid).WaitAsync(TimeSpan.FromSeconds(5));

            if (_primaryService != null)
            {
                var characteristic = await _primaryService.GetCharacteristicAsync(characteristicUuid).WaitAsync(TimeSpan.FromSeconds(5));
                try
                {
                    await characteristic.WriteValueWithoutResponseAsync(data);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Log("failed to write: " + ex);
                    return false;
                }
            }
            return false;
        }
        catch (TimeoutException)
        {
            Logger.Log("Operation timed out.");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Log("failed to write: " + ex);
            return false;
        }
    }
    protected override void VerifyConnection(object? state)
    {
        throw new NotImplementedException();
    }
    protected override void ChildDisconnectAndCleanupFirst()
    {
        _cleaningUp = true;
        _deviceDiscoveryTimer?.Dispose();
    }
    protected override void ChildDisconnectAndCleanupSecond()
    {
        BluetoothDevice?.Gatt.Disconnect();
        BluetoothDevice = null;
    }
    protected override async Task UnsubscribeFromAllNotifications()
    {
        if (BluetoothDevice != null)
        {
            try
            {
                if (_gaTTeventsCharacteristicUuid != null) //@TODO: Make sure i fixed the crash here.
                {
                    await _gaTTeventsCharacteristicUuid.StopNotificationsAsync();
                }

                if (_gaTTheartbeatCharacteristicUuid != null)
                {
                    await _gaTTheartbeatCharacteristicUuid.StopNotificationsAsync();
                }

                if (_gaTTwriteResponseCharacteristicUuid != null)
                {
                    await _gaTTwriteResponseCharacteristicUuid.StopNotificationsAsync();
                }

                if (_gaTTmeasurementCharacteristic != null)
                {
                    await _gaTTmeasurementCharacteristic.StopNotificationsAsync();
                }
                Logger.Log("Successfully unsubscribed from all notifications.");
            }
            catch (Exception ex)
            {
                Logger.Log($"Error during unsubscription: {ex.Message}");
            }
        }
    }
    protected override async Task<bool> VerifyDeviceConnection(object device)
    {
        try
        {
            _ = await ((BluetoothDevice)device).Gatt.GetPrimaryServiceAsync(ServiceUuid).WaitAsync(TimeSpan.FromSeconds(5));
            return true;
        }
        catch (TimeoutException)
        {
            Logger.Log("Operation timed out.");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Log($"An error occurred: {ex.Message}");
            return false;
        }
    }
    public override async Task RestartDeviceWatcher()
    {
        await TriggerDeviceDiscovery();
    }
}