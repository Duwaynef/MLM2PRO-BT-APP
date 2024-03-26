using System.Globalization;
using MLM2PRO_BT_APP.devices;
using MLM2PRO_BT_APP.util;
using Newtonsoft.Json;
using System.Windows;

namespace MLM2PRO_BT_APP.connections
{
    public abstract class BluetoothBase<TBluetoothDeviceType> : IBluetoothBaseInterface where TBluetoothDeviceType : class
    {
        protected TBluetoothDeviceType? BluetoothDevice;
        private Timer? _heartbeatTimer;
        protected readonly ByteConversionUtils ByteConversionUtils = new();
        protected readonly Encryption BtEncryption = new();
        private bool _settingUpConnection;
        private long _lastHeartbeatReceived = DateTimeOffset.Now.ToUnixTimeSeconds() + 20;
        private int _connectionAttempts;
        private bool _isDeviceArmed;

        protected readonly Guid ServiceUuid = new("DAF9B2A4-E4DB-4BE4-816D-298A050F25CD");
        private readonly Guid _authRequestCharacteristicUuid = new("B1E9CE5B-48C8-4A28-89DD-12FFD779F5E1"); // Write Only
        private readonly Guid _commandCharacteristicUuid = new("1EA0FA51-1649-4603-9C5F-59C940323471"); // Write Only
        private readonly Guid _configureCharacteristicUuid = new("DF5990CF-47FB-4115-8FDD-40061D40AF84"); // Write Only
        protected readonly Guid EventsCharacteristicUuid = new("02E525FD-7960-4EF0-BFB7-DE0F514518FF");
        protected readonly Guid HeartbeatCharacteristicUuid = new("EF6A028E-F78B-47A4-B56C-DDA6DAE85CBF");
        protected readonly Guid MeasurementCharacteristicUuid = new("76830BCE-B9A7-4F69-AEAA-FD5B9F6B0965");
        protected readonly Guid WriteResponseCharacteristicUuid = new("CFBBCB0D-7121-4BC2-BF54-8284166D61F0");
        protected readonly List<Guid> NotifyUuiDs = [];

        protected BluetoothBase()
        {
            NotifyUuiDs.Add(EventsCharacteristicUuid);
            NotifyUuiDs.Add(HeartbeatCharacteristicUuid);
            NotifyUuiDs.Add(MeasurementCharacteristicUuid);
            NotifyUuiDs.Add(WriteResponseCharacteristicUuid);
            if (App.SharedVm != null)
            {
                if (SettingsManager.Instance.Settings?.LaunchMonitor?.AutoStartLaunchMonitor ?? true) App.SharedVm.LmStatus = "Watching for bluetooth devices...";
            }
        }

        protected abstract Task<bool> SubscribeToCharacteristicsAsync();
        protected abstract byte[] GetCharacteristicValueAsync(object args);
        protected abstract Guid GetSenderUuidAsync(object sender);
        protected abstract Task<bool> WriteCharacteristic(Guid serviceUuid, Guid characteristicUuid, byte[]? data);
        protected abstract void VerifyConnection(object? state);
        protected abstract void ChildDisconnectAndCleanupFirst();
        protected abstract void ChildDisconnectAndCleanupSecond();
        protected abstract Task UnsubscribeFromAllNotifications();
        protected abstract Task<bool> VerifyDeviceConnection(object input);
        public abstract Task RestartDeviceWatcher();
        protected abstract Task TriggerDeviceDiscovery();

        protected async Task SetupBluetoothDevice()
        {
            _connectionAttempts++;

            if (_connectionAttempts > 5)
            {
                Logger.Log("Bluetooth Manager: Failed to connect after 5 attempts.");
                if (App.SharedVm != null) App.SharedVm.LmStatus = "NOT CONNECTED";
                await DisconnectAndCleanup();
                _connectionAttempts = 0;
                return;
            }
            _settingUpConnection = true;
            Logger.Log("Bluetooth Manager: Starting");

            //Check last used token?
            var tokenExpiryDate = SettingsManager.Instance.Settings?.WebApiSettings?.WebApiExpireDate;
            if (tokenExpiryDate > 0 && tokenExpiryDate < DateTimeOffset.Now.ToUnixTimeSeconds())
            {
                if (App.SharedVm != null) App.SharedVm.LmStatus = "SEARCHING, 3RD PARTY TOKEN EXPIRED?";
                Logger.Log("Bluetooth token expired, refresh 3rd party app token, or saved token might be old");
            }
            else
            {
                if (App.SharedVm != null) App.SharedVm.LmStatus = "SEARCHING";
            }

            var isConnected = false;
            //if (BluetoothDevice != null)
                //isConnected = await VerifyDeviceConnection(BluetoothDevice);

            isConnected = true;

            // Only proceed with setup if the connection is successful
            if (App.SharedVm != null)
            {
                App.SharedVm.LmStatus = "CONNECTING";
                if (isConnected)
                {
                    App.SharedVm.LmStatus = "SETTING UP CONNECTION";

                    {
                        var isDeviceSetup = await SubscribeToCharacteristicsAsync();
                        if (!isDeviceSetup)
                        {
                            if (DeviceManager.Instance != null) DeviceManager.Instance.DeviceStatus = "NOT CONNECTED";
                            App.SharedVm.LmStatus = "NOT CONNECTED";
                            Logger.Log("Failed to setup device.");
                            await RetryBtConnection();
                        }
                        else
                        {
                            // After successful setup, start the heartbeat
                            App.SharedVm.LmStatus = "CONNECTED";
                            if (DeviceManager.Instance != null) DeviceManager.Instance.DeviceStatus = "CONNECTED";
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
                    if (DeviceManager.Instance != null) DeviceManager.Instance.DeviceStatus = "NOT CONNECTED";
                    App.SharedVm.LmStatus = "NOT CONNECTED";
                    Logger.Log("Failed to connect to device or retrieve services.");
                    await RetryBtConnection();
                }
            }

            _settingUpConnection = false;
        }
        public async Task ArmDevice()
        {
            var data = ByteConversionUtils.HexStringToByteArray("010D0001000000"); //01180001000000 also found 010D0001000000 == arm device???
            await WriteCommand(data);
            _isDeviceArmed = true;
        }
        public bool IsBluetoothDeviceValid()
        {
            if (BluetoothDevice != null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public async Task DisarmDevice()
        {
            var data = ByteConversionUtils.HexStringToByteArray("010D0000000000"); //01180000000000 also found 010D0000000000 == disarm device???
            await WriteCommand(data);
            _isDeviceArmed = false;
        }
        protected async void Characteristic_ValueChanged(object? sender, object? args)
        {
            if (args == null || sender == null) return;
            byte[] value = GetCharacteristicValueAsync(args);
            Guid senderUuid = GetSenderUuidAsync(sender);
            if (HeartbeatCharacteristicUuid == senderUuid)
            {
                _lastHeartbeatReceived = DateTimeOffset.Now.ToUnixTimeSeconds();
                return;
            }
            else
            {
                Logger.Log($"Notification received for {senderUuid}: {ByteConversionUtils.ByteArrayToHexString(value)}");
            }

            if (senderUuid == WriteResponseCharacteristicUuid)
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
                                    if (App.SharedVm != null) App.SharedVm.LmStatus = "APP TOKEN EXPIRED, REFRESH IN MLM SOFTWARE";
                                    Logger.Log("Bluetooth token expired, refresh 3rd party app token");
                                }
                                return;
                            }

                            byte[] byteArr3 = new byte[4];
                            Array.Copy(byteArray, 0, byteArr3, 0, 4);
                            int byteArrayToInt = ByteConversionUtils.ByteArrayToInt(byteArr3, true);
                            if (SettingsManager.Instance.Settings?.WebApiSettings?.WebApiUserId != null)
                            {
                                SettingsManager.Instance.Settings.WebApiSettings.WebApiUserId = byteArrayToInt;
                            }
                            Logger.Log("UserId Generated from device: " + byteArrayToInt.ToString());
                            SettingsManager.Instance.SaveSettings();

                            WebApiClient client = new();
                            WebApiClient.ApiResponse? response = await client.SendRequestAsync(byteArrayToInt);

                            if (response is { Success: true, User.Token: not null })
                            {
                                if (SettingsManager.Instance.Settings?.WebApiSettings != null)
                                {
                                    SettingsManager.Instance.Settings.WebApiSettings.WebApiDeviceId = response.User.Id;
                                    SettingsManager.Instance.Settings.WebApiSettings.WebApiToken = response.User.Token;
                                    SettingsManager.Instance.Settings.WebApiSettings.WebApiExpireDate = response.User.ExpireDate;
                                    SettingsManager.Instance.SaveSettings();
                                    var dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(response.User.ExpireDate);
                                    var localDateTime = dateTimeOffset.ToLocalTime().DateTime;
                                    if (response.User.ExpireDate > DateTimeOffset.Now.ToUnixTimeSeconds() &&
                                        response.User.ExpireDate < DateTimeOffset.Now.ToUnixTimeSeconds() + 10800)
                                    {
                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            EventAggregator.Instance.PublishSnackBarMessage($"Your 3rd party token is close to expiry: {localDateTime:yyyy-MM-dd hh:mm:ss tt}", 10);
                                        });
                                    }
                                    else if (response.User.ExpireDate < DateTimeOffset.Now.ToUnixTimeSeconds())
                                    {
                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            EventAggregator.Instance.PublishSnackBarMessage($"Your 3rd party token is expired, Expiry: {localDateTime:yyyy-MM-dd hh:mm:ss tt}", 10);
                                        });
                                    }
                                }
                                Logger.Log($"User ID: {response.User.Id}, Token: {response.User.Token}, Expire Date: {response.User.ExpireDate}");
                                byte[]? bytes = DeviceManager.Instance?.GetInitialParameters(response.User.Token);
                                Logger.Log("InitialParameters returned ByteArray: " + ByteConversionUtils.ByteArrayToHexString(bytes));
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

                    if (App.SharedVm != null) App.SharedVm.LmStatus = "CONNECTED";

                    Logger.Log("Connected, did not run InitialParameters");
                }

                //mRapsodoDeviceInfo.updateResponseMessage(value);
                //mLaunchMonitorInfo.setFromRapsodoDeviceInfo(mRapsodoDeviceInfo);
                //sendMessageResponse();
            }
            else if (senderUuid == EventsCharacteristicUuid && !_settingUpConnection)
            {
                try
                {
                    byte[]? decrypted = BtEncryption.Decrypt(value);
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
                                    if (App.SharedVm != null) App.SharedVm.LmStatus = "CONNECTED, SHOT HAPPENED";
                                    Logger.Log("BluetoothManager: Shot happened!");
                                    break;
                                }
                            case 1:
                                {
                                    if (App.SharedVm != null) App.SharedVm.LmStatus = "CONNECTED, PROCESSING SHOT";
                                    Logger.Log("BluetoothManager: Device is processing shot!");
                                    break;
                                }
                            case 2:
                                {
                                    if (App.SharedVm != null) App.SharedVm.LmStatus = "CONNECTED, READY";
                                    Logger.Log("BluetoothManager: Device is ready for next shot!");
                                    break;
                                }
                            case 3:
                                {
                                    int batteryLife = decrypted[1];
                                    if (App.SharedVm != null) App.SharedVm.LmBatteryLife = batteryLife.ToString();

                                    DeviceManager.Instance?.UpdateBatteryLevel(batteryLife);
                                    Logger.Log("Battery Level: " + batteryLife);
                                    break;
                                }
                            case 5 when decrypted[1] == 0:
                                // App.SharedVM.LMStatus = "CONNECTED, SHOT WAS A MISREAD ALL ZEROS";
                                // Logger.Log("BluetoothManager: last shot was misread, all zeros...");
                                break;
                            case 5 when decrypted[1] == 1:
                                {
                                    if (App.SharedVm != null) App.SharedVm.LmStatus = "CONNECTED, DISARMED";
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
            else if (senderUuid == MeasurementCharacteristicUuid && !_settingUpConnection)
            {
                try
                {
                    byte[]? decrypted = BtEncryption.Decrypt(value);
                    if (decrypted == null)
                    {
                        Logger.Log("Decryption failed for EVENT");
                        await RetryBtConnection();
                    }
                    else
                    {
                        Logger.Log("");
                        Logger.Log($"### MEASUREMENT = {string.Join(", ", ByteConversionUtils.ByteArrayToHexString(decrypted))}");
                        Logger.Log($"### MEASUREMENT = {string.Join(", ", ByteConversionUtils.ArrayByteToInt(decrypted))}");
                        Logger.Log("");

                        if (MeasurementData.Instance != null)
                        {
                            var messageToSend = MeasurementData.Instance.ConvertHexToMeasurementData(ByteConversionUtils.ByteArrayToHexString(decrypted));
                            Logger.Log("Measurement: " + JsonConvert.SerializeObject(messageToSend));
                            await Task.Run(() =>
                            {
                                (Application.Current as App)?.SendShotData(messageToSend);
                            });
                        }
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
        private async Task<bool> WriteValue(Guid uuid, Guid uuid2, byte[]? byteArray)
        {
            if (BluetoothDevice != null)
            {
                bool status = await WriteCharacteristic(uuid, uuid2, byteArray);
                Logger.Log("WriteValue: " + ByteConversionUtils.ByteArrayToHexString(byteArray));
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
                await WriteValue(ServiceUuid, _commandCharacteristicUuid, BtEncryption.Encrypt(data));
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
                Logger.Log($"### BluetoothManager: Writing CONFIG = " + ByteConversionUtils.ByteArrayToHexString(data));
                await WriteValue(ServiceUuid, _configureCharacteristicUuid, BtEncryption.Encrypt(data));
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
            if (BluetoothDevice == null) return;
            if (_lastHeartbeatReceived < DateTimeOffset.Now.ToUnixTimeSeconds() - 20)
            {
                Logger.Log("Heartbeat not received for 20 seconds, resubscribing...");
                _lastHeartbeatReceived = DateTimeOffset.Now.ToUnixTimeSeconds() + 20;
                // await UnSubAndReSub();
                await SubscribeToCharacteristicsAsync();
            }
            byte[] heartbeatData = [0x01];
            // Send the heartbeat signal to the HEARTBEAT_CHARACTERISTIC_UUID
            await WriteCharacteristic(ServiceUuid, HeartbeatCharacteristicUuid, heartbeatData);

            // Logger.Log("Heartbeat signal sent.");
        }
        private async Task<bool?> SendDeviceAuthRequest()
        {
            var intToByteArray = ByteConversionUtils.IntToByteArray(1, true);
            var encryptionTypeBytes = Encryption.GetEncryptionTypeBytes();
            var keyBytes = BtEncryption.GetKeyBytes();
            if (keyBytes == null) return null;
            if (intToByteArray != null)
            {
                byte[] bArr = new byte[intToByteArray.Length + encryptionTypeBytes.Length + keyBytes.Length];
                Array.Copy(intToByteArray, 0, bArr, 0, intToByteArray.Length);
                Array.Copy(encryptionTypeBytes, 0, bArr, intToByteArray.Length, encryptionTypeBytes.Length);
                Array.Copy(keyBytes, 0, bArr, intToByteArray.Length + encryptionTypeBytes.Length, keyBytes.Length);
                Logger.Log(string.Format("### DEVICE: AUTH Request = " + ByteConversionUtils.ByteArrayToHexString(bArr)));
                if (App.SharedVm != null) App.SharedVm.LmStatus = "SENDING AUTH REQUEST";
                bool status = await WriteValue(ServiceUuid, _authRequestCharacteristicUuid, bArr);
                return status;
            }
            return null;
        }
        public byte[]? ConvertAuthRequest(byte[]? input)
        {
            // Extracting keyBytes from input
            const int intToByteArrayLength = sizeof(int);
            const int encryptionTypeBytesLength = 2; // Assuming the encryption type bytes length is fixed to 2
            if (input != null)
            {
                int keyBytesLength = input.Length - intToByteArrayLength - encryptionTypeBytesLength;
                byte[] keyBytes = new byte[keyBytesLength];
                Buffer.BlockCopy(input, intToByteArrayLength + encryptionTypeBytesLength, keyBytes, 0, keyBytesLength);

                // Outputting keyBytes to console
                Logger.Log("KeyBytes: " + ByteConversionUtils.ByteArrayToHexString(keyBytes));
                return keyBytes;
            }
            return null;
        }
        public async Task DisconnectAndCleanup()
        {
            if (App.SharedVm != null) App.SharedVm.LmStatus = "DISCONNECTING...";
            await Task.Delay(TimeSpan.FromSeconds(1));
            _heartbeatTimer?.Dispose();
            ChildDisconnectAndCleanupFirst();

            await Task.Delay(250);
            if (App.SharedVm != null) App.SharedVm.LmStatus = "DISCONNECTING...";

            if (_isDeviceArmed)
            {
                await DisarmDevice();
            }

            if (BluetoothDevice != null)
            {
                byte[] data = [0, 0, 0, 0, 0, 0, 0]; // Tell the Launch Monitor to disconnect
                await WriteCommand(data);
                await UnsubscribeFromAllNotifications();
            }

            ChildDisconnectAndCleanupSecond();

            if (App.SharedVm != null) App.SharedVm.LmStatus = "DISCONNECTED";
            Logger.Log("Disconnected and cleaned up resources.");
        }
        protected async Task RetryBtConnection()
        {
            // await DisconnectAndCleanup();
            await TriggerDeviceDiscovery();
        }
        public async Task UnSubAndReSub()
        {
            if (App.SharedVm != null) App.SharedVm.LmStatus = "CONNECTED, NOT READY";
            await UnsubscribeFromAllNotifications();
            await SubscribeToCharacteristicsAsync();
        }   
        public byte[]? GetEncryptionKey()
        {
            return BtEncryption.GetKeyBytes();
        }

    }
}
