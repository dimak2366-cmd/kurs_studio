Length limit reached. DeepSeek can only read the first 25%.
# Пошаговый план доработки проекта

Ниже — конкретный порядок работ, от «ядра» к «косметике». Для каждого шага: цель, файлы, что сделать, критерий готовности. Порядок рассчитан так, чтобы после шага 1–3 у вас уже был защищаемый продукт, а шаги 4–8 можно было делать «в хвосте».

---

## Шаг 0. Гигиена и подготовка (30 мин)

1. Удалить `Client/bin/Debug/net10.0-windows` и прочий мусор из `bin/obj`.
2. Привести порт к одному значению: в `Server/Properties/launchSettings.json` поставить `applicationUrl: "http://localhost:5155"` (как в `Client/appsettings.json` и в `Server/appsettings.json`).
3. Написать `reset-db.ps1` в корне:
   ```powershell
   psql -U postgres -c "DROP DATABASE IF EXISTS budget_institute;"
   psql -U postgres -c "CREATE DATABASE budget_institute;"
   dotnet ef database update --project Server --startup-project Server
   ```
4. Зафиксировать в `_GLOBAL_CONTEXT.md` фактический стек: **ASP.NET Core Web API + EF Core + PostgreSQL, WPF-клиент**, чтобы дальше править тексты глав от этого факта.

**Готово, если:** `dotnet run --project Server` слушает 5155, `reset-db.ps1` проходит без ошибок, клиент логинится под `admin`.

---

## Шаг 1. Ядро курсовой: факторный анализ (2–3 ч) ⚠ критично

Сейчас `ActualExpense` не хранит количество, поэтому в `AnalysisController` стоит `qFact = qPlan`, и вся разница уходит в цену. Это главный содержательный провал.

### 1.1. Модель данных
- `Server/Models/ActualExpense.cs` — добавить `public decimal Quantity { get; set; }`.
- Миграция:
  ```
  dotnet ef migrations add AddQuantityToActualExpense --project Server --startup-project Server
  dotnet ef database update --project Server --startup-project Server
  ```
- В `AppDbContext` для `ActualExpense.Quantity` — `HasPrecision(18, 3)`.
- В `Shared/Dtos/ExpenseDtos.cs`: в `CreateActualExpenseRequest`, `UpdateActualExpenseRequest`, `ActualExpenseDto` добавить `Quantity`.
- `Client/Views/Accountant/ActualExpenseEditDialog.xaml` — поле «Количество»; `.xaml.cs` — валидация `> 0`.

### 1.2. Сервис анализа
В `Server/Services/AnalysisService.cs` реализовать честные цепные подстановки:
```csharp
// Pp = PlanAmount / Qp (если Qp == 0 — Pp = 0, вклад ΔQ обнуляется)
// Pf = Sf / Qf
// ΔQ = (Qf - Qp) * Pp
// ΔP = Qf * (Pf - Pp)
// Сумма = Sf - Sp (проверять в Debug.Assert)
```
Для статьи «ФОТ» `Quantity = численность`, `Price = средняя зарплата` — тот же алгоритм без отдельной ветки. В `AnalysisController` убрать все заглушки `qFact = qPlan`.

### 1.3. Демо-данные
В `Server/Data/DbSeeder.cs` добавить сценарий из таблицы 1 главы:
- Материалы: план 100 × 50 = 5000, факт 120 × 60 = 7200 → ΔQ = 1000, ΔP = 1200, итого +2200.
- ФОТ: план 10 × 3000 = 30000, факт 11 × 3200 = 35200 → ΔQ = 3000, ΔP = 2200, итого +5200.
Это то, что вы покажете на защите.

### 1.4. Юнит-тест
В `Tests/AnalysisServiceTests.cs`:
- `FactorAnalysis_Materials_ReturnsExpectedDeltas()` — проверяет 1000 и 1200;
- `FactorAnalysis_SumsMatchTotalDeviation()` — для обоих примеров;
- `FactorAnalysis_ZeroPlanQuantity_DoesNotCrash()`.

**Готово, если:** на экране `AnalysisView` для Материалов видно «влияние количества = 1000, влияние цены = 1200», тесты зелёные, `dotnet test` проходит.

---

## Шаг 2. Ролевой доступ по всей системе (2 ч) ⚠ D13

Сейчас часть `[AuthorizeRole]` без ролей = «любой вошедший».

### 2.1. Единый guard
Создать `Server/Security/AccessGuard.cs`:
```csharp
public static class AccessGuard
{
    public static void EnsureDepartmentAccess(HttpContext ctx, int? smetaDepartmentId)
    {
        var role = ctx.Items["RoleName"] as string;
        if (role != "Руководитель отдела") return;
        var myDept = ctx.Items["DepartmentId"] as int?;
        if (smetaDepartmentId != myDept)
            throw new UnauthorizedAccessException("Чужая смета отдела.");
    }
}
```
`ExceptionMiddleware` → 403 для этого исключения.

### 2.2. Пройтись по контроллерам
| Контроллер | Что сделать |
|---|---|
| `DecisionsController.GetAll` | роли: Директор, Главный экономист, Администратор |
| `PeriodsController.GetAll` | роли: Бухгалтер, Главный экономист, Директор, Администратор |
| `LimitsController.Get/Check` | добавить `AccessGuard.EnsureDepartmentAccess` для Head |
| `AnalysisController` | то же + фильтр по отделу для Head |
| `ReportsController`, `RequestsController`, `ActualExpensesController` | то же для Head |

### 2.3. Тест таблицы 3 «роли ↔ функции»
В `Tests` — `AuthorizationMatrixTests.cs`: для каждой (эндпоинт × роль) ожидание 200/403. Достаточно 15–20 кейсов, но это лучший аргумент на защите.

**Готово, если:** Head не видит чужую смету ни в анализе, ни в лимитах, ни в расходах, ни в отчётах; матрица тестов зелёная.

---

## Шаг 3. Лимиты с утверждением (D2) и пояснения к отклонениям (2–3 ч)

### 3.1. Модель
`ExpenseItem`:
```csharp
public string LimitStatus { get; set; } = "Предложен"; // Предложен/Утверждён/Возвращён
public string? LimitReturnComment { get; set; }
```
Миграция. В `LimitService.SetLimitsAsync` при роли «Главный экономист» ставить `LimitStatus = "Предложен"`; при «Директор» на новом эндпоинте `POST /api/limits/{smetaId}/approve` — «Утверждён». В расчёте остатка (`CheckExcessAsync`) учитывать только «Утверждён».

### 3.2. Экран директора
`Client/Views/Director/LimitsApprovalView.xaml` + `LimitsApprovalViewModel.cs` (по образцу `SmetaApprovalView`). Пункт меню в `MainShellViewModel` для роли «Директор».

### 3.3. Пояснения к отклонениям
- Сущность `Server/Models/DeviationExplanation.cs`: `ExpenseItemId, PeriodId, UserId, Text, CreatedAt`.
- Контроллер `ExplanationsController`: POST (Head), GET по smetaId.
- DTO в `Shared/Dtos/ExplanationDtos.cs`.
- В `ReportService.CreateAsync` подмешивать пояснения в текст отчёта.
- Клиент: `Client/Views/Head/HeadExplanationsView.xaml` с кнопкой «Добавить пояснение».

### 3.4. Ещё три экрана Head
- `HeadDeviationsView` — из `AnalysisController` с фильтром по отделу.
- `HeadFactorAnalysisView` — переиспользовать `AnalysisView` с параметром «только моя смета».
- `HeadLimitsView` — `GET /api/limits/{smetaId}/check` для своей сметы.

**Готово, если:** под Head в левом меню 7 пунктов, лимит становится «Утверждён» только после директора, пояснения попадают в отчёт.

---

## Шаг 4. Выгрузка в файл (1–2 ч, закрывает 4 функции)

- `Server/Services/ExportService.cs` — формирование CSV с BOM и разделителем `;` (Excel → кириллица корректно).
- Эндпоинты:
  - `GET /api/reports/{id}/export` — уже есть `Download`, оформить как «Сохранить в файл».
  - `GET /api/expenses/export?periodId=`
  - `GET /api/analysis/export?smetaId=`
  - `GET /api/smeta/{id}/export`
- Клиент: общий хелпер `Client/Services/FileSaver.cs` с `SaveFileDialog`, кнопка «Выгрузить» в каждом View.

**Готово, если:** все 4 отчёта открываются в Excel без кракозябр, дата/суммы в ru-BY.

---

## Шаг 5. Безопасность и AES (1–2 ч)

1. **Включить HTTPS** в `Program.cs` (`app.UseHttpsRedirection()`), в `Client/appsettings.json` — `ServerUrl: "https://localhost:5xxx"`. В главе либо подтвердить TLS, либо явно писать «демонстрация AES в эндпоинте `/login-encrypted`».
2. **Убрать статический IV.** В `AesEncryptor.Encrypt`: генерировать `RandomNumberGenerator.GetBytes(16)` на каждый вызов, класть в начало шифртекста. `Decrypt` читает IV из первых 16 байт. Юнит-тест: два шифрования одного текста дают разные результаты, оба расшифровываются.
3. **Ключ из `appsettings.json`** перенести в `dotnet user-secrets` или переменные окружения; в `appsettings.json` оставить `Security:AesKeyBase64: null` и загрузку из `IConfiguration` с fallback на секрет.
4. В `README` (или в главе 3) — пункт «пароль Postgres `1234` и `Tests/credentials.json` — только для учебного стенда».

**Готово, если:** клиент ходит по `https://`, `AesEncryptorTests` — зелёные, ключ не в git.

---

## Шаг 6. Тесты — расширяем до 10–12 (1–2 ч)

`Tests/`:
1. `AnalysisServiceTests` — 3 теста из шага 1.
2. `PasswordHasherTests` — `SaltUnique`, `Verify_OK`, `Verify_Wrong`.
3. `AesEncryptorTests` — `RandomIv`, `Roundtrip`.
4. `AuthServiceTests` — блокировка после `MaxFailedAttempts`.
5. `PeriodServiceTests` — закрытие с несверенными расходами (в роадмапе D12).
6. `LimitServiceTests` — D14 (превышение лимита по расходу: помечаем, не блокируем).
7. `AuthorizationMatrixTests` — шаг 2.3.

**Готово, если:** `dotnet test` — 12+ зелёных, время выполнения < 20 с.

---

## Шаг 7. Полировка UI (3–4 ч, делать после шагов 1–6)

1. `Client.csproj` — добавить `LiveChartsCore.SkiaSharpView.WPF` (v2.x). В `AnalysisView.xaml` — 3 графика: план/факт по статьям, структура расходов (пирог), факторы (столбики ΔQ / ΔP, как рис. 2 главы).
2. Единый формат BYN во всех гридах: конвертер или `StringFormat={}{0:N2}` с `xml:lang="ru-BY"` в `App.xaml`.
3. Поиск и сортировка на всех `DataGrid` — `CollectionViewSource` + `IsReadOnly="True"` + `CanUserSortColumns="True"`. Минимум: Catalog (уже), Users, Projects, Smetas, Expenses.
4. Экран «Профиль» + «Сменить пароль» — backend (`/api/auth/change-password`) уже есть, нужен `Client/Views/ProfileView.xaml` + кнопка в `MainShell`.
5. Таймер бездействия: `DispatcherTimer` на `SessionTimeoutMinutes`, блокирующий диалог с повторным вводом пароля.
6. Экран «Справка» — заменить `PlaceholderView` на статический текст.

**Готово, если:** на `AnalysisView` виден график с теми же цифрами, что в таблице (1000/1200), гриды сортируются, пароль меняется из клиента.

---

## Шаг 8. Согласование документации с кодом (2–3 ч) ⚠ обязательный

Не откладывать до ночи перед защитой.

1. **Глава 1.3** — переписать: REST, Kestrel + async, EF Core + Npgsql, токены в `TokenService`. Убрать «TCP, консольный многопоточный сервер, ADO.NET».
2. **`Планируемый_функционал_бюджет_НИИ.docx`** — то же самое + пересчитать «53 функции» по факту.
3. **`Docs/roadmap.md`** — обновить, отметить сделанные пункты, добавить: D2 (утверждение лимитов), D13 (расширенный guard), D14 (мягкая схема расходов / жёсткая для заявок — одной строкой), выгрузка в файл, пояснения Head.
4. **`Docs/Глава_2_Проектирование.md`** — если нет ER-диаграммы `DeviationExplanation` и `LimitStatus` — дописать; проверить 3НФ (`catalog_expense_items` vs `expense_items` — не дублировать, у вас `expense_items` ссылается на конкретную смету, а `catalog_expense_items` — справочник, это нормально, но описать).
5. **`Docs/Глава_3_Тестирование.md`** — добавить результаты новых тестов (шаг 6), таблицу «роли × эндпоинты».
6. Если форм смет Минфина нет — в «Ограничения» главы 1; если есть — пункт про экспорт.

**Готово, если:** открыли код → увидели REST/EF/HTTPS; открыли главу 1 → там то же самое; число функций в плане совпадает с реализованным.

---

## Шаг 9. Репетиция защиты (1 ч)

1. `reset-db.ps1` → `dotnet run --project Server` → запуск клиента.
2. Пройти сценарий: `admin` создаёт пользователей → `economist` создаёт смету с Материалами 100×50 → `director` утверждает → `accountant` вводит факт 120×60 → `economist` показывает факторный анализ (1000/1200) → выгружает отчёт → `head` добавляет пояснение → `director` утверждает отчёт.
3. Показать `dotnet test` — 12+ зелёных.
4. Отдельно отрепетировать ответы на D13 (чужая смета), D14 (превышение лимита), D29 (правка закрытого периода — сервер отдаёт 409, а не только кнопка disabled). Если на сервере проверки `IsClosed` нет — добавьте в `ExpenseService` до репетиции.

---

## Итоговая последовательность и оценка

| # | Шаг | Часы | Критичность |
|---|---|---|---|
| 0 | Гигиена | 0.5 | высокая |
| 1 | Факторный анализ | 2–3 | **критично** |
| 2 | Guard по ролям | 2 | **критично** |
| 3 | Лимиты + пояснения + Head | 2–3 | высокая |
| 4 | Экспорт | 1–2 | средняя |
| 5 | HTTPS + AES | 1–2 | средняя |
| 6 | Тесты | 1–2 | **критично** для оценки |
| 7 | UI-полировка | 3–4 | средняя |
| 8 | Документация | 2–3 | **критично** |
| 9 | Репетиция | 1 | **критично** |

Итого ~15–22 ч. Если времени в обрез: **0 → 1 → 2 → 6 → 8 → 9** — это минимальный защищаемый набор (именно в этом порядке главы и тесты не разойдутся с кодом). Шаги 3, 4, 5, 7 — по остатку.

Если пришлёте `AnalysisService.cs`, `AnalysisController.cs`, `ActualExpenseEditDialog.xaml.cs` и `DbSeeder.cs`, я подготовлю конкретные патчи под шаг 1, чтобы вы только вставили код.
