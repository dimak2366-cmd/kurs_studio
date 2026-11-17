# Roadmap: только приложение (код)

Без документации, без финальных прогонов, без методички. Только правки в коде приложения.

Легенда: ☐ не начато · ☑ готово.
Коммиты: `7b4e595`, `3a79651`, `374f2cd`, `26694e3`, `555515b`, `89eace6`.

---

## P1 — критично (≈1 час)

| # | ID | Задача | Файл(ы) | Критерий | Статус |
|---|---|---|---|---|---|
| 1.1 | D7 | Установить `PGCLIENTENCODING=UTF8` в `psi.Environment` для `pg_dump`/`pg_restore` | `Server/Services/BackupService.cs` | резервная копия и восстановление не портят кириллицу | ☐ |
| 1.2 | — | Сбросить БД (`DROP DATABASE` + `CREATE`) → перезапуск сервера | `psql` + `dotnet run --project Server` | в UI нет `?????` (миграции + `DbSeeder` + backfill `catalog_expense_items` создадут чистые данные) | ☐ |
| 1.3 | D13 | Проверка `DepartmentId` / `RoleName` в методах анализа | `Server/Controllers/AnalysisController.cs` | head не получает чужую смету (403 или фильтр по отделу) | ☐ |
| 1.4 | D13 | То же для лимитов | `Server/Controllers/LimitsController.cs` | head не получает лимиты чужой сметы | ☐ |

---

## P2 — функциональные (≈1.5 ч)

| # | ID | Задача | Файл(ы) | Критерий | Статус |
|---|---|---|---|---|---|
| 2.1 | D12 | `DbSeeder` не создаёт закрытый период с несверенными расходами | `Server/Data/DbSeeder.cs` | `2026/09` либо `IsClosed=false`, либо все расходы `Сверен` | ☐ |
| 2.2 | D2 | `CanEdit` для лимитов только у «Главный экономист» | `Client/Views/Economist/LimitsViewModel.cs` | кнопка «Сохранить» disabled у не-экономиста | ☐ |
| 2.3 | D3 | Убрать дублирование «Ввод расходов» / «Реестр» (если есть) | `Client/Views/MainShellViewModel.cs` | два пункта меню ведут в разные экраны или один удалён | ☐ (проверить) |
| 2.4 | D10 | `SaveAsync` использует `BackupPathEdit` вместо `BackupPath` | `Client/Views/Admin/SettingsViewModel.cs` | после сохранения поле пути не пустое | ☐ |

---

## P3 — логика / UX (≈1.5 ч)

| # | ID | Задача | Файл(ы) | Критерий | Статус |
|---|---|---|---|---|---|
| 3.1 | D14 | Проверка лимита при создании заявки: `Amount ≤ LimitAmount − факт − на рассмотрении` | `Server/Services/RequestService.cs` | заявка сверх лимита → 400 | ☐ |
| 3.2 | D15 | Проверка лимита при вводе расхода — либо 409 Conflict, либо явный комментарий «мягкий контроль» | `Server/Services/ExpenseService.cs` | код и комментарий согласованы | ☐ |
| 3.3 | D16 | `CanExecute` Approve/Reject по `Status == "На утверждении"` | `Client/Views/Director/SmetaApprovalViewModel.cs` | кнопки disabled для других статусов | ☐ |
| 3.4 | D17 | То же для отчётов | `Client/Views/Director/ReportsApprovalViewModel.cs` | кнопки disabled для других статусов | ☐ |
| 3.5 | D29 | Disable «Сверить/Редактировать» для закрытого периода (добавить `IsClosed` в `ActualExpenseDto` + биндинг) | `Shared/Dtos/ExpenseDtos.cs`, `Server/Services/ExpenseService.cs`, `Client/Views/Accountant/ExpensesViewModel.cs` | в закрытом периоде кнопки disabled | ☐ |

---

## P4 — косметика (≈30 мин)

| # | ID | Задача | Файл(ы) | Статус |
|---|---|---|---|---|
| 4.1 | D25 | Объединить счётчики «Журнал действий» и «Журнал ошибок» в один `StatusMessage` | `Client/Views/Admin/LogsViewModel.cs` | ☐ |
| 4.2 | D26, D27 | `SafeLoadAsync()` (try/catch внутри) вместо `_ = LoadAsync()` в ~16 VM | все VM с `_ = LoadAsync()` | ☐ |

---

## Уже сделано (для контекста)

| Что | Коммит |
|---|---|
| DELETE `/api/expenses/{id}` + guard + audit + кнопка в UI | `7b4e595` |
| Мёртвый `RejectReasonDialog` в `ApproveAsync` удалён | `3a79651` |
| Комментарии `qFact = qPlan` / `chFact = chPlan` в `AnalysisController` | `3a79651` |
| Serilog: консоль + файл `logs/server-YYYYMMDD.log`, suppress EF SQL-шума | `374f2cd` |
| N+1 fix: `GET /api/smeta/items/by-project/{projectId}` — 1 запрос вместо 1+N | `26694e3` |
| Catalog CRUD server: таблица `catalog_expense_items` + миграция + 5 endpoints | `555515b` |
| Catalog CRUD UI: `CatalogView` с `DataGrid` + `CatalogItemEditDialog` + 3 команды | `89eace6` |

---

## Минимальный путь до защиты

1. **P1** — 1 час. После `DROP DATABASE` UI чистый, guard в Analysis/Limits работает.
2. **P2** — 1.5 часа. Сев закрытого периода, `CanEdit`, `BackupPathEdit`, меню.
3. **P4** — 30 мин (опционально, косметика).

**Итого ≈2.5–3 часа до защиты**, если пропустить P3 (логика лимитов, `CanExecute`). P3 — бонус, но не блокер: сценарии проверяются, баги видны только при попытке нарушения порядка действий.

---

С чего начинаем — **P1** (правка `BackupService.cs` + guard в `AnalysisController`/`LimitsController` + `DROP DATABASE`)? Дам пошагово, с кодом и проверками.
