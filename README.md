<p align="center">
  <img src="assets/icon.png" width="100" alt="TruckSim Widget icon" />
</p>

<p align="center">
  <sub>
    🌐 Change language →
    <a href="README.ua-UA.md">🇺🇦 UA</a>
  </sub>
</p>

<h1 align="center">TruckSim Widget</h1>

<p align="center">
  <strong>Never wonder if TrucksBook is still recording your delivery.</strong>
</p>

<p align="center">
  TruckSim Widget is a lightweight telemetry overlay for Euro Truck Simulator 2 and American Truck Simulator. It monitors TrucksBook and game telemetry in real time, provides configurable alerts, a built-in trip logbook, customizable interface options, Supporter features, and optional cloud synchronization.
</p>

<p align="center">
  <strong>Free core features • Lightweight • ETS2 & ATS</strong>
</p>

<p align="center">
  <a href="https://trucksim.uk"><img src="https://img.shields.io/badge/Website-trucksim.uk-2ea44f?style=flat-square"></a>
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

## Why TruckSim Widget?

TrucksBook is excellent at logging deliveries, but problems are not always obvious while you are driving. A disconnected telemetry plugin, a recording issue, or a synchronization problem can remain unnoticed until the trip is already over.

TruckSim Widget is a lightweight companion overlay for TrucksBook. It keeps an eye on the important parts in the background, so you can see when your delivery is being tracked correctly and notice problems before they cost you a trip.

It is not here to replace the in-game GPS or become another giant dashboard. When everything works, it stays out of your way. When something needs attention, it makes that clear.

---

## At a glance

| What it keeps an eye on | Why it matters |
| --- | --- |
| **TrucksBook status** | Know whether the client is online and your delivery is being tracked. |
| **Telemetry status** | Catch a missing or disconnected telemetry plugin early. |
| **Delivery state** | See whether a job is active, paused, delivered, or needs attention. |
| **Route and distance** | Keep essential trip information visible without extra windows. |
| **Warnings and errors** | Notice recording, sync, upload, or client issues before they ruin a delivery. |

---

## Features

### 📊 TrucksBook monitoring and smart alerts

Know at a glance whether TrucksBook is online and your delivery is being tracked correctly. If the client, telemetry, recording, synchronization, or upload needs attention, the widget makes it visible while the trip is still in progress.

### 📦 Delivery tracking

Keep cargo status, route, distance, and current delivery progress visible in one compact overlay. Active delivery progress is preserved across widget or game restarts when possible.

> **What do "REAL" and "RACE" mean?**  
> The widget displays the current delivery category according to **TrucksBook's official rules** based on your maximum speed:
> - **REAL**: Maximum speed has not exceeded 100 km/h (ETS2) or 80 mph (ATS). Standard realism category.
> - **RACE**: Maximum speed reached between 100–180 km/h (ETS2) or 80–112 mph (ATS).

### 🚛 Live telemetry

See essential driving and delivery information at a glance without cluttering your screen:
- Current speed and speed limit
- TrucksBook status and telemetry connection
- Route, distance, and delivery progress
- Cargo and destination details

### 🫥 Auto-hide

Automatically hides or reduces the interface when TrucksBook status is normal and brings it back when attention is needed. Expands smoothly on mouse hover.

### 🎛️ Flexible interface

Choose between **Full** and **Minimal** layouts, adjust opacity and scale, and pin the widget above the game when needed.

### ⚠️ Speed warnings

Set a custom speed warning threshold and receive clear visual indications and optional looping audio alerts when you reach it.

### 📖 Trip Logbook

Automatically saves completed deliveries locally and lets you review your driving history at any time.

- **Trip details:** Route, cargo, distance, duration, start and completion time, game, and play mode (Singleplayer, Convoy, TruckersMP).
- **Export (Supporter):** Export your trip logbook to CSV and JSON files.
- **Free tier:** Access your 5 most recent trips with essential trip details.
- **Supporter tier:** Unlimited history, extended statistics (speed, fuel consumption, vehicle damage), fines, and data export.

### 🖥️ HUD Mode *(Supporter)*

A sleek, focused overlay mode designed to show only the most essential telemetry data without cluttering the screen.
- Displays core driving metrics without text labels to maximize screen real estate.
- Smooth value animations and dynamic color synchronization with alerts.

### 🎨 Interface customization *(Supporter)*

Personalize the widget to match your setup:
- Four built-in themes: **Classic**, **Midnight**, **Carbon**, and **OLED Black**.
- **Custom UI Mode** to choose which widget sections are displayed.
- Custom accent colors and individual tile color settings.
- Adjustable opacity and interface scale.

### ☁️ Cloud Sync *(Supporter)*

Synchronize supported widget settings between your computers via TruckSim Cloud, with automatic sync, manual upload/download, and conflict resolution.

### 🛡️ Smart Braking Assistant *(Supporter)*

Helps maintain your TrucksBook **Real** category by gently braking when approaching the configured speed threshold (default: 98 km/h for ETS2, 78 mph for ATS).

### 🔄 Automatic updates

TruckSim Widget automatically checks for new releases on startup. When an update is available, confirming it installs the new version automatically, safely preserves your settings and Trip Logbook, and restarts the widget.

> [!NOTE]
> **Legacy versions:** Versions older than 1.5.9 may not detect current updates correctly. Download the latest installer manually from [GitHub Releases](https://github.com/TheVarmax/TruckSim-Widget/releases/latest).

### 🌍 English and Ukrainian

The interface supports English and Ukrainian, including optional translation of in-game city names.

---

## Free vs Supporter

TruckSim Widget is **free forever**. All core features required to monitor TrucksBook and track deliveries are available to everyone.

Supporter status unlocks additional customization, convenience tools, and extended statistics while directly funding further development.

[![Become a Supporter](https://img.shields.io/badge/Become_a_Supporter-trucksim.uk%2Fdonate-FFDD00?style=for-the-badge&logo=buymeacoffee&logoColor=000000)](https://trucksim.uk/donate)

| Feature | Free | Supporter |
| --- | :---: | :---: |
| TrucksBook monitoring & alerts | ✅ | ✅ |
| Live telemetry & delivery tracking | ✅ | ✅ |
| Active delivery recovery | ✅ | ✅ |
| Auto-hide | ✅ | ✅ |
| Speed warnings | ✅ | ✅ |
| Automatic updates | ✅ | ✅ |
| English & Ukrainian | ✅ | ✅ |
| Trip Logbook | Last 5 trips | Unlimited |
| Essential trip details | ✅ | ✅ |
| Extended trip statistics | ❌ | ✅ |
| Trip Logbook export (CSV & JSON) | ❌ | ✅ |
| HUD Mode | ❌ | ✅ |
| Custom UI Mode & themes | ❌ | ✅ |
| Cloud Sync | ❌ | ✅ |
| Smart Braking Assistant | ❌ | ✅ |

### Trip Logbook information

| Available trip data | Free | Supporter |
| --- | :---: | :---: |
| Route | ✅ | ✅ |
| Cargo | ✅ | ✅ |
| Distance | ✅ | ✅ |
| Duration | ✅ | ✅ |
| Started & Completed time | ✅ | ✅ |
| Game & Play Mode (Singleplayer / Convoy / TruckersMP) | ✅ | ✅ |
| Income | ❌ | ✅ |
| Average & Maximum Speed | ❌ | ✅ |
| Fuel Consumed & Average Consumption | ❌ | ✅ |
| Truck, Trailer & Cargo Damage | ❌ | ✅ |
| Truck (Brand & Model) | ❌ | ✅ |
| Fines (Total & Breakdown) | ❌ | ✅ |
| Export to CSV & JSON | ❌ | ✅ |

---

## Getting Started

> [!NOTE]
> **System Requirement:** [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) is required to run the installer and the widget.

### Installation

1. **Download:** Get `TruckSimWidgetSetup-<version>.exe` from the [latest release](https://github.com/TheVarmax/TruckSim-Widget/releases/latest).
2. **Run setup:** Launch the installer. It automatically detects Euro Truck Simulator 2 and American Truck Simulator. If a game is not detected automatically, you can select its folder manually.
3. **Configure telemetry:** The installer sets up the required `scs-telemetry.dll` for your selected games and safely handles existing telemetry plugins.
4. **Start driving:**
   - Launch **TrucksBook Client** and make sure you are signed in.
   - Start **ETS2** or **ATS**.
   - Launch **TruckSim Widget** and verify that TrucksBook and telemetry show active. Drive!

### Existing Installation / Maintenance

If TruckSim Widget is already installed, running the installer again provides three options:
- **Update:** Upgrades to a newer version while safely preserving all settings, Trip Logbook history, and license state.
- **Reinstall:** Reinstalls or repairs the current version without touching your user data.
- **Uninstall:** Removes the widget, allowing you to choose whether to keep or delete your user data.

### Need help or found an issue?

- Report bugs or request features: [GitHub Issues](https://github.com/TheVarmax/TruckSim-Widget/issues)
- Website contact form: [trucksim.uk](https://trucksim.uk)
- Telegram: [@thevarmax](https://t.me/thevarmax)
- Email: `support@trucksim.uk`

---

## When the widget shows a warning

Every warning is designed to help you fix a problem before it costs your delivery.

| What you see | What to check first |
| --- | --- |
| **TrucksBook offline** | Start TrucksBook Client and sign in. |
| **Telemetry issue** | Check that `scs-telemetry.dll` is in the correct game's `plugins` folder. |
| **Recording or sync warning** | Keep TrucksBook open and check its current status before continuing the delivery. |
| **Upload-related warning** | Let TrucksBook stay open until the completed delivery is processed. |

If something behaves unexpectedly, use the project website or the Telegram link above to report it with the relevant logs.

---

## Notes

> [!IMPORTANT]
> **Minimum Version Requirement: 1.6.1**  
> Online services (license validation, Supporter features, and Cloud Sync) require TruckSim Widget version **1.6.1 or newer**. Older versions cannot validate licenses or sync data. If you are using version 1.6.0 or older, update using the latest installer to continue using online services.

### Custom truck and trailer mods

When using custom truck or trailer mods, you might occasionally see false telemetry errors in the widget. This happens because some custom mods send incorrect or incomplete data to the telemetry plugin. If you encounter unexpected widget warnings while driving a modded vehicle, it is likely caused by the mod itself. If you experience these problems, please contact support at support@trucksim.uk and attach your TrucksBook logs and the widget's log file located at `%LocalAppData%\TruckSimWidget\app_log.txt`.

### Compatibility

TruckSim Widget is an independent project. It is not affiliated with, endorsed by, or associated with SCS Software, Euro Truck Simulator 2, American Truck Simulator, or TrucksBook.

---

## Philosophy

> Show only what matters. Nothing more.

TruckSim Widget was never designed to become another dashboard.

Its purpose is simple: give you confidence that TrucksBook is doing its job.

If everything works, the widget stays quiet. If something breaks, you will know immediately.

Nothing more. Nothing less.

---

## Support

TruckSim Widget is free to use. If it helps you keep your deliveries tracked and you would like to support development, you can do so here:

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

## Third-Party Assets

**Truck icon** by Magnific from Flaticon.  
Used under the Flaticon license with attribution.  
Source: [https://www.flaticon.com/free-icon/truck_2554978](https://www.flaticon.com/free-icon/truck_2554978)

---

## License

TruckSim Widget is licensed under the Mozilla Public License 2.0 (MPL-2.0).

You are free to use, modify, and distribute this software, including for commercial purposes, provided that modifications to MPL-covered files remain available under the same license when distributed.

The MPL-2.0 license does not grant rights to use the TruckSim Widget name, logo, branding, or other project trademarks.

For full license details, see [LICENSE](LICENSE).

---

<p align="center">
  Built with ❤️ for the Truck Simulator community
</p>

<p align="center">
  <a href="https://trucksim.uk">trucksim.uk</a>
</p>
