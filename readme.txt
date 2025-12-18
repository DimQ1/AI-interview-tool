Инструкция по настройке API ключа OpenRouter

Для работы приложения необходимо установить API ключ OpenRouter в хранилище секретов пользователя (User Secrets).
Это позволяет не хранить ключ в исходном коде.

1. Откройте терминал в папке с решением (там где папка SystemAudioAnalyzer).
2. Выполните следующую команду, заменив <ВАШ_КЛЮЧ> на ваш реальный API ключ OpenRouter:

dotnet user-secrets set "OpenRouterApiKey" "<ВАШ_КЛЮЧ>" --project SystemAudioAnalyzer/SystemAudioAnalyzer.csproj

Пример:
dotnet user-secrets set "OpenRouterApiKey" "sk-or-v1-..." --project SystemAudioAnalyzer/SystemAudioAnalyzer.csproj

После этого приложение сможет автоматически загружать ключ при запуске.