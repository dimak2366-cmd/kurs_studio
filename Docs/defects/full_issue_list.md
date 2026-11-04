# Полный список проблем в программе

Ниже — всё, что выявлено за прогон (подтверждено снапшотами) + всё, что видно по коду, но не проверено UI-тестом. Разбито по критичности.

---

## 🔴 Критичные (исправить в первую очередь)

### 1. D7 — Повреждение кириллицы в БД
**Где:** `expense_items.Name`, `actual_expenses.DocumentType`, `actual_expenses.DocumentNumber`, `expense_requests.Comment`.
**Проявление:** в UI вместо русских слов — `????????? ?????????`, `??-2026-1001`.
**Причина:** `BackupService.CreateBackupAsync` / `RestoreAsync` (`Server/Services/BackupService.cs`) **не устанавливают `PGCLIENTENCODING=UTF8`** для дочерних процессов `pg_dump.exe` / `pg_restore.exe`. На русской Windows по умолчанию используется `WIN1251` — при восстановлении из копии кириллица портится.
**Фикс:**
```csharp
// в CreateBackupAsync и RestoreAsync, после psi.Environment["PGPASSWORD"]:
psi.Environment["PGCLIENTENCODING"] = "UTF8";
```
Плюс — пересоздать корректную БД (см. п. 3).

### 2. D13 — Утечка данных через API (не проверено UI, видно в коде)
**Где:** `Server/Controllers/AnalysisController.cs` → `GetDeviation`, `AnalyzeMaterialsFromDb`, `AnalyzeLaborFromDb`; `Server/Controllers/LimitsController.cs` → `Get`, `Check`.
**Проявление:** атрибут `[AuthorizeRole]` без указания роли → **любой аутентифицированный пользователь** может запросить данные сметы чужого отдела, если знает `smetaId`.
**Пример:** head запрашивает `GET /api/analysis/deviation/3` (смета `НИР-2026-03`, чужой отдел) — сервер вернёт данные без фильтрации.
**Фикс:** добавить в методы явную проверку `session.DepartmentId` (для head) и `session.RoleName` (для director / economist), как это уже сделано в `SmetaController.GetById`.

---

## 🟠 Функциональные (не работают как задумано)

### 3. D6 — БД ≠ `DbSeeder.cs`
**Проявление:** 6 смет вместо 2, 11 расходов вместо 10, 3 заявки вместо 0, 1 отчёт вместо 0, 5 форм вместо 1.
**Причина:** БД «накопила» данные от прошлых прогонов, не сброшена.
**Фикс:**
```powershell
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -c "DROP DATABASE budget_institute;"
```
Пересобрать и перезапустить `Server.exe` (`MigrateAsync` + `DbSeeder` создадут чистый baseline). Делать **до демонстрации на защите**.

### 4. D12 — Seeder создаёт закрытый период с несверенными записями
**Где:** `Server/Data/DbSeeder.cs` — `closedPeriod = new Period { IsClosed = true, ... }` создаётся **до** заполнения `ActualExpenses`, без проверки статусов.
**Проявление:** `2026/09 IsClosed=true`, но `UnverifiedCount = 10` (все записи `Status = "Введён"`).
**Нарушение:** инвариант `PeriodService.CloseAsync` (`UnverifiedCount > 0 → throw`) — такой период закрыть через API невозможно, а в сиде он уже закрыт «в обход».
**Фикс:** либо `IsClosed = false`, либо установить всем 10 расходам `Status = "Сверен"` перед сохранением периода.

### 5. D2 — Кнопка «Сохранить лимиты» активна у директора
**Где:** `Client/Views/Economist/LimitsViewModel.cs` → `CanEdit`.
**Проявление:** под Director’ом кнопка enabled, сервер (`LimitsController.Set` с `[AuthorizeRole("Главный экономист")]`) вернёт **403**.
**Фикс:**
```csharp
private readonly SessionService _session; // инжектить

private bool CanEdit(SmetaDto smeta) =>
    _session.RoleName == "Главный экономист"
    && smeta.Status != "Утверждена"
    && smeta.Status != "Закрыта";
```

### 6. D3 — «Ввод расходов» = «Реестр» (дублирование)
**Где:** `Client/Views/MainShellViewModel.cs` → `ResolveView`:
```csharp
case "Ввод расходов": return _serviceProvider.GetRequiredService<ExpensesViewModel>();
case "Реестр":        return _serviceProvider.GetRequiredService<ExpensesViewModel>();
```
**Проявление:** оба пункта меню открывают один и тот же `ExpensesView` с идентичным UI (проверено: 10 колонок, 4 кнопки, те же 11 записей).
**Фикс (рекомендуется вариант B):**
- **A:** убрать «Реестр» из `BuildMenu("Бухгалтер")`.
- **B:** создать `RegistryViewModel` + `RegistryView.xaml` (только чтение + фильтры по датам/статусу/периоду) — лучше согласуется с постановкой курсовой.

### 7. D10 — `BackupPath` пустой в UI (сервер отдаёт корректно)
**Где:** `Client/Views/Admin/SettingsViewModel.cs` → `LoadAsync`.
**Причина:** `BackupPath = s.BackupPath;` вызывает `OnPropertyChanged("BackupPath")`, а `TextBox` в `SettingsView.xaml` привязан к **`BackupPathEdit`** — WPF не получает уведомление.
**Фикс:**
```csharp
// LoadAsync — заменить:
BackupPath = s.BackupPath;   // ❌
// на:
BackupPathEdit = s.BackupPath;   // ✅
```
Или объединить два свойства в одно `BackupPath` с `public set` (убрать `BackupPathEdit`).

### 8. D14 — Заявка на расход без проверки лимита
**Где:** `Server/Services/RequestService.cs` → `CreateAsync`.
**Проявление:** `Amount` валидируется только на `> 0`. Не проверяется, что `Amount ≤ item.LimitAmount` и не превышает остаток. Студент-экономист должен вручную сверять лимит в другом экране.
**Фикс:** добавить проверку в `CreateAsync`:
```csharp
var factSum = await _db.ActualExpenses
    .Where(a => a.ExpenseItemId == item.Id).SumAsync(a => a.Amount);
var pendingSum = await _db.ExpenseRequests
    .Where(r => r.ExpenseItemId == item.Id && r.Status == "На рассмотрении")
    .SumAsync(r => r.Amount);
if (request.Amount > item.LimitAmount - factSum - pendingSum)
    throw new ArgumentException($"Превышение лимита: доступно {item.LimitAmount - factSum - pendingSum:N2} BYN.");
```

### 9. D15 — Ввод фактического расхода без проверки лимита
**Где:** `Server/Services/ExpenseService.cs` → `CreateAsync`.
**Проявление:** в коде **комментарий-заглушка**:
```csharp
// Сумма уже введённых расходов по статье + новая не должна превышать лимит.
// (мягкая проверка — только предупреждение в API не возвращаем, оставляем на этап контроля лимитов)
```
Проверки нет. Бухгалтер может вводить расходы, превышающие лимит. `LimitService.CheckExcessAsync` **обнаруживает** превышение, но не **предотвращает**.
**Фикс:** либо реализовать проверку (и возвращать 409 Conflict), либо явно задокументировать «мягкий контроль» в постановке, но не оставлять мёртвый комментарий.

### 10. D16 / D17 — Кнопки Approve/Reject без проверки статуса
**Где:** `Client/Views/Director/SmetaApprovalViewModel.cs` и `ReportsApprovalViewModel.cs`.
**Проявление:**
```csharp
ApproveCommand = new AsyncRelayCommand(ApproveAsync, () => SelectedSmeta != null);
// нет проверки SelectedSmeta.Status == "На утверждении"
```
Сервер вернёт 409 Conflict, если статус не тот, но UI даёт нажать кнопку → MessageBox с ошибкой вместо disabled.
**Фикс:** добавить проверку в `CanExecute`:
```csharp
() => SelectedSmeta != null && SelectedSmeta.Status == "На утверждении"
```

### 11. D29 — UI не проверяет статус периода для действий с расходом
**Где:** `Client/Views/Accountant/ExpensesViewModel.cs` → `VerifyCommand`, `EditCommand`.
**Проявление:** для записи в **закрытом** периоде (2026/09) кнопки «Сверить»/«Редактировать» включаются, если выбрана строка. При попытке — сервер вернёт 409 «Период закрыт».
**Фикс:** `ActualExpenseDto` уже содержит `PeriodId`, но не содержит флага `IsClosed`. Либо добавить поле в DTO и в `ExpenseService.ToDto`, либо отдельный запрос `/api/periods` в VM и disable кнопки для закрытых.

---

## 🟡 Данные / состояние

### 12. D6-расширение — Накопленные записи в UI
**Проявление:** `Settings` — форма `BackupPath` пуста (D10); записи ID 11 в `actual_expenses` (из прошлых сессий) — источник `?`-замен (D7); 3 заявки в `expense_requests` (в сиде должно быть 0).
**Фикс:** тот же сброс БД, что и D6. Дополнительно — при следующем прогоне фиксировать в отчёте, какие записи добавлены **тестом**, а какие сид-ом.

---

## 🟢 Мелкие / косметические

### 13. D21 — `ExpensesViewModel.DeleteCommand` — мёртвый код
**Где:** `Client/Views/Accountant/ExpensesViewModel.cs`.
**Проявление:** команда есть в VM, но кнопки «Удалить» нет в `ExpensesView.xaml`. `DeleteAsync` показывает MessageBox «Удаление не поддерживается» — ничего не делает.
**Фикс:** либо убрать `DeleteCommand` + `DeleteAsync`, либо реализовать `DELETE /api/expenses/{id}` (с проверкой, что период открыт) и добавить кнопку.

### 14. D25 — `LogsViewModel.LoadAllAsync` перезаписывает `StatusMessage`
**Где:** `Client/Views/Admin/LogsViewModel.cs`.
**Проявление:** `LoadAuditAsync` пишет «Журнал действий: N…», затем `LoadErrorsAsync` перезаписывает «Журнал ошибок: M…». В статус-строке пользователь видит только счётчик ошибок.
**Фикс:** объединять в одну строку: `StatusMessage = $"Журнал действий: {AuditLogs.Count}, журнал ошибок: {ErrorLogs.Count}.";`.

### 15. D26 / D27 — Fire-and-forget `_ = LoadAsync()` в конструкторах VM
**Где:** `OverviewViewModel`, `SmetasViewModel`, `HeadSmetasViewModel`, `HeadExpensesViewModel`, `RequestsReviewViewModel`, `CatalogViewModel`, `LogsViewModel`, `LimitsViewModel`, `AnalysisViewModel`, `UsersViewModel`, `DepartmentsViewModel`, `ProjectsViewModel`, `BackupViewModel`, `SettingsViewModel`, `ExpensesViewModel`, `PeriodsViewModel`.
**Проявление:** если `LoadAsync` бросит исключение (сеть, 500, таймаут), UI не покажет ошибку — статус останется «Загрузка…» навсегда, пользователь не поймёт, что не так.
**Фикс:** заменить `_ = LoadAsync()` на `_ = SafeLoadAsync()` с try/catch и явным `StatusMessage = "Ошибка: " + ex.Message`. Либо использовать `Loaded` event у View с обработчиком.

### 16. D20 — Мёртвый код в `RequestsReviewViewModel.ApproveAsync`
**Где:** `Client/Views/Economist/RequestsReviewViewModel.cs`.
**Проявление:** создаётся `var dialog = new RejectReasonDialog(...)`, устанавливается `Owner`, но `ShowDialog()` не вызывается — потом используется `MessageBox.Show`. Объект `dialog` не нужен.
**Фикс:** удалить мёртвый код.

---

## 🔵 Безопасность / production concerns

### 17. D18 — Plaintext-пароли в `DbSeeder.cs`
**Где:** `Server/Data/DbSeeder.cs`.
**Проявление:** `PasswordHasher.ComputeHash("Admin123!", adminSalt)`, `"Director123!"`, `"Economist123!"`, `"Head123!"`, `"Accountant123!"` — пароли в открытом виде в исходниках.
**Риск:** для курсовой это допустимо (демо-данные), но в отчёте стоит отметить. Если репозиторий публичный — пароли утекут.
**Фикс (для защиты):** перенести в `appsettings.Development.json` или в `.env`-файл.

### 18. D19 — Хардкод дефолтного логина в `LoginViewModel`
**Где:** `Client/ViewModels/LoginViewModel.cs`:
```csharp
private string _login = "admin";
private string _password = "Admin123!";
```
**Проявление:** при запуске поле логина уже содержит `admin`, поле пароля — `Admin123!`. Пользователь заходит «по умолчанию» одним кликом.
**Риск:** для демонстрации удобно, для production — плохо.
**Фикс:** убрать дефолты или перенести в `appsettings.Development.json`.

### 19. D22 — `CORS: AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()`
**Где:** `Server/Program.cs`.
**Проявление:** любой сайт в интернете может отправить запрос на ваш API.
**Риск:** для курсовой допустимо (нет веб-клиента), но в отчёте о безопасности стоит упомянуть.
**Фикс:** `WithOrigins("http://localhost:5155")` или ограничить по IP.

### 20. D23 — Статический IV в AES (CBC)
**Где:** `Server/Security/AesEncryptor.cs`.
**Проявление:** `AesIvBase64` — фиксированный IV из конфига. При CBC повторное использование IV для разных сообщений с одним ключом **утекает информацию** о совпадающих префиксах.
**Фикс:** генерировать случайный IV для каждого шифрования и передавать его вместе с шифртекстом (IV || ciphertext).

### 21. D24 — Захардкоженная строка подключения с паролем в `appsettings.json`
**Проявление:** `Password=1234` в `Server/appsettings.json`. Аналогично — `AesKeyBase64` и `AesIvBase64` в открытом виде.
**Фикс:** `dotnet user-secrets` или переменные окружения.

---

## ⚪ Инфраструктура (не дефекты ПО)

### 22. D11 — Артефакт WpfPilot
**Проявление:** клик по `ListItem` не срабатывает после resize окна / без maximize. Симптом «навигация застревает» (D11 в отчёте).
**Не дефект ПО** — артефакт инструмента: `click` использует координаты из последнего snapshot. **Фикс в тестах:** `{AutomationId:"Maximize-Restore"}` + `invoke` перед кликом, или `select` на `ListItem` по `Index`.

### 23. Падения во время прогона (тоже не дефекты)
- **Client.exe падал** — случайный клик на `TitleBar → Close` из-за устаревших координат (следствие D11). В тестах — не кликать по TitleBar.
- **Server.exe отваливался** — причина не установлена. В тестах — предусловие: `Test-NetConnection localhost 5155 = True` перед прогоном; при `False` — перезапуск.

---

## 📊 Сводная таблица по приоритетам

| Приоритет | ID | Что | Где | Время на фикс |
|---|---|---|---|---|
| 🔴 P1 | **D7** | Кириллица в 4 местах | `Server/Services/BackupService.cs` + сброс БД | 5 мин + сброс |
| 🔴 P1 | **D13** | API data leak | `AnalysisController.cs`, `LimitsController.cs` | 30 мин |
| 🟠 P2 | **D6** | БД ≠ сид | `DROP DATABASE` | 5 мин |
| 🟠 P2 | **D12** | Противоречивый период в сиде | `DbSeeder.cs` | 5 мин |
| 🟠 P2 | **D2** | Save лимитов у директора | `LimitsViewModel.cs` | 10 мин |
| 🟠 P2 | **D3** | Дублирование экрана | `MainShellViewModel.cs` + новый View | 30–60 мин |
| 🟠 P2 | **D10** | `BackupPath` не биндится | `SettingsViewModel.cs` | 5 мин |
| 🟡 P3 | **D14** | Заявка без лимита | `RequestService.cs` | 20 мин |
| 🟡 P3 | **D15** | Расход без лимита | `ExpenseService.cs` | 20 мин |
| 🟡 P3 | **D16, D17** | Approve без статуса | `SmetaApprovalViewModel.cs`, `ReportsApprovalViewModel.cs` | 15 мин |
| 🟡 P3 | **D29** | UI не знает статус периода | `ExpensesViewModel.cs` | 20 мин |
| 🟢 P4 | **D21** | Мёртвый код `DeleteCommand` | `ExpensesViewModel.cs` | 5 мин |
| 🟢 P4 | **D25** | `StatusMessage` перезаписывается | `LogsViewModel.cs` | 5 мин |
| 🟢 P4 | **D26, D27** | Fire-and-forget в конструкторах | ~16 VM | 2–3 часа |
| 🟢 P4 | **D20** | Dead code Approve | `RequestsReviewViewModel.cs` | 2 мин |
| 🔵 P5 | **D18–D24** | Пароли/CORS/AES/конфиг | разные | по желанию |

---

## Итого

- **P1 (критично для защиты):** D7, D13 — исправить обязательно, иначе будут падения и вопросы от комиссии.
- **P2 (функциональные):** D6, D12, D2, D3, D10 — легко правятся, есть готовые рекомендации.
- **P3 (логика / UX):** D14, D15, D16, D17, D29 — добавят качества, но не блокируют защиту.
- **P4 (косметика):** D20, D21, D25, D26, D27 — можно отложить.
- **P5 (production):** D18–D24 — упомянуть в главе «Охрана труда и безопасность» или оставить с пометкой «демо-уровень».
- **Инфраструктура:** D11, падения Client/Server — не дефекты кода, фиксируются в `docs/wpfpilot_check_instructions.md`.

**Минимальный набор для успешной защиты:** P1 + P2 (около 2–3 часов работы). Всё остальное — бонус к качеству и к оценке главы «Тестирование».
