# Архитектура решения

Тренажёр проводника ВСМ на Unity 6 (URP), сборки под Windows и WebGL. Главный принцип: **сценарий целиком описывается данными**. Тексты, ветвление, эффекты на шкалы, разбор и расстановка персонажей лежат в JSON, код не знает конкретного сюжета.

Подробности формата сценария и движка - в [Docs/ScenarioEngine_RU.md](../../Docs/ScenarioEngine_RU.md).

## Слои

Решение разделено на четыре слоя с однонаправленными зависимостями:

| Слой | Сборка | Ответственность |
|---|---|---|
| **Данные** | `Assets/Data/Scenarios` | Сценарий (`*.json`), состав сцены (`Casts/*.cast.json`), строки UI и меню (`Ui/*.json`) |
| **Движок** | `Game.Scenarios.Core` | Чистый C# без зависимостей от Unity: граф узлов, условия, эффекты, шкалы, таймеры, триггеры, концовки, запись решений |
| **Представление** | `Game.Scenarios.Presentation` | Связка движка со сценой: диалоги, HUD, терминал ММТ, разбор, актёры, объекты и зоны в мире, сохранение результата |
| **Мир и персонажи** | VSM + `Game.Characters.*` | Модель поезда, ходьба от первого лица, взаимодействие, пассажиры (рассадка, анимации, мимика, внешность) |

Отдельно - **редакторские билдеры** (`Game.Scenarios.Editor`): собирают UI, меню и расстановку актёров в сцене из JSON. Сцена руками не правится и всегда воспроизводима.

## Диаграмма компонентов

```mermaid
flowchart TB
    subgraph Data["Данные (JSON)"]
        SJ["Сценарий<br/>UnattendedItem.json + части"]
        CJ["Состав сцены<br/>Casts/*.cast.json"]
        UJ["UI и меню<br/>ScenarioUiStrings.json, ScenarioMenu.json"]
    end

    subgraph Editor["Редактор Unity (Game.Scenarios.Editor)"]
        SB["ScenarioSceneBuilder<br/>UI сцены"]
        CB["ScenarioCastBuilder<br/>актёры, объекты, точки"]
        MB["ScenarioMenuBuilder<br/>меню, Build Settings"]
    end

    subgraph Core["Движок (Game.Scenarios.Core, чистый C#)"]
        LD["ScenarioLoader + ScenarioValidator"]
        SS["ScenarioSession<br/>граф узлов, хабы, триггеры, таймеры"]
        ST["ScenarioState<br/>шкалы, переменные, флаги, компетенции"]
        CE["ConditionEvaluator / EffectApplier"]
        RS["ScenarioResult<br/>решения, оценки, разбор"]
    end

    subgraph Pres["Представление (Game.Scenarios.Presentation)"]
        RN["ScenarioRunner<br/>оркестратор"]
        UI["UI: DialogueView, ScenarioHud, QuestTracker,<br/>TerminalView (ММТ), CardView, BreathingView,<br/>HintView, PauseView, DebriefView"]
        WD["Мир: ScenarioStarter, ScenarioActor,<br/>ScenarioInteractable, ScenarioZone,<br/>ScenarioWorldStates, ScenarioGuide, LookHighlighter"]
        SK["IScenarioResultSink<br/>LocalResultSink → JSON"]
    end

    subgraph World["Мир и персонажи"]
        VSM["VSM: VSMWalkController, VSMInteractor,<br/>VSMCursorMode, Input System"]
        PX["Пассажиры: Passenger, PassengerSpot,<br/>анимации, мимика, CharacterCustomizer"]
    end

    Backend[("Backend / LMS<br/>(после хакатона)")]

    SJ --> LD --> SS
    SS <--> ST
    SS --> CE --> ST
    SS --> RS
    UJ --> SB
    UJ --> MB
    CJ --> CB
    SB -.создаёт.-> UI
    CB -.расставляет.-> WD
    CB -.рассаживает.-> PX

    RN -- "Choose / Tick / Signal" --> SS
    SS -- "NodeEntered / ChoiceResolved / Ended" --> RN
    RN --> UI
    RN --> WD
    WD --> PX
    VSM -- "действие игрока" --> WD
    RN -- "IPlayerPlacement / IViewFocus" --> VSM
    RS --> RN --> SK
    SK -.HTTP.-> Backend
```

Зависимости: `Presentation → Core`, `VSM → Presentation`, `Presentation → Characters`. Core не знает ни о сцене, ни об UI, поэтому вся игровая логика покрыта EditMode-тестами (93 теста, включая по 30 случайных прохождений каждого варианта сюжета до концовки).

## Диаграмма последовательности: прохождение сценария

```mermaid
sequenceDiagram
    actor P as Игрок
    participant M as Меню (VSM_Menu)
    participant ST as ScenarioStarter
    participant R as ScenarioRunner
    participant S as ScenarioSession
    participant UI as UI (диалог, HUD, ММТ)
    participant W as Мир (актёры, объекты)
    participant K as IScenarioResultSink

    P->>M: выбирает карточку сценария
    M->>ST: загрузка сцены поезда (вариант сюжета)
    P->>W: подходит к пассажирке 3Г, нажимает E
    W->>ST: Interact()
    ST->>R: StartScenario(json)
    R->>S: Load + Validate, жребий варианта (A/B/C)
    S-->>R: NodeEntered(первый узел)
    R->>UI: реплика, варианты ответа

    loop Диалог и узлы выбора
        P->>UI: выбирает вариант
        UI->>R: OnOptionChosen(id)
        R->>S: Choose(id)
        S->>S: условия → эффекты (шкалы, флаги, компетенции),<br/>запись DecisionRecord, проверка триггеров
        S-->>R: ChoiceResolved(ответ NPC, изменения шкал)
        R->>UI: ответ персонажа, анимация шкал
        R->>W: изменения сцены (states), эмоции актёров
        S-->>R: NodeEntered(следующий узел)
    end

    Note over R,W: Хаб «в мире» (roam): диалог закрыт,<br/>цели в трекере, путь к цели по G
    P->>W: подходит к объекту (сумка, место 3Б, сосед)
    W->>R: Interact(target) / Signal(зона)
    R->>S: Choose(действие хаба) или Signal
    S-->>R: событие-прерывание (триггер) или возврат в хаб

    S-->>R: Ended(концовка по приоритету)
    R->>S: BuildResult()
    R->>UI: экран итогов: шкалы, оценки, разбор каждого решения
    R->>K: Save(ScenarioResult)
    K-->>K: ScenarioResults/<id>_<дата>.json
```

## Поток создания контента

```mermaid
flowchart LR
    A["Методист / сценарист<br/>правит JSON"] --> B["EditMode-тесты:<br/>валидатор + случайные прохождения"]
    B --> C{"Что изменилось?"}
    C -- "только сценарий" --> D["Play: JSON читается при старте"]
    C -- "состав / UI / меню" --> E["Game → Scenarios → Build / Setup Cast"] --> D
```

## Ключевые решения

- **Данные отделены от кода.** Новая развилка или вариант сюжета - правка JSON за несколько минут, без программиста и пересборки кода. Крупный сценарий делится на части через `include`.
- **Детерминированный движок.** Время двигается только через `Tick`, случайность только в жребии варианта, поэтому любое прохождение воспроизводимо в тестах.
- **Единое состояние-словарь** (`loyalty`, `safety`, `var.*`, `flag.*`, `comp.*`, `variant.*`, `visited.*`): одни и те же условия и эффекты работают для шкал, скрытых переменных и компетенций.
- **Мир общается с движком через интерфейсы** (`IScenarioInteractable`, `IPlayerPlacement`, `IViewFocus`, сигналы зон), поэтому модель поезда VSM можно заменить, не трогая движок.
- **Точка расширения для backend.** Результат прохождения уже сериализуется в JSON (сценарий, вариант, концовка, шкалы с оценками, компетенции, все решения с разбором, подсказки, длительность). Это контракт для будущего сервера: достаточно реализовать `IScenarioResultSink` с HTTP-отправкой.

## Стек

Unity 6 (URP), Input System, Cinemachine, VContainer, MessagePipe, UniTask, R3, DOTween, TextMesh Pro, Newtonsoft.Json. Персонажи - CharacterCustomizer и анимации Mixamo. Сборки: Windows и WebGL (хостинг в Yandex Object Storage).
