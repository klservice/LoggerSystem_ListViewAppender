# BewerbungsAssiLogger – Legacy + WPF MVVM (.NET 8)

## Voraussetzungen
- Windows, Visual Studio 2022 (17.8+) mit Workload **.NET Desktop Development**
- .NET 8 SDK und NuGet-Zugriff auf `log4net` 3.0.3

## Start
1. `LoggerSystem.csproj` in Visual Studio öffnen.
2. NuGet-Pakete wiederherstellen und Projekt starten.
3. Im **LogViewer** auf **Legacy Logger testen** klicken: INFO und WARN werden über die **alte LoggerFactory** geschrieben. Die manuelle Meldung geht ausschließlich in den ListView-Puffer.
4. Unter **LoggerSettings** die Konfiguration anpassen und **Apply** wählen; **Save** speichert die Einstellungen separat.

## Kompatibilität
- Der unveränderte öffentliche Einstiegspunkt `BewerbungsAssiLogger.Logger.GetLog<T>()` bleibt bestehen.
- `Logger.GetInstance()`, `GetLog(Type)`, `GetLoggingEvents()`, `AddLogEntryToListView(...)` und `LoggerFactory.CreateLogger(...)` bleiben erhalten.
- Die bestehenden Flag-Werte `RollingFile=1`, `Console=2`, `Memory=4`, `ListView=Memory` bleiben unverändert.
- `LoggerFileAppender : AppenderSkeleton`, `LoggerPath`, `LoggerFactoryHelpers` und das alte `BewerbungsAssiLogger.LogViewModel` sind enthalten.
- `LoggerSystem.Integration.LegacyLoggerBridge` stellt einen Adapter für die neue Konfiguration und Beispielaufrufe bereit.
- `LoggerSystem.Services.LoggerConfigurator` konfiguriert **ausschließlich** `BewerbungsAssiLogger.LoggerFactory` und abonniert dessen vorhandenen Memory-Appender. Es wird kein zweites Repository erzeugt.
- Für neue UI-Komponenten `LoggerSystem.ViewModels.LogViewModel` verwenden. Das Legacy-ViewModel kann weiterhin separat mit dem bestehenden Memory-Appender verwendet werden; nach einer vollständigen Appender-Neukonfiguration muss es neu an den aktuellen Appender gebunden werden.

## Einstellungen
Standard: ListView+RollingFile, DEBUG, 2000 Einträge, AutoScroll, 30 Tage, AutoClean, `Logs/application.log`, 10 MB, 10 Backups.
JSON-Datei: `%LOCALAPPDATA%\BewerbungsAssiLogger\LoggerSettings.json`.

## Hinweise
- Bereinigung löscht nur alte nummerierte Rolling-Backups (`application.log.1` und `application.1.log`), niemals `application.log`.
- `CleanLogFolder` bewirkt eine Bereinigung beim Apply; `AutoCleanLogFolder` aktiviert zusätzlich einen täglichen Timer.
- `AppenderType.Memory` ist für `GetLoggingEvents()` und manuelle ListView-Meldungen erforderlich.
- `%type` wird nach `%property{type}` umgesetzt; per `LegacyLoggerBridge.SetCategory("STARTUP")` setzen.
- Ein Windows-Build war in der Erstellungsumgebung nicht möglich; die Projektdateien wurden statisch geprüft, aber nicht kompiliert.
# LoggerSystem_ListViewAppender
