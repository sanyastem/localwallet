# LocalWallet

Простое приложение для учёта личных финансов под Android (.NET 10 MAUI).

## Возможности (MVP)

- Учёт доходов и расходов по категориям
- Несколько счетов в разных валютах
- Автоконвертация валют через NBP (Narodowy Bank Polski) + frankfurter.app как fallback
- Биометрическая защита входа
- Графики и статистика
- Экспорт в CSV
- Удаление данных за выбранный период
- Локальное хранение (SQLite на устройстве)
- Базовая валюта по умолчанию: **PLN**

## Дальнейшие этапы (вне MVP)

- Семьи и совместные бюджеты с сильным E2E шифрованием
- Синхронизация между устройствами по локальной сети (mDNS + Noise handshake)
- Без серверов — прямое P2P

## Как получить APK

1. Пуш в `master` — запускается GitHub Actions workflow
2. Открыть вкладку **Actions** → последний успешный run → раздел **Artifacts**
3. Скачать `LocalWallet-apk.zip` (можно прямо с телефона)
4. Распаковать, установить APK (разрешить установку из неизвестных источников)

## Локальная разработка

Требуется: .NET 10 SDK + MAUI workload + Android SDK.

```bash
dotnet workload install maui-android
dotnet restore
dotnet build src/LocalWallet/LocalWallet.csproj -f net10.0-android -c Debug
```

## Структура

```
src/LocalWallet/
├── Models/              # Сущности SQLite
├── Services/            # БД, курсы валют, биометрия, экспорт, настройки
├── ViewModels/          # MVVM через CommunityToolkit.Mvvm
├── Views/               # XAML-страницы
├── Converters/          # XAML value converters
├── Resources/           # Стили, иконки, шрифты
└── Platforms/Android/   # MainActivity + AndroidManifest
```
