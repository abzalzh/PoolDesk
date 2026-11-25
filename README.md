# PoolDesk 1.0

**RU** · [English below](#english)

Минималистичная программа для Windows: вставь ссылку на osu!Collector, выбери стадию и установи карты вместе с коллекцией в **osu!stable**.

![PoolDesk, русский интерфейс](docs/screenshots/ru.png)

## Скачать и запустить

Открой [Releases](https://github.com/abzalzh/PoolDesk/releases) и скачай **PoolDesk-1.0.0-Setup.exe** или **PoolDesk-1.0.0-win-x64.zip**. ZIP нужно распаковать целиком, затем запустить `PoolDesk.exe`. Исходники предназначены для разработки, не для обычного запуска.

Windows 10/11 x64. .NET 8 включён в EXE — отдельно устанавливать .NET Framework не нужно. Для нативного расчёта звёзд нужен Microsoft Visual C++ Runtime x64. Установщик предлагает скачать его с Microsoft, проверяет цифровую подпись и запускает установку; запрос администратора относится к Microsoft Runtime. Для portable при необходимости используй [официальный установщик Microsoft](https://aka.ms/vc14/vc_redist.x64.exe).

1. Закрой osu!stable.
2. Вставь HTTPS-ссылку на турнир или коллекцию с osucollector.com.
3. Нажми **Открыть пул**, выбери стадию.
4. **Добавить в osu!** → проверь имя и галочку создания коллекции → **Установить**.
5. Открой osu!, перейди в выбор песни. Если карты ещё не видны, нажми F5.

Папка игры определяется автоматически, а выбранный путь запоминается. Если найдено несколько установок, выбери нужную. osu!lazer не поддерживается.

[Анимация интерфейса](docs/screenshots/preview.gif) · [Светлая тема](docs/screenshots/light.png)

## Возможности

- Русский / English, тёмная и светлая темы, собственная тёмная рамка без системного белого заголовка, масштабирование под DPI.
- Звёзды **с модами слота** в таблице; без модов и с модами рядом в карточке. Нажми строку для CS, AR, OD, HP, BPM и длительности.
- Расчёт rosu-pp 4.0.1 в режиме stable по проверенному `.osu`. FM/TB отображаются как NM со специальной пометкой: конкретные свободные моды неизвестны. Остальные параметры карточки — без модов. Значения могут отличаться от другой версии алгоритма osu!.
- Сканирование Songs, включая ожидающие импорта `.osz`, и пропуск уже имеющихся точных карт. Индекс ускоряет повторное сканирование.
- Сравнение MD5: при другой локальной версии предлагается поиск турнирной. Старые файлы сохраняются; если точной версии нет на зеркалах, она пропускается, а не подменяется.
- Параллельные загрузки, автоматический переход Nerinyan → Catboy → Sayobot, повторы с увеличением задержки, пауза / продолжение и отмена.
- Частичный файл докачивается, если сервер поддерживает Range и прислал подходящий ETag. Иначе загрузка безопасно начинается заново. После перезапуска выбери тот же пул и запусти операцию повторно: готовый кеш и локальные карты используются повторно. Это не фоновая очередь с автоматическим запуском.
- Недоступные карты и ошибки отдельных наборов не останавливают остальные загрузки. Неизвестный слот и исходная ссылка показываются в программе; подробности — в отдельном логе операции.
- Разные звуки завершения и ошибки, отключаемые в настройках.
- История установок и удаление пула. По выбору удаляются только неизменённые файлы, созданные PoolDesk; общие с другими пулами или коллекциями и изменённые файлы сохраняются. Карты, установленные до PoolDesk, не удаляются.
- Атомарная замена `collection.db`, проверка параллельных изменений, резервные копии и блокировка записи при запущенном osu!.
- Меню **☰**: настройки, история, открыть логи, скопировать диагностику, проверить обновления. Проверка обновлений открывает страницу нового релиза канала 1.x; автоматической установки обновлений нет.

**Настройки по умолчанию:** 3 одновременные загрузки, 3 попытки на зеркало, 10 резервных копий, кеш до 5 ГиБ, тёмная тема, звуки включены. Лимит кеша применяется после операции; при загрузке он может временно превышаться.

## Данные и восстановление

Настройки, кеш, история и логи находятся в `%LOCALAPPDATA%\PoolDesk`. Бэкапы лежат рядом с `collection.db`: `collection.db.pooldesk.<timestamp>.bak`. Перед ручным восстановлением закрой osu! и сохрани текущий файл отдельно. Логи и история могут содержать локальные пути и названия пулов — проверь их перед публикацией в Issues. Кнопка диагностики не добавляет личные пути.

Не удаляй папку History, если нужен автоматический откат. Удаление самой программы сохраняет пользовательские настройки, логи и историю. Установка, прерванная перед записью коллекции, остаётся в истории; уже записанные карты можно удалить оттуда. Если после аварии `collection.db` был изменён другой программой, автоматическое удаление останавливается и предлагает ручное восстановление.

Недоступные, удалённые и старые версии карт не всегда есть на зеркалах. PoolDesk не обходит вход, ограничения доступа или платные функции и не гарантирует наличие каждой карты. Загрузка карт, их музыка и изображения не входят в лицензию этого приложения.

## Разработка

Нужен .NET 8 SDK для Windows. Открой `PoolDesk.sln` в IDE или выполни:

```powershell
.\test.ps1
.\build.ps1
# Для установщика дополнительно нужен Inno Setup 6:
.\build.ps1 -Installer -Compiler 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
```

При запрете запуска локальных скриптов: `powershell -NoProfile -ExecutionPolicy Bypass -File .\test.ps1` (только для проверенных исходников).

`src/` — C#; `assets/` — иконка и манифест; `tests/` — автономные тесты и фикстуры; `third_party/` — нативная библиотека и лицензии; `installer/` — установщик; `docs/` — изображения; `.github/` — CI и шаблоны Issues. Сборки появляются в `artifacts/` и не коммитятся.

Тесты не обращаются к сети и не изменяют установленную игру: проверяют коллекции, ошибки/конфликты записи, бэкапы, скачивание через поддельный HTTP, MD5, докачку, параллельность, паузу, историю, архивы и UI. Фикстура API содержит метаданные; тестовые `.osu` генерируются самим тестом, музыка не включена. Настоящий API и доступность зеркал могут меняться независимо от этих тестов.

GitHub Actions собирает и проверяет проект. Тег `v1.0.0` запускает подготовку релизных файлов; процесс публикации описан в `PUBLISH-RU.md`. Скриншоты демонстрационные: названия и численные значения на них созданы для проверки интерфейса.

## Статус, авторство и лицензии

**Неофициальный независимый проект.** PoolDesk не связан с ppy, peppy, osu!, osu!Collector, их авторами, компаниями и другими продуктами, не одобрен и не спонсируется ими. Названия используются для указания совместимости. Цель проекта — упростить скачивание и установку карт и коллекций.

**Я понимаю и открыто указываю: собственный код и тексты PoolDesk на 100% созданы с помощью нейросети — проект «свайбкоджен».** Это не означает, что сторонние библиотеки созданы мной или ИИ. Код проверяется тестами, но отсутствие ошибок не гарантируется; сообщения об ошибках и независимая проверка приветствуются.

Собственный код — [MIT](LICENSE). Сторонние компоненты остаются под своими лицензиями: [уведомления](THIRD-PARTY-NOTICES.md), `third_party/`. Лицензия PoolDesk не предоставляет прав на чужие карты, музыку, изображения и товарные знаки. Используй материалы и внешние сервисы в соответствии с применимыми разрешениями и их условиями.

Релиз не подписан Authenticode: сертификат издателя не предоставлен. SHA-256 опубликованы отдельно для проверки целостности; они не заменяют подпись издателя. Не отключай защиту Windows ради запуска.

## Связь с разработчиком:
**Discord:** @scamming.org
**Telegram:** @scam0scam0scam0scam0scam0scam0

---

# English

**PoolDesk 1.0** is a small Windows app for downloading osu!Collector pools and installing their maps and collections into **osu!stable**.

![PoolDesk, English interface](docs/screenshots/en.png)

Download the setup EXE or portable ZIP from [Releases](https://github.com/abzalzh/PoolDesk/releases). Extract the entire ZIP before starting `PoolDesk.exe`. Windows 10/11 x64 is required. .NET 8 is included. Native star calculation requires Microsoft Visual C++ Runtime x64; setup offers a download from Microsoft, verifies its signature and launches its installer. The portable version may need the [Microsoft runtime](https://aka.ms/vc14/vc_redist.x64.exe).

Close osu!, paste an osu!Collector tournament/collection link, open the pool, select a stage, choose **Add to osu!**, review the collection name and install. Open song selection in osu! afterward and press F5 if necessary. The game folder is detected and remembered. osu!lazer is not supported.

Features include Russian/English UI, dark/light themes, custom dark window chrome, modded stars in the table and base/modded stars side by side, detailed map stats, existing-map detection, exact MD5 checks, parallel downloads, mirror fallback, retries, pause/resume, per-operation logs, sounds, install history and selective rollback. FM/TB use an explicitly marked NM calculation because specific free mods are unknown; other displayed stats are unmodded. Calculation uses rosu-pp 4.0.1 in stable mode and may differ from other osu! algorithm versions.

Defaults: **3 downloads, 3 attempts per mirror, 10 backups, 5 GiB cache, dark theme, sounds on.** Mirrors: Nerinyan, Catboy, Sayobot. Resume requires compatible Range/ETag responses; otherwise downloads restart. After restarting the app, reopen the same pool and start again to reuse cached/installed maps. Cache limits are applied after an operation, not as a hard in-flight quota.

Failed sets do not block other downloads. Unavailable tournament MD5 versions are skipped instead of silently replaced. Collection writes are atomic and blocked while osu! is running. Rollback preserves pre-existing, modified and shared maps. The ☰ menu provides settings, history, logs, path-free diagnostics and a manual check for newer 1.x releases (no automatic update installation).

Data lives in `%LOCALAPPDATA%\PoolDesk`; timestamped collection backups live beside `collection.db`. Logs/history may contain local paths: review before sharing. Keep History for rollback. Uninstalling the app keeps its user data. After an interrupted transaction, unexpected external collection changes stop automatic rollback; restore a backup manually with osu! closed if needed.

Build with .NET 8 SDK using `test.ps1` and `build.ps1`; add `-Installer` with Inno Setup 6 for setup output. The `.sln` and `.csproj` support normal IDE workflows. Offline tests use synthetic maps and HTTP responses, never your installed game. Network service availability is independent of those tests. Demo screenshots use synthetic data.

**Unofficial and independent:** not affiliated with, endorsed by or sponsored by ppy, peppy, osu!, osu!Collector, their authors or any of their products. Names identify compatibility only. This project exists to simplify map and collection installation.

**I openly acknowledge that 100% of PoolDesk's own code and text was generated with AI: this is a vibe-coded project.** This does not claim authorship of third-party libraries. Tests do not guarantee bug-free operation; independent review and bug reports are welcome.

## Contact with developer:
**Discord:** @scamming.org
**Telegram:** @scam0scam0scam0scam0scam0scam0

Own code is MIT licensed; third-party licenses and notices are retained. Maps, music, images and trademarks are not covered by this app's license. Respect applicable permissions and service terms; PoolDesk does not bypass access controls or paid features and cannot guarantee map availability.

This release is **not Authenticode-signed**. SHA-256 checksums verify file integrity, not publisher identity. A signing certificate is required for a signed future build.
