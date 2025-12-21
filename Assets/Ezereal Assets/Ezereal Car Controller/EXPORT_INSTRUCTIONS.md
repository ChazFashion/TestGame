# Инструкция по переносу Ezereal Car Controller с мобильным управлением

## Вариант 1: Полный перенос (рекомендуется)

Скопируйте всю папку `Ezereal Assets/Ezereal Car Controller` в новый проект:

1. В текущем проекте найдите папку:
   ```
   Assets/Ezereal Assets/Ezereal Car Controller
   ```

2. Скопируйте всю папку в новый проект:
   ```
   [Новый проект]/Assets/Ezereal Assets/Ezereal Car Controller
   ```

3. Убедитесь, что скопированы все файлы, включая:
   - Scripts/ (все скрипты)
   - Prefabs/ (префабы)
   - Materials/ (материалы)
   - Meshes/ (модели)
   - Input Actions/ (настройки ввода)
   - MOBILE_SETUP_README.md (инструкция)

## Вариант 2: Минимальный перенос (только изменения)

Если у вас уже есть Ezereal Car Controller в новом проекте, скопируйте только измененные/новые файлы:

### Измененные файлы:
1. `Scripts/EzerealCarController.cs` - добавлены методы для мобильного управления
2. `Scripts/EzerealCameraController.cs` - добавлен мобильный режим камеры

### Новые файлы:
1. `Scripts/MobileCarController.cs` - контроллер мобильного управления
2. `Scripts/Joystick.cs` - виртуальный джойстик
3. `Scripts/MobileCarUICreator.cs` - создатель UI элементов
4. `MOBILE_SETUP_README.md` - документация

### Структура для копирования:
```
Assets/Ezereal Assets/Ezereal Car Controller/
├── Scripts/
│   ├── EzerealCarController.cs (ЗАМЕНИТЬ)
│   ├── EzerealCameraController.cs (ЗАМЕНИТЬ)
│   ├── MobileCarController.cs (НОВЫЙ)
│   ├── Joystick.cs (НОВЫЙ)
│   └── MobileCarUICreator.cs (НОВЫЙ)
└── MOBILE_SETUP_README.md (НОВЫЙ)
```

## Зависимости

Убедитесь, что в новом проекте установлены:

1. **TextMeshPro** (обычно уже есть в проекте)
   - Window → TextMeshPro → Import TMP Essential Resources

2. **Input System** (если используется)
   - Package Manager → Input System

3. **Unity UI** (обычно уже есть)
   - Package Manager → Unity UI

## После переноса

1. Откройте сцену с машиной
2. Добавьте компонент `MobileCarUICreator` на любой GameObject
3. Запустите игру - UI элементы создадутся автоматически
4. Или используйте контекстное меню: ПКМ на компоненте → "Create Mobile UI"

## Проверка

После переноса убедитесь, что:
- ✅ Все скрипты компилируются без ошибок
- ✅ В сцене есть машина с компонентом `EzerealCarController`
- ✅ Canvas создается автоматически при запуске
- ✅ Джойстик и кнопки появляются на экране

## Решение проблем

Если после переноса возникают ошибки:

1. **Ошибки компиляции**: Проверьте, что все using директивы на месте
2. **UI не появляется**: Убедитесь, что `Enable In Editor` включен в `MobileCarUICreator`
3. **Джойстик не работает**: Проверьте, что EventSystem создается автоматически

## Альтернативный способ: Unity Package

Вы можете создать Unity Package для удобного переноса:

1. В Project окне выберите папку `Ezereal Assets/Ezereal Car Controller`
2. ПКМ → Export Package...
3. Убедитесь, что выбраны все файлы
4. Сохраните .unitypackage файл
5. В новом проекте: Assets → Import Package → Custom Package...

