# Neon Horizon — Живые обои для рабочего стола Windows

Нативные анимированные обои для рабочего стола Windows в эстетике **Synthwave / Outrun 84** и аналогового **CRT / VHS-шейдера**, вдохновлённые живой новой вкладкой из проекта [retro-browser](https://github.com/Roma-Skull/retro-browser).

Работает полностью на **Direct3D 11 + DirectComposition + HLSL** напрямую на видеокарте — без браузерного движка, без сторонних программ вроде Wallpaper Engine или Lively Wallpaper и без внешних зависимостей.

---

## Скриншоты

| Neon Horizon (`neon_horizon`) | Miami Sunset Drive (`miami_drive`) |
| :---: | :---: |
| ![Neon Horizon](img/horizon.png) | ![Miami Sunset Drive](img/car.png) |

---

## Возможности

- **Выбор из нескольких процедурных сцен (через панель настроек или меню в трее):**
  1. **Neon Horizon (`neon_horizon`):** сумеречное синтвейв-небо с авророй, мерцающие звёзды с 4-лучевыми бликами, закатное Outrun-солнце с дышащей короной и сползающими полосами-срезами, каркасные low-poly горы, бесконечный полёт над перспективой неоновой сетки и яркая линия горизонта.
  2. **Miami Sunset Drive (`miami_drive`):** ретро-спорткар, едущий в закат по 4-полосному проспекту Майами, встречный трафик (легковушки и фуры), неоновый скайлайн Майами в стиле Art Deco, пролетающие за пределы экрана пальмы и уличные фонари.
- **Проход постобработки ЭЛТ / VHS (`shaders/crt_vhs.ps.hlsl`):**
  - CRT-бочкообразный изгиб экрана и скруглённая стеклянная рамка с внутренней тенью и фиолетовой подсветкой кромки;
  - чересстрочный сканлайн и 3-субпиксельная RGB-решётка с привязкой к физическому DPI монитора (без муара на `100%`, `125%`, `150%`, `200%`);
  - оптическое свечение люминофора (**Bloom**);
  - **VHS-эффекты:** хроматический RGB-сдвиг, дрожание горизонтальной синхронизации, плёночное зерно и бегущая полоса развёртки;
  - виньетка и стеклянный блик.
- **Поддержка Windows 11 24H2+ (Build 26100) и Windows 10 / 11 (Pre-24H2):**
  - автоматически определяет режим **Raised Desktop** (`Progman` + `DirectComposition` + Z-порядок под `SHELLDLL_DefView`) или классический `WorkerW`, чтобы обои отображались строго **за значками рабочего стола** и не мешали кликам.
- **Мультимониторность, вертикальные экраны и Per-Monitor DPI:** отдельный SwapChain и изотропный расчёт пропорций и сканлайнов для горизонтальных и портретных (`9:16`, `10:16` и др.) мониторов.
- **Экономия ресурсов:** автоматическая пауза при запуске полноэкранных игр/приложений или блокировке экрана, ограничитель FPS (по умолчанию `30 FPS`; доступны `30`, `60`, `120`, `Monitor VSync`), управление из системного трея.
- **4 готовых пресета:** `Soft CRT`, `Outrun 84`, `VHS Tape`, `Minimal` + тонкие ползунки, скорость движения, автозапуск с Windows.

---

## Быстрый запуск

1. Скачайте готовый `NeonHorizonWallpaper.exe` (или ZIP-архив) со страницы [Releases](https://github.com/Roma-Skull/synthwave-wallpapper/releases), либо соберите локально через `build.ps1`.
2. Запустите `NeonHorizonWallpaper.exe` (иконка появится в системном трее рядом с часами).
3. Двойной клик по иконке в трее (или `Configure CRT / VHS & Scene...`) открывает панель настроек.
4. Полезные параметры запуска:
   - `NeonHorizonWallpaper.exe --settings` — сразу открыть окно настроек;
   - `NeonHorizonWallpaper.exe --preview` — дополнительно открыть отдельное окно предпросмотра;
   - `NeonHorizonWallpaper.exe --preview-only` — только окно предпросмотра (без встраивания в рабочий стол);
   - `NeonHorizonWallpaper.exe --exit` — корректно завершить работающий экземпляр;
   - `NeonHorizonWallpaper.exe --test-render frame.png [miami_drive|neon_horizon] [width height]` — отрендерить один кадр выбранной сцены в PNG через GPU для проверки.

Настройки сохраняются в `AppData\Roaming\NeonHorizonWallpaper\settings.json` (`%APPDATA%\NeonHorizonWallpaper\settings.json`), а локальный диагностический лог пишется в `AppData\Local\NeonHorizonWallpaper\neon_horizon.log` (`%LOCALAPPDATA%\NeonHorizonWallpaper\neon_horizon.log`).

---

## Пересборка и упаковка релиза из исходников

Внешние SDK не требуются — используется встроенный компилятор .NET Framework 4 (`csc.exe`):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File package.ps1 -Version 1.0.0
```

Структура проекта:
- [`src/`](src/) — Win32/DesktopHost (24H2 + WorkerW), D3D11/DXGI/DirectComposition рендерер, окно настроек и трей;
- [`shaders/`](shaders/) — HLSL-шейдеры (`fullscreen.vs.hlsl`, `synthwave_scene.ps.hlsl`, `miami_drive.ps.hlsl`, `crt_vhs.ps.hlsl`);
- [`img/`](img/) — скриншоты сцен на рабочем столе;
- [`app.manifest`](app.manifest) — совместимость Windows 8/10/11 и `PerMonitorV2` DPI;
- [`build.ps1`](build.ps1) — предкомпилятор шейдеров (DXBC), генератор `.ico` и сборщик `NeonHorizonWallpaper.exe`;
- [`package.ps1`](package.ps1) — сборщик дистрибутива в `dist/`.
