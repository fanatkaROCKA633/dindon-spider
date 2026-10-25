# SpiderWidget — задачи

Команды (из `SPEC.md`):
- Build: `dotnet build SpiderWidget.sln -c Debug`
- Test: `dotnet test SpiderWidget.sln`
- Run: `dotnet run --project src/SpiderWidget`

---

## Task 0: Установить .NET 8 SDK

**Description:** На машине нет `dotnet`. Пользователь ставит .NET 8 SDK
(x64) с официального сайта Microsoft или `winget install Microsoft.DotNet.SDK.8`.

**Acceptance criteria:**
- [x] `dotnet --list-sdks` показывает `8.0.x`

**Verification:**
- [ ] Manual check: команда выше в новом терминале

**Dependencies:** None
**Files likely touched:** —
**Estimated scope:** XS

---

## Task 1: Каркас решения и окно с головой

**Description:** Создать `SpiderWidget.sln`, проекты `src/SpiderWidget`
(WinForms, `net8.0-windows`, nullable, warnings as errors, PerMonitorV2) и
`tests/SpiderWidget.Tests` (xUnit). Окно `SpiderForm`: layered, topmost,
toolwindow, noactivate; рисует спрайт головы 16×16 ×3 в центре экрана;
прозрачные пиксели пропускают клики. Трей с «Пауза»/«Выход». Один экземпляр
через именованный `Mutex`.

**Acceptance criteria:**
- [x] Голова видна поверх всех окон (скриншот); в Alt-Tab и на панели задач её нет — **глазами не проверено**, задано стилями `WS_EX_TOOLWINDOW` + `ShowInTaskbar=false`
- [x] Клик по прозрачной части окна попадает в окно под ним (`WindowFromPoint`)
- [x] Повторный запуск не создаёт второго паука; «Выход» закрывает процесс — **пункт меню руками не нажимался**

Заметки: click-through сделан альфой layered-окна, а не `HTTRANSPARENT`
(тот не пропускает клики в окна других процессов). «Пауза» в трее
перенесена в Task 2 — до появления движения ей нечего останавливать.

**Verification:**
- [ ] Build succeeds: `dotnet build SpiderWidget.sln -c Debug`
- [ ] Tests pass: `dotnet test SpiderWidget.sln` (пустой проект собирается)
- [ ] Manual check: запустить, покликать рядом с головой, Alt-Tab, второй запуск

**Dependencies:** Task 0
**Files likely touched:**
- `SpiderWidget.sln`, `src/SpiderWidget/SpiderWidget.csproj`, `Program.cs`
- `src/SpiderWidget/Overlay/SpiderForm.cs`, `Overlay/Sprites.cs`
- `src/SpiderWidget/Native/User32.cs`
- `tests/SpiderWidget.Tests/SpiderWidget.Tests.csproj`
**Estimated scope:** M

---

## Task 2: Гравитация — голова падает на низ экрана

**Description:** `Physics` (чистый класс): тело с позицией и скоростью,
гравитация, фиксированный шаг 1/60 с, приземление на горизонтальную кромку.
Пока единственная опора — низ рабочей области монитора. `Tuning.cs` с
константами. Таймер 60 Гц в `SpiderForm` двигает окно. Пункт «Пауза» в трее
останавливает таймер.

**Acceptance criteria:**
- [x] Тело, отпущенное над опорой, через конечное время стоит на ней (y = кромка, скорость 0)
- [x] Тело не проваливается сквозь опору при большой скорости (тест на 3000 px/с)
- [x] При запуске голова падает и останавливается на панели задач/низу экрана (скриншот)

Заметка: «Пауза» в трее подключена, но руками не нажималась.

**Verification:**
- [ ] Tests pass: `dotnet test SpiderWidget.sln --filter Physics`
- [ ] Manual check: запуск — голова падает вниз

**Dependencies:** Task 1
**Files likely touched:**
- `src/SpiderWidget/Spider/Physics.cs`, `Spider/Tuning.cs`, `Targets/Surface.cs`
- `src/SpiderWidget/Overlay/SpiderForm.cs`
- `tests/SpiderWidget.Tests/PhysicsTests.cs`
**Estimated scope:** M

---

## Task 3: Шесть ног с IK, рисуются линиями

**Description:** `Leg` и `Legs`: 6 ног (3 слева, 3 справа), 2 звена, аналитическая
IK (колено наружу/вверх). Пока паук стоит — ступни на кромке опоры.
Отрисовка ног пиксельными линиями (Брезенхэм) в логических пикселях, затем
×3. Окно растёт до ~96×96, чтобы ноги помещались.

**Acceptance criteria:**
- [x] IK: при достижимой цели конец ноги в цели ±0,5 px; при недостижимой — нога вытянута в её сторону (тесты)
- [x] В покое все 6 ступней лежат на кромке ±1 px (тест)
- [x] На экране видна голова на 6 ногах, без сглаживания (скриншот)

Заметка: окно 40×32 логических пикселя (120×96 px), Брезенхэм — в `Sprites.DrawLine`.
Перерисовка только при сдвиге окна.

**Verification:**
- [ ] Tests pass: `dotnet test SpiderWidget.sln --filter Legs`
- [ ] Manual check: скриншот паука в покое

**Dependencies:** Task 2
**Files likely touched:**
- `src/SpiderWidget/Spider/Legs.cs`
- `src/SpiderWidget/Overlay/SpiderForm.cs`, `Overlay/Sprites.cs`
- `tests/SpiderWidget.Tests/LegsTests.cs`
**Estimated scope:** M

---

## Task 4: Ходьба по полу трипод-походкой

**Description:** `SpiderBrain` с состояниями `Idle`/`Walk`: случайные паузы и
прогулки влево-вправо в пределах опоры (seed через `Random`). Ступня шагает,
когда отстала дальше порога; ноги делятся на две тройки, одновременно
шагает только одна. Голова слегка покачивается.

**Acceptance criteria:**
- [x] За время ходьбы в шаге никогда нет ног из обеих троек одновременно (тест)
- [x] Паук не уходит за край опоры — разворачивается (тест)
- [x] Визуально ходьба выглядит как перебирание ног, а не скольжение (серия кадров; оценку «похоже ли» — на Checkpoint A)

Заметки: первая версия отрисовки ела ~16 % ядра; теперь холст — одна
DIB-секция на всё время работы, окно двигается тем же `UpdateLayeredWindow`,
отрисовка ~30 кадров/с (физика по-прежнему 1/60 с). Замер: 1,1 % ядра, 46 МБ.

**Verification:**
- [ ] Tests pass: `dotnet test SpiderWidget.sln`
- [ ] Manual check: понаблюдать минуту

**Dependencies:** Task 3
**Files likely touched:**
- `src/SpiderWidget/Spider/SpiderBrain.cs`, `Spider/Legs.cs`
- `src/SpiderWidget/Overlay/SpiderForm.cs`
- `tests/SpiderWidget.Tests/SpiderBrainTests.cs`
**Estimated scope:** M

---

## Checkpoint A: паук ходит по низу экрана
- [x] Все тесты зелёные, сборка без предупреждений (21/21)
- [x] CPU в простое ≤ 2 % одного ядра (замер скриптом: 1,1 %)
- [x] Показать пользователю, согласовать вид и походку (голову меняли по просьбе пользователя; итог — пиксельное фото, «запустил, всё ок»)

---

## Task 5: Опоры — окна и панель задач

**Description:** `Targets/Windows.cs`: `EnumWindows` → видимые, не свёрнутые,
не cloaked; рамка через `DWMWA_EXTENDED_FRAME_BOUNDS`; верхняя кромка с
вычетом частей, перекрытых окнами выше по Z. Панели задач на всех
мониторах. Опрос 2 Гц. Паук падает на ближайшую кромку под собой и ходит по ней;
если окно уехало из-под ног — падает.

**Acceptance criteria:**
- [x] Чистая функция вычитания перекрытий даёт правильные отрезки (тесты на 5 случаев)
- [x] Паук стоит на верхней кромке окна и уезжает вместе с ним при перетаскивании (≤ 0,5 с)
- [x] Свернул окно под пауком — паук падает

Заметки: Win32-часть в `Targets/WindowSurfaces.cs` (не `Windows.cs` — чтобы
класс не путался с пространством имён `System.Windows`). Перенос вместе с
окном — `Physics.Carry` по handle окна. Проверено скриптом на тестовом окне:
приземление, сдвиг окна, сворачивание. При опросе 2 Гц паук едет за
перетаскиваемым окном рывками раз в 0,5 с.

**Verification:**
- [ ] Tests pass: `dotnet test SpiderWidget.sln --filter Surfaces`
- [ ] Manual check: двигать/сворачивать окно под пауком

**Dependencies:** Task 4
**Files likely touched:**
- `src/SpiderWidget/Targets/Windows.cs`, `Targets/Surface.cs`
- `src/SpiderWidget/Native/Dwmapi.cs`, `Native/User32.cs`
- `src/SpiderWidget/Overlay/SpiderForm.cs`
- `tests/SpiderWidget.Tests/SurfacesTests.cs`
**Estimated scope:** M

---

## Task 6: Опоры — иконки рабочего стола

**Description:** `Targets/DesktopIcons.cs`: `IShellWindows.FindWindowSW(SWC_DESKTOP)` →
`IFolderView` → позиции и размер иконок, перевод в экранные координаты.
Иконки дают кромки (верх) и бока для лазания. Иконки, перекрытые окнами,
не считаются опорами. Если иконки скрыты или Explorer перезапущен — пусто,
без исключений наружу.

**Acceptance criteria:**
- [x] Прямоугольники иконок совпадают с иконками на экране (отладочная обводка по флагу `--debug-surfaces`)
- [x] Перезапуск Explorer не роняет виджет, иконки снова находятся
- [x] Паук может стоять на иконке — подтвердил пользователь после Task 7 («посмотрел на иконках, всё ок»)

Заметки: позиции — `IFolderView2` (COM рабочего стола), размер — `GetViewModeAndIconSize` × DPI;
X позиции = левый край картинки, Y — верх ячейки + 6 лог. px (подобрано по скриншоту на 125 %).
Иконки опрашиваются раз в 3 с (с опросом 2 Гц было 3,2 % CPU). Голова теперь рисуется
один раз в готовый битмап — общий CPU 1,3 %. Добавлен `Overlay/DebugSurfacesForm.cs` для флага.
Риск: COM-вызовы в Explorer идут из UI-потока — если Explorer завис, паук замрёт вместе с ним.

**Verification:**
- [ ] Build succeeds
- [ ] Manual check: `dotnet run --project src/SpiderWidget -- --debug-surfaces`, перезапуск Explorer

**Dependencies:** Task 5
**Files likely touched:**
- `src/SpiderWidget/Targets/DesktopIcons.cs`
- `src/SpiderWidget/Native/Shell32.cs`
- `src/SpiderWidget/Overlay/SpiderForm.cs`
**Estimated scope:** M

---

## Task 7: Лазание, висение и срыв

**Description:** Состояния `Climb`, `Hang`, `Slip`, `Fall`, `Land`. Паук
выбирает соседнюю опору в радиусе, забирается по боковой грани
иконки/окна, повисает; в `Hang` на иконке каждую секунду шанс
`SlipChancePerSecond` (3 %) сорваться: анимация соскальзывания → падение →
приземление на опору ниже.

**Acceptance criteria:**
- [x] С фиксированным seed срыв с иконки заканчивается приземлением на опору под ней (тест)
- [x] В `Hang` ступни на кромке ±1 px (тест)
- [x] Средняя частота срыва в симуляции 10 000 с — 3 % ± 0,5 % в секунду (тест)

Заметки: новые состояния `Approach → Climb → Mantle → Hang → Slip → Fall → Land`;
ноги получили режимы `Grip` (обхватывает иконку), `Wall` (цепляется за грань), `Air`.
Подъём — на иконки и кромки окон выше на 20–400 px в радиусе 900 px (до грани идёт пешком),
по ближней грани; после паузы идёт лезть с шансом 60 %. Было 300 px и 40 % — по отзыву
пользователя на иконки лез слишком редко (иконки у края экрана, паук гуляет в центре).
Вживую проверен подъём с пола на тестовое окно (скрипт); висение и срыв на настоящих
иконках вживую **не смотрели** — на Checkpoint B.

**Verification:**
- [ ] Tests pass: `dotnet test SpiderWidget.sln`
- [ ] Manual check: понаблюдать 2–3 минуты на столе с иконками

**Dependencies:** Task 6
**Files likely touched:**
- `src/SpiderWidget/Spider/SpiderBrain.cs`, `Spider/Legs.cs`, `Spider/Tuning.cs`
- `tests/SpiderWidget.Tests/SpiderBrainTests.cs`
**Estimated scope:** M

---

## Checkpoint B: паук живёт на рабочем столе
- [x] Все тесты зелёные
- [ ] Два монитора (если есть) и масштаб 100 % / 150 % — опоры совпадают — **не проверено**: один монитор, 125 %
- [x] CPU ≤ 2 %, память ≤ 80 МБ (1,3 %, 48 МБ)
- [x] Показать пользователю

---

## Task 8: Захват и бросок

**Description:** Непрозрачные пиксели → `HTCLIENT`. ЛКМ на пауке →
`Grabbed`: паук висит под курсором, дрыгает ногами. `Grab` (чистый класс)
хранит историю позиций курсора за ~100 мс и даёт скорость броска с
ограничением сверху. Отпустил → `Thrown` → `Fall` → `Land`. Клик без
движения — падение с места.

**Acceptance criteria:**
- [x] Скорость броска по истории позиций считается верно и ограничена (тесты)
- [x] Брошенный вправо паук приземляется правее точки броска (тест)
- [x] Вручную: схватил, швырнул — летит по дуге, ноги поджаты (скрипт с синтетической мышью, 4 из 6 бросков; 2 первых не схватили — вероятно, мешал живой ввод, точно не выяснено)

Заметки: `HTCLIENT` отдельно не понадобился — непрозрачные пиксели layered-окна и так
ловят клик. Добавлены границы мира (`Physics.World`): края экрана — стены, провалившегося
под несуществующий пол паука роняем сверху заново. Ноги: `Flail` в руке, `Air` поджаты;
при переходе в `Ground` ступни сразу встают на кромку.

**Verification:**
- [ ] Tests pass: `dotnet test SpiderWidget.sln --filter Grab`
- [ ] Manual check: несколько бросков в разные стороны

**Dependencies:** Task 7
**Files likely touched:**
- `src/SpiderWidget/Overlay/Grab.cs`, `Overlay/SpiderForm.cs`
- `src/SpiderWidget/Spider/SpiderBrain.cs`
- `tests/SpiderWidget.Tests/GrabTests.cs`
**Estimated scope:** M

---

## Task 9: Охота за курсором

**Description:** `GetCursorPos` в такте. Состояния `Hunt → Jump → Pounce`:
курсор ближе `HuntRadius` (300 px), паук на опоре, шанс
`HuntChancePerSecond` (5 %), кулдаун `HuntCooldown` (20 с). Подкрадывается,
приседает, прыгает по баллистической дуге в точку курсора на момент
прыжка, «хватает», падает. Не во время `Grabbed` и паузы.

**Acceptance criteria:**
- [x] Баллистика: без опор на пути паук попадает в цель ±2 px (тест, 4 направления)
- [x] Охота не начинается раньше кулдауна и дальше радиуса (тест)
- [x] Вручную: паук иногда прыгает на курсор, мышь работает как обычно — подтвердил пользователь («охота ок»)

Заметки: состояния `Hunt → Crouch → Jump → Pounce → Fall/Land`; скорость прыжка считается
под ту же схему интегрирования, что в `Physics.Step`, поэтому попадание точное. Цель — курсор
+ 40 px вниз (курсор у головы). Охота возможна с пола, окна и с иконки.
Риск: в момент «захвата» (~0,3 с) непрозрачные пиксели паука оказываются под курсором — клик
в это время схватит паука, а не уйдёт в окно под ним.

**Verification:**
- [ ] Tests pass: `dotnet test SpiderWidget.sln`
- [ ] Manual check: держать курсор рядом с пауком пару минут

**Dependencies:** Task 8
**Files likely touched:**
- `src/SpiderWidget/Spider/SpiderBrain.cs`, `Spider/Physics.cs`, `Spider/Tuning.cs`
- `src/SpiderWidget/Overlay/SpiderForm.cs`
- `tests/SpiderWidget.Tests/SpiderBrainTests.cs`
**Estimated scope:** M

---

## Checkpoint C: всё поведение готово
- [x] Все тесты зелёные
- [x] Показать пользователю, при необходимости подстроить `Tuning.cs` (подняли радиус и шанс подъёма; добавили «поверх всех окон»)

---

## Task 10: Фильтр путей

**Description:** `RecycleBin.Check(path) → Allowed | Rejected(reason)`, чистая
функция поверх абстракции «диски/папки окружения», чтобы тестировать без
реальной системы. Отказ: не абсолютный путь, UNC/сеть, не фиксированный
диск, корень диска, `%WINDIR%`, `%ProgramFiles%`, `%ProgramFiles(x86)%`,
профиль пользователя, сам рабочий стол, а также любой путь, внутри которого
лежит одна из этих папок. Пути нормализуются (`..`, регистр, хвостовой `\`).

**Acceptance criteria:**
- [x] Все примеры из спека (`C:\`, `C:\Windows`, `C:\Program Files`, профиль, Desktop, `\\server\share\x.txt`, флешка) — отказ (тесты)
- [x] Обычный файл и обычная папка на рабочем столе — разрешены (тесты)
- [x] `C:\Users\me\Desktop\..` и `c:\windows\` — отказ (тесты на нормализацию)

Заметки: строже спека — отказ и для всего *внутри* Windows / Program Files, для общего
рабочего стола (`C:\Users\Public\Desktop`), сетевых дисков с буквой (`Z:`), путей `\\?\` и `\\.\`,
коротких имён 8.3 (`C:\PROGRA~1` раскрывается через `GetLongPathName`). Тесты: 38 случаев
на выдуманной машине + один на настоящих правилах этой системы.
Не покрыто: ссылки/junction на рабочем столе — фильтр смотрит на путь самой ссылки, не на цель;
корзина удаляет ссылку, а не то, на что она указывает (проверить вживую в Task 11).

**Verification:**
- [ ] Tests pass: `dotnet test SpiderWidget.sln --filter RecycleBin`

**Dependencies:** Task 1
**Files likely touched:**
- `src/SpiderWidget/Recycle/RecycleBin.cs`
- `tests/SpiderWidget.Tests/RecycleBinTests.cs`
**Estimated scope:** S

---

## Task 11: Drop на паука и отправка в корзину

**Description:** `AllowDrop` на окне паука, `DataFormats.FileDrop`. Пока над
пауком тащат — рот открыт. На drop: каждый путь через `Check`; разрешённые —
одним вызовом `SHFileOperation(FO_DELETE)` с
`FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_WANTNUKEWARNING | FOF_SILENT`;
отказ — паук мотает головой. Состояние `Eat`. Лог в
`%LOCALAPPDATA%\SpiderWidget\log.txt`.

**Acceptance criteria:**
- [x] Файл и папка из `%TEMP%`, брошенные на паука, оказываются в корзине (видны в корзине с исходным расположением; восстановление руками не проверялось)
- [x] Ярлык уходит в корзину, программа, на которую он указывает, на месте
- [x] Отклонённый путь не удаляется, в логе запись с причиной

Заметки: живая проверка — настоящий OLE drag-and-drop из тестового окна скрипта (синтетическая
мышь), не из Проводника. Эффект броска — Copy, а не Move, чтобы источник сам ничего не удалил.
В корзину уходит нормализованный путь — ровно тот, что проверил фильтр. Удаление запускается
после ответа на бросок (`BeginInvoke`) и идёт в UI-потоке: большая папка подвесит паука, пока едет.
Junction на папку: в корзину ушла сама ссылка, файл в целевой папке остался (проверено).
«Ест» — не отдельное состояние, а слой лица поверх любого (`Gape`/`Fed`, `Hold.MouthOpen/HeadShift`).

**Verification:**
- [ ] Tests pass: `dotnet test SpiderWidget.sln`
- [ ] Manual check: drop файла, папки, ярлыка (тестовые, из `%TEMP%`), восстановление из корзины

**Dependencies:** Task 9, Task 10
**Files likely touched:**
- `src/SpiderWidget/Recycle/RecycleBin.cs`
- `src/SpiderWidget/Native/Shell32.cs`
- `src/SpiderWidget/Overlay/SpiderForm.cs`
- `src/SpiderWidget/Spider/SpiderBrain.cs`
**Estimated scope:** M

---

## Checkpoint D: v1 готова
- [x] Все Success Criteria из `SPEC.md` выполнены — кроме ручного нажатия «Выход»/«Пауза» в трее и Alt-Tab глазами (см. отчёт)
- [x] `dotnet format SpiderWidget.sln --verify-no-changes` чистый
- [x] `dotnet publish` даёт один `.exe`, он запускается (229 КБ, нужен .NET 8 Desktop Runtime; CPU 1,5–1,7 % за 60 с, 53 МБ)
- [ ] Обзор с пользователем
