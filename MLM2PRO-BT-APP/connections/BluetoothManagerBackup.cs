using MLM2PRO_BT_APP.util;
using Newtonsoft.Json;
using InTheHand.Bluetooth;

namespace MLM2PRO_BT_APP.connections;

public class BluetoothManagerBackup : BluetoothBase<InTheHand.Bluetooth.BluetoothDevice>
{
    BluetoothDevice? _bluetoothdevice;
    GattService? _primaryService;
    private GattCharacteristic? _gaTTeventsCharacteristicUuid;
    private GattCharacteristic? _gaTTheartbeatCharacteristicUuid;
    private GattCharacteristic? _gaTTwriteResponseCharacteristicUuid;
    private GattCharacteristic? _gaTTmeasurementCharacteristic;
    private Timer? _deviceDiscoveryTimer;
    private bool _currentlySearching = false;
    private bool _cleaningUp = false;
    public BluetoothManagerBackup() : base()
    {
        Logger.Log("BackupBluetooth: Running");
        if (!(SettingsManager.Instance?.Settings?.LaunchMonitor?.AutoStartLaunchMonitor ?? true)) return;
        Logger.Log("BackupBluetooth: initialized");
        StartDeviceDiscoveryTimer();
    }

    private void StartDeviceDiscoveryTimer()
    {
        // Stop the timer if it's already running
        _deviceDiscoveryTimer?.Dispose();

        // Start the timer to call the SendHeartbeatSignal method every 2 seconds (2000 milliseconds)
        _deviceDiscoveryTimer = new Timer(DeviceDiscoveryTimerSignal, null, 0, 10000);
    }

    private async void DeviceDiscoveryTimerSignal(object? state)
    {
        if (_bluetoothDevice != null)
        {
            if (_deviceDiscoveryTimer != null) await _deviceDiscoveryTimer.DisposeAsync();
            return;
        }
        else if (_currentlySearching)
        {
            return;
        }
        else if (_bluetoothDevice == null && !_currentlySearching)
        {
            await DiscoverDevicesAsync();
        }
    }
    public override async Task TriggerDeviceDiscovery()
    {
        _cleaningUp = false;
        if (App.SharedVm != null) App.SharedVm.LMStatus = "TRIGGERING DISCOVERY";
        await DiscoverDevicesAsync();
    }

    public async Task DiscoverDevicesAsync()
    {
        try
        {
            if (App.SharedVm != null) App.SharedVm.LMStatus = "LOOKING FOR DEVICES";
            Logger.Log("BACKUP BLUETOOTH MANAGER LOOKING FOR DEVICES");
            _currentlySearching = true;
            foreach (BluetoothDevice pairedDevice in Bluetooth.GetPairedDevicesAsync().Result)
            {
                if (pairedDevice.Name.Contains("MLM2-") || pairedDevice.Name.Contains("BlueZ "))
                {
                    if (App.SharedVm != null) App.SharedVm.LMStatus = "FOUND PAIRED DEVICE: " + pairedDevice.Name;
                    Logger.Log("BACKUP BLUETOOTH MANAGER FOUND PAIRED DEVICE IN BT CACHE ATTEMPTING TO CONNECT CONNECTING: " + pairedDevice.Name);
                    await ConnectToDeviceAsync(pairedDevice);
                    return;
                }
                else
                {
                    if (App.SharedVm != null) App.SharedVm.LMStatus = "BLUETOOTH MANAGER FOUND NO MATCHES";
                    Logger.Log("BACKUP BLUETOOTH MANAGER FOUND BUT NO MATCH: " + pairedDevice.Name);
                    Logger.Log(pairedDevice.Id);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Error during device discovery: {ex.Message}");
        }
        _currentlySearching = false;
    }
    public async Task ConnectToDeviceAsync(BluetoothDevice device)
    {
        try
        {

            if (App.SharedVm != null) App.SharedVm.LMStatus = "ATTEMPTING TO CONNECT: : " + device.Name;
            device.GattServerDisconnected += Device_GattServerDisconnected;
            device.Gatt.ConnectAsync().Wait();
            device.Gatt.AutoConnect = true;
            _primaryService = await device.Gatt.GetPrimaryServiceAsync(_serviceUuid).WaitAsync(TimeSpan.FromSeconds(5));
            if (_primaryService == null)
            {
                Logger.Log("Primary service not found.");
                return;
            }
            _bluetoothDevice = device;
            if (App.SharedVm != null) App.SharedVm.LMStatus = "CONNECTION ESTABILISHED: " + device.Name ;
            _currentlySearching = false;
            await SetupBluetoothDevice();
        }
        catch (Exception ex)
        {
            Logger.Log($"Error connecting to device: {ex.Message}");
        }
    }
    private async void Bluetooth_AvailabilityChanged(object sender, EventArgs e)
    {
        var current = await Bluetooth.GetAvailabilityAsync();
        System.Diagnostics.Debug.Write($"Availability: {current}");
    }
    private void Bluetooth_AdvertisementReceived(object sender, BluetoothAdvertisingEvent e)
    {
        Logger.Log($"Name:{e.Name} Rssi:{e.Rssi}");
    }
    private async void Device_GattServerDisconnected(object sender, EventArgs e)
    {
        var device = sender as BluetoothDevice;
        if (_bluetoothDevice != null && !_cleaningUp)
        {
            Logger.Log("Device disconnected. Attempting to reconnect...");
            await TriggerDeviceDiscovery();
        }
    }
    protected override async Task<bool> SubscribeToCharacteristicsAsync()
    {
        try
        {
            if (_primaryService == null) return false;
            _gaTTeventsCharacteristicUuid = await _primaryService.GetCharacteristicAsync(_eventsCharacteristicUuid).WaitAsync(TimeSpan.FromSeconds(5));
            if (_gaTTeventsCharacteristicUuid == null) return false;
            _gaTTeventsCharacteristicUuid.CharacteristicValueChanged += Characteristic_ValueChanged;
            await _gaTTeventsCharacteristicUuid.StartNotificationsAsync();

            _gaTTheartbeatCharacteristicUuid = await _primaryService.GetCharacteristicAsync(_heartbeatCharacteristicUuid).WaitAsync(TimeSpan.FromSeconds(5));
            if (_gaTTheartbeatCharacteristicUuid == null) return false;
            _gaTTheartbeatCharacteristicUuid.CharacteristicValueChanged += Characteristic_ValueChanged;
            await _gaTTheartbeatCharacteristicUuid.StartNotificationsAsync();

            _gaTTwriteResponseCharacteristicUuid = await _primaryService.GetCharacteristicAsync(_writeResponseCharacteristicUuid).WaitAsync(TimeSpan.FromSeconds(5));
            if (_gaTTwriteResponseCharacteristicUuid == null) return false;
            _gaTTwriteResponseCharacteristicUuid.CharacteristicValueChanged += Characteristic_ValueChanged;
            await _gaTTwriteResponseCharacteristicUuid.StartNotificationsAsync();

            _gaTTmeasurementCharacteristic = await _primaryService.GetCharacteristicAsync(_measurementCharacteristicUuid).WaitAsync(TimeSpan.FromSeconds(5));
            if (_gaTTmeasurementCharacteristic == null) return false;
            _gaTTmeasurementCharacteristic.CharacteristicValueChanged += Characteristic_ValueChanged;
            await _gaTTmeasurementCharacteristic.StartNotificationsAsync();
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
        return (args as GattCharacteristicValueChangedEventArgs)?.Value?.ToArray() ?? new byte[0];
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

            if (_bluetoothDevice == null)
            {
                Logger.Log("Bluetooth device not connected.");
                return false;
            }

            var service = await _bluetoothDevice.Gatt.GetPrimaryServiceAsync(_serviceUuid).WaitAsync(TimeSpan.FromSeconds(5));
            if (service == null)
            {
                Logger.Log("Service not found.");
                return false;
            }

            var characteristic = await _primaryService.GetCharacteristicAsync(characteristicUuid).WaitAsync(TimeSpan.FromSeconds(5));
            if (characteristic == null)
            {
                Logger.Log("Characteristic not found.");
                return false;
            }
            else
            {
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
        _bluetoothDevice?.Gatt.Disconnect();
        _bluetoothDevice = null;
    }
    protected override async Task UnsubscribeFromAllNotifications()
    {
        if (_bluetoothDevice != null)
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
    public override async Task<bool> VerifyDeviceConnection(object device)
    {

        var characteristic = await ((BluetoothDevice)device).Gatt.GetPrimaryServiceAsync(_serviceUuid);
        if (characteristic != null)
        {
            Logger.Log("verify device connection: got GATT services");
            return true;
        }
        else
        {
            Logger.Log("verify device connection: failed to get GATT services");
            return false;
        }
    }
    public override async Task RestartDeviceWatcher()
    {
        await DiscoverDevicesAsync();
    }
}