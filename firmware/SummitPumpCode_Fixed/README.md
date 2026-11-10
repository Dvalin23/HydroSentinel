# SummitPumpCode mobile-data setup

This is the project copy of the sketch supplied in Downloads. The original file is left unchanged.

The sketch now opens a SIM800L GPRS data bearer after the modem registers on the mobile network. It keeps the pump-control loop running while the modem connects and continues to support the existing SMS alerts.

## Before uploading

1. Replace `YOUR_CARRIER_APN` in `SummitPumpCode_Fixed.ino` with the APN provided by the SIM card's mobile carrier.
2. Leave `GPRS_USERNAME` and `GPRS_PASSWORD` empty unless the carrier gave you values for them.
3. Use a SIM with an active data plan and 2G/GPRS coverage where the controller is installed.
4. Open Serial Monitor at 9600 baud. After GSM registration, the sketch reports whether it received a mobile-data IP address. If setup fails, it retries after one minute.

This first step establishes mobile data only; it does not upload readings to HydroSentinel yet. The backend needs a public HTTPS address before the firmware can send check-ins. Once the app is hosted, the next firmware change will add the device check-in URL and its unique per-device key. Do not put the backend's shared relay secret in the sketch.

SIMCom documents the `AT+SAPBR` bearer setup commands in its [SIM800 Series AT Command Manual](https://www.simcom.com/product/SIM800.html). Exact behavior can vary by SIM800L board firmware and carrier APN.
