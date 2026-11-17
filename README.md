# ЛР №2* — Робота з файлами XML (з Google Drive)

**Варіант 12:** «Індивідуальна робота кафедри зі студентами».
**Виконав:** Максимов Гліб, група К-28.

Застосунок MAUI (Windows) аналізує XML-файл трьома способами (SAX, DOM, LINQ to XML), перетворює його в HTML за XSL-таблицею, зберігає результати на Google Drive і веде протокол подій.

## Як запустити

Потрібні .NET 9 SDK і робоче навантаження `maui-windows`.

```bash
dotnet build IndividualWork.App
```

```bash
dotnet run --project IndividualWork.App -f net9.0-windows10.0.19041.0
```

Або відкрийте `IndividualWork.sln` у Visual Studio й запустіть проєкт `IndividualWork.App`.

Тести:

```bash
dotnet test IndividualWork.Tests
```

## Як користуватися

1. **Відкрити XML…** — оберіть `Data/individual_work.xml`. Списки «Студент» і «Група» заповняться значеннями з файлу.
2. **Відкрити XSL…** — оберіть `Data/individual_work.xsl`.
3. Задайте параметри пошуку (будь-яку комбінацію), оберіть спосіб аналізу, натисніть **Пошук**.
4. **Трансформувати в HTML** — HTML з'явиться у вікні, і програма запропонує зберегти його у файл. Якщо пошук уже виконано, перетворюється відфільтрований фрагмент, інакше — весь документ.
5. **XML на Drive / HTML на Drive** — зберігає відфільтрований фрагмент на Google Drive.
6. **Зберегти вхідні файли на Drive**, **XML з Drive…**, **XSL з Drive…** — зберігання та читання вхідних файлів на Google Drive.
7. **Clear** — очищує поле виводу та параметри пошуку.
8. **Протокол подій** — показує файл протоколу.
9. **Вихід** або закриття вікна — із підтвердженням.

Для Google Drive потрібен файл `client_secret.json` — див. [GOOGLE_DRIVE_SETUP.md](GOOGLE_DRIVE_SETUP.md).

## Відповідність умові

| Вимога | Де реалізовано |
|---|---|
| XML за варіантом 12 | `Data/individual_work.xml` |
| Різна кількість атрибутів у головного вузла | вузол `Work`: від 3 до 9 атрибутів |
| Не менше 2 рівнів | `IndividualWork → Work → Questions → Question → Answer` |
| Аналіз SAX / DOM / LINQ to XML | `IndividualWork.Core/Search/*SearchStrategy.cs` |
| Шаблон «Стратегія» | `IXmlSearchStrategy` + контекст `XmlAnalyzer` |
| Пошук за ключовими словами, динамічні запити | `SearchCriteria`; запит збирається лише із заданих умов (напр. `DomSearchStrategy.BuildXPath`) |
| Трансформація в HTML за XSL | `Transform/XslTransformer.cs`, `Data/individual_work.xsl` |
| «Підвантаження даних» | `MainViewModel.LoadXml` заповнює списки значень атрибутів `student` і `group` |
| Підтвердження виходу | `MainViewModel.ConfirmExitAsync`, `MauiProgram.ConfirmExitOnClose` |
| Кнопка Clear | `MainViewModel.Clear` |
| Без «магічної кнопки» | MVVM: у `MainPage.xaml.cs` немає обробників подій |
| Google Drive (ЛР2*) | `Storage/GoogleDriveStorage.cs` |
| «Фабричний метод» (ЛР2*) | `Export/FragmentExporter.cs`: `CreateWriter()` у `XmlFragmentExporter` і `HtmlFragmentExporter` |
| «Одинак», протокол подій (ЛР2*) | `Logging/EventLogger.cs`; три рівні: Фільтрація, Трансформація, Збереження |

## Структура

- `IndividualWork.Core` — уся логіка: модель, стратегії пошуку, трансформація, експорт, протокол, Google Drive, модель подання.
- `IndividualWork.App` — вікно MAUI та служби діалогів і файлів.
- `IndividualWork.Tests` — модульні тести (xUnit).
- `Data` — приклад XML і XSL.

Протокол подій: `%LOCALAPPDATA%\IndividualWorkXml\events.log`.
