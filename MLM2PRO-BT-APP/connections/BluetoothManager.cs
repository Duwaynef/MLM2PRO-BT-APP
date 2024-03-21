using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using MLM2PRO_BT_APP.devices;
using MLM2PRO_BT_APP.util;
using Newtonsoft.Json;

namespace MLM2PRO_BT_APP.connections;

public class BluetoothManager : BluetoothBase<Windows.Devices.Bluetooth.BluetoothLEDevice>
{
    private DeviceWatcher? _deviceWatcher;
    protected readonly Dictionary<string, DeviceInformation?> _foundDevices = new Dictionary<string, DeviceInformation?>();
    public BluetoothManager() : base()
    {
        InitializeDeviceWatcher();
    }
    private void InitializeDeviceWatcher()
    {
        var serviceSelector = GattDeviceService.GetDeviceSelectorFromUuid(_serviceUuid);
        _deviceWatcher = DeviceInformation.CreateWatcher(serviceSelector);

        _deviceWatcher.Added += DeviceWatcher_Added;
        _deviceWatcher.Updated += DeviceWatcher_Updated;
        // _deviceWatcher.Removed += DeviceWatcher_Removed;
        if (SettingsManager.Instance.Settings.LaunchMonitor.AutoStartLaunchMonitor)
        {
            _deviceWatcher.Start();
        }
    }
    private async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation deviceInfo)
    {
        if (!_foundDevices.TryAdd(deviceInfo.Id, deviceInfo))
        {
            Logger.Log("Device Already in found devices list: " + deviceInfo.Name);
            return;
        }
        Logger.Log("Device Watcher found: " + deviceInfo.Name);
        DeviceWatcher_StartDeviceConnection(deviceInfo);
    }
    private async void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
    {
        if (!_foundDevices.TryGetValue(deviceInfoUpdate.Id, out var deviceInfo)) return;
        deviceInfo?.Update(deviceInfoUpdate);
        Logger.Log("Device Watcher updated " + deviceInfo?.Name);
        var isConnected = _bluetoothDevice != null && await VerifyDeviceConnection(_bluetoothDevice);
        if (!isConnected)
        {
            if (deviceInfo != null) DeviceWatcher_StartDeviceConnection(deviceInfo);
        }
    }
    private async void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
    {
        // Remove the device from the internal dictionary.
        if (!_foundDevices.TryGetValue(deviceInfoUpdate.Id, out var deviceInfo)) return;
        //deviceInfo?.Update(deviceInfoUpdate);
        //_foundDevices.Remove(deviceInfoUpdate.Id);
        Logger.Log("Device Watcher removed " + deviceInfo?.Name);
        //await DisconnectAndCleanup();
    }
    private async void DeviceWatcher_StartDeviceConnection(DeviceInformation deviceInfo)
    {
        Logger.Log("Device Watcher start device connection " + deviceInfo.Name);
        if (SettingsManager.Instance.Settings.WebApiSettings.WebApiSecret != "")
        {
            Logger.Log("Device Watcher connecting to device " + deviceInfo.Name);
            var device = await BluetoothLEDevice.FromIdAsync(deviceInfo.Id);
            Logger.Log("Device Watcher verifying the device connection " + deviceInfo.Name);
            var isConnected = await VerifyDeviceConnection(device);
            if (isConnected)
            {
                Logger.Log("Device Watcher verified device connection " + deviceInfo.Name);
                _bluetoothDevice = device;
                await SetupBluetoothDevice();
            } else
            {
                Logger.Log("Device Watcher connection to device failed " + deviceInfo.Name);
            }
        } else
        {
            Logger.Log("Device Watcher stopped device connection " + deviceInfo.Name + " web api token is blank or incorrect");
            if (App.SharedVm != null) App.SharedVm.LMStatus = "WEB API TOKEN MISSING OR INCORRECT";
        }
    }
    public override async Task RestartDeviceWatcher()
    {
        if (_deviceWatcher != null && (_deviceWatcher.Status == DeviceWatcherStatus.Started || _deviceWatcher.Status == DeviceWatcherStatus.EnumerationCompleted))
        {
            Logger.Log("Restarting device watcher");
            _deviceWatcher.Stop();
            _foundDevices.Clear();

            while (_deviceWatcher.Status != DeviceWatcherStatus.Created && _deviceWatcher.Status != DeviceWatcherStatus.Stopped && _deviceWatcher.Status != DeviceWatcherStatus.Aborted)
            {
                await Task.Delay(100);
            }
        }

        _deviceWatcher?.Start();
        Logger.Log("Device watcher restarted");
    }
    protected override async Task<bool> SubscribeToCharacteristicsAsync()
    {
        var serviceResult = await _bluetoothDevice?.GetGattServicesForUuidAsync(_serviceUuid);
        if (serviceResult.Status != GattCommunicationStatus.Success)
        {
            return false;
        }

        foreach (var service in serviceResult.Services)
        {
            foreach (var charUuid in _notifyUuiDs)
            {
                var charResult = await service.GetCharacteristicsForUuidAsync(charUuid);
                if (charResult.Status == GattCommunicationStatus.Success)
                {
                    var characteristic = charResult.Characteristics.FirstOrDefault();
                    if (characteristic != null)
                    {
                        try
                        {
                            GattCommunicationStatus status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                            if (status == GattCommunicationStatus.Success)
                            {
                                // Successfully subscribed
                                characteristic.ValueChanged += Characteristic_ValueChanged;
                                Logger.Log($"Subscribed to notifications for characteristic {characteristic.Uuid}");
                            }
                        }
                        catch
                        {
                            DeviceManager.Instance.DeviceStatus = "NOT CONNECTED";
                            Logger.Log("Characteristic Error: in SetupDeviceAsync");
                            await RetryBtConnection();
                            return false;
                        }
                    }
                }
            }
        }
        return true;
    }
    protected override async Task<byte[]> GetCharacteristicValueAsync(object args)
    {
        return (args as GattValueChangedEventArgs)?.CharacteristicValue.ToArray() ?? new byte[0];
    }
    protected override async Task<Guid> GetSenderUuidAsync(object sender)
    {
        return (sender as GattCharacteristic)?.Uuid ?? Guid.Empty;
    }
    private async Task<GattDeviceService?> GetGattServiceAsync(Guid serviceUuid)
    {
        var services = await _bluetoothDevice?.GetGattServicesAsync();
        return services.Services.FirstOrDefault(s => s.Uuid == serviceUuid);
    }
    private async Task<GattCharacteristic?> GetCharacteristicAsync(GattDeviceService service, Guid characteristicUuid)
    {
        var characteristics = await service.GetCharacteristicsAsync();
        if (characteristics == null)
        {
            return null;
        }

        return characteristics.Characteristics.FirstOrDefault(c => c.Uuid == characteristicUuid);
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

        var service = await GetGattServiceAsync(serviceUuid);
        if (service == null)
        {
            Logger.Log("Service not found.");
            return false;
        }

        var characteristic = await GetCharacteristicAsync(service, characteristicUuid);
        if (characteristic == null)
        {
            Logger.Log("Characteristic not found.");
            return false;
        }
        else
        {
            try
            {
                var writeResult = await characteristic.WriteValueAsync(data.AsBuffer());
                return writeResult == GattCommunicationStatus.Success;
            }
            catch
            {
                Logger.Log("failed to write.");
                return false;
            }
        }
    }
    protected override async void VerifyConnection(object? state)
    {
        try
        {
            var readResult = await ReadCharacteristicValueAsync(_serviceUuid, _measurementCharacteristicUuid);
            if (readResult == null || readResult.Length == 0)
            {
                if (App.SharedVm != null) App.SharedVm.LMStatus = "CONNECTION MIGHT BE LOST, RECONNECTING";
                Logger.Log("Connection might be lost. Re-subscribing...");
                await RetryBtConnection();
            }
            else
            {
                Logger.Log("Connection verified successfully.");
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Error verifying connection: {ex.Message}");
        }
    }
    public override async Task TriggerDeviceDiscovery()
    {
        RestartDeviceWatcher();
    }
    protected override async Task ChildDisconnectAndCleanupFirst()
    {
        if (_deviceWatcher != null && (_deviceWatcher.Status == DeviceWatcherStatus.Started || _deviceWatcher.Status == DeviceWatcherStatus.EnumerationCompleted))
        _deviceWatcher?.Stop();
        _foundDevices.Clear();
    }
    protected override async Task ChildDisconnectAndCleanupSecond()
    {
        _bluetoothDevice?.Dispose();
        _bluetoothDevice = null;
    }
    protected override async Task UnsubscribeFromAllNotifications()
    {
        if (_bluetoothDevice != null)
        {
            try
            {
                var services = await _bluetoothDevice.GetGattServicesForUuidAsync(_serviceUuid);
                foreach (var service in services.Services)
                {
                    var characteristics = await service.GetCharacteristicsAsync();
                    foreach (var characteristic in characteristics.Characteristics)
                    {
                        if (characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))
                        {
                            await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                                GattClientCharacteristicConfigurationDescriptorValue.None);
                            characteristic.ValueChanged -= Characteristic_ValueChanged;
                        }
                    }
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
        var servicesResult = await ((BluetoothLEDevice)device).GetGattServicesForUuidAsync(_serviceUuid, BluetoothCacheMode.Uncached);

        if (servicesResult.Status == GattCommunicationStatus.Success && servicesResult.Services.Count > 0)
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
    private async Task<IBuffer?> ReadCharacteristicValueAsync(Guid serviceUuid, Guid characteristicUuid)
    {
        try
        {
            // Ensure the device is connected
            if (_bluetoothDevice == null)
            {
                Logger.Log("Not connected to a device.");
            }

            // Get the GATT service
            var serviceResult = await _bluetoothDevice?.GetGattServicesForUuidAsync(serviceUuid);
            if (serviceResult.Status != GattCommunicationStatus.Success)
            {
                Logger.Log("Service not found." + serviceUuid.ToString());
            }

            var service = serviceResult.Services[0]; // Assuming the service exists and is the first one found

            // Get the characteristic
            var characteristicResult = await service.GetCharacteristicsForUuidAsync(characteristicUuid);
            if (characteristicResult.Status != GattCommunicationStatus.Success)
            {
                Logger.Log("Characteristic not found." + characteristicUuid.ToString());
            }

            var characteristic = characteristicResult.Characteristics[0]; // Assuming the characteristic exists and is the first one found

            // Read the characteristic value
            var readResult = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
            if (readResult.Status == GattCommunicationStatus.Success)
            {
                Logger.Log("Data Stream: from UUID:" + characteristic.Uuid);
                Logger.Log(_byteConversionUtils.ByteArrayToHexString(_byteConversionUtils.ConvertIBufferToBytes(readResult.Value)));
                Logger.Log("Decrypted Stream: from UUID:" + characteristic.Uuid);
                byte[]? decrypted = _btEncryption.Decrypt(_byteConversionUtils.ConvertIBufferToBytes(readResult.Value));
                Logger.Log(_byteConversionUtils.ByteArrayToHexString(decrypted));
                Logger.Log("");
                return readResult.Value; // This is the raw data buffer
            }
            else
            {
                Logger.Log("Failed to read characteristic value: " + readResult.Status + characteristic.Uuid);
                return null;
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Error reading characteristic value: {ex.Message}");
            return null;
        }
    }
}
