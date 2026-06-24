<p align="center">
  <img src="assets/icon.png" width="100" />
</p>

<p align="center">
  <sub>
    🌐 Змінити мову →
    <a href="README.md">🇺🇸 EN</a>
  </sub>
</p>

<h1 align="center">TruckSim Widget</h1>

<p align="center">
  Легкий статусний і телеметричний оверлей для Euro Truck Simulator 2 та American Truck Simulator
</p>

<p align="center">
  <strong>Статус TrucksBook • Відстеження доставки • Розумні сповіщення • Auto-hide оверлей</strong>
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

## Що це таке?

TruckSim Widget — це легкий оверлей, який працює разом із Euro Truck Simulator 2 та American Truck Simulator.

Його головна мета — зробити статус TrucksBook помітним під час поїздки: чи запущений клієнт, чи підключена телеметрія та чи правильно відстежується доставка. Віджет також показує основну інформацію про рейс, не перетворюючи екран на ще одну панель приладів.

---

## Функції

### 📊 Статус TrucksBook і розумні сповіщення

Показує, чи TrucksBook онлайн і чи коректно записується рейс. Віджет виділяє важливі проблеми з клієнтом, телеметрією, записом, синхронізацією або завантаженням доставки.

### 📦 Відстеження доставки

Показує статус вантажу, маршрут, пройдену відстань і прогрес поточного рейсу.

### 🚛 Телеметрія в реальному часі

Швидкість, маршрут, стан гри та інформація про доставку в реальному часі з ETS2 і ATS.

### 🫥 Режим Auto-hide

Коли все працює нормально, віджет може переходити в ненав’язливий стан. Він розкривається при наведенні курсора та одразу стає видимим знову, якщо виявлено попередження або помилку.

Auto-hide також коректно працює, коли гра закрита: оверлей не заважає, якщо немає проблем, які потребують уваги.

### 🎛️ Гнучкий інтерфейс

Обирайте між повним і мінімальним режимами UI, налаштовуйте прозорість і масштаб, а також за потреби закріплюйте віджет поверх гри.

### ⚠️ Попередження про швидкість

Налаштовувані попередження про перевищення швидкості з візуальними індикаторами.

### 🌍 Локалізація

Підтримка англійської та української мов інтерфейсу, зокрема переклад назв міст з англійської на українську в бета-режимі.

### ✨ Покращений UI

Повністю стилізовані діалоги, плавні анімації віджета та вікна налаштувань, оновлені макети й анімовані випадаючі списки в налаштуваннях.

### 🔄 Покращене оновлення

Вбудований оновлювач підтримує застосунок актуальним. Після успішного оновлення TruckSim Widget тепер показує внутрішнє вікно успіху зі швидкими посиланнями замість примусового відкриття сторінки в браузері.

---

## Встановлення

### Вимоги

* TrucksBook Client (потрібен для відстеження кілометражу)

### Налаштування плагіна

Скопіюй `scs-telemetry.dll` у папку гри:

#### Euro Truck Simulator 2

```
...\Steam\steamapps\common\Euro Truck Simulator 2\bin\win_x64\plugins
```

#### American Truck Simulator

```
...\Steam\steamapps\common\American Truck Simulator\bin\win_x64\plugins
```

Якщо папки `plugins` не існує — створи її вручну.

---

## Запуск

1. Запусти ETS2 або ATS
2. Запусти TruckSim Widget
3. Переконайся, що TrucksBook онлайн, а телеметрія підключена
4. Катай

---

## Примітки

Локалізація міст усе ще перебуває на стадії бета-тестування.

Деякі назви міст можуть бути відсутні або неточні через велику кількість DLC у ETS2 та ATS. Покриття активно покращується.

---

## Філософія

> Показуй тільки те, що важливо. Нічого зайвого.

TruckSim Widget створений, щоб не відволікати, коли все працює, і бути корисним саме тоді, коли щось потребує уваги.

---

## Підтримка

Якщо вам подобається TruckSim Widget і ви хочете підтримати його розвиток, ви можете зробити це тут:

<p align="center">
  <a href="https://buymeacoffee.com/thevarmax">
    <img src="https://img.shields.io/badge/Buy_Me_A_Coffee-Підтримати-FFDD00?style=for-the-badge&logo=buymeacoffee&logoColor=white">
  </a>
</p>

<p align="center">
  <a href="https://send.monobank.ua/8Q2FKkJr3B">
    <img src="https://img.shields.io/badge/Monobank-Донат-ff5f5f?style=for-the-badge">
  </a>
</p>

---

## Ліцензія

TruckSim Widget поширюється за умовами ліцензії Mozilla Public License 2.0 (MPL-2.0).

Ви можете використовувати, змінювати та поширювати це програмне забезпечення, зокрема й у комерційних цілях, за умови, що зміни до файлів, які підпадають під MPL, залишаються доступними під тією ж ліцензією під час розповсюдження.

Ліцензія MPL-2.0 не надає прав на використання назви TruckSim Widget, логотипа, брендингу або інших торговельних марок проєкту.

Цей проєкт не пов’язаний, не схвалений і не афілійований із SCS Software, Euro Truck Simulator 2, American Truck Simulator або TrucksBook.

Повний текст ліцензії доступний у файлі [LICENSE](LICENSE).

---

<p align="center">
  Зроблено з ❤️ для спільноти Truck Simulator
</p>

<p align="center">
  https://trucksim.maksym.uk
</p>