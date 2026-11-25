# Как опубликовать PoolDesk 1.0 без GitHub Desktop

Репозиторий уже существует: https://github.com/abzalzh/PoolDesk . Создавать второй не нужно. Ниже — обновление через обычный Git и публикация готовых файлов. Файлы исходного ZIP лежат прямо в его корне; подпапки сохраняются при распаковке.

## 1. Подготовь файлы

Скачай `PoolDesk-1.0.0-source.zip`, `PoolDesk-1.0.0-Setup.exe`, `PoolDesk-1.0.0-win-x64.zip`, `SHA256SUMS.txt`. Положи их в «Загрузки». Не загружай ZIP исходников через Upload files вместо исходного проекта: GitHub не распаковывает такой ZIP.

Проверь Git в PowerShell:

```powershell
git --version
```

Если команда неизвестна, установи Git for Windows с https://git-scm.com/download/win и открой новое окно PowerShell.

## 2. Создай новую локальную копию

Эти команды не трогают старую папку `PoolDesk-git`. Для новой копии используется уникальное имя.

```powershell
$releaseRepo = Join-Path $env:USERPROFILE ('Documents\PoolDesk-1.0-upload-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$sourceZip = Join-Path $env:USERPROFILE 'Downloads\PoolDesk-1.0.0-source.zip'
if (-not (Test-Path -LiteralPath $sourceZip)) { throw 'Не найден source.zip в Загрузках. Исправь путь $sourceZip.' }
git clone https://github.com/abzalzh/PoolDesk.git $releaseRepo
if ($LASTEXITCODE -ne 0) { throw 'Не получилось клонировать репозиторий' }
Set-Location -LiteralPath $releaseRepo
```

## 3. Замени старую версию чистыми исходниками

Команда `git rm` ниже удаляет старые отслеживаемые файлы **только в свежей копии**, чтобы в корне не остались дубликаты. История Git и папка `.git` сохраняются, опубликованный репозиторий пока не меняется.

```powershell
if ((git remote get-url origin) -ne 'https://github.com/abzalzh/PoolDesk.git') { throw 'Это другая папка репозитория' }
git rm -r -- .
if ($LASTEXITCODE -ne 0) { throw 'Остановлено: старые файлы не удалены' }
Expand-Archive -LiteralPath $sourceZip -DestinationPath $releaseRepo -Force
if (-not (Test-Path .\src\Window.cs)) { throw 'Неверная структура ZIP: src\Window.cs не найден' }
Get-ChildItem -Force
git add --all
git diff --cached --stat
```

В корне должны быть `src`, `assets`, `tests`, `third_party`, `installer`, `docs`, `.github`, а также README, LICENSE, `.sln`, `.csproj` и несколько скриптов/документов. Небольшое число файлов в корне нормально. Не переноси README или `.github` в случайную вложенную папку. В корне не должно быть россыпи C# и лицензий.

## 4. Сохрани и отправь

```powershell
git commit -m "Release PoolDesk 1.0"
git push origin HEAD
```

Если Git попросит имя и email, настрой их для этой папки. Вместо примеров укажи свои данные; приватный noreply-email можно скопировать из GitHub → Settings → Emails.

```powershell
git config user.name "Твоё имя или ник"
git config user.email "Твой email из GitHub"
# Затем повтори commit и push.
```

При первом push Git может открыть вход в GitHub в браузере. Пароль аккаунта в PowerShell вводить не нужно. После push обнови страницу репозитория: README и его скриншоты появятся автоматически.

## 5. Создай релиз с готовыми файлами

1. На странице репозитория открой **Releases → Draft a new release**.
2. В **Choose a tag** введи **v1.0.0**, выбери создание нового тега. Target — ветка с только что загруженным кодом (обычно main).
3. Заголовок: **PoolDesk 1.0**.
4. Скопируй описание из `RELEASE-NOTES.md`.
5. В область файлов приложи **Setup.exe**, **win-x64.zip**, **source.zip** и **SHA256SUMS.txt** этой версии.
6. Не ставь **pre-release**. Отметь **Set as the latest release**, если эта опция показана: прежние 2.x были прототипами.
7. Нажми **Publish release**.

На странице релиза появятся также автоматически созданные GitHub `Source code (zip/tar.gz)` — это нормально. Большой EXE не коммить в корень репозитория, прикрепляй его к Releases.

Старые 2.x не нужно удалять из Git-истории. Можно отредактировать их описание, добавив: «Development prototype; use PoolDesk 1.0». Новый 1.0 должен быть latest.

### Альтернатива: автоматический релиз через CI

Используй **вместо** пункта 5, если хочешь, чтобы GitHub сам пересобрал файлы. После успешного зелёного запуска Actions для твоего коммита:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

Workflow соберёт и опубликует 1.0.0. Не создавай тег повторно, если релиз уже создан вручную. Уже существующий релиз workflow не перезаписывает. Если Actions отключён, разреши его во вкладке Actions. Для автоматической публикации workflow запрашивает `contents: write`; ограничения организации могут потребовать настройки владельца. Сгенерированные CI контрольные суммы относятся к файлам CI, а выданные вместе с этим архивом — к локальной сборке. Не смешивай их.

## 6. Что остаётся на твоей стороне

- Проверить новый релиз на своём компьютере и прикрепить файлы/опубликовать его.
- При желании заменить демонстрационные скриншоты в `docs/screenshots/` своими. Для запуска это не требуется; README уже содержит картинки.
- Цифровая подпись необязательна для публикации. Сейчас EXE и установщик **не подписаны**. Для подписи нужен твой сертификат Code Signing с закрытым ключом в `Cert:\CurrentUser\My`. Поддержан параметр сборки `-CertificateThumbprint`; без него получается unsigned-релиз. Не загружай сертификат, PFX, пароль или закрытый ключ в Git. Встроенный путь подписи не добавляет сервер времени: для долгосрочной действительности подписи настрой timestamp своим инструментом подписи.

Пример только при наличии сертификата и средств сборки:

```powershell
.\build.ps1 -Installer -CertificateThumbprint 'ОТПЕЧАТОК_ТВОЕГО_СЕРТИФИКАТА'
```

После подписи/пересборки используй новые архивы и новые SHA256SUMS из `artifacts`, старые суммы уже не подходят. Не отключай SmartScreen/антивирус в инструкции пользователям.

## Проверка скачанного файла

```powershell
Get-FileHash "$env:USERPROFILE\Downloads\PoolDesk-1.0.0-Setup.exe" -Algorithm SHA256
```

Сравни строку с `SHA256SUMS.txt`. Контрольная сумма проверяет целостность, но не заменяет цифровую подпись.

Официальные справки: https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository и https://docs.github.com/en/actions/tutorials/build-and-test-code/net . Инструкция подготовлена 25 ноября 2026 года.
