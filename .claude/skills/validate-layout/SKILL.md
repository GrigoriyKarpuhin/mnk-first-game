---
name: validate-layout
description: >
  Валидация построения карты Block C и графа комнат. Использовать, когда меняют
  геометрию уровня (BlockCPlayableLayout: FloorAreas/InteriorWalls/DoorCells/VoidAreas),
  генерируют/правят карту, или просят проверить герметичность комнат, наличие
  дверей, соответствие схемы прототипу, либо нарисовать граф комнат.
version: 1.0.0
---

# Валидация карты Block C (граф комнат)

Инструмент проверяет, что карта собрана корректно, и рисует граф комнат для сверки
с нарисованным прототипом. Логика — C# поверх того же грида, что и рантайм; запуск
через EditMode-тесты (гейт CI) или редакторное меню.

## Что проверяется

Источник истины — тайлы `GameGrid`, собранные из `BlockCPlayableLayout`. Комната =
связная область пола/укрытий, ограниченная стенами; дверь = проём-портал между
комнатами. Три проверки в `Assets/Scripts/Layout/LayoutValidator.cs`:

1. **«Комната — это комната»** (`ValidateRooms`): не вырождена (≥2 клеток), имеет
   дверь (не изолирована), без тупиковых дверей (пол с одной стороны) вне whitelist
   и без дверей-сирот (проём в сплошной стене). Тупик/сирота = дыра в стене →
   нарушение герметичности.
2. **Совпадение с прототипом** (`GraphMatchesPrototype`): граф комнат сверяется с
   `Assets/Scripts/Layout/LayoutPrototype.cs` (нарисованный прототип из
   `Design/BLOCK_C_BLOCKOUT_V02.md`). Ловит слитые комнаты (забыта стена),
   потерянные/неописанные комнаты и недостающие/лишние дверные связи.
3. **Достижимость** (`Reachability`): все комнаты достижимы от старта игрока с
   учётом лестниц между этажами (`LayoutPrototype.StairLinks`).

Герметичность отдельной «рамочной» проверкой не делается намеренно: за гридом всё —
Wall, поэтому брешь всегда проявляется как тупик/сирота или как слияние/лишнее
ребро в проверках выше (см. комментарий в `LayoutValidator`).

## Рисование графа

Граф рисуется в Mermaid: `Design/generated/room_graph.mmd` (узлы — комнаты с
центроидом, сплошные рёбра — двери, пунктир `stairs` — лестницы, пунктир `sealed` —
тупиковые двери, красные узлы — комнаты с нарушениями). Сверять визуально с
`Design/BLOCK_C_BLOCKOUT_F1_V02.svg` / `..._F2_V02.svg`.

## Как запускать

### Вариант A — EditMode-тесты (CI-гейт, без GUI)

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.4.10f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -runTests \
  -projectPath "/Users/brrdlam/projects/mnk-first-game" \
  -testPlatform EditMode \
  -testFilter "RoomGraphTests" \
  -testResults "/tmp/roomgraph-results.xml" \
  -logFile -
```

Заметки:
- Не передавать `-quit` вместе с `-runTests` (раннер выходит сам; код выхода 0 = все
  тесты зелёные).
- Проект не должен быть открыт в другом инстансе Unity (иначе лок). Версия редактора
  берётся из `ProjectSettings/ProjectVersion.txt`.
- Падение тестов печатает список нарушений из соответствующего метода валидатора.
- Тесты: `Assets/Tests/EditMode/RoomGraphTests.cs` (рядом с `LayoutIntegrityTests`).

### Вариант B — редакторное меню (визуальный прогон + экспорт графа)

В открытом проекте: меню **Game ▶ Layout ▶ Validate + Export Room Graph**. Сцена не
нужна. Пишет `Design/generated/room_graph.mmd` и полный отчёт в консоль (ошибки —
`LogError`). Реализация: `Assets/Editor/LayoutGraphMenu.cs`.

## Когда карту меняют намеренно

Если правка геометрии меняет граф осознанно (новая комната, новая дверь, убранная
стена), нужно привести прототип в соответствие, иначе `Graph_MatchesPrototype`
останется красным:

1. Прогнать вариант B (или тесты — в сообщении о падении видно, что разошлось).
2. Обновить `Assets/Scripts/Layout/LayoutPrototype.cs`:
   `Rooms` — имя + якорная (заведомо внутренняя) клетка + список соседей по именам;
   `StairLinks` — межэтажные лестничные связи. Имена брать из
   `Design/BLOCK_C_BLOCKOUT_V02.md`.
3. Перепрогнать тесты до зелёного.

## Файлы

- `Assets/Scripts/Layout/RoomGraph.cs` — построение графа заливкой из `GameGrid`.
- `Assets/Scripts/Layout/LayoutValidator.cs` — три проверки.
- `Assets/Scripts/Layout/LayoutPrototype.cs` — эталонный граф (прототип).
- `Assets/Scripts/Layout/RoomGraphExporter.cs` — рисование в Mermaid.
- `Assets/Editor/LayoutGraphMenu.cs` — меню запуска/экспорта.
- `Assets/Tests/EditMode/RoomGraphTests.cs` — EditMode-тесты.
