# Инструкция по проверке MainShell в WpfPilot

## 0. Важное замечание до начала

В присланном коде **для роли «Директор» зарегистрировано только 4 ViewModel** (`Client/App.xaml.cs`):

```
OverviewViewModel          → OverviewView
SmetaApprovalViewModel     → SmetaApprovalView
ReportsApprovalViewModel   → ReportsApprovalView
DecisionsViewModel         → DecisionsView
```

Файл `MainShellViewModel.cs` в снимок не попал, поэтому точный состав меню неизвестен. Если после «фикса бага MainShell» Вы добавили ещё 2 экрана, они появятся в snapshot — тогда проверяйте по факту. Если нет — счёт 6 против 4 будет расхождением, которое нужно зафиксировать в отчёте как «цель не достигнута: у Директора 4 экрана вместо 6».

По остальным ролям (из `App.xaml.cs`):

| Роль | ViewModel-ы (экраны меню) | Кол-во |
|---|---|---|
| Администратор | Users, Departments, Projects, Catalog, Logs, Backup, Settings | **7** |
| Директор | Overview, SmetaApproval, ReportsApproval, Decisions | **4** (ожидалось 6) |
| Главный экономист | Smetas, Limits, Analysis, RequestsReview, Reports | **5** |
| Руководитель отдела | HeadSmetas, HeadExpenses, HeadRequests, HeadReports | **4** |
| Бухгалтер | Expenses, Periods | **2** |

Также нужны **учётные данные всех 5 пользователей** (`Server/Data/DbSeeder.cs` в снимке нет). По умолчанию в `LoginViewModel` уже стоит `admin / Admin123!`. Остальные учётки заведите в тестовый конфиг (например, `tests/credentials.json`) до начала прогона.

---

## 1. Базовые приёмы WpfPilot (общие для всех шагов)

Имена MCP-инструментов и параметров могут немного отличаться в Вашей сборке WpfPilot; ниже — логическая схема, которую нужно один-в-один перенести на фактические имена.

### 1.1. Обнаружение процесса и окна

```
wpfpilot.list_processes()                    → найти ProcessName = "Client"
wpfpilot.attach(pid=<pid>)  ИЛИ  wpfpilot.attach(name="Client")
wpfpilot.list_windows(pid=<pid>)             → список окон с Handle/Title/IsModal
```

Клиент открывает сначала окно входа (`MainWindow`), после логина — окно `MainShell`. Ищем по заголовку (см. `MainWindow.xaml`, `MainShell.xaml`).

### 1.2. Snapshot и поиск элементов

```
snap = wpfpilot.snapshot(window=<handle>)
```

Каждый узел в ответе содержит: `id` (**меняется между вызовами — не сохраняйте!**), `ControlType`, `Name`, `AutomationId`, `IsEnabled`, `IsOffscreen`, `bounds`, `supported_patterns` (Invoke, Value, SelectionItem, ExpandCollapse…).

Селектор — всегда словарь по стабильным полям:

```
{A: "AutomationId"}                    # самый надёжный
{Name: "Войти", ControlType: "Button"} # если AutomationId нет
{Name__contains: "Обзор"}              # для пунктов меню
{ControlType: "DataGrid", Index: 0}    # если на экране их несколько
```

Ищите через `wpfpilot.find(snap, selector)` или `wpfpilot.find_one(snap, selector)`.

### 1.3. Действия

```
wpfpilot.act(window=H, selector={...}, verb="invoke")               # кнопки, ListItem, MenuItem
wpfpilot.act(window=H, selector={...}, verb="click")                # если Invoke не поддержан
wpfpilot.act(window=H, selector={...}, verb="set_value", value="…") # TextBox, ComboBox.Editable
wpfpilot.act(window=H, selector={...}, verb="select", value="…")    # ComboBox, ListBox, TabControl
wpfpilot.act(window=H, selector={...}, verb="focus")                # PasswordBox: сначала focus
wpfpilot.act(window=H, selector={...}, verb="send_keys", value="P@ssw0rd") # затем send_keys
```

### 1.4. Ожидания и скриншоты

```
wpfpilot.wait_for(window=H, selector={...}, timeout_ms=10000, poll_ms=200)
wpfpilot.screenshot(window=H, path="reports/<run>/<role>/<step>.png")
wpfpilot.screenshot_element(window=H, selector={...}, path="…")
```

### 1.5. Универсальные проверки «нет падения»

После **каждого** действия выполняйте:

1. **Поиск модального окна необработанного исключения** (`Client/App.xaml.cs`):

   ```
   wpfpilot.list_windows(pid=P)  →
       если найдено окно с Title ∈ {"Неперехваченное исключение", "Критическая ошибка"}:
           snap_err = wpfpilot.snapshot(window=err_handle)
           текст = find_one(snap_err, {ControlType: "TextBlock", MaxDepth:…}) | {ControlType: "Edit"}
           wpfpilot.screenshot(window=err_handle, path="…/exception.png")
           wpfpilot.act(window=err_handle, selector={Name:"OK", ControlType:"Button"}, verb="invoke")
           → FAIL, в отчёт: полный текст исключения
   ```

2. **Поиск MessageBox-ошибки бизнес-логики** (Title "Ошибка", "Ошибка загрузки…") — то же самое, но отдельно логируется как «валидационная ошибка» (не обязательно FAIL, но фиксируем текст).

3. **Живость главного окна** — `MainShell` остаётся в `list_windows`; отсутствие → FAIL «окно закрылось».

### 1.6. Каркас проверки одного экрана меню

```
def check_menu_item(role, menu_name, expected_headers, expected_grid=True):
    snap  = snapshot(main_shell)
    item  = find_one(snap, {Name__contains: menu_name, ControlType__in:["ListItem","Button","TreeItem"]})
    act(main_shell, item, "invoke")
    sleep(300); wait_for(main_shell, {ControlType:"ContentPresenter", HasContent:True}, timeout=10s)

    if exception_dialog():                 return FAIL("exception", text, screenshot)
    snap2 = snapshot(main_shell)
    for h in expected_headers:
        assert find_one(snap2, {ControlType:"TextBlock", Name__contains:h}) → иначе FAIL
    if expected_grid:
        assert find_one(snap2, {ControlType:"DataGrid"}) → иначе FAIL
    if message_box("Ошибка…"):             log warning + text
    screenshot(main_shell, f"{role}/{menu_name}.png")
    return OK
```

---

## 2. Общий сценарий входа (применяется 5 раз, по разу на роль)

### Шаг 2.1. Attach + окно входа

```
wpfpilot.attach(name="Client") → pid
wpfpilot.list_windows(pid)  → должно быть окно MainWindow (Title из MainWindow.xaml,
                               вероятно «Управление бюджетом НИИ — Вход» или просто MainWindow)
```

Если приложение ещё не запущено — запустите `Client.exe` вручную, дождитесь окна входа, затем attach.

### Шаг 2.2. Snapshot окна входа и заполнение

Из `MainWindow.xaml.cs` известно имя `PasswordInput` (PasswordBox). Имя поля логина в XAML не видно — берём из snapshot (обычно `AutomationId="LoginBox"` или `TextBox` с привязкой `Login`). Кнопка входа — `Button` с Name `Войти`.

```
snap = snapshot(main_window)
txt  = find_one(snap, {ControlType:"Edit", AutomationId:"LoginBox"})       # или {ControlType:"Edit", Index:0}
pwd  = find_one(snap, {ControlType:"Edit", AutomationId:"PasswordInput"})  # PasswordBox маскируется как Edit
btn  = find_one(snap, {ControlType:"Button", Name:"Войти"})

act(main_window, txt, "set_value", value=<login>)
act(main_window, pwd, "focus")
act(main_window, pwd, "send_keys", value=<password>)   # PasswordBox не поддерживает set_value;
                                                       # PasswordChanged в code-behind подхватит ввод
act(main_window, btn, "invoke")
```

> ⚠️ В `LoginViewModel` есть дефолт `_password = "Admin123!"`, но PasswordBox его не отображает. Если не трогать поле пароля, сервер получит именно этот дефолт — годится только для быстрой проверки admin.

### Шаг 2.3. Ожидание MainShell

```
wait_for(pid, {ControlType:"Window", Title__contains:"Управление бюджетом"}, timeout_ms=15000)
wpfpilot.list_windows(pid) → должна появиться новая window (MainShell); MainWindow закрывается
snap_shell = snapshot(main_shell)
```

Проверки успешного входа:
- в snapshot есть `TextBlock`/`Label` с ФИО / логином (`MainShellViewModel` выводит `SessionService.Login/FullName/RoleName`);
- пункт меню «Выйти»/«Выход» присутствует;
- нет MessageBox «Неверный логин или пароль» / «Учётная запись временно заблокирована».

Если вход не удался — фиксируем текст из найденного `TextBlock` ошибки в окне входа (`LoginViewModel.ErrorMessage`) и переходим к следующей роли.

### Шаг 2.4. Проверка консистентности меню роли

Перед кликами:

```
snap_shell = snapshot(main_shell)
items = find_all(snap_shell, {ControlType__in:["ListItem","Button","TreeItem"],
                              IsTopLevelMenu:true})   # фактический селектор — по дереву
```

Выгрузите `(Name, AutomationId)` каждого пункта, сравните с ожидаемым списком (таблица в разделе 0). Расхождение (лишний/недостающий пункт) — отметить в отчёте как **DEFECT**.

Для каждой роли ниже — свой набор пунктов и проверок.

---

## 3. Роль «Директор» — детальный прогон

> **Цель:** 6 экранов меню открываются без падений. Фактически в коде зарегистрировано 4; если после фикса `MainShellViewModel` их 6 — проверка ниже масштабируется добавлением 2 записей.

### 3.1. Вход под директором

Учётку взять из `DbSeeder.cs` (роль `Директор`). Выполнить шаги 2.1–2.4.

Дополнительно:

```
snap = snapshot(main_shell)
assert find_one(snap, {ControlType:"TextBlock", Name__contains:"Директор"})
```

### 3.2. «Обзор» (OverviewView)

Селектор пункта: `{Name__contains:"Обзор"}` (или `{AutomationId: "NavOverview"}` — если после фикса добавлены).

```
act(main_shell, {Name__contains:"Обзор"}, "invoke")
wait_for(main_shell, {ControlType:"TextBlock", Name__contains:"Загружено смет"}, timeout_ms=10000)
   # В OverviewViewModel статус: "Загружено смет: N, ожидают утверждения: M."
```

Проверяемые элементы (`OverviewView.xaml`):
- TextBlock `Name__contains:"Смет на утверждении"` (карточка-счётчик);
- `Name__contains:"Отчётов на утверждении"`;
- `Name__contains:"Заявок на рассмотрении"`;
- `Name__contains:"Итого план"` / `"Итого факт"` / `"Отклонение"`;
- `ControlType:"DataGrid"` с колонками «Проект», «Год», «Статус» (проверить через `find_all({ControlType:"DataGridColumn"})` или визуально по Header).

**Особый случай:** `OverviewViewModel` использует `Dispatcher.BeginInvoke(async …)` — при ошибке внутри `LoadAsync` срабатывает `MessageBox.Show(ex.ToString(), "Ошибка загрузки сводки", …)`. После клика в течение 10 с проверить наличие окна с Title `"Ошибка загрузки сводки"`:

```
err = wpfpilot.list_windows(pid).find(w => w.Title == "Ошибка загрузки сводки")
if err: 
    snap_err = snapshot(err)
    text = содержимое Edit/TextBlock (стектрейс)
    screenshot(err)
    act(err, {Name:"OK"}, "invoke")
    → FAIL «Overview падает при загрузке»
```

Успех = нет MessageBox «Ошибка загрузки сводки», есть все 3 счётчика и заполненный (возможно пустой) DataGrid.

### 3.3. «Утверждение смет» (SmetaApprovalView)

```
act(main_shell, {Name__contains:"Утверждение смет"}, "invoke")
wait_for(main_shell, {ControlType:"DataGrid"}, timeout_ms=10000)
```

Проверки:
- DataGrid отображает сметы со `Status = "На утверждении"` (при пустом списке — `StatusMessage` = «Смет на утверждении: 0», DataGrid пуст — это успех);
- `Button` «Утвердить» и `Button` «Отклонить» присутствуют и **disabled**, пока не выделена строка (`find_one(...,{ControlType:"Button",Name:"Утвердить"}).IsEnabled == false` — проверка `CanExecute`);
- кликнуть по 1-й строке DataGrid → обе кнопки стали `IsEnabled == true`;
- **не нажимать** «Утвердить»/«Отклонить» (иначе меняете БД). Только визуальная проверка `CanExecute`.

### 3.4. «Отчёты на утверждении» (ReportsApprovalView)

```
act(main_shell, {Name__contains:"Отчёты на утверждении"}, "invoke")
wait_for(main_shell, {ControlType:"DataGrid"}, timeout_ms=10000)
```

- DataGrid с колонками `SmetaId, SmetaFormCode, CreatedAt, Status` (содержимое по `ReportDto`);
- Кнопки «Утвердить», «Скачать CSV» — по тем же правилам `CanExecute`;
- `StatusMessage = "Отчётов на утверждении: N."` — проверьте наличие.

### 3.5. «Решения» (DecisionsView)

```
act(main_shell, {Name__contains:"Решения"}, "invoke")
wait_for(main_shell, {ControlType:"ComboBox"}, timeout_ms=10000)
```

- 3 ComboBox: «Смета» (`SelectedSmetaForItems`), «Статья» (`SelectedItem`), «Период» (`SelectedPeriod`);
- `TextBox` для текста решения (`NewDecisionText`);
- `Button` «Создать» (`CreateCommand`) — при пустых полях должна быть `IsEnabled == false`;
- DataGrid ранее принятых решений (`DecisionDto`) — колонки `ExpenseItemName, KosguCode, PeriodYear, PeriodMonth, Decision, CreatedAt`.

Проверка CanExecute:

```
act(main_shell, {ControlType:"ComboBox", Index:0}, "select", value=<любое значение>)
act(main_shell, {ControlType:"ComboBox", Index:1}, "select", value=<любое значение>)
act(main_shell, {ControlType:"ComboBox", Index:2}, "select", value=<любое значение>)
act(main_shell, {ControlType:"Edit", Name__contains:"решение"}, "set_value", value="Тест")
assert find_one(...,{Name:"Создать"}).IsEnabled == true
```

Создать решение **не нужно** (изменение БД); достаточно проверить, что `CanExecute` переключается.

### 3.6. Дополнительные 2 экрана (если добавлены фиксом)

После 3.2–3.5 повторите шаг 2.4 (`snapshot`) — если в меню появились ещё 2 пункта, для каждого выполните:

```
act(main_shell, <item>, "invoke")
wait_for(main_shell, {ControlType:"ContentPresenter", HasContent:True}, timeout=10s)
проверить exception_dialog(); screenshot; записать в отчёт
```

Без знания ожидаемых заголовков — критерий успеха: **нет MessageBox об исключении, DataGrid/ComboBox/TextBlock-заголовок присутствуют** (хотя бы один интерактивный контрол в ContentPresenter).

### 3.7. Выход

```
act(main_shell, {Name__contains:"Выйти"}  или  {Name__contains:"Выход"}, "invoke")
wait_for(pid, {ControlType:"Window", Title__contains:"Вход"}, timeout_ms=10000)
```

`MainShell.xaml.cs.OnLogoutRequested` создаёт новый `MainWindow` — при успехе старое окно `MainShell` закрывается, окно входа снова в `list_windows`. Если окно входа не появилось — FAIL «Logout не работает».

---

## 4. Роль «Администратор» (7 экранов)

Вход → проверка меню (7 пунктов) → для каждого:

| Пункт | Ключевой элемент для ожидания | Что проверить |
|---|---|---|
| Пользователи | `DataGrid` + кнопка «Добавить» | DataGrid со списком; `Login/FullName/RoleName` колонки; кнопки Add/Edit/Block/Unblock/ResetPassword/Delete существуют; статус «Загружено пользователей: N» |
| Отделы | `DataGrid` + «Добавить» | колонки Id/Name/ProjectCount/UserCount |
| Проекты | `DataGrid` + «Добавить» | Code/Name/DepartmentName/StartDate/EndDate/SmetaCount |
| Справочники | `TabControl`/3× `DataGrid` | три таблицы: Roles, ExpenseItems, SmetaForms; статус «Роли: N, статьи: N, формы: N.» |
| Журналы | 2× `DataGrid` | AuditLogs и ErrorLogs; фильтры-`TextBox` (Entity, Action, Level) |
| Резервные копии | `DataGrid` + «Создать копию» | колонки FileName/SizeBytes/CreatedAt; кнопка «Восстановить» disabled без выбора |
| Настройки сервера | `TextBox`-ы + «Сохранить» | поля Address/Port/MaxFailedAttempts/LockoutMinutes/SessionTimeoutMinutes; **не сохранять** — только чтение |

Для каждого пункта используйте каркас из 1.6. Дополнительно: для «Пользователи» попробуйте открыть диалог «Добавить» → `wait_for` окна `"Новый пользователь"` → **сразу закрыть** (кнопка «Отмена» / ESC через `send_keys`), не заполняя — это проверка, что диалог создаётся, но не вносит изменения в БД.

Выход.

---

## 5. Роль «Главный экономист» (5 экранов)

| Пункт | Ожидаемые элементы |
|---|---|
| Сметы | `DataGrid` (SmetaDto); кнопки «Создать», «Редактировать», «Изменение»; статус «Загружено смет: N». CanExecute: «Редактировать» включена только для `Status ∈ {На утверждении, Отклонена}`, «Изменение» — только для `Status == Утверждена`. |
| Лимиты | ComboBox выбора сметы; `DataGrid` c колонками План/Лимит/Факт/Остаток/Превышение; кнопки «Пересчитать», «Сохранить» (Save — disabled для `Status ∈ {Утверждена, Закрыта}`) |
| Анализ | ComboBox сметы; блок «План-факт» (TotalPlan/TotalFact/TotalAbsolute/TotalRelativePercent); отдельные блоки «Материалы» (Q_plan, Q_fact, P_plan, P_fact) и «ФОТ» (Ч_plan, Ч_fact, З_plan, З_fact); кнопки «Рассчитать», «Автозаполнить»; поле статуса. При загрузке выбранной сметы — нет исключения (`ReloadSmetaDetailsAsync`) |
| Заявки | ComboBox статуса (Все / На рассмотрении / Согласована / Отклонена); `DataGrid`; кнопки «Согласовать», «Отклонить» — disabled без выделения; после выбора строки в статусе «На рассмотрении» включены |
| Отчёты | `DataGrid`; ComboBox выбора сметы для создания; кнопки «Создать», «Скачать CSV». **Не создавать отчёт** — только проверить CanExecute |

Для «Анализ» при клике на «Пересчитать» (ReloadDeviation) — обязательная проверка `exception_dialog()` + проверка появления `TextBlock` со статусом «План: … BYN, факт: … BYN, отклонение: …».

Выход.

---

## 6. Роль «Руководитель отдела» (4 экрана)

| Пункт | Ожидаемые элементы |
|---|---|
| Мои сметы | `DataGrid` смет (SmetaDto) + вторая таблица статей при выборе строки; статус «Загружено смет отдела: N»; **только чтение** — нет кнопок создания/редактирования |
| Мои расходы | ComboBox статуса; `DataGrid` (ActualExpenseDto); статус «Загружено расходов: N, сумма: … BYN» |
| Заявки | `DataGrid` заявок + кнопка «Подать заявку»; при открытии диалога `CreateRequestDialog` — `wait_for` окна → **закрыть без сохранения** |
| Отчёты | `DataGrid` только `Status == "Утверждён"`; кнопка «Скачать CSV» (disabled без выделения) |

Проверка фильтрации по отделу: в snapshot `DataGrid` не должно быть строк с чужим `DepartmentName` — если поле видно. Для точной проверки — открыть «Мои сметы», в колонке `ProjectCode` значения соответствуют проектам отдела (сверить с БД вручную).

Выход.

---

## 7. Роль «Бухгалтер» (2 экрана)

| Пункт | Ожидаемые элементы |
|---|---|
| Расходы | ComboBox статуса; `DataGrid`; кнопки «Добавить», «Редактировать», «Сверить», «Удалить». CanExecute: «Сверить» — `SelectedExpense != null && Status != "Сверен"`; «Добавить» открывает `ActualExpenseEditDialog` — открыть и закрыть без сохранения |
| Периоды | `DataGrid`; кнопка «Закрыть период» disabled для `IsClosed==true`; проверить, что строки с `UnverifiedCount > 0` → закрытие отклоняется (не нажимать «Закрыть» на реальном периоде!) |

**Не нажимать** «Закрыть период» — изменит БД.

Выход.

---

## 8. Что фиксировать в отчёте

Заведите таблицу:

| # | Роль | Пункт меню | Действие | Ожидание | Факт | Скриншот | Результат |
|---|---|---|---|---|---|---|---|
| 1 | Директор | Обзор | invoke | 3 счётчика + DataGrid | есть/нет | `reports/run1/director/overview.png` | OK / FAIL + причина |

Для каждого FAIL — обязательно приложить:

- полный **текст MessageBox** (из `ControlType:Edit` внутри окна «Неперехваченное исключение» или «Ошибка загрузки сводки»);
- скриншот окна с исключением;
- фрагмент snapshot `main_shell` на момент падения (10–15 строк вокруг ContentPresenter);
- идентификатор последнего успешного шага (по какому пункту меню дошли).

Для успеха — достаточно: скриншот экрана + строка с ожидаемыми/фактическими счётчиками из `StatusMessage` (`ControlType:"TextBlock"` нижней панели).

### 8.1. Сводка по «фиксу MainShell»

Отдельным блоком в отчёте:

- Сколько пунктов меню фактически показано под каждой ролью (из snapshot).
- Совпадает ли с ожидаемым (Директор = 6 по ТЗ, либо 4 по коду).
- Есть ли роли, под которыми какой-то пункт меню отсутствует (сравнить с `App.xaml.cs`-регистрацией ViewModel).
- Для директора: какой из 4 (или 6) экранов падал **до** фикса и какой теперь открывается (лог приложить к коммиту-фиксу).

### 8.2. Технические артефакты прогона

- Папка `reports/run-YYYYMMDD-HHMMSS/` с подпапками `admin/`, `director/`, `economist/`, `head/`, `accountant/`.
- `snapshot_<role>_<menu>.json` — снимок для каждого экрана (можно отфильтровать до `ControlType` ∈ {Window, ContentPresenter, DataGrid, TextBlock, Button, ComboBox}).
- `exceptions.log` — все найденные MessageBox-исключения с текстом.
- `menu_diff.md` — таблица «роль → ожидаемые пункты → фактические пункты».

---

## 9. Порядок выполнения (последовательно, без параллелизма)

1. `Admin` (7 экранов) → logout → шаг 2.
2. `Director` (4 или 6 экранов) → logout.
3. `Economist` (5 экранов) → logout.
4. `Head` (4 экрана) → logout.
5. `Accountant` (2 экрана) → logout → закрыть клиент.

При первом же падении UI-потока (окно закрылось / появился MessageBox «Критическая ошибка» из `AppDomain.UnhandledException`) — прекратить прогон этой роли, зафиксировать и **перезапустить Client** для следующей роли, чтобы не тащить невалидное состояние.

---

## 10. Быстрый чек-лист «успех/провал» на один пункт меню

1. `invoke` по пункту → нет исключения в течение 2 с.
2. `wait_for` ключевого контрола (DataGrid / ComboBox / TextBlock-заголовок) ≤ 10 с.
3. Snapshot содержит `StatusMessage`-TextBlock с непустым текстом (кроме пустых списков — это норма).
4. Нет модальных окон с Title из {`Неперехваченное исключение`, `Критическая ошибка`, `Ошибка загрузки сводки`}.
5. `screenshot` сохранён.
6. Запись в отчёте.

Если все 6 — **OK**. Иначе — **FAIL** с текстом исключения из шага 4.
