# gsp-r10-adapter

Utility to bridge R10 launch monitor to GSPro. Starts an "E6 Connect" compatible server and translates the messages into GSPro OpenConnect format.  Also provides an option to connect directly to the R10 via bluetooth.

Heavily inspired by this project https://github.com/travislang/gspro-garmin-connect-v2. 

The goal of this project was to provide an ultra lightweight alterntive to the current offering, with a focus on API transparency.

![Sample](screenshot.png)


## Using Direct Bluetooth Connector

In order to use the direct bluetooth connection to the R10 you must
- Enable bluetooth in `settings.json` file
- Edit `settings.json` to reflect your desired altitude, tee distance, temperature, etc.
- Set device in pairing mode (blue blinking light) by holding power button for few seconds
- **Pair the R10 from the windows bluetooth settings**
  - On windows 11 you may need to set "Bluetooth Device Discovery" to `advanced`
  - This step only needs to be done once
  - You may need to disable bluetooth on previously paired devices to prevent them from stealing the connection


## Running

### From release

- Download either the standalone or net6 package from the release page. Extract zip to your local machine and run the exe file.
  - Use the standalone package if you are unsure whether your computer has a dotnet runtime installed
  - Use the net6 package if you believe your computer has a dotnet runtime installed.

### From Source

- Install a dotnet 6 sdk if you don't have one already
- `dotnet run` from project directory