using MLM2PRO_BT_APP.devices;
using MLM2PRO_BT_APP.util;
using Newtonsoft.Json;
using System.Windows;
using Windows.Devices.Enumeration;

namespace MLM2PRO_BT_APP.connections
{
    public abstract class BluetoothBase<bluetoothDeviceType> : BluetoothBaseInterface where bluetoothDeviceType : class
    {
        protected bluetoothDeviceType? _bluetoothDevice;
        protected Timer? _heartbeatTimer;
        protected Timer? _subscriptionVerificationTimer;
        protected readonly ByteConversionUtils _byteConversionUtils = new ByteConversionUtils();
        protected readonly Encryption _btEncryption = new Encryption();
        protected bool _settingUpConnection;
        protected long _lastHeartbeatReceived = DateTimeOffset.Now.ToUnixTimeSeconds() + 20;
        protected int _connectionAttempts;
        // ReSharper disable once NotAccessedField.Local
        protected byte[]? _storeConfigureBytes;
        protected bool _isDeviceArmed;

        protected readonly Guid _serviceUuid = new Guid("DAF9B2A4-E4DB-4BE4-816D-298A050F25CD");
        protected readonly Guid _authRequestCharacteristicUuid = new Guid("B1E9CE5B-48C8-4A28-89DD-12FFD779F5E1"); // Write Only
        protected readonly Guid _commandCharacteristicUuid = new Guid("1EA0FA51-1649-4603-9C5F-59C940323471"); // Write Only
        protected readonly Guid _configureCharacteristicUuid = new Guid("DF5990CF-47FB-4115-8FDD-40061D40AF84"); // Write Only
        protected readonly Guid _eventsCharacteristicUuid = new Guid("02E525FD-7960-4EF0-BFB7-DE0F514518FF");
        protected readonly Guid _heartbeatCharacteristicUuid = new Guid("EF6A028E-F78B-47A4-B56C-DDA6DAE85CBF");
        protected readonly Guid _measurementCharacteristicUuid = new Guid("76830BCE-B9A7-4F69-AEAA-FD5B9F6B0965");
        protected readonly Guid _writeResponseCharacteristicUuid = new Guid("CFBBCB0D-7121-4BC2-BF54-8284166D61F0");
        protected readonly List<Guid> _notifyUuiDs = [];

        public BluetoothBase()
        {
            _notifyUuiDs.Add(_eventsCharacteristicUuid);
            _notifyUuiDs.Add(_heartbeatCharacteristicUuid);
            _notifyUuiDs.Add(_measurementCharacteristicUuid);
            _notifyUuiDs.Add(_writeResponseCharacteristicUuid);
            if (App.SharedVm != null && SettingsManager.Instance.Settings.LaunchMonitor.AutoStartLaunchMonitor) App.SharedVm.LMStatus = "Watching for bluetooth devices...";
        }

        protected async Task SetupBluetoothDevice()
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
            isConnected = await VerifyDeviceConnection(_bluetoothDevice);

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
        public async Task ArmDevice()
        {
            var data = _byteConversionUtils.HexStringToByteArray("010D0001000000"); //01180001000000 also found 010D0001000000 == arm device???
            _ = WriteCommand(data);
            _isDeviceArmed = true;
        }
        public async Task DisarmDevice()
        {
            var data = _byteConversionUtils.HexStringToByteArray("010D0000000000"); //01180000000000 also found 010D0000000000 == disarm device???
            _ = WriteCommand(data);
            _isDeviceArmed = false;
        }
        protected abstract Task<bool> SubscribeToCharacteristicsAsync();
        protected abstract Task<byte[]> GetCharacteristicValueAsync(object args);
        protected abstract Task<Guid> GetSenderUuidAsync(object sender);
        protected async void Characteristic_ValueChanged(object? sender, object? args)
        {
            byte[] value = await GetCharacteristicValueAsync(args);
            Guid senderUuid = await GetSenderUuidAsync(sender);
            if (_heartbeatCharacteristicUuid == senderUuid)
            {
                _lastHeartbeatReceived = DateTimeOffset.Now.ToUnixTimeSeconds();
                return;
            }
            else
            {
                Logger.Log($"Notification received for {senderUuid}: {_byteConversionUtils.ByteArrayToHexString(value)}");
            }

            if (senderUuid == _writeResponseCharacteristicUuid)
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
                            */
                        }
                    }

                    if (App.SharedVm != null) App.SharedVm.LMStatus = "CONNECTED";

                    Logger.Log("Connected, did not run InitialParameters");
                }

                //mRapsodoDeviceInfo.updateResponseMessage(value);
                //mLaunchMonitorInfo.setFromRapsodoDeviceInfo(mRapsodoDeviceInfo);
                //sendMessageResponse();
            }
            else if (senderUuid == _eventsCharacteristicUuid && !_settingUpConnection)
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
            else if (senderUuid == _measurementCharacteristicUuid && !_settingUpConnection)
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
                        Task.Run(() =>
                        {
                            (Application.Current as App)?.SendShotData(messageToSend);
                        });
                        
                    }
                }
                catch (Exception)
                {
                    Logger.Log("Decryption exception for MEASUREMENT");
                }
            }
        }
        // protected abstract Task<object> GetGattServiceAsync(Guid serviceUuid);
        // protected abstract Task<object?> GetCharacteristicAsync(object service, Guid characteristicUuid);
        protected abstract Task<bool> WriteCharacteristic(Guid serviceUuid, Guid characteristicUuid, byte[]? data);
        protected async Task<bool> WriteValue(Guid uuid, Guid uuid2, byte[]? byteArray)
        {
            if (_bluetoothDevice != null)
            {
                bool status = await WriteCharacteristic(uuid, uuid2, byteArray);
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
        protected async Task WriteCommand(byte[]? data)
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
        protected async Task<bool> WriteConfig(byte[]? data)
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
        protected async Task StartHeartbeat()
        {
            // Stop the timer if it's already running
            _heartbeatTimer?.Dispose();

            // Start the timer to call the SendHeartbeatSignal method every 2 seconds (2000 milliseconds)
            _heartbeatTimer = new Timer(SendHeartbeatSignal, null, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(2));
            _lastHeartbeatReceived = DateTimeOffset.Now.ToUnixTimeSeconds() + 20;
        }
        protected async void SendHeartbeatSignal(object? state)
        {
            if (_bluetoothDevice == null) return;
            if (_lastHeartbeatReceived < DateTimeOffset.Now.ToUnixTimeSeconds() - 20)
            {
                Logger.Log("Heartbeat not received for 20 seconds, resubscribing...");
                _lastHeartbeatReceived = DateTimeOffset.Now.ToUnixTimeSeconds() + 20;
                // await UnSubAndReSub();
                await SubscribeToCharacteristicsAsync();
            }
            byte[] heartbeatData = [0x01];
            // Send the heartbeat signal to the HEARTBEAT_CHARACTERISTIC_UUID
            await WriteCharacteristic(_serviceUuid, _heartbeatCharacteristicUuid, heartbeatData);

            // Logger.Log("Heartbeat signal sent.");
        }
        protected async Task<bool?> SendDeviceAuthRequest()
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
        public async Task StartSubscriptionVerificationTimer()
        {
            // Stop the timer if it's already running
            _subscriptionVerificationTimer?.Dispose();

            // Set the timer to check every 60 seconds
            _subscriptionVerificationTimer = new Timer(VerifyConnection, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(30));
        }
        protected abstract void VerifyConnection(object? state);
        protected abstract Task ChildDisconnectAndCleanupFirst();
        protected abstract Task ChildDisconnectAndCleanupSecond();
        public async Task DisconnectAndCleanup()
        {
            _heartbeatTimer.Dispose();
            await Task.Delay(TimeSpan.FromSeconds(2));
            ChildDisconnectAndCleanupFirst();

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

            ChildDisconnectAndCleanupSecond();

            if (App.SharedVm != null) App.SharedVm.LMStatus = "DISCONNECTED";
            Logger.Log("Disconnected and cleaned up resources.");
        }
        public abstract Task TriggerDeviceDiscovery();
        protected async Task RetryBtConnection()
        {
            // await DisconnectAndCleanup();
            TriggerDeviceDiscovery();
        }
        public async Task UnSubAndReSub()
        {
            if (App.SharedVm != null) App.SharedVm.LMStatus = "CONNECTED, NOT READY";
            await UnsubscribeFromAllNotifications();
            await SubscribeToCharacteristicsAsync();
        }
        protected abstract Task UnsubscribeFromAllNotifications();        
        public byte[]? GetEncryptionKey()
        {
            return _btEncryption.GetKeyBytes();
        }
        public abstract Task<bool> VerifyDeviceConnection(object input);
        public abstract Task RestartDeviceWatcher();

    }
}
