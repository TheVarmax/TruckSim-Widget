<p align="center">
  <img src="assets/icon.png" width="100" />
</p>

<p align="center">
  <sub>
    🌐 Change language →
    <a href="README.ua-UA.md">🇺🇦 UA</a>
  </sub>
</p>

<h1 align="center">TruckSim Widget</h1>

<p align="center">
  Lightweight status and telemetry overlay for Euro Truck Simulator 2 & American Truck Simulator
</p>

<p align="center">
  <strong>TrucksBook status • Delivery tracking • Smart alerts • Auto-hide overlay</strong>
</p>

<p align="center">
  <a href="https://trucksim.maksym.uk"><img src="https://img.shields.io/badge/Website-trucksim.maksym.uk-2ea44f?style=flat-square"></a>
  <a href="https://t.me/thevarmax"><img src="https://img.shields.io/badge/Telegram-@thevarmax-26A5E4?style=flat-square&logo=telegram&logoColor=white"></a>
  <a href="https://github.com/TheVarmax/TruckSim-Widget/releases"><img src="https://img.shields.io/badge/Download-Latest_Release-5865F2?style=flat-square&logo=github&logoColor=white"></a>
  <a href="https://send.monobank.ua/8Q2FKkJr3B"><img src="https://img.shields.io/badge/Donate-Monobank-ff5f5f?style=flat-square"></a>
  <a href="https://buymeacoffee.com/thevarmax"><img src="https://img.shields.io/badge/Buy_Me_A_Coffee-Support-FFDD00?style=flat-square&logo=buymeacoffee&logoColor=000000"></a>
</p>

<p align="center">
  <a href="https://github.com/TheVarmax/TruckSim-Widget/releases/latest"><img src="https://img.shields.io/github/v/release/TheVarmax/TruckSim-Widget?display_name=release" /></a>
  <a href="https://github.com/TheVarmax/TruckSim-Widget/releases"><img src="https://img.shields.io/github/downloads/TheVarmax/TruckSim-Widget/total?style=flat-square&color=blue" /></a>
  <a href="https://github.com/TheVarmax/TruckSim-Widget/blob/master/LICENSE"><img src="https://img.shields.io/github/license/TheVarmax/TruckSim-Widget?style=flat-square" /></a>
</p>

---

## What is this?

TruckSim Widget is a lightweight overlay that runs alongside Euro Truck Simulator 2 and American Truck Simulator.

Its main purpose is to make TrucksBook status easy to see while you drive: whether the client is running, telemetry is connected, and a delivery is being tracked correctly. It also displays essential trip information without turning your screen into another dashboard.

---

## Features

### 📊 TrucksBook status & smart alerts

See whether TrucksBook is online and recording correctly. The widget highlights meaningful issues such as client, telemetry, recording, synchronization, or upload problems.

### 📦 Delivery tracking

Shows cargo status, route, driven distance, and current trip progress.

### 🚛 Live telemetry

Real-time speed, route, game state, and delivery information from ETS2 and ATS.

### 🫥 Auto-hide mode

When everything is healthy, the widget can fade into an unobtrusive idle state. It expands when you hover over it and immediately becomes visible again when a warning or error is detected.

Auto-hide also works correctly when the game is closed, keeping the overlay out of the way unless attention is needed.

### 🎛️ Flexible interface

Choose between Full and Minimal UI modes, adjust opacity and scale, and keep the widget pinned above the game when needed.

### ⚠️ Speed warnings

Configurable speed warnings with visual indicators.

### 🌍 Localization

English and Ukrainian interface support, including English → Ukrainian city name translation in beta.

### ✨ Polished UI

Fully themed dialogs, smooth widget and settings animations, refined layouts, and animated settings dropdowns.

### 🔄 Safer update experience

Built-in updates keep the app current. After a successful update, TruckSim Widget now shows an in-app success dialog with quick links instead of forcibly opening a browser page.

---

## Installation

### Before you start

You need:

* Windows 10 or Windows 11
* Euro Truck Simulator 2 or American Truck Simulator installed through Steam
* [TrucksBook Client](https://trucksbook.eu/) installed and signed in (required for mileage tracking)

### 1. Download TruckSim Widget

1. Open the [latest release](https://github.com/TheVarmax/TruckSim-Widget/releases/latest).
2. Under **Assets**, download the TruckSim Widget `.zip` archive.
3. When the download finishes, right-click the archive and choose **Extract All...**.
4. Choose any folder where you want to keep the widget, for example `Documents\TruckSim Widget`.

> Do not run the app directly from inside the ZIP archive. Extract the whole archive first.

### 2. Install the telemetry plugin

Inside the extracted TruckSim Widget folder, open the `plugin` folder. It contains `scs-telemetry.dll`.

Copy `scs-telemetry.dll` into the `plugins` folder for every game you want to use with TruckSim Widget.

#### Euro Truck Simulator 2

```
...\Steam\steamapps\common\Euro Truck Simulator 2\bin\win_x64\plugins
```

#### American Truck Simulator

```
...\Steam\steamapps\common\American Truck Simulator\bin\win_x64\plugins
```

If the `plugins` folder does not exist, create it manually.

> Copy only `scs-telemetry.dll` into the game's `plugins` folder. Keep the rest of the TruckSim Widget files together in the folder where you extracted the archive.

### 3. Start the widget

1. Start TrucksBook Client and make sure you are signed in.
2. Launch ETS2 or ATS and load your profile.
3. Open `TruckSim Widget.exe` from the extracted TruckSim Widget folder.
4. Check that the widget shows TrucksBook as online and telemetry as connected.
5. Start driving.

---

## Notes

City localization is still in beta.

Some city names may be missing or inaccurate due to the large amount of DLC content in ETS2 and ATS. Coverage is actively improving.

---

## Philosophy

> Show only what matters. Nothing more.

TruckSim Widget is built to stay quiet when everything works and become useful the moment something needs your attention.

---

## Support

If you enjoy TruckSim Widget and want to support its development, you can do so here:

<p align="center">
  <a href="https://buymeacoffee.com/thevarmax">
    <img src="https://img.shields.io/badge/Buy_Me_A_Coffee-Support-FFDD00?style=for-the-badge&logo=buymeacoffee&logoColor=white">
  </a>
</p>

<p align="center">
  <a href="https://send.monobank.ua/8Q2FKkJr3B">
    <img src="https://img.shields.io/badge/Monobank-Donate-ff5f5f?style=for-the-badge">
  </a>
</p>

---

## License

TruckSim Widget is licensed under the Mozilla Public License 2.0 (MPL-2.0).

You are free to use, modify, and distribute this software, including for commercial purposes, provided that modifications to MPL-covered files remain available under the same license when distributed.

The MPL-2.0 license does not grant rights to use the TruckSim Widget name, logo, branding, or other project trademarks.

This project is not affiliated with, endorsed by, or associated with SCS Software, Euro Truck Simulator 2, American Truck Simulator, or TrucksBook.

For full license details, see the [LICENSE](LICENSE) file.

---

<p align="center"> 
  Built with ❤️ for the Truck Simulator community
</p>
<p align="center"> 
  https://trucksim.maksym.uk 
</p>