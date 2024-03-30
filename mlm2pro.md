# MLM2PRO: How the bluetooth functions

## Introduction

One thing to note for this writeup is that its going to be technical as its intended for other developers to be able to 
take this information and make their own connector if they wish. or use it as a reference point.

This was written at version 0.9.8.5 of my builds and if anything changes in the future i will try to remember to update it.

To make my life easier i will be using simplified code snippets from the project to explain how things work.

First I am going to talk about the bluetooth functions that are implemented in the project.
And how the device interactions work. From first connection to the device, to reading the measurement data.

Some of the data is transmitted encrypted and some is not, i will talk about the encryption and the code for it below.

In case this file gets seperated from the original project here is a link to the original project:

https://github.com/Duwaynef/MLM2PRO-BT-APP

## Bluetooth Characteristics

```csharp
public Guid SERVICE_UUID = new Guid("DAF9B2A4-E4DB-4BE4-816D-298A050F25CD");
public Guid AUTH_REQUEST_CHARACTERISTIC_UUID = new Guid("B1E9CE5B-48C8-4A28-89DD-12FFD779F5E1"); // Write Only
public Guid COMMAND_CHARACTERISTIC_UUID = new Guid("1EA0FA51-1649-4603-9C5F-59C940323471"); // Write Only
public Guid CONFIGURE_CHARACTERISTIC_UUID = new Guid("DF5990CF-47FB-4115-8FDD-40061D40AF84"); // Write Only
public Guid EVENTS_CHARACTERISTIC_UUID = new Guid("02E525FD-7960-4EF0-BFB7-DE0F514518FF");
public Guid HEARTBEAT_CHARACTERISTIC_UUID = new Guid("EF6A028E-F78B-47A4-B56C-DDA6DAE85CBF");
public Guid MEASUREMENT_CHARACTERISTIC_UUID = new Guid("76830BCE-B9A7-4F69-AEAA-FD5B9F6B0965");
public Guid WRITE_RESPONSE_CHARACTERISTIC_UUID = new Guid("CFBBCB0D-7121-4BC2-BF54-8284166D61F0");
```
First and most obvious is the service UUID. this is the primary service used when communicating with the device.
- The AUTH_REQUEST_CHARACTERISTIC_UUID is used to send the auth request to the device.
- The COMMAND_CHARACTERISTIC_UUID is used to send commands to the device.
- The CONFIGURE_CHARACTERISTIC_UUID is used to send configuration data to the device.
- The EVENTS_CHARACTERISTIC_UUID is used to receive events from the device.
- The HEARTBEAT_CHARACTERISTIC_UUID is used to send the heartbeat to the device.
- The MEASUREMENT_CHARACTERISTIC_UUID is used to receive measurement data from the device.
- The WRITE_RESPONSE_CHARACTERISTIC_UUID is used to receive write responses from the device.

Many of these characteristics are used together to create the first connection with the device.

## First Connection

The primary library this project is using at the time of this writing is InTheHand.Bluetooth so i will show code snippets from that.

I'm going to try to simplify the code as much as possible to make it easier to understand. removing all my logging as well as any error handling.

First we require the device to be paired. This is done by the user in the bluetooth settings of windows.

We loop though the paired devices and check if the name contains MLM2- or BlueZ
#### BluetoothManagerBackup.cs
```csharp
private async Task DiscoverDevicesAsync()
    var pairedDevices = Bluetooth.GetPairedDevicesAsync().Result;
    foreach (var pairedDevice in pairedDevices)
    {
        if (pairedDevice.Name.Contains("MLM2-") || pairedDevice.Name.Contains("BlueZ ")|| pairedDevice.Name.Contains("MLM2_BT_"))
        {
            await ConnectToDeviceAsync(pairedDevice);
            if (BluetoothDevice != null) return;
        }
    }
}
```

This makes the initial connection to the bluetooth device, and if successful runs the "SetupBluetoothDevice" function.
#### BluetoothManagerBackup.cs
```csharp
private async Task ConnectToDeviceAsync(BluetoothDevice device)
{
    await device.Gatt.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(20));            
    _primaryService = await device.Gatt.GetPrimaryServiceAsync(ServiceUuid).WaitAsync(TimeSpan.FromSeconds(20));
    BluetoothDevice = device;
    await SetupBluetoothDevice();
}
```

Here we are subscribing to the characteristics we need to receive data from the device.

Then sending the initial auth request to the device.

and finally starting a heartbeat. this heartbeat is a timer that sends a simple 0x01 byte to the 
heartbeat uuid every 2 seconds. this is used to keep the connection alive.

#### BluetoothBase.cs
```csharp
protected async Task SetupBluetoothDevice()
{
    var isDeviceSetup = await SubscribeToCharacteristicsAsync();
    var authStatus = await SendDeviceAuthRequest();
    StartHeartbeat();
}
```
Here we are subscribing to the characteristics we need to receive data from the device.

We are also setting up Characteristic_ValueChanged as a event handler to process the data we receive from the device.
#### BluetoothManagerBackup.cs
```csharp
protected override async Task<bool> SubscribeToCharacteristicsAsync()
{
    if (_primaryService == null) return false;
    _gaTTeventsCharacteristicUuid = await _primaryService.GetCharacteristicAsync(EventsCharacteristicUuid).WaitAsync(TimeSpan.FromSeconds(5));
    _gaTTeventsCharacteristicUuid.CharacteristicValueChanged += Characteristic_ValueChanged;
    await _gaTTeventsCharacteristicUuid.StartNotificationsAsync();

    _gaTTheartbeatCharacteristicUuid = await _primaryService.GetCharacteristicAsync(HeartbeatCharacteristicUuid).WaitAsync(TimeSpan.FromSeconds(5));
    _gaTTheartbeatCharacteristicUuid.CharacteristicValueChanged += Characteristic_ValueChanged;
    await _gaTTheartbeatCharacteristicUuid.StartNotificationsAsync();

    _gaTTwriteResponseCharacteristicUuid = await _primaryService.GetCharacteristicAsync(WriteResponseCharacteristicUuid).WaitAsync(TimeSpan.FromSeconds(5));
    _gaTTwriteResponseCharacteristicUuid.CharacteristicValueChanged += Characteristic_ValueChanged;
    await _gaTTwriteResponseCharacteristicUuid.StartNotificationsAsync();

    _gaTTmeasurementCharacteristic = await _primaryService.GetCharacteristicAsync(MeasurementCharacteristicUuid).WaitAsync(TimeSpan.FromSeconds(5));
    _gaTTmeasurementCharacteristic.CharacteristicValueChanged += Characteristic_ValueChanged;
    await _gaTTmeasurementCharacteristic.StartNotificationsAsync();
    
    return true;
}
```
Now that we are subscribed to the characteristics we need, we can send the auth request to the device.

This is a request saying hey i want to communicate with you, here is the encryption key we will use to communicate.
From there the device will respond on the WRITE_RESPONSE_CHARACTERISTIC_UUID.

We will start seeing my ByteConversionUtils class being used here. this is a class i made to help with byte conversions.
and is located under the util directory in the project. most of the functions are self explanatory.

Just in case this readme is seperated from my project i have included the class at the bottom of this document.

You will also start seeing reference to my Encryption class. this is a class i made to handle the encryption of the data being sent to the device.
This is located in the file Encryption.cs under the util directory in the project. and i will go into more detail later.

#### BluetoothBase.cs
```csharp
private async Task<bool?> SendDeviceAuthRequest()
{
    var intToByteArray = ByteConversionUtils.IntToByteArray(1, true); // return [0x01]    
    var encryptionTypeBytes = Encryption.GetEncryptionTypeBytes(); // return encryption type in bytes
    var keyBytes = BtEncryption.GetKeyBytes(); // return encryption key to be sent to the device
    
    byte[] bArr = new byte[intToByteArray.Length + encryptionTypeBytes.Length + keyBytes.Length];
    Array.Copy(intToByteArray, 0, bArr, 0, intToByteArray.Length);
    Array.Copy(encryptionTypeBytes, 0, bArr, intToByteArray.Length, encryptionTypeBytes.Length);
    Array.Copy(keyBytes, 0, bArr, intToByteArray.Length + encryptionTypeBytes.Length, keyBytes.Length);
    bool status = await WriteValue(ServiceUuid, _authRequestCharacteristicUuid, bArr); // here we call the write command to send the initial connection request with the encryption key to be used
    return status;
}
```
There are three write classes in the project that are used to send data to the device.

WriteValue is a generic write function that takes the service uuid, characteristic uuid, and the data to be sent.
#### BluetoothBase.cs
```csharp
private async Task<bool> WriteValue(Guid uuid, Guid uuid2, byte[]? byteArray)
{
    bool status = await WriteCharacteristic(uuid, uuid2, byteArray);
}
```
WriteCommand is a function that takes a byte array and sends it to the COMMAND_CHARACTERISTIC_UUID.
#### BluetoothBase.cs
```csharp
private async Task WriteCommand(byte[]? data)
{
    await WriteValue(ServiceUuid, _commandCharacteristicUuid, BtEncryption.Encrypt(data));
}
```
WriteConfiguration is a function that takes a byte array and sends it to the CONFIGURE_CHARACTERISTIC_UUID.
#### BluetoothBase.cs
```csharp
private async Task<bool> WriteConfig(byte[]? data)
{
    await WriteValue(ServiceUuid, _configureCharacteristicUuid, BtEncryption.Encrypt(data));
}
```

Finally all three end up here in the write characteristic function.
#### BluetoothManagerBackup.cs
```csharp
protected override async Task<bool> WriteCharacteristic(Guid serviceUuid, Guid characteristicUuid, byte[]? data)
{
    var characteristic = await _primaryService.GetCharacteristicAsync(characteristicUuid).WaitAsync(TimeSpan.FromSeconds(5));
    await characteristic.WriteValueWithoutResponseAsync(data);
}
```

Now that we have told the device we want to connect, the encryption key to be used for the communication, and have subscribed to all the necessary characteristics.

We can start listening for the Write Response characteristic to send the required authorization token for the device to communicate with us.

I am going to be removing alot from this upcoming code snippet as its a bit more complex, and keeping it to a minimum.
#### BluetoothBase.cs
```csharp
protected async void Characteristic_ValueChanged(object? sender, object? args)
{
    byte[] value = GetCharacteristicValueAsync(args);
    Guid senderUuid = GetSenderUuidAsync(sender);
    
    if (senderUuid == WriteResponseCharacteristicUuid)
    {
        if (value.Length >= 2)
        {
            byte byte2 = value[0]; // 02 Means Send initial parameters
            byte byte3 = value[1]; // 00 is neeeded here saying its accepting a response

            if (value.Length > 2)
            {
                byte[] byteArray = new byte[value.Length - 2];
                Array.Copy(value, 2, byteArray, 0, value.Length - 2);

                if (byte2 == 2)
                {
                    byte[] byteArr3 = new byte[4];
                    Array.Copy(byteArray, 0, byteArr3, 0, 4);
                    // here we retrive the USER ID from the devices response to us.
                    // we need to use the API to get the authorization token for the device from this USER ID.
                    int byteArrayToInt = ByteConversionUtils.ByteArrayToInt(byteArr3, true);
                    
                    WebApiClient client = new();
                    WebApiClient.ApiResponse? response = await client.SendRequestAsync(byteArrayToInt);
                    
                    // now that we have the authorization token we can send the initial parameters to the device.
                    if (response is { Success: true, User.Token: not null })
                    {
                        // see just below this for the GetInitialParameters function
                        byte[]? bytes = DeviceManager.Instance?.GetInitialParameters(response.User.Token);
                        await WriteConfig(bytes);
                        // I'm really not sure why sending this TWICE with a delay makes any difference.
                        // but i have observed that other apps also do the same thing, and it works.....
                        await Task.Delay(200);
                        await WriteConfig(bytes);
                    }
                    return;
                }
            }
        }
    }
}
```
This is the function that generates the initial parameters to be sent to the device.
#### DeviceManager.cs
```csharp
public byte[] GetInitialParameters(string tokenInput)
    {
        UserToken = tokenInput;
        // Generate required byte arrays
        byte[] airPressureBytes = ByteConversionUtils.GetAirPressureBytes(0.0);
        byte[] temperatureBytes = ByteConversionUtils.GetTemperatureBytes(15.0);
        byte[] longToUintToByteArray = ByteConversionUtils.LongToUintToByteArray(long.Parse(UserToken), true);
        // Concatenate all byte arrays
        byte[] concatenatedBytes = new byte[] { 1, 2, 0, 0 }.Concat(airPressureBytes)
         .Concat(temperatureBytes)
         .Concat(longToUintToByteArray)
         .Concat(new byte[] { 0, 0 })
         .ToArray();
        
        return concatenatedBytes;
    }
```
At this point if all went well we should have a blue light on the device.

## Arming and Disarming the Device
Now that we are connected to the device i will talk about one of the more simple parts. telling the device to be ready for a shot.

There are two different arm and disarm events i have found and i am unsure of the difference between the two.

01180001000000 and 010D0001000000 are the two commands i have found to arm the device.

01180000000000 and 010D0000000000 are the two commands i have found to disarm the device.

ARM = be ready for shot

DISARM = stop being ready for shot

#### BluetoothBase.cs
```csharp
public async Task ArmDevice()
{
    if (BluetoothDevice == null) return;
    var data = ByteConversionUtils.HexStringToByteArray("010D0001000000"); //01180001000000 also found 010D0001000000
    await WriteCommand(data);
    _isDeviceArmed = true;
}
public async Task DisarmDevice()
{
    if (BluetoothDevice == null) return;
    var data = ByteConversionUtils.HexStringToByteArray("010D0000000000"); //01180000000000 also found 010D0000000000
    await WriteCommand(data);
    _isDeviceArmed = false;
}
```

## Reading the shot data from the device
Now that the device is armed and ready for a shot we can start reading the data from the device.

The device reports the shot data in HEX that has to be converted to INT16 little endian to be read correctly.

Example shot data:

44004F00E2FF0A01C8FFFC0705000A0000000000

Using this site:
https://www.scadacore.com/tools/programming-calculators/online-hex-converter/

we can see the data looks like this:

| byte | hex | int16 | Description                       |
| --- | --- | --- |-----------------------------------|
| 0 | 00 44 | 68 | Club Head Speed                   |
| 2 | 00 4F | 79 | Ball Speed                        |
| 4 | FF E2 | -30 | HLA Moved one decibel: -3.0       |
| 6 | 01 0A | 266 | VLA Moved one decibel: 26.6       |
| 8 | FF C8 | -56 | Spin Axis Moved one decibel: -5.6 |
| 10 | 07 FC | 2044 | Total Spin                        |
| 12 | 00 05 | 5 | Carry Distance                    |
| 14 | 00 0A | 10 | Total Distance                    |
| 16 | 00 00 | 0 | Unknown                           |
| 18 | 00 00 | 0 | Unknown                           |

#### BluetoothBase.cs
```csharp
protected async void Characteristic_ValueChanged(object? sender, object? args)
{
    if (args == null || sender == null) return;
    byte[] value = GetCharacteristicValueAsync(args);
    Guid senderUuid = GetSenderUuidAsync(sender);    

    else if (senderUuid == MeasurementCharacteristicUuid && !_settingUpConnection)
    {
        // Here we get the measurement data from the device
        byte[]? decrypted = BtEncryption.Decrypt(value);
        // now that we got the data we convert it to shot data for GSPro
        var messageToSend = MeasurementData.Instance.ConvertHexToMeasurementData(ByteConversionUtils.ByteArrayToHexString(decrypted));
        
        // here we send the shot data to GSPro
        await Task.Run(() =>
        {
            (Application.Current as App)?.SendShotData(messageToSend);
        });
    }
}
```
Here we take the shot data and prepare it for GSPro, ill leave most of the code in since it helps explain the retrieved data.
#### MeasurementData.cs
```csharp
public OpenConnectApiMessage ConvertHexToMeasurementData(string? hexData)
{
    const double multiplier = 2.2375; // this is the multiplier for when the device is reporting MPH
    byte[] bytes = Enumerable.Range(0, hexData?.Length ?? 0)
                     .Where(x => x % 2 == 0)
                     .Select(x => Convert.ToByte(hexData?.Substring(x, 2), 16))
                     .ToArray();

    ClubHeadSpeed = Math.Round(BitConverter.ToInt16(bytes, 0) / 10.0 * multiplier, 2); // Round to 2 decimal places
    BallSpeed = Math.Round(BitConverter.ToInt16(bytes, 2) / 10.0 * multiplier, 2); // Round to 2 decimal places
    Hla = BitConverter.ToInt16(bytes, 4) / 10.0;
    Vla = BitConverter.ToInt16(bytes, 6) / 10.0;
    SpinAxis = BitConverter.ToInt16(bytes, 8) / 10.0;
    TotalSpin = BitConverter.ToUInt16(bytes, 10);
    Unknown1 = BitConverter.ToUInt16(bytes, 12); // carry distance?
    Unknown2 = BitConverter.ToUInt16(bytes, 14); // total distance? both seem lower than AG, but not crazy off...
    // Serialize MeasurementData instance to JSON string

    double backSpin = CalculateBackSpin(TotalSpin, SpinAxis);
    double sideSpin = CalculateSideSpin(TotalSpin, SpinAxis);
    OpenConnectApiMessage.Instance.ShotNumber++;
    return new OpenConnectApiMessage()
    {
        ShotNumber = OpenConnectApiMessage.Instance.ShotNumber,
        BallData = new BallData()
        {
            Speed = BallSpeed,
            SpinAxis = SpinAxis,
            TotalSpin = TotalSpin,
            BackSpin = backSpin,
            SideSpin = sideSpin,
            Hla = Hla,
            Vla = Vla,
        },
        ClubData = new ClubData()
        {
            Speed = ClubHeadSpeed
        },
        ShotDataOptions = new ShotDataOptions()
        {
            ContainsBallData = true,
            ContainsClubData = true,
            LaunchMonitorIsReady = true,
            IsHeartBeat = false
        }
    };
}
```

## Events
Here are the different events that come from the device

EVENT

00 = shot happened

01 = processing shot

02 = ready for shot

03 = battery life second array item value ( 0x03, 0x55 ) 55% batt

04 = not sure yet or if its used

05 = last shot misread / all zeros

#### BluetoothBase.cs
```csharp
protected async void Characteristic_ValueChanged(object? sender, object? args)
{
    if (args == null || sender == null) return;
    byte[] value = GetCharacteristicValueAsync(args);
    Guid senderUuid = GetSenderUuidAsync(sender);    

    else if (senderUuid == EventsCharacteristicUuid && !_settingUpConnection)
    {
        byte[]? decrypted = BtEncryption.Decrypt(value);
        switch (decrypted[0])
        {
            case 0:
                {
                    Logger.Log("BluetoothManager: Shot happened!");
                    break;
                }
            case 1:
                {
                    Logger.Log("BluetoothManager: Device is processing shot!");
                    break;
                }
            case 2:
                {
                    Logger.Log("BluetoothManager: Device is ready for next shot!");
                    break;
                }
            case 3:
                {
                    int batteryLife = decrypted[1];
                    if (App.SharedVm != null) App.SharedVm.LmBatteryLife = batteryLife.ToString();
                    Logger.Log("Battery Level: " + batteryLife);
                    break;
                }
            case 5 when decrypted[1] == 0:
                {
                // Logger.Log("BluetoothManager: last shot was misread, all zeros...");
                break;
                }
            case 5 when decrypted[1] == 1:
                {
                    Logger.Log("BluetoothManager: device disarmed");
                    break;
                }
        }
    }
}
```

## Write Configuration
Here is what i have figured out for the write configuration on the device

| Byte/Hex                     | Description |
|------------------------------| --- |
| 010200007DC8DC05A63C5A440000 | Limited Ball Flight, Rapsodo Ball, Altitude 0 | 
| 010100007DC8DC05A63C5A440000 | Limited Ball Flight, Range Ball, Altitude 0 |
| 010000007DC8DC05A63C5A440000 | Limited Ball Flight, Premium Ball, Altitude 0 |
| 010201007DC8DC05A63C5A440000 | OutDoors, Rapsodo Ball, Altitude 0 |
| 010200007CBADC05A63C5A440000 | Limited Ball Flight, Rapsodo Ball, Altitude 1000 |
| 01020000B953DC05A63C5A440000 | Limited Ball Flight, Rapsodo Ball, Altitude 10000 |


So with that the first byte is always 0x01

There don't seem to be a byte to tell the device the units of measurement that is done via the rapsodo app.

Second byte seems to be The type of ball, 0x00 is range ball, 0x01 is premium ball, 0x02 is rapsodo ball
and i am curious if this will be 0x03/04 for pro v1/v1x balls.

This byte seems to be indoor vs outdoor flight, 0x00 is indoor, 0x01 is outdoor.

Fifth and sixth byte seems to be the altitude

## Encryption

The device uses AES 256 with CBC and PKCS7 padding encryption to communicate with the device with.

there seems to be a predetermined IV in the byte array below.

and the key can be generated at will but has to be sent to the device in the initial auth request outlined above.

```csharp
byte[] _ivParameter = [109, 46, 82, 19, 33, 50, 4, 69, 111, 44, 121, 72, 16, 101, 109, 66]
using var aes = Aes.Create();
aes.Key = _encryptionKey ?? aes.Key;
aes.IV = _ivParameter;
aes.Mode = CipherMode.CBC;
aes.Padding = PaddingMode.PKCS7;
```

in my connector i have a encryption help utility hidden behind the debug button, 
if you hold the debug console for more than 2 seconds you will get a new window.

if you check the get key option and put in the wireshark auth request value you will get the encryption key.

Example communication 
0100000000017f624f5ac3c1377eb2e10451a2258b0501be91c384317c0294e647b92056d567

Gets the key:
7F624F5AC3C1377EB2E10451A2258B0501BE91C384317C0294E647B92056D567

Here is how you can get the key from the auth request value.
#### BluetoothBase.cs
```csharp
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
```
And here is how you can decrypt the data communication when you know the key

#### Encryption.cs
```csharp
public byte[] DecryptKnownKey(byte[] input, byte[] encryptionKeyInput)
{
    byte[] _ivParameter = [109, 46, 82, 19, 33, 50, 4, 69, 111, 44, 121, 72, 16, 101, 109, 66]
    using var aes = Aes.Create();
    aes.Key = encryptionKeyInput;
    aes.IV = _ivParameter;
    aes.Mode = CipherMode.CBC;
    aes.Padding = PaddingMode.PKCS7;

    using var decrypted = aes.CreateDecryptor(aes.Key, aes.IV);
    return decrypted.TransformFinalBlock(input, 0, input.Length);
}
```

## API information

Rapsodo API URL: https://mlm.rapsodo.com/api/simulator/user/

You need to append the user id from the device to the url to get the authorization token for the device.

like so: https://mlm.rapsodo.com/api/simulator/user/123456

The api expects a header with a secret like so:

#### WebApiClient.cs
```csharp
using HttpClient httpClient = new();
httpClient.DefaultRequestHeaders.Add(SecretKey, _secretValue);
```

Response will look like this in JSON:
```json
{"success":true,"user":{"id":123456,"token":"1043255814","expireDate":1711562523}}
```

From there we know the tokens expiry time in UNIX time, and the token required to get the device to communicate with us.

It is possible to bypass this authorization token on the device. but this connector is not designed to do that.

I personally like what rapsodo is doing with the MLM2PRO and I believe we should support them in their efforts
by maintaining a subscription to their service, and going this route ensures the user has a subscription to use this connector.

How to get the API secret.

There are many methods some easier than others. but it is not hard to obtain. you can decompile any of the applications that support
direct bluetooth connection to the device and look for it there.

Easiest method is to download Fiddler Classic free open it and enable HTTPS packet inspection, download Awesome golf simulator for pc and connect your device.

## Support my work
If you like my work and want to support me, you can donate to me via ko-fi. I would be very grateful for your support.

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)
](https://ko-fi.com/D1D8VL7RV)

## Reference
**Here is a reference to the encryption class and the byte conversion class.**

The prederminedKey value below is purely for simple debugging of peoples wireshark data.
you can generate your own key at will or just use the encryption tools to make one so each connection has a unique key if you wanted.
#### Encryption.cs
```csharp
using System.Security.Cryptography;

namespace MLM2PRO_BT_APP.util
{
    public class Encryption
    {
        private readonly byte[] _ivParameter = [109, 46, 82, 19, 33, 50, 4, 69, 111, 44, 121, 72, 16, 101, 109, 66];
        private readonly byte[]? _encryptionKey;

        public static byte[] GetEncryptionTypeBytes()
        {
            return [0, 1];
        }

        public Encryption()
        {
            byte[] predeterminedKey = new byte[32] { 26, 24, 1, 38, 249, 154, 60, 63, 149, 185, 205, 150, 126, 160, 38, 61, 89, 199, 68, 140, 255, 21, 250, 131, 55, 165, 121, 250, 49, 121, 233, 21 };
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Key = predeterminedKey;
            _encryptionKey = aes.Key;
        }

        public byte[]? GetKeyBytes()
        {
            return _encryptionKey;
        }

        public byte[] Encrypt(byte[]? input)
        {
            if (input == null)
            {
                return Array.Empty<byte>(); // Return an empty byte array or handle it according to your requirements
            }

            using var aes = Aes.Create();
            aes.Key = _encryptionKey ?? aes.Key;
            aes.IV = _ivParameter;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            return encryptor.TransformFinalBlock(input, 0, input.Length);
        }


        public byte[]? Decrypt(byte[]? input)
        {
            try
            {
                using var aes = Aes.Create();
                aes.Key = _encryptionKey ?? aes.Key;
                aes.IV = _ivParameter;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var decrypted = aes.CreateDecryptor(aes.Key, aes.IV);
                return input is { Length: > 0 } ? decrypted.TransformFinalBlock(input, 0, input.Length) : null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public byte[] DecryptKnownKey(byte[] input, byte[] encryptionKeyInput)
        {
            try
            {
                using var aes = Aes.Create();
                aes.Key = encryptionKeyInput;
                aes.IV = _ivParameter;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var decrypted = aes.CreateDecryptor(aes.Key, aes.IV);
                return decrypted.TransformFinalBlock(input, 0, input.Length);
            }
            catch (Exception ex)
            {
                return Array.Empty<byte>();
            }
        }
    }
}

```
#### ByteConversionUtils.cs
```csharp
using System.Text;
using Windows.Storage.Streams;

namespace MLM2PRO_BT_APP.util
{
    public enum WriteType
    {
        WithResponse = 2,
        WithoutResponse = 1,
        Signed = 4
    }

    public class WriteTypeProperties
    {
        public int Property { get; private set; }
        public int WriteType { get; private set; }

        private WriteTypeProperties(int writeType, int property)
        {
            WriteType = writeType;
            Property = property;
        }

        public static WriteTypeProperties? GetWriteTypeProperties(WriteType writeType)
        {
            int writeTypeValue = (int)writeType; // Convert enum to int
            switch (writeTypeValue)
            {
                case 2:
                    return new WriteTypeProperties(2, 8);
                case 1:
                    return new WriteTypeProperties(1, 4);
                case 4:
                    return new WriteTypeProperties(4, 64);
                default:
                    return null;
            }
        }
    }


    public class ByteConversionUtils
    {
        private static byte[] ShortToByteArray(short s, bool littleEndian)
        {
            return littleEndian ? new byte[] { (byte)s, (byte)(s >> 8) } : new byte[] { (byte)(s >> 8), (byte)s };
        }
        public static int[] ArrayByteToInt(byte[]? byteArray)
        {
            if (byteArray == null)
            {
                return Array.Empty<int>(); // Return an empty array or handle it according to your requirements
            }

            int length = byteArray.Length;
            int[] intArray = new int[length];
            for (int i = 0; i < length; i++)
            {
                intArray[i] = byteArray[i] & 0xFF;
            }
            return intArray;
        }
        public static byte[] GetAirPressureBytes(double d)
        {
            double d2 = d * 0.0065;
            return ShortToByteArray((short)((int)((((Math.Pow(1.0 - (d2 / ((15.0 + d2) + 273.15)), 5.257) * 1013.25) * 0.1) - 50.0) * 1000.0)), true);

        }
        public static byte[] GetTemperatureBytes(double d)
        {
            return ShortToByteArray((short)((int)(d * 100.0d)), true);
        }
        public static byte[] LongToUintToByteArray(long j, bool littleEndian)
        {
            if (littleEndian)
            {
                return BitConverter.GetBytes(j);
            }
            return BitConverter.GetBytes(j);
        }
        public static byte[] IntToByteArray(int i, bool littleEndian)
        {
            if (littleEndian)
            {
                return [(byte)i, (byte)(i >> 8), (byte)(i >> 16), (byte)(i >> 24)];
            }
            return [(byte)(i >> 24), (byte)(i >> 16), (byte)(i >> 8), (byte)i];
        }
        public static byte[] ConvertIBufferToBytes(IBuffer buffer)
        {
            // Create a DataReader from the IBuffer
            var reader = DataReader.FromBuffer(buffer);

            // Create a byte array with the same length as the buffer
            byte[] bytes = new byte[buffer.Length];

            // Read the bytes from the buffer into the byte array
            reader.ReadBytes(bytes);

            return bytes;
        }
        public static int ByteArrayToInt(byte[]? byteArray, bool isLittleEndian)
        {
            if (byteArray == null)
            {
                return 0;
            }

            int result;
            if (isLittleEndian)
            {
                result = (byteArray[0] & 0xFF) | ((byteArray[1] & 0xFF) << 8) | ((byteArray[2] & 0xFF) << 16) | ((byteArray[3] & 0xFF) << 24);
            }
            else
            {
                Array.Reverse(byteArray); // Convert to big-endian if necessary
                result = (byteArray[0] & 0xFF) << 24 | (byteArray[1] & 0xFF) << 16 | (byteArray[2] & 0xFF) << 8 | byteArray[3] & 0xFF;
            }

            return result;
        }
        public byte[] StringToByteArray(string hex)
        {
            try
            {
                int numberChars = hex.Length;
                byte[] bytes = new byte[numberChars / 2];
                for (int i = 0; i < numberChars; i += 2)
                {
                    bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
                }
                return bytes;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Handle the exception by returning null or an empty byte array
                // return null;
                return Array.Empty<byte>();
            }
        }
        public static string ByteArrayToHexString(byte[]? bytes)
        {
            if (bytes == null)
            {
                return string.Empty; // Or handle the null case according to your needs
            }

            StringBuilder hex = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                hex.AppendFormat("{0:X2}", b);
            }
            return hex.ToString();
        }
        public static byte[] HexStringToByteArray(string hex)
        {
            if (hex.Length % 2 != 0)
            {
                Logger.Log("The hexadecimal string must have an even number of characters." + nameof(hex));
            }

            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }
        public string IntArrayToString(int[] intArray)
        {
            return string.Join(", ", intArray);
        }
        public string ByteArrayToString(byte[] byteArray)
        {
            return string.Join(", ", byteArray);
        }
    }
}

```
