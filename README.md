# PremiumDock

Преміальна панель швидкого запуску для Windows, яку можна запустити як звичайний `.exe` файл.

## Можливості
- Топова скляна панель, що розміщується над панеллю задач по центру екрана.
- Перетягніть ярлики або програми зі столу в панель — вони збережуться між перезапусками.
- Клік по іконці відкриває відповідний застосунок чи папку.

1. Встановіть .NET 8 SDK.
2. Збірка:
   ```bash
   dotnet publish PremiumDock/PremiumDock.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
   ```
3. Готовий `PremiumDock.exe` буде у `PremiumDock/bin/Release/net8.0-windows/win-x64/publish/`.

## Запуск
Запускайте `PremiumDock.exe`, перетягніть ярлики/програми в панель та користуйтеся.
