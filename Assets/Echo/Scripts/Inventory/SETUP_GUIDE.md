# Система Инвентаря — Инструкция по настройке в Unity

## Архитектура

```
InventoryGrid        — чистая логика сетки (не MonoBehaviour)
InventoryItem        — экземпляр предмета в сетке
ItemSO               — ScriptableObject данные предмета
InventorySystem      — MonoBehaviour-менеджер (на Player)
EquipmentSystem      — MonoBehaviour-система экипировки (на Player)
InventoryUI          — MonoBehaviour-менеджер UI (на Canvas)
InventoryItemUI      — MonoBehaviour-визуал предмета в сетке
PickupItem           — MonoBehaviour на объекте в мире
PlayerWeaponHolder   — существующий компонент (обновлён)
```

---

## Шаг 1 — Компоненты на Player

Добавьте на GameObject игрока (тот же, где уже есть `PlayerWeaponHolder`):
- `InventorySystem` (задайте `Grid Width` = 10, `Grid Height` = 6)
- `EquipmentSystem` (добавляется автоматически через `[RequireComponent]`)

---

## Шаг 2 — Создание UI в сцене

### 2.1 Canvas (если ещё нет)
`Hierarchy → правая кнопка → UI → Canvas`
- Render Mode: **Screen Space - Overlay**
- Добавьте компонент **GraphicRaycaster**
- В сцене должен быть **EventSystem** (создаётся автоматически)

### 2.2 Структура иерархии UI

```
Canvas
└── InventoryRoot            (пустой GameObject, компонент InventoryUI)
    ├── InventoryPanel       (Panel — тёмный фон)
    │   └── GridContainer    (пустой RectTransform, pivot = top-left)
    ├── ContextMenu          (Panel — контекстное меню)
    │   ├── BtnEquip         (Button + TextMeshProUGUI "Экипировать")
    │   ├── BtnUnequip       (Button + TextMeshProUGUI "Снять")
    │   └── BtnDrop          (Button + TextMeshProUGUI "Выбросить")
    ├── Tooltip              (Panel — тултип)
    │   ├── TooltipName      (TextMeshProUGUI)
    │   ├── TooltipDesc      (TextMeshProUGUI)
    │   └── TooltipSize      (TextMeshProUGUI)
    └── EquipmentDisplay     (опционально — слот оружия)
        ├── WeaponIcon       (Image)
        └── WeaponName       (TextMeshProUGUI)
```

### 2.3 Настройка GridContainer
- Anchor: **Top-Left** (anchorMin = (0,1), anchorMax = (0,1))
- Pivot: **(0, 1)** — верхний левый угол
- Размер задаётся автоматически скриптом

---

## Шаг 3 — Создание префабов

### 3.1 SlotPrefab (ячейка фона)
1. `Hierarchy → UI → Image`
2. Размер: **64×64** px
3. Цвет: тёмно-серый с чуть-чуть прозрачным (например `#2A2A2A CC`)
4. Добавьте **Outline** (белый, 1px) — опционально
5. Перетащите в `Assets/Echo/Prefabs/` как `InventorySlot.prefab`

### 3.2 ItemPrefab (визуал предмета)
1. `Hierarchy → UI → Image` (это корень)
2. Добавьте компонент **InventoryItemUI**
3. Дочерние объекты:
   - **Icon** — Image (иконка, растянуть на весь родитель, Preserve Aspect = true)
   - **StackCount** — TextMeshProUGUI (в правом нижнем углу, размер ~12px, цвет белый с тенью)
   - **EquippedHighlight** — Image (зелёная/жёлтая рамка, по умолчанию скрытая)
   - **HoverHighlight** — Image (белая полупрозрачная заливка, по умолчанию скрытая)
4. В компоненте **InventoryItemUI** заполните ссылки: Icon Image, StackCount TMP, оба Highlight
5. Добавьте **CanvasGroup** на корень для удобного затемнения
6. Перетащите в `Assets/Echo/Prefabs/` как `InventoryItem.prefab`

**Важно:** ItemPrefab должен иметь компонент `InventoryItemUI` на корневом объекте!

---

## Шаг 4 — Настройка InventoryUI

На объекте `InventoryRoot` в инспекторе заполните поля:

| Поле | Что назначить |
|------|--------------|
| Inventory Panel | InventoryPanel (GameObject) |
| Grid Container | GridContainer (RectTransform) |
| Cell Size | 64 |
| Cell Gap | 2 |
| Slot Prefab | InventorySlot.prefab |
| Item Prefab | InventoryItem.prefab |
| Context Menu Panel | ContextMenu (GameObject) |
| Btn Equip | BtnEquip (Button) |
| Btn Unequip | BtnUnequip (Button) |
| Btn Drop | BtnDrop (Button) |
| Tooltip Panel | Tooltip (GameObject) |
| Tooltip Name | TooltipName (TMP) |
| Tooltip Description | TooltipDesc (TMP) |
| Tooltip Size | TooltipSize (TMP) |
| Equipped Weapon Icon | WeaponIcon (Image) — опционально |
| Equipped Weapon Name | WeaponName (TMP) — опционально |

---

## Шаг 5 — Создание ItemSO (данные предмета)

`Assets → правая кнопка → Create → Inventory → Item`

Поля:
- **Item Name** — название
- **Icon** — Sprite иконки (тип текстуры = Sprite)
- **Item Type** — Weapon / Ammo / Health / Key / Quest / Misc
- **World Prefab** — GameObject в мире (с компонентом Weapon для оружия)
- **Size X / Size Y** — размер в ячейках (например, пистолет: 2×1, снайперка: 4×2)
- **Is Stackable** — например, для патронов: true
- **Max Stack Size** — максимум в стаке (для патронов: 30, 100 и т.д.)
- **Description** — текст описания

---

## Шаг 6 — Настройка PickupItem (объект в мире)

Добавьте компонент `PickupItem` на объект в мире:
- **Item Data** — назначьте нужный ItemSO
- **Amount** — количество
- **Auto Pickup** — подбирать автоматически при сближении (без E)
- **Pickup Range** — радиус подбора
- Добавьте `Collider` с `Is Trigger = true` нужного размера

---

## Шаг 7 — Управление

| Клавиша | Действие |
|---------|---------|
| **I** | Открыть/закрыть инвентарь |
| **E** | Подобрать предмет (при нахождении в зоне триггера) |
| **ПКМ** по предмету | Контекстное меню (Экипировать / Снять / Выбросить) |

---

## Пример: Создание пистолета в инвентаре

1. Создайте `ItemSO` → назовите `PistolItem`
   - Name: "Пистолет", Type: Weapon, SizeX: 2, SizeY: 1, Stackable: false
   - World Prefab: ваш префаб пистолета с компонентом `Weapon`

2. Создайте в сцене пустой GameObject (место где лежит пистолет)
   - Добавьте `PickupItem`, назначьте `PistolItem` в Item Data
   - Добавьте `BoxCollider`, включите `Is Trigger`
   - Опционально добавьте визуальную модель пистолета как дочерний объект

3. При подходе игрока нажмите **E** — пистолет появится в инвентаре
4. Откройте инвентарь **I** → нажмите **ПКМ** на пистолет → **Экипировать**

---

## Стек патронов — пример

1. `ItemSO` → `AmmoRifle9mm`
   - Name: "Патроны 9мм", Type: Ammo, SizeX: 1, SizeY: 1
   - Is Stackable: **true**, Max Stack Size: **60**

2. PickupItem: Amount = 15 (подберёт 15 патронов за раз)
3. Повторный подбор автоматически добавит в существующий стак

---

## Типичные ошибки

- **"InventorySystem.Instance не найден"** → добавьте `InventorySystem` на Player
- **"itemPrefab не содержит InventoryItemUI"** → проверьте что ItemPrefab имеет компонент `InventoryItemUI` на корне
- **Предметы не отображаются** → проверьте что `GridContainer` назначен в InventoryUI
- **ПКМ не работает** → убедитесь что Canvas имеет `GraphicRaycaster` и в сцене есть `EventSystem`
- **Курсор не разблокируется** → проверьте что `InventorySystem` есть на сцене и инвентарь открывается (лог в Console)
