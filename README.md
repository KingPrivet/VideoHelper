# Video Helper

Copyright (c) 2026 KingPrivet. **All rights reserved.**

WinUI 3 приложение для склейки MP4-частей в одно видео без повторного кодирования.

## Использование

FFmpeg и необходимые компоненты .NET/Windows App SDK входят в автономную публикацию.

1. Запустите `VideoHelper.exe` из папки публикации.
2. Добавьте минимум две MP4-части или перетащите их в окно.
3. Проверьте порядок файлов и нажмите «Склеить видео».
4. Выберите имя итогового MP4.

Приложение использует concat demuxer FFmpeg и параметр `-c copy`, поэтому качество не меняется.

## Сборка

Нужны Windows x64, .NET SDK с поддержкой .NET 8 и компоненты сборки WinUI 3.
Поместите `ffmpeg.exe` в `VideoHelper/Tools/ffmpeg.exe` — бинарный файл не хранится в Git.

```powershell
dotnet publish VideoHelper/VideoHelper.csproj -c Release -r win-x64 --self-contained true -o publish
```

Установщик собирается Inno Setup 6 из `VideoHelper.iss` после публикации приложения.

## Права

Исходный код, интерфейс и документация Video Helper — собственность KingPrivet.
Копирование, изменение, использование и распространение требуют письменного разрешения автора.
Просмотр репозитория не предоставляет лицензию на эти материалы. См. [LICENSE.txt](LICENSE.txt).

Сторонние компоненты, включая FFmpeg, .NET и Windows App SDK, имеют собственные лицензии.
