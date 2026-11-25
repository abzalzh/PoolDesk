# Публикация PoolDesk 1.0 из Documents\Other projects\PoolDesk

Все файлы уже на местах. Не клонируй репозиторий заново, не распаковывай архив повторно и не выполняй `git rm`.

- `Source` — подготовленный Git-репозиторий с исходниками 1.0. Команды Git выполняй здесь.
- `Deploy` — готовые файлы для релиза и установщик.
- `Old Version` — старые версии для личного хранения. Эта папка не входит в Git.

Существующий репозиторий GitHub: https://github.com/abzalzh/PoolDesk . Git уже установлен (в твоём выводе: 2.56.0.windows.2).

## 1. Проверь подготовленные исходники

Открой PowerShell и выполни:

```powershell
$poolDesk = Join-Path $env:USERPROFILE 'Documents\Other projects\PoolDesk'
Set-Location -LiteralPath (Join-Path $poolDesk 'Source')
if (-not (Test-Path -LiteralPath '.\src\Window.cs')) { throw 'Открыта не папка Source PoolDesk 1.0' }
git remote get-url origin
git branch --show-current
git diff --cached --stat
```

Первые две команды Git должны показать `https://github.com/abzalzh/PoolDesk.git` и `main`. Последняя показывает подготовленную замену старого кода на 1.0. В Source должны быть `src`, `assets`, `tests`, `third_party`, `installer`, `docs`, `.github`, README и файлы проекта. Старые файлы в корне Git будут заменены, но история прошлых коммитов сохранится.

## 2. Отправь исходники на GitHub

Оставаясь в `Source`, выполни:

```powershell
git commit -m "Release PoolDesk 1.0"
if ($LASTEXITCODE -ne 0) { throw 'Commit не создан; проверь сообщение Git' }
git push origin main
if ($LASTEXITCODE -ne 0) { throw 'Push не выполнен; проверь сообщение Git' }
```

Если Git запросит имя и email, укажи свои данные **в этой папке** и повтори `commit`, затем `push`:

```powershell
git config user.name "Твой ник"
git config user.email "Твой email из GitHub"
```

Git может открыть вход в GitHub через браузер. После успешного push обнови страницу репозитория и проверь папки и README. Большие EXE/ZIP из `Deploy` не загружай в корень репозитория.

## 3. Создай релиз вручную

1. Открой https://github.com/abzalzh/PoolDesk → **Releases → Draft a new release**.
2. В **Choose a tag** введи `v1.0.0`, выбери создание тега для ветки `main`.
3. Название: **PoolDesk 1.0**. Описание скопируй из `Deploy\RELEASE-NOTES.md`.
4. Приложи из `Deploy` четыре файла: `PoolDesk-1.0.0-Setup.exe`, `PoolDesk-1.0.0-win-x64.zip`, `PoolDesk-1.0.0-source.zip`, `SHA256SUMS.txt`.
5. Не отмечай pre-release. Если доступен переключатель latest release, включи его. Нажми **Publish release**.

GitHub также автоматически покажет свои `Source code (zip/tar.gz)`; это нормально. Старый тег `v2.2.0` и прежние коммиты удалять не нужно. В описании старого релиза при желании можно написать, что это прототип и пользователям рекомендуется 1.0.

### Вместо ручного релиза: GitHub Actions

После успешного push можно дождаться зелёной проверки во вкладке **Actions** и запустить автоматическую сборку релиза:

```powershell
Set-Location -LiteralPath (Join-Path $poolDesk 'Source')
git tag v1.0.0
git push origin v1.0.0
```

Используй **один** способ: либо ручной релиз из пункта 3, либо тег для Actions. Workflow заново соберёт свои EXE/ZIP и свои SHA-256; не смешивай их с файлами локальной сборки из `Deploy`. Уже существующий релиз workflow не перезаписывает.

## 4. Установи себе и пользуйся

Запусти `Deploy\PoolDesk-1.0.0-Setup.exe`. Если установщик предложит Microsoft Visual C++ Runtime x64, оставь опцию включённой. .NET 8 уже включён в приложение.

Закрой osu!stable, вставь ссылку на турнир/коллекцию в PoolDesk, нажми **Открыть пул**, выбери стадию → **Добавить в osu!** → проверь название коллекции и нажми **Установить**. Если папка игры не определилась, открой **☰ → Настройки** и выбери папку с `osu!.exe`; путь запомнится. После установки открой выбор песни в osu! и при необходимости нажми F5. Подробные логи и история доступны через меню ☰.

## 5. Проверка контрольной суммы

```powershell
$deploy = Join-Path $poolDesk 'Deploy'
Get-FileHash -LiteralPath (Join-Path $deploy 'PoolDesk-1.0.0-Setup.exe') -Algorithm SHA256
Get-Content -LiteralPath (Join-Path $deploy 'SHA256SUMS.txt')
```

Сравни хеш установщика со строкой для него в `SHA256SUMS.txt`. Контрольная сумма проверяет целостность, но не заменяет подпись издателя. Этот релиз не подписан сертификатом; для подписи нужен твой сертификат Code Signing. Скриншоты в README уже добавлены и менять их для публикации не требуется.

Инструкция обновлена 25 ноября 2026 года для папки `Documents\Other projects\PoolDesk`.
