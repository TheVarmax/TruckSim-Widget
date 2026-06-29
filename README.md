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
  TruckSim Widget is a lightweight telemetry overlay for Euro Truck Simulator 2 and American Truck Simulator. It monitors TrucksBook and game telemetry in real time, provides configurable alerts, interface customization, Supporter subscriptions, and optional cloud synchronization for widget settings.
</p>

<p align="center">
  <strong>Free core features • Lightweight • ETS2 & ATS</strong>
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

Keep your cargo state, route, driven distance, and current delivery progress visible in one compact overlay.

### 🚛 Live telemetry

See the information that matters during a delivery without filling the screen with another dashboard.

### 🎨 Interface customization *(Supporters Exclusive)*

Configure the appearance of the widget to match your preferences.

- Four built-in themes: **Classic**, **Midnight**, **Carbon**, and **OLED Black**.
- Three card styles: **Standard**, **Rounded**, and **Compact**.
- Global accent colors and individual colors for each tile.
- Adjustable opacity and interface scale.
- Appearance preferences are retained when Supporter access is inactive.

### ☁️ Cloud Sync *(Supporters Exclusive)*

Synchronize supported widget settings between your computers.

- Automatic synchronization through TruckSim Cloud.
- Manual upload and download.
- Conflict resolution for settings changed on multiple devices.
- Cloud backup removal without changing local settings.

### 🔐 Supporter subscriptions

Supporter subscriptions provide access to premium functionality.

- Device-based activation.
- Device token authentication stored using Windows data protection.
- Subscription plan, activation details, and validity displayed in settings.
- Server-managed feature access.

### 🫥 Auto-hide

Reduces the widget's visibility when everything is operating normally.

- Smooth fade transitions.
- Expands on hover.
- Automatically becomes visible when attention is required.

### 🎛️ Flexible interface

Choose between **Full** and **Minimal** layouts, adjust opacity and scale, and pin the widget above the game when needed.

### ⚠️ Speed warnings

Set a speed warning threshold and receive a clear visual indication when you exceed it.

### 🌍 English and Ukrainian

The interface supports English and Ukrainian. City names can also be translated from English to Ukrainian; this feature is still in beta.

### ✨ Interface and settings

The app includes themed dialogs, launch and settings animations, layout transitions, animated dropdowns, and a tab-based settings interface.

### 🔄 Automatic updates

Stay up to date without manually checking for every new release. After a successful update, the widget shows an in-app confirmation window with useful links instead of forcing a browser page open.

---

## Quick start

The initial setup takes a minute and only needs to be completed once:

1. Download either the **Installer** or the **Portable ZIP** from the latest release.
2. Install the app or extract the portable archive to a permanent folder.
3. If you use the installer, select ETS2 and/or ATS during **Telemetry Plugin Setup** and choose the game folder when asked. The installer will create the `plugins` folder and copy `scs-telemetry.dll` automatically.
4. If you use the portable ZIP, copy `scs-telemetry.dll` from the included `plugin` folder into your game's `plugins` folder manually.
5. Start TrucksBook Client and sign in.
6. Launch ETS2 or ATS, then start TruckSim Widget.
7. Check that TrucksBook and telemetry are online. Drive.

---

## Installation

### Before you start

You need:

- Windows 10 or Windows 11
- Euro Truck Simulator 2 or American Truck Simulator installed through Steam
- [TrucksBook Client](https://trucksbook.eu/) installed and signed in, because it handles mileage tracking

### 1. Choose a release format

Open the [latest release](https://github.com/TheVarmax/TruckSim-Widget/releases/latest). TruckSim Widget is available in two formats:

| Format | Best for | What to do |
| --- | --- | --- |
| **Installer (recommended)** `TruckSimWidgetSetup-<version>.exe` | Most users | Run the setup, choose an installation folder, and use the built-in telemetry plugin setup page to configure ETS2 and/or ATS. The installer creates Windows shortcuts and an uninstall entry. |
| **Portable ZIP** `TruckSimWidget-<version>-portable.zip` | Users who prefer not to install the app | Extract the entire archive to a permanent folder and keep its files together. The telemetry plugin must be copied manually. |

> **Important:** Do not run the portable version from inside the ZIP archive. Extract it first.

### 2. Install the telemetry plugin

TruckSim Widget needs `scs-telemetry.dll` inside the game `plugins` folder.

#### Installer users

During setup, the installer shows a **Telemetry Plugin Setup** page. Select the games you want to configure:

- **Euro Truck Simulator 2**
- **American Truck Simulator**

If the installer detects a standard Steam installation, the game folder may be filled in automatically. Otherwise, choose the root game folder manually, for example:

```text
...\Steam\steamapps\common\Euro Truck Simulator 2
```

or:

```text
...\Steam\steamapps\common\American Truck Simulator
```

The installer will create this folder if needed:

```text
bin\win_x64\plugins
```

and copy `scs-telemetry.dll` into it.

#### Portable ZIP users

Open the extracted TruckSim Widget folder, then open the `plugin` folder inside it. You will find:

```text
scs-telemetry.dll
```

Copy that file into the `plugins` folder of every game you want to use with TruckSim Widget.

#### Euro Truck Simulator 2 plugin folder

```text
...\Steam\steamapps\common\Euro Truck Simulator 2\bin\win_x64\plugins
```

#### American Truck Simulator plugin folder

```text
...\Steam\steamapps\common\American Truck Simulator\bin\win_x64\plugins
```

If the `plugins` folder does not exist, create it manually.

> Copy **only** `scs-telemetry.dll` into the game folder. Leave the rest of TruckSim Widget together in its installation or portable folder.

### 3. Start everything in the right order

1. Start **TrucksBook Client** and make sure you are signed in.
2. Launch **ETS2** or **ATS**, then load your profile.
3. Start **TruckSim Widget** from the Start menu, desktop shortcut, or its portable folder.
4. Check the widget:
   - TrucksBook should show as online.
   - Telemetry should show as connected.
5. Start driving.

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

### City translation

City localization is still in beta. Some city names may be missing or inaccurate because ETS2 and ATS have a large amount of map and DLC content. Coverage is being improved over time.

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
  <a href="https://trucksim.maksym.uk">trucksim.maksym.uk</a>
</p>