using MLM2PRO_BT_APP.util;
using Newtonsoft.Json;
using InTheHand.Bluetooth;

namespace MLM2PRO_BT_APP.connections;

public class BluetoothManagerBackup : BluetoothBase<InTheHand.Bluetooth.BluetoothDevice>
{
    BluetoothDevice _bluetoothdevice;
    GattService _primaryService;
    public GattCharacteristic _GaTTeventsCharacteristicUuid;
    public GattCharacteristic _GaTTheartbeatCharacteristicUuid;
    public GattCharacteristic _GaTTwriteResponseCharacteristicUuid;
    public GattCharacteristic _GaTTmeasurementCharacteristic;
    protected Timer? _deviceDiscoveryTimer;
    bool currentlySearching = false;
    public BluetoothManagerBackup() : base()
    {
        Logger.Log("BACKUP BLUETOOTH MANAGER INITIALIZED");
        StartDeviceDiscoveryTimer();
    }

    protected void StartDeviceDiscoveryTimer()
    {
        // Stop the timer if it's already running
        _deviceDiscoveryTimer?.Dispose();

        // Start the timer to call the SendHeartbeatSignal method every 2 seconds (2000 milliseconds)
        _deviceDiscoveryTimer = new Timer(deviceDiscoveryTimerSignal, null, 0, 10000);
    }

    protected async void deviceDiscoveryTimerSignal(object? state)
    {
        if (_bluetoothDevice != null)
        {
            _deviceDiscoveryTimer?.Dispose();
            return;
        }
        else if (currentlySearching)
        {
            return;
        }
        else if (_bluetoothDevice == null && !currentlySearching)
        {
            await DiscoverDevicesAsync();
        }
    }

    public override async Task TriggerDeviceDiscovery()
    {
        if (App.SharedVm != null) App.SharedVm.LMStatus = "TRIGGERING DISCOVERY";
        DiscoverDevicesAsync();
    }

    public async Task DiscoverDevicesAsync()
    {
        try
        {
            if (App.SharedVm != null) App.SharedVm.LMStatus = "LOOKING FOR DEVICES";
            Logger.Log("BACKUP BLUETOOTH MANAGER LOOKING FOR DEVICES");
            currentlySearching = true;
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
        currentlySearching = false;
    }
    public async Task ConnectToDeviceAsync(BluetoothDevice device)
    {
        try
        {

            if (App.SharedVm != null) App.SharedVm.LMStatus = "ATTEMPTING TO CONNECT: : " + device.Name;
            device.GattServerDisconnected += Device_GattServerDisconnected;
            device.Gatt.ConnectAsync().Wait();
            device.Gatt.AutoConnect = true;
            _bluetoothDevice = device;
            _primaryService = _bluetoothDevice.Gatt.GetPrimaryServiceAsync(_serviceUuid).WaitAsync(TimeSpan.FromSeconds(5)).Result;
            if (App.SharedVm != null) App.SharedVm.LMStatus = "CONNECTION ESTABILISHED: " + device.Name ;
            currentlySearching = false;
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
        if (_bluetoothDevice != null)
        {
            Logger.Log("Device disconnected. Attempting to reconnect...");
            await ConnectToDeviceAsync(_bluetoothDevice);
        }
    }

    protected override async Task<bool> SubscribeToCharacteristicsAsync()
    {
        try
        {
            _GaTTeventsCharacteristicUuid = _primaryService.GetCharacteristicAsync(_eventsCharacteristicUuid).WaitAsync(TimeSpan.FromSeconds(5)).Result;
            _GaTTeventsCharacteristicUuid.CharacteristicValueChanged += Characteristic_ValueChanged;
            await _GaTTeventsCharacteristicUuid.StartNotificationsAsync();
            _GaTTheartbeatCharacteristicUuid = _primaryService.GetCharacteristicAsync(_heartbeatCharacteristicUuid).WaitAsync(TimeSpan.FromSeconds(5)).Result;
            _GaTTheartbeatCharacteristicUuid.CharacteristicValueChanged += Characteristic_ValueChanged;
            await _GaTTheartbeatCharacteristicUuid.StartNotificationsAsync();
            _GaTTwriteResponseCharacteristicUuid = _primaryService.GetCharacteristicAsync(_writeResponseCharacteristicUuid).WaitAsync(TimeSpan.FromSeconds(5)).Result;
            _GaTTwriteResponseCharacteristicUuid.CharacteristicValueChanged += Characteristic_ValueChanged;
            await _GaTTwriteResponseCharacteristicUuid.StartNotificationsAsync();
            _GaTTmeasurementCharacteristic = _primaryService.GetCharacteristicAsync(_measurementCharacteristicUuid).WaitAsync(TimeSpan.FromSeconds(5)).Result;
            _GaTTmeasurementCharacteristic.CharacteristicValueChanged += Characteristic_ValueChanged;
            await _GaTTmeasurementCharacteristic.StartNotificationsAsync();
        } 
        catch (Exception ex)
        {
            Logger.Log($"Error subscribing to characteristics: {ex.Message}");
            return false;
        }
        return true;
    }

    protected override async Task<byte[]> GetCharacteristicValueAsync(object args)
    {
        return (args as GattCharacteristicValueChangedEventArgs)?.Value?.ToArray() ?? new byte[0];
    }

    protected override async Task<Guid> GetSenderUuidAsync(object sender)
    {
        return (sender as GattCharacteristic)?.Uuid ?? Guid.Empty;
    }

    protected override async Task<bool> WriteCharacteristic(Guid serviceUuid, Guid characteristicUuid, byte[]? data)
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

        var service = _bluetoothDevice.Gatt.GetPrimaryServiceAsync(_serviceUuid).WaitAsync(TimeSpan.FromSeconds(5)).Result;
        if (service == null)
        {
            Logger.Log("Service not found.");
            return false;
        }

        var characteristic = _primaryService.GetCharacteristicAsync(characteristicUuid).WaitAsync(TimeSpan.FromSeconds(5)).Result;
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

    protected override void VerifyConnection(object? state)
    {
        throw new NotImplementedException();
    }

    protected override void ChildDisconnectAndCleanupFirst()
    {
        
    }

    protected override void ChildDisconnectAndCleanupSecond()
    {
        
    }

    protected override async Task UnsubscribeFromAllNotifications()
    {
        if (_bluetoothDevice != null)
        {
            try
            {
                await _GaTTeventsCharacteristicUuid.StopNotificationsAsync();
                await _GaTTheartbeatCharacteristicUuid.StopNotificationsAsync();
                await _GaTTwriteResponseCharacteristicUuid.StopNotificationsAsync();
                await _GaTTmeasurementCharacteristic.StopNotificationsAsync();
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
        var rssi = await ((BluetoothDevice)device).Gatt.ReadRssi();
        Logger.Log("Device RSSI: " + rssi);
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
        DiscoverDevicesAsync();
    }
}