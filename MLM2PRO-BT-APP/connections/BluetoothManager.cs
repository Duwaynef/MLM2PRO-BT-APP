using System.Windows;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using Newtonsoft.Json;

namespace MLM2PRO_BT_APP;

public class BluetoothManager
{
    private Timer heartbeatTimer;
    private Timer subscriptionVerificationTimer;
    public BluetoothLEDevice bluetoothDevice;
    private Dictionary<Guid, GattCharacteristic> characteristicMap = new Dictionary<Guid, GattCharacteristic>();
    ByteConversionUtils byteConversionUtils = new ByteConversionUtils();
    Encryption btEncryption = new Encryption();
    bool settingUpConnection = false;
    long lastHeartbeatReceived = DateTimeOffset.Now.ToUnixTimeSeconds();

    public Guid SERVICE_UUID = new Guid("DAF9B2A4-E4DB-4BE4-816D-298A050F25CD");
    public Guid AUTH_REQUEST_CHARACTERISTIC_UUID = new Guid("B1E9CE5B-48C8-4A28-89DD-12FFD779F5E1"); // Write Only
    public Guid COMMAND_CHARACTERISTIC_UUID = new Guid("1EA0FA51-1649-4603-9C5F-59C940323471"); // Write Only
    public Guid CONFIGURE_CHARACTERISTIC_UUID = new Guid("DF5990CF-47FB-4115-8FDD-40061D40AF84"); // Write Only
    public Guid EVENTS_CHARACTERISTIC_UUID = new Guid("02E525FD-7960-4EF0-BFB7-DE0F514518FF");
    public Guid HEARTBEAT_CHARACTERISTIC_UUID = new Guid("EF6A028E-F78B-47A4-B56C-DDA6DAE85CBF");
    public Guid MEASUREMENT_CHARACTERISTIC_UUID = new Guid("76830BCE-B9A7-4F69-AEAA-FD5B9F6B0965");
    public Guid WRITE_RESPONSE_CHARACTERISTIC_UUID = new Guid("CFBBCB0D-7121-4BC2-BF54-8284166D61F0");

    private bool IsNotifyCharacteristic(Guid uuid)
    {
        return uuid == EVENTS_CHARACTERISTIC_UUID ||
               uuid == HEARTBEAT_CHARACTERISTIC_UUID ||
               uuid == MEASUREMENT_CHARACTERISTIC_UUID ||
               uuid == WRITE_RESPONSE_CHARACTERISTIC_UUID;
    }
    private bool IsSetupCharacteristic(Guid uuid)
    {
        return uuid == EVENTS_CHARACTERISTIC_UUID ||
               uuid == HEARTBEAT_CHARACTERISTIC_UUID ||
               uuid == MEASUREMENT_CHARACTERISTIC_UUID ||
               uuid == WRITE_RESPONSE_CHARACTERISTIC_UUID ||
               uuid == AUTH_REQUEST_CHARACTERISTIC_UUID ||
               uuid == COMMAND_CHARACTERISTIC_UUID ||
               uuid == CONFIGURE_CHARACTERISTIC_UUID;
    }
    public async Task ConnectAndSetup(string deviceNameInput)
    {
        settingUpConnection = true;
        Logger.Log("Bluetooth Manager: Starting");
        Logger.Log("Bluetooth Manager: Looking for: " + deviceNameInput);

        //Check last used token?
        long tokenExpiryDate = SettingsManager.Instance.Settings.WebApiSettings.WebApiExpireDate;
        if (tokenExpiryDate > 0 && tokenExpiryDate < DateTimeOffset.Now.ToUnixTimeSeconds())
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                App.SharedVM.LMStatus = deviceNameInput + ", 3RD PARTY TOKEN EXPIRED?";
            });
            Logger.Log("Bluetooth token expired, refresh 3rd party app token");
        }
        else
        {
            App.SharedVM.LMStatus = "SEARCHING: " + deviceNameInput;
        }

        var bluetoothDevice = await FindBluetoothDeviceByNameAsync(deviceNameInput);
        if (bluetoothDevice == null)
        {
            DeviceManager.Instance.DeviceStatus = "f";
            App.SharedVM.LMStatus = "NOTCONNECTED";

            Logger.Log("Bluetooth device not found or failed to connect.");
            await DisconnectAndCleanupWithRetry();
            return;
        }

        // Only proceed with setup if the connection is successful
        App.SharedVM.LMStatus = "CONNECTING";
        bool isConnected = await ConnectToDeviceAsync(bluetoothDevice);
        if (isConnected)
        {
            App.SharedVM.LMStatus = "SETTING UP CONNECTION";
            // Introduce a delay to ensure the device is ready
            await Task.Delay(TimeSpan.FromSeconds(8)); // Adjust the delay time as needed

            bool isDeviceSetup = await SetupDeviceAsync(bluetoothDevice);
            if (!isDeviceSetup)
            {
                DeviceManager.Instance.DeviceStatus = "NOTCONNECTED";
                App.SharedVM.LMStatus = "NOTCONNECTED";
                Logger.Log("Failed to setup device.");
                await DisconnectAndCleanupWithRetry();
            } 
            else
            {
                // After successful setup, start the heartbeat
                App.SharedVM.LMStatus = "CONNECTED";
                DeviceManager.Instance.DeviceStatus = "CONNECTED";
                Logger.Log("CONNECTED to device. sending auth");
                await Task.Delay(TimeSpan.FromSeconds(3)); // Adjust the delay time as needed
                await SendDeviceAuthRequest();

                // After successful subscriptions
                // StartSubscriptionVerificationTimer(); // this might be doing more harm than good...
                StartHeartbeat();

                // this is a HACK i'm not sure why it's needed.... but it is... for some reason the first subscription doesn't work
                // await UnSubAndReSub(); this hack didn't work :(
            }
        }
        else
        {
            DeviceManager.Instance.DeviceStatus = "NOTCONNECTED";
            App.SharedVM.LMStatus = "NOTCONNECTED";
            Logger.Log("Failed to connect to device or retrieve services.");
            await DisconnectAndCleanupWithRetry();
        }
        settingUpConnection = false;
    }
    private async Task<BluetoothLEDevice> FindBluetoothDeviceByNameAsync(string deviceNameInput)
    {
        string deviceSelector = BluetoothLEDevice.GetDeviceSelectorFromDeviceName(deviceNameInput);
        var devices = await DeviceInformation.FindAllAsync(deviceSelector);
        Logger.Log("Bluetooth Manager: Got Device List");
        App.SharedVM.LMStatus = "GOT DEVICE LIST";

        if (devices.Count > 0)
        {
            App.SharedVM.LMStatus = "CHECKING DEVICES";
            var device = await BluetoothLEDevice.FromIdAsync(devices[0].Id);
            return device;
        }
        return null;
    }
    private async Task<bool> ConnectToDeviceAsync(BluetoothLEDevice device)
    {
        bluetoothDevice = device;
        var servicesResult = await bluetoothDevice.GetGattServicesAsync();
        if (servicesResult.Status == GattCommunicationStatus.Success)
        {
            return true;
        }
        return false;
    }
    public async Task<bool> SetupDeviceAsync(BluetoothLEDevice bluetoothDevice) // get and trigger subscribe to device characteristics
    {
        var servicesResult = await bluetoothDevice.GetGattServicesAsync();
        foreach (var service in servicesResult.Services)
        {
            // Introduce a delay to ensure the device is ready
            await Task.Delay(TimeSpan.FromSeconds(2)); // Adjust the delay time as needed

            var characteristicsResult = await service.GetCharacteristicsAsync();
            if (characteristicsResult.Status == GattCommunicationStatus.Success)
            {
                bool isSucessful = await SubscribeToCharacteristicsAsync(characteristicsResult);
                if (!isSucessful)
                {
                    Logger.Log("Failed to subscribe to characteristics.");
                    //await DisconnectAndCleanupWithRetry();
                    return false;
                }
            }
        }
        return true;
    }
    private async Task<bool> SubscribeToCharacteristicsAsync(GattCharacteristicsResult characteristicsResult)
    {
        foreach (var characteristic in characteristicsResult.Characteristics)
        {
            // Check if the characteristic is one of the interested UUIDs before proceeding
            if (IsNotifyCharacteristic(characteristic.Uuid))
            {
                /* 
                if (IsSetupCharacteristic(characteristic.Uuid))
                {
                    await ReadCharacteristicValueAsync(SERVICE_UUID, characteristic.Uuid);
                }
                */

                // Subscribe to notifications if supported
                if (characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))
                {
                    try
                    {
                        var status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                            GattClientCharacteristicConfigurationDescriptorValue.Notify);
                        if (status == GattCommunicationStatus.Success)
                        {
                            characteristic.ValueChanged += Characteristic_ValueChanged;
                            Logger.Log($"Subscribed to notifications for characteristic {characteristic.Uuid}");
                        }
                        else
                        {
                            Logger.Log($"Failed to subscribe to notifications for {characteristic.Uuid}, status: {status}");
                            await DisconnectAndCleanupWithRetry();
                            return false;
                        }
                    }
                    catch
                    {
                        DeviceManager.Instance.DeviceStatus = "NOTCONNECTED";
                        Logger.Log("Characteristic Error: in SetupDeviceAsync");
                        await DisconnectAndCleanupWithRetry();
                        return false;
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
        if (HEARTBEAT_CHARACTERISTIC_UUID == uuid)
        {
            lastHeartbeatReceived = DateTimeOffset.Now.ToUnixTimeSeconds();
            return;
        }
        else
        {
            Logger.Log($"Notification received for {sender.Uuid}: {byteConversionUtils.ByteArrayToHexString(value)}");
        }

        if (uuid == WRITE_RESPONSE_CHARACTERISTIC_UUID && !settingUpConnection)
        {
            Logger.Log($"### WRITE RESPONSE = {string.Join(", ", byteConversionUtils.ArrayByteToInt(value))}");

            if (value.Length >= 2)
            {
                byte byte2 = value[0]; // 02 Means Send initial paremeters
                byte byte3 = value[1]; // 00

                if (value.Length > 2)
                {
                    byte[] byteArray = new byte[value.Length - 2];
                    Array.Copy(value, 2, byteArray, 0, value.Length - 2);

                    if (byte2 == 2)
                    {
                        Logger.Log("Auth requested: Running InitialParameters");

                        if (byte3 != 0 || value == null || value.Length < 4)
                        {
                            Logger.Log("Auth failed, returning");
                            if (byte3 == 1)
                            {
                                App.SharedVM.LMStatus = "APP TOKEN EXPIRED";
                                Logger.Log("Bluetooth token expired, refresh 3rd party app token");
                            }
                            return;
                        }

                        byte[] byteArr3 = new byte[4];
                        Array.Copy(byteArray, 0, byteArr3, 0, 4);
                        int byteArrayToInt = byteConversionUtils.ByteArrayToInt(byteArr3, true);
                        SettingsManager.Instance.Settings.WebApiSettings.WebApiUserId = byteArrayToInt;
                        Logger.Log("UserId Generated from device: " + byteArrayToInt.ToString());
                        SettingsManager.Instance.SaveSettings();

                        WebApiClient client = new WebApiClient();
                        WebApiClient.ApiResponse response = await client.SendRequestAsync(byteArrayToInt);

                        if (response != null && response.Success && response?.User?.Token != null)
                        {
                            SettingsManager.Instance.Settings.WebApiSettings.WebApiDeviceId = response.User.Id;
                            SettingsManager.Instance.Settings.WebApiSettings.WebApiToken = response.User.Token;
                            SettingsManager.Instance.Settings.WebApiSettings.WebApiExpireDate = response.User.ExpireDate;
                            SettingsManager.Instance.SaveSettings();
                            Logger.Log($"User ID: {response?.User?.Id}, Token: {response?.User?.Token}, Expire Date: {response?.User?.ExpireDate}");
                            byte[] bytes = DeviceManager.Instance.GetInitialParameters(response.User.Token);
                            Logger.Log("InitialParameters returned ByteArray: " + byteConversionUtils.ByteArrayToHexString(bytes));
                            await WriteConfig(bytes);
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
                App.SharedVM.LMStatus = "CONNECTED";

                Logger.Log("Connected, did not run InitialParameters");
            }

            //mRapsodoDeviceInfo.updateResponseMessage(value);
            //mLaunchMonitorInfo.setFromRapsodoDeviceInfo(mRapsodoDeviceInfo);
            //sendMessageResponse();
        }
        else if (uuid == EVENTS_CHARACTERISTIC_UUID && !settingUpConnection)
        {
            try
            {
                byte[] decrypted = btEncryption.Decrypt(value);
                if (decrypted == null)
                {
                    Logger.Log("Decryption failed for EVENT");
                    await DisconnectAndCleanupWithRetry();
                } 
                else
                {
                    Logger.Log($"### EVENT = {string.Join(", ", byteConversionUtils.ArrayByteToInt(decrypted))}");
                    int isBattlife = decrypted[2];

                    if (decrypted[0] == 0)
                    {
                        App.SharedVM.LMStatus = "CONNECTED, SHOT HAPPENED";
                        Logger.Log("BluetoothManager: Shot happened!");
                    }
                    else if (decrypted[0] == 1)
                    {
                        App.SharedVM.LMStatus = "CONNECTED, PROCESSING SHOT";
                        Logger.Log("BluetoothManager: Device is processing shot!");
                    }
                    else if (decrypted[0] == 2)
                    {
                        App.SharedVM.LMStatus = "CONNECTED, READY";
                        Logger.Log("BluetoothManager: Device is ready for next shot!");
                    }
                    else if (decrypted[0] == 3 && isBattlife >= 0 && isBattlife <= 2)
                    {
                        int battLife = decrypted[1];
                        App.SharedVM.LMBattLife = battLife.ToString();
                        
                        DeviceManager.Instance.UpdateBatteryLevel(battLife);
                        Logger.Log("Battery Level: " + battLife);
                    }
                    else if (decrypted[0] == 5 && decrypted[1] == 0)
                    {
                        App.SharedVM.LMStatus = "CONNECTED, SHOT WAS A MISREAD ALL ZEROS";
                        Logger.Log("BluetoothManager: last shot was misread, all zeros...");
                    }
                    else if (decrypted[0] == 5 && decrypted[1] == 1)
                    {
                        App.SharedVM.LMStatus = "CONNECTED, DISARMED";
                        Logger.Log("BluetoothManager: device disarmed");
                    }
                }
            }
            catch (Exception)
            {
                Logger.Log("Decryption exception for EVENT");
            }
        }
        else if (uuid == MEASUREMENT_CHARACTERISTIC_UUID && !settingUpConnection)
        {
            try
            {
                byte[] decrypted = btEncryption.Decrypt(value);
                if (decrypted == null)
                {
                    Logger.Log("Decryption failed for EVENT");
                    await DisconnectAndCleanupWithRetry();
                }
                else
                {
                    Logger.Log("");
                    Logger.Log($"### MEASUREMENT = {string.Join(", ", byteConversionUtils.ByteArrayToHexString(decrypted))}");
                    Logger.Log($"### MEASUREMENT = {string.Join(", ", byteConversionUtils.ArrayByteToInt(decrypted))}");
                    Logger.Log("");

                    OpenConnectApiMessage messageToSend = MeasurementData.Instance.ConvertHexToMeasurementData(byteConversionUtils.ByteArrayToHexString(decrypted));
                    Logger.Log("Measurement: " + JsonConvert.SerializeObject(messageToSend));
                    (App.Current as App)?.SendShotData(messageToSend);
                }                
            }
            catch (Exception)
            {
                Logger.Log("Decryption exception for MEASUREMENT");
            }
        }
    }
    private async Task<GattDeviceService> GetGattServiceAsync(Guid serviceUuid)
    {
        var services = await bluetoothDevice?.GetGattServicesAsync();
        return services.Services.FirstOrDefault(s => s.Uuid == serviceUuid);
    }
    public async Task<IBuffer> ReadCharacteristicValueAsync(Guid serviceUuid, Guid characteristicUuid)
    {
        try
        {
            // Ensure the device is connected
            if (bluetoothDevice == null)
            {
                Logger.Log("Not connected to a device.");
            }

            // Get the GATT service
            var serviceResult = await bluetoothDevice?.GetGattServicesForUuidAsync(serviceUuid);
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
                Logger.Log(byteConversionUtils.ByteArrayToHexString(byteConversionUtils?.ConvertIBufferToBytes(readResult?.Value)));
                Logger.Log("Decrypted Stream: from UUID:" + characteristic.Uuid);
                byte[] decrypted = btEncryption.Decrypt(byteConversionUtils.ConvertIBufferToBytes(readResult.Value));
                Logger.Log(byteConversionUtils.ByteArrayToHexString(decrypted));
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
        if (service == null)
        {
            Logger.Log("Service is null: " + service);
        }

        var characteristics = await service?.GetCharacteristicsAsync();
        if (characteristics == null)
        {
            return null;
        }

        return characteristics.Characteristics.FirstOrDefault(c => c.Uuid == characteristicUuid);
    }
    public async Task<bool> WriteCharacteristic(Guid serviceUuid, Guid characteristicUuid, byte[] data, WriteType writeType)
    {
        if (serviceUuid == Guid.Empty || characteristicUuid == Guid.Empty || data == null)
        {
            Logger.Log("Invalid input provided.");
            return false;
        }

        if (bluetoothDevice == null)
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
        } else
        {
            var writeOption = writeType == WriteType.WITH_RESPONSE ?
                                GattWriteOption.WriteWithResponse :
                                GattWriteOption.WriteWithoutResponse;

            try
            {
                var writeResult = await characteristic.WriteValueAsync(data.AsBuffer(), writeOption);
                return writeResult == GattCommunicationStatus.Success;
            } catch
            {
                Logger.Log("failed to write.");
                return false;
            }
        }
    }
    public async Task WriteValue(Guid uuid, Guid uuid2, byte[] byteArray)
    {
        if (bluetoothDevice != null)
        {
            await WriteCharacteristic(uuid, uuid2, byteArray, WriteType.WITH_RESPONSE);
            Logger.Log("WriteValue: " + byteConversionUtils.ByteArrayToHexString(byteArray));
        }
    }
    public async Task TriggerSimpleWriteConfig(byte[] data)
    {
        // Send write request
        // Call the WriteConfig method to write the configuration
        bool success = await WriteConfig(data);

        if (success)
        {
            Logger.Log("Configuration write successful!");
        }
        else
        {
            Logger.Log("Configuration write failed.");
        }
    }
    public async Task WriteCommand(byte[] data)
    {
        try
        {
            Logger.Log($"### BluetoothManager: Writing COMMAND = {string.Join(", ", byteConversionUtils.ArrayByteToInt(data))}");
            await WriteValue(SERVICE_UUID, COMMAND_CHARACTERISTIC_UUID, btEncryption.Encrypt(data));
        }
        catch (Exception)
        {
            Logger.Log("### BluetoothManager: Encryption exception when writing to command");
        }
    }
    public async Task<bool> WriteConfig(byte[] data)
    {
        try
        {
            Logger.Log($"### BluetoothManager: Writing CONFIG = " + byteConversionUtils.ByteArrayToHexString(data));
            await WriteValue(SERVICE_UUID, CONFIGURE_CHARACTERISTIC_UUID, btEncryption.Encrypt(data));
            return true;
        }
        catch (Exception)
        {
            Logger.Log("### BluetoothManager: Encryption exception when writing to configure");
            return false;
        }
    }
    public void StartHeartbeat()
    {
        // Stop the timer if it's already running
        heartbeatTimer?.Dispose();

        // Start the timer to call the SendHeartbeatSignal method every 2 seconds (2000 milliseconds)
        heartbeatTimer = new Timer(SendHeartbeatSignal, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2));
    }
    private async void SendHeartbeatSignal(object state)
    {
        if (bluetoothDevice != null)
        {
            if (lastHeartbeatReceived < DateTimeOffset.Now.ToUnixTimeSeconds() - 20)
            {
                Logger.Log("Heartbeat not received for 20 seconds, resubing...");
                lastHeartbeatReceived = DateTimeOffset.Now.ToUnixTimeSeconds() + 20;
                await SetupDeviceAsync(bluetoothDevice);
            }
            byte[] heartbeatData = new byte[] { 0x01 };
            // Send the heartbeat signal to the HEARTBEAT_CHARACTERISTIC_UUID
            await WriteCharacteristic(SERVICE_UUID, HEARTBEAT_CHARACTERISTIC_UUID, heartbeatData, WriteType.WITH_RESPONSE);

            // Logger.Log("Heartbeat signal sent.");
        }
    }
    public async Task SendDeviceAuthRequest()
    {
        byte[] intToByteArray = byteConversionUtils.IntToByteArray(1, true);
        byte[] encryptionTypeBytes = btEncryption.GetEncryptionTypeBytes();
        byte[] keyBytes = btEncryption.GetKeyBytes();
        byte[] bArr = new byte[intToByteArray.Length + encryptionTypeBytes.Length + keyBytes.Length];
        Array.Copy(intToByteArray, 0, bArr, 0, intToByteArray.Length);
        Array.Copy(encryptionTypeBytes, 0, bArr, intToByteArray.Length, encryptionTypeBytes.Length);
        Array.Copy(keyBytes, 0, bArr, intToByteArray.Length + encryptionTypeBytes.Length, keyBytes.Length);
        Logger.Log(string.Format("### DEVICE: AUTH Request = " + byteConversionUtils.ByteArrayToHexString(bArr)));
        App.SharedVM.LMStatus = "SENDING AUTH REQUEST";

        await WriteValue(SERVICE_UUID, AUTH_REQUEST_CHARACTERISTIC_UUID, bArr);
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
        Logger.Log("KeyBytes: " + byteConversionUtils.ByteArrayToHexString(keyBytes));
        return Task.FromResult(keyBytes);
    }
    public void StartSubscriptionVerificationTimer()
    {
        // Stop the timer if it's already running
        subscriptionVerificationTimer?.Dispose();

        // Set the timer to check every 60 seconds
        subscriptionVerificationTimer = new Timer(VerifyConnection, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(30));
    }
    private async void VerifyConnection(object state)
    {
        try
        {
            var readResult = await ReadCharacteristicValueAsync(SERVICE_UUID, MEASUREMENT_CHARACTERISTIC_UUID);
            if (readResult == null || readResult.Length == 0)
            {
                App.SharedVM.LMStatus = "CONNECTION MIGHT BE LOST, RECONNECTING";
                Logger.Log("Connection might be lost. Re-subscribing...");
                await DisconnectAndCleanupWithRetry();
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
        if (bluetoothDevice == null)
        {
            Logger.Log("Not connected to a device.");
            App.SharedVM.LMStatus = "DISCONNECTED";
            subscriptionVerificationTimer?.Dispose();
            heartbeatTimer?.Dispose();
            return;
        }
        // Unsubscribe from all notifications
        App.SharedVM.LMStatus = "DISCONNECTING...";
        subscriptionVerificationTimer?.Dispose();
        heartbeatTimer?.Dispose();
        await UnsubscribeFromAllNotifications();
        App.SharedVM.LMStatus = "DISCONNECTED";
        // Close the device explicitly to release resources immediately
        bluetoothDevice.Dispose();
        bluetoothDevice = null;
        Logger.Log("Disconnected and cleaned up resources.");
    }
    public async Task DisconnectAndCleanupWithRetry()
    {
        await DisconnectAndCleanup();
        await ConnectAndSetup(SettingsManager.Instance.Settings.LaunchMonitor.BluetoothDeviceName);
    }
    public async Task UnSubAndReSub()
    {
        App.SharedVM.LMStatus = "CONNECTED, NOT READY";
        await UnsubscribeFromAllNotifications();
        await SetupDeviceAsync(bluetoothDevice);
    }
    public async Task UnsubscribeFromAllNotifications()
    {
        try
        {
            var services = await bluetoothDevice.GetGattServicesForUuidAsync(SERVICE_UUID);
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
    public byte[] getEncryptionKey()
    {
        return btEncryption.GetKeyBytes();
    }
}
