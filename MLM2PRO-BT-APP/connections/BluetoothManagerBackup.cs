/*
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using MLM2PRO_BT_APP.devices;
using MLM2PRO_BT_APP.util;
using Newtonsoft.Json;
using InTheHand.Bluetooth;

namespace MLM2PRO_BT_APP.connections;

public class BluetoothManagerBackup : BluetoothBase<InTheHand.Bluetooth.BluetoothDevice>
{
    public BluetoothManagerBackup() : base()
    {
        DiscoverDevicesAsync();
    }

    public async Task DiscoverDevicesAsync()
    {
        var requestOptions = new BluetoothDeviceRequestOptions
        {
            AcceptAllDevices = true, // Or use filters based on services
            OptionalServices = new List<Guid> { _serviceUuid } // Specify services you're interested in
        };

        try
        {
            var device = await Bluetooth.RequestDeviceAsync(requestOptions);
            if (device != null)
            {
                // Device found, you can save this device or connect immediately
                await ConnectToDeviceAsync(device);
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Error during device discovery: {ex.Message}");
        }
    }
    public async Task ConnectToDeviceAsync(BluetoothDevice device)
    {
        try
        {
            _bluetoothDevice = device;
            await SetupBluetoothDevice();
        }
        catch (Exception ex)
        {
            Logger.Log($"Error connecting to device: {ex.Message}");
        }
    }

    /* old connect and find method
    private void InitializeDeviceWatcher()
    {
        var serviceSelector = GattDeviceService.GetDeviceSelectorFromUuid(_serviceUuid);
        _deviceWatcher = DeviceInformation.CreateWatcher(serviceSelector);

        _deviceWatcher.Added += DeviceWatcher_Added;
        _deviceWatcher.Updated += DeviceWatcher_Updated;
        _deviceWatcher.Removed += DeviceWatcher_Removed;
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
        deviceInfo?.Update(deviceInfoUpdate);
        _foundDevices.Remove(deviceInfoUpdate.Id);
        Logger.Log("Device Watcher removed " + deviceInfo?.Name);
        await DisconnectAndCleanup();
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
            Logger.Log("Device Watcher stopped device connection " + deviceInfo.Name + " web api token is blank");
            if (App.SharedVm != null) App.SharedVm.LMStatus = "WEB API TOKEN MISSING";
        }
    }
    public async void RestartDeviceWatcher()
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
    //

    private async Task SetupBluetoothDevice()
    {
        _connectionAttempts++;

        if (_connectionAttempts > 5)
        {
            Logger.Log("Bluetooth Manager: Failed to connect after 5 attempts.");
            if (App.SharedVm != null) App.SharedVm.LMStatus = "NOT CONNECTED";
            await DisconnectAndCleanup();
            _connectionAttempts = 0;
            return;
        }
        _settingUpConnection = true;
        Logger.Log("Bluetooth Manager: Starting");

        //Check last used token?
        var tokenExpiryDate = SettingsManager.Instance.Settings.WebApiSettings.WebApiExpireDate;
        if (tokenExpiryDate > 0 && tokenExpiryDate < DateTimeOffset.Now.ToUnixTimeSeconds())
        {
            if (App.SharedVm != null) App.SharedVm.LMStatus = "SEARCHING, 3RD PARTY TOKEN EXPIRED?";
            Logger.Log("Bluetooth token expired, refresh 3rd party app token, or saved token might be old");
        }
        else
        {
            if (App.SharedVm != null) App.SharedVm.LMStatus = "SEARCHING";
        }

        var isConnected = false;
        if (_bluetoothDevice != null) isConnected = await VerifyDeviceConnection(_bluetoothDevice);

        // Only proceed with setup if the connection is successful
        if (App.SharedVm != null)
        {
            App.SharedVm.LMStatus = "CONNECTING";
            if (isConnected)
            {
                App.SharedVm.LMStatus = "SETTING UP CONNECTION";

                {
                    var isDeviceSetup = await SubscribeToCharacteristicsAsync();
                    if (!isDeviceSetup)
                    {
                        DeviceManager.Instance.DeviceStatus = "NOT CONNECTED";
                        App.SharedVm.LMStatus = "NOT CONNECTED";
                        Logger.Log("Failed to setup device.");
                        await RetryBtConnection();
                    }
                    else
                    {
                        // After successful setup, start the heartbeat
                        App.SharedVm.LMStatus = "CONNECTED";
                        DeviceManager.Instance.DeviceStatus = "CONNECTED";
                        Logger.Log("CONNECTED to device. sending auth");
                        var authStatus = await SendDeviceAuthRequest();

                        if ((bool)(!authStatus)!)
                        {
                            Logger.Log("Failed to send auth request.");
                            await RetryBtConnection();
                        }
                        else
                        {
                            Logger.Log("Auth request sent.");
                            // After successful subscriptions
                            // StartSubscriptionVerificationTimer(); // this might be doing more harm than good...
                            StartHeartbeat();
                        }
                    }
                }
            }
            else
            {
                DeviceManager.Instance.DeviceStatus = "NOT CONNECTED";
                App.SharedVm.LMStatus = "NOT CONNECTED";
                Logger.Log("Failed to connect to device or retrieve services.");
                await RetryBtConnection();
            }
        }

        _settingUpConnection = false;
    }
    
    private async Task<bool> VerifyDeviceConnection(BluetoothLEDevice device)
    {
        var servicesResult = await device.GetGattServicesForUuidAsync(_serviceUuid, BluetoothCacheMode.Uncached);

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
    public Task ArmDevice()
    {
        var data = _byteConversionUtils.HexStringToByteArray("010D0001000000"); //01180001000000 also found 010D0001000000 == arm device???
        _ = WriteCommand(data);
        _isDeviceArmed = true;
        return Task.CompletedTask;
    }
    public Task DisarmDevice()
    {
        var data = _byteConversionUtils.HexStringToByteArray("010D0000000000"); //01180000000000 also found 010D0000000000 == disarm device???
        _ = WriteCommand(data);
        _isDeviceArmed = false;
        return Task.CompletedTask;
    }
    private async Task<bool> SubscribeToCharacteristicsAsync()
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
    private async void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        var value = args.CharacteristicValue.ToArray();
        var uuid = sender.Uuid;
        if (_heartbeatCharacteristicUuid == uuid)
        {
            _lastHeartbeatReceived = DateTimeOffset.Now.ToUnixTimeSeconds();
            return;
        }
        else
        {
            Logger.Log($"Notification received for {sender.Uuid}: {_byteConversionUtils.ByteArrayToHexString(value)}");
        }

        if (uuid == _writeResponseCharacteristicUuid)
        {
            Logger.Log($"### WRITE RESPONSE = {string.Join(", ", ByteConversionUtils.ArrayByteToInt(value))}");

            if (value.Length >= 2)
            {
                byte byte2 = value[0]; // 02 Means Send initial parameters
                byte byte3 = value[1]; // 00

                if (value.Length > 2)
                {
                    byte[] byteArray = new byte[value.Length - 2];
                    Array.Copy(value, 2, byteArray, 0, value.Length - 2);

                    if (byte2 == 2)
                    {
                        Logger.Log("Auth requested: Running InitialParameters");

                        if (byte3 != 0 || value.Length < 4)
                        {
                            Logger.Log("Auth failed, returning");
                            if (byte3 == 1)
                            {
                                if (App.SharedVm != null) App.SharedVm.LMStatus = "APP TOKEN EXPIRED, REFRESH IN MLM SOFTWARE";
                                Logger.Log("Bluetooth token expired, refresh 3rd party app token");
                            }
                            return;
                        }

                        byte[] byteArr3 = new byte[4];
                        Array.Copy(byteArray, 0, byteArr3, 0, 4);
                        int byteArrayToInt = _byteConversionUtils.ByteArrayToInt(byteArr3, true);
                        SettingsManager.Instance.Settings.WebApiSettings.WebApiUserId = byteArrayToInt;
                        Logger.Log("UserId Generated from device: " + byteArrayToInt.ToString());
                        SettingsManager.Instance.SaveSettings();

                        WebApiClient client = new WebApiClient();
                        WebApiClient.ApiResponse response = await client.SendRequestAsync(byteArrayToInt);

                        if (response is { Success: true, User.Token: not null })
                        {
                            SettingsManager.Instance.Settings.WebApiSettings.WebApiDeviceId = response.User.Id;
                            SettingsManager.Instance.Settings.WebApiSettings.WebApiToken = response.User.Token;
                            SettingsManager.Instance.Settings.WebApiSettings.WebApiExpireDate = response.User.ExpireDate;
                            SettingsManager.Instance.SaveSettings();
                            Logger.Log($"User ID: {response.User.Id}, Token: {response.User.Token}, Expire Date: {response.User.ExpireDate}");
                            byte[]? bytes = DeviceManager.Instance.GetInitialParameters(response.User.Token);
                            _storeConfigureBytes = bytes;
                            Logger.Log("InitialParameters returned ByteArray: " + _byteConversionUtils.ByteArrayToHexString(bytes));
                            await WriteConfig(bytes);
                            // I'm really not sure why sending this TWICE makes any difference.
                            await Task.Delay(200);
                            await WriteConfig(bytes);
                            await Task.Delay(500);
                        }
                        else
                        {
                            Logger.Log("Failed to get a valid response.");
                        }
                        return;
                    }
                    else if (byte2 != 1)
                    {
                        if (byte2 != 0)
                        {
                            Logger.Log("### Unknown WRITE RESPONSE type");
                        }

                        Logger.Log("byte2 not equal to 1 : " + byte2.ToString());
                        /*
                        else if (mAwaitingInitialConfigureResponse)
                        {
                            AwaitingInitialConfigureResponse = false;
                            onInitialConfigurationResponse(byte3, byteArray);
                            return;
                        }
                        
                    }
                }

                if (App.SharedVm != null) App.SharedVm.LMStatus = "CONNECTED";

                Logger.Log("Connected, did not run InitialParameters");
            }

            //mRapsodoDeviceInfo.updateResponseMessage(value);
            //mLaunchMonitorInfo.setFromRapsodoDeviceInfo(mRapsodoDeviceInfo);
            //sendMessageResponse();
        }
        else if (uuid == _eventsCharacteristicUuid && !_settingUpConnection)
        {
            try
            {
                byte[]? decrypted = _btEncryption.Decrypt(value);
                if (decrypted == null)
                {
                    Logger.Log("Decryption failed for EVENT");
                    await RetryBtConnection();
                }
                else
                {
                    Logger.Log($"### EVENT = {string.Join(", ", ByteConversionUtils.ArrayByteToInt(decrypted))}");

                    switch (decrypted[0])
                    {
                        case 0:
                        {
                            if (App.SharedVm != null) App.SharedVm.LMStatus = "CONNECTED, SHOT HAPPENED";
                            Logger.Log("BluetoothManager: Shot happened!");
                            break;
                        }
                        case 1:
                        {
                            if (App.SharedVm != null) App.SharedVm.LMStatus = "CONNECTED, PROCESSING SHOT";
                            Logger.Log("BluetoothManager: Device is processing shot!");
                            break;
                        }
                        case 2:
                        {
                            if (App.SharedVm != null) App.SharedVm.LMStatus = "CONNECTED, READY";
                            Logger.Log("BluetoothManager: Device is ready for next shot!");
                            break;
                        }
                        case 3:
                        {
                            int batteryLife = decrypted[1];
                            if (App.SharedVm != null) App.SharedVm.LmBatteryLife = batteryLife.ToString();

                            DeviceManager.Instance.UpdateBatteryLevel(batteryLife);
                            Logger.Log("Battery Level: " + batteryLife);
                            break;
                        }
                        case 5 when decrypted[1] == 0:
                            // App.SharedVM.LMStatus = "CONNECTED, SHOT WAS A MISREAD ALL ZEROS";
                            // Logger.Log("BluetoothManager: last shot was misread, all zeros...");
                            break;
                        case 5 when decrypted[1] == 1:
                        {
                            if (App.SharedVm != null) App.SharedVm.LMStatus = "CONNECTED, DISARMED";
                            Logger.Log("BluetoothManager: device disarmed");
                            break;
                        }
                    }
                }
            }
            catch (Exception)
            {
                Logger.Log("Decryption exception for EVENT");
            }
        }
        else if (uuid == _measurementCharacteristicUuid && !_settingUpConnection)
        {
            try
            {
                byte[]? decrypted = _btEncryption.Decrypt(value);
                if (decrypted == null)
                {
                    Logger.Log("Decryption failed for EVENT");
                    await RetryBtConnection();
                }
                else
                {
                    Logger.Log("");
                    Logger.Log($"### MEASUREMENT = {string.Join(", ", _byteConversionUtils.ByteArrayToHexString(decrypted))}");
                    Logger.Log($"### MEASUREMENT = {string.Join(", ", ByteConversionUtils.ArrayByteToInt(decrypted))}");
                    Logger.Log("");

                    OpenConnectApiMessage messageToSend = MeasurementData.Instance.ConvertHexToMeasurementData(_byteConversionUtils.ByteArrayToHexString(decrypted));
                    Logger.Log("Measurement: " + JsonConvert.SerializeObject(messageToSend));
                    (Application.Current as App)?.SendShotData(messageToSend);
                }
            }
            catch (Exception)
            {
                Logger.Log("Decryption exception for MEASUREMENT");
            }
        }
    }
    private async Task<GattDeviceService?> GetGattServiceAsync(Guid serviceUuid)
    {
        var services = await _bluetoothDevice?.GetGattServicesAsync();
        return services.Services.FirstOrDefault(s => s.Uuid == serviceUuid);
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
    private async Task<GattCharacteristic?> GetCharacteristicAsync(GattDeviceService service, Guid characteristicUuid)
    {
        var characteristics = await service.GetCharacteristicsAsync();
        if (characteristics == null)
        {
            return null;
        }

        return characteristics.Characteristics.FirstOrDefault(c => c.Uuid == characteristicUuid);
    }

    private async Task<bool> WriteCharacteristic(Guid serviceUuid, Guid characteristicUuid, byte[]? data, WriteType writeType)
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
            var writeOption = writeType == WriteType.WITH_RESPONSE ?
                                GattWriteOption.WriteWithResponse :
                                GattWriteOption.WriteWithoutResponse;

            try
            {
                var writeResult = await characteristic.WriteValueAsync(data.AsBuffer(), writeOption);
                return writeResult == GattCommunicationStatus.Success;
            }
            catch
            {
                Logger.Log("failed to write.");
                return false;
            }
        }
    }

    private async Task<bool> WriteValue(Guid uuid, Guid uuid2, byte[]? byteArray)
    {
        if (_bluetoothDevice != null)
        {
            bool status = await WriteCharacteristic(uuid, uuid2, byteArray, WriteType.WITH_RESPONSE);
            Logger.Log("WriteValue: " + _byteConversionUtils.ByteArrayToHexString(byteArray));
            return status;
        }
        else
        {
            Logger.Log("Bluetooth device not connected.");
            return false;
        }
    }
    public async Task TriggerSimpleWriteConfig(byte[]? data)
    {
        // Send write request
        // Call the WriteConfig method to write the configuration
        var success = await WriteConfig(data);

        Logger.Log(success ? "Configuration write successful!" : "Configuration write failed.");
    }

    private async Task WriteCommand(byte[]? data)
    {
        try
        {
            Logger.Log($"### BluetoothManager: Writing COMMAND = {string.Join(", ", ByteConversionUtils.ArrayByteToInt(data))}");
            await WriteValue(_serviceUuid, _commandCharacteristicUuid, _btEncryption.Encrypt(data));
        }
        catch (Exception)
        {
            Logger.Log("### BluetoothManager: Encryption exception when writing to command");
        }
    }

    private async Task<bool> WriteConfig(byte[]? data)
    {
        try
        {
            Logger.Log($"### BluetoothManager: Writing CONFIG = " + _byteConversionUtils.ByteArrayToHexString(data));
            await WriteValue(_serviceUuid, _configureCharacteristicUuid, _btEncryption.Encrypt(data));
            return true;
        }
        catch (Exception)
        {
            Logger.Log("### BluetoothManager: Encryption exception when writing to configure");
            return false;
        }
    }

    private void StartHeartbeat()
    {
        // Stop the timer if it's already running
        _heartbeatTimer?.Dispose();

        // Start the timer to call the SendHeartbeatSignal method every 2 seconds (2000 milliseconds)
        _heartbeatTimer = new Timer(SendHeartbeatSignal, null, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(2));
        _lastHeartbeatReceived = DateTimeOffset.Now.ToUnixTimeSeconds() + 20;
    }
    private async void SendHeartbeatSignal(object? state)
    {
        if (_bluetoothDevice == null) return;
        if (_lastHeartbeatReceived < DateTimeOffset.Now.ToUnixTimeSeconds() - 20)
        {
            Logger.Log("Heartbeat not received for 20 seconds, resubscribing...");
            _lastHeartbeatReceived = DateTimeOffset.Now.ToUnixTimeSeconds() + 20;
            await UnSubAndReSub();
        }
        byte[] heartbeatData = [0x01];
        // Send the heartbeat signal to the HEARTBEAT_CHARACTERISTIC_UUID
        await WriteCharacteristic(_serviceUuid, _heartbeatCharacteristicUuid, heartbeatData, WriteType.WITH_RESPONSE);

        // Logger.Log("Heartbeat signal sent.");
    }

    private async Task<bool?> SendDeviceAuthRequest()
    {
        var intToByteArray = _byteConversionUtils.IntToByteArray(1, true);
        var encryptionTypeBytes = _btEncryption.GetEncryptionTypeBytes();
        var keyBytes = _btEncryption.GetKeyBytes();
        if (keyBytes == null) return null;
        var bArr = new byte[intToByteArray.Length + encryptionTypeBytes.Length + keyBytes.Length];
        Array.Copy(intToByteArray, 0, bArr, 0, intToByteArray.Length);
        Array.Copy(encryptionTypeBytes, 0, bArr, intToByteArray.Length, encryptionTypeBytes.Length);
        Array.Copy(keyBytes, 0, bArr, intToByteArray.Length + encryptionTypeBytes.Length, keyBytes.Length);
        Logger.Log(string.Format("### DEVICE: AUTH Request = " + _byteConversionUtils.ByteArrayToHexString(bArr)));
        if (App.SharedVm != null) App.SharedVm.LMStatus = "SENDING AUTH REQUEST";
        var status = await WriteValue(_serviceUuid, _authRequestCharacteristicUuid, bArr);
        return status;

    }
    public Task<byte[]> ConvertAuthRequest(byte[] input)
    {
        // Extracting keyBytes from input
        int intToByteArrayLength = sizeof(int);
        int encryptionTypeBytesLength = 2; // Assuming the encryption type bytes length is fixed to 2
        int keyBytesLength = input.Length - intToByteArrayLength - encryptionTypeBytesLength;
        byte[] keyBytes = new byte[keyBytesLength];
        System.Buffer.BlockCopy(input, intToByteArrayLength + encryptionTypeBytesLength, keyBytes, 0, keyBytesLength);

        // Outputting keyBytes to console
        Logger.Log("KeyBytes: " + _byteConversionUtils.ByteArrayToHexString(keyBytes));
        return Task.FromResult(keyBytes);
    }
    public void StartSubscriptionVerificationTimer()
    {
        // Stop the timer if it's already running
        _subscriptionVerificationTimer?.Dispose();

        // Set the timer to check every 60 seconds
        _subscriptionVerificationTimer = new Timer(VerifyConnection, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(30));
    }
    private async void VerifyConnection(object? state)
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
    public async Task DisconnectAndCleanup()
    {
        _deviceWatcher?.Stop();
        _foundDevices.Clear();
        if (_subscriptionVerificationTimer != null) await _subscriptionVerificationTimer.DisposeAsync();
        if (_heartbeatTimer != null) await _heartbeatTimer.DisposeAsync();

        await Task.Delay(1000);
        if (App.SharedVm != null) App.SharedVm.LMStatus = "DISCONNECTING...";

        if (_isDeviceArmed)
        {
            await DisarmDevice();
        }

        if (_bluetoothDevice != null)
        {
            byte[] data = [0, 0, 0, 0, 0, 0, 0]; // Tell the Launch Monitor to disconnect
            await WriteCommand(data);
            await UnsubscribeFromAllNotifications();
        }

        _bluetoothDevice?.Dispose();
        _bluetoothDevice = null;

        if (App.SharedVm != null) App.SharedVm.LMStatus = "DISCONNECTED";
        Logger.Log("Disconnected and cleaned up resources.");
    }

    private async Task RetryBtConnection()
    {
        // await DisconnectAndCleanup();
        await SetupBluetoothDevice();
    }
    public async Task UnSubAndReSub()
    {
        if (App.SharedVm != null) App.SharedVm.LMStatus = "CONNECTED, NOT READY";
        await UnsubscribeFromAllNotifications();
        await SubscribeToCharacteristicsAsync();
    }

    private async Task UnsubscribeFromAllNotifications()
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
    public byte[]? GetEncryptionKey()
    {
        return _btEncryption.GetKeyBytes();
    }
}

*/