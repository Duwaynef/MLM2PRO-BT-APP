# MLM2PRO-BT-GSPRO
Connector for the MLM2PRO to connect to GSPro via direct bluetooth connection.
This is in beta to say the least.. there will be bugs and crashes.

please open an issue if crash

## Support my work
If you like my work and want to support me, you can donate to me via ko-fi. I would be very grateful for your support.

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)
](https://ko-fi.com/D1D8VL7RV)

## HOW TO USE APP
The main requirement is that you have a Rapsodo subscription for third party apps, **no other subscriptions are nessicary**.

Open Rapsodo MLM2PRO app, go to play, select third party, and authorize Awesome golf. **This does not require a Awesome Golf subscription**.

Pair your device with windows: **MLM2-SerialNumber not MLM2_BT_**, you may need to turn on advanced bluetooth discovery to see the device on the list.

**Pairing is only required the first time. and should not be required if you have used the Awesome Golf pc app recently**

Open the app and set the Web API Token if it has not been saved from before. **You will have to ask around for this key as it uses an api secret to get the device authorization, use this app at your own risk**

The app should handle everything, it should connect,and the main window should show "CONNECTED" as the launch monitor status

Once you load into a round, the device should turn green automatically and be ready to read hits.

When you leave a round it will turn blue again until you start a new round.

Selected club in GSPro does nothing for the app outside of labeling the club in the Shot Data section, and discovering when you are putting to enable the putting cam ( if enabled ).

## CONFIGURATION
On first load of the application a settings.json file is created in the same directory as the executable, it will prompt for the Web Api secret if it is not set.

The only setting that is required to communicate with the device is the web api secret.

Config file was overhauled after a few updates. if you have been keeping a config file. i suggest you open the app and let it build a new
config file. then compare your old settings ( or go to settings in the app and set your desired settings ).

## MAIN WINDOW INFO
It should be self explanitory however each text box below their respective labels are for status updates of that item.

next to GSPro after a communication has gone back and forth it will show the selected club, this is purely for knowing when to enable or disable putting. or if you want to track stats on a particular club batch, however GSPro may show the data better.

Next to launch monitor you will see a number when connected. This is the device battery life.

Shot Data shows the list of shots and the "Result" shows if GSPro sucessfully accept the data transmission.

## How to use Relay with other connectors
- Open this connector
- Go to Settings and Enabled API Relay under GSPro settings
- Close and reopen this connector
- Open the other connector
- Go to settings and set GSPro port to **951** or the port you set in the settings of this connector if you changed it
- hit connect on other connector. *some connectors require the GSPro API window to be open. so make sure its open when testing.*

## Troubleshooting
getting a connection can sometimes be unstable, easiest method is to turn device on, wait for it to be solid red. then open the app.

Common ways to get the **first** connection working:
- close app
- unpair
- toggle off and back on Bluetooth
- pair the device and make sure to choose mlm2-serialnumber ( not MLM2_BT_ , however BlueZ is okay )
- windows shows connected. just wait for it to flip to not connected
- Open app and let it attempt to connect

Putting cam location
- if you need the putting cam to open and close in the same position each time
- get on a green so the putting cam is triggered
- place the cam window where you want it to be
- hit Q in the putting cam window so it closes
- it should auto re-open and remember the position for the future

if you get it all connected and all seems well, and you have a green light, but you do not get shot data.
press the **Resub** button. when the light is green avoid the other buttons as it may break the connection with the device.
and if that happens you need to close the app, power cycle the device, and reopen the app when the device is solid red again.


## Thank you
I just want to say thank you to the many projects that came before mine where i am using, have used as inpiration, used code, or even just design ideas.

[cam-putting-py by alleexx](https://github.com/alleexx/cam-putting-py)

[gsp-r10-adapter by mholow](https://github.com/mholow/gsp-r10-adapter)

[MLM2PRO-GSPro-Connector by springbok](https://github.com/springbok/MLM2PRO-GSPro-Connector)

