using System.Text.Json;
using AppDock.SDK;
internal static partial class Program
{
    private sealed class FakeContext : IExtensionContext, ICommandService, ISettingsService, IUiService, ISchedulerService
    {
        public string ExtensionId => "at365.web-browser-tools";
        public Dictionary<string, Func<CancellationToken, Task>> Handlers = [];
        public Dictionary<string, string> Titles = [];
        public Dictionary<string, object> Values = [];
        public Func<CancellationToken, Task>? Changed, Timer;
        public ICommandService Commands => this;
        public ISettingsService Settings => this;
        public IUiService Ui => this;
        public ISchedulerService Scheduler => this;
        public ITrayService Tray => throw new NotSupportedException();
        public INotificationService Notifications => throw new NotSupportedException();
        public IBrowserService Browser => throw new NotSupportedException();
        public ILogService Log => throw new NotSupportedException();
        public IStorageService Storage => throw new NotSupportedException();
        public ISecretService Secrets => throw new NotSupportedException();
        public void Register(string id, string title, Func<CancellationToken, Task> handler) { Handlers.Add(id, handler); Titles.Add(id, title); }
        public Task ReplaceAsync(IReadOnlyList<CommandRegistration> commands, CancellationToken token = default) {
            Handlers = commands.ToDictionary(c => c.Id, c => c.Handler); Titles = commands.ToDictionary(c => c.Id, c => c.Title); return Task.CompletedTask;
        }
        public T Get<T>(string key, T fallback) => Values.TryGetValue(key, out var value) ? JsonSerializer.SerializeToElement(value).Deserialize<T>()! : fallback;
        public Task SetAsync<T>(string key, T value, CancellationToken token = default) { Values[key] = value!; return Task.CompletedTask; }
        public IDisposable OnChanged(Func<CancellationToken, Task> handler) { Changed = handler; return new Release(() => Changed = null); }
        public Task SetOptionsAsync(string key, IReadOnlyList<SettingOption> options, CancellationToken token = default) => throw new NotSupportedException();
        public Task ShowPanelAsync(AppDock.SDK.Panel panel, CancellationToken token = default) => Task.CompletedTask;
        public Task<string> GetImageDirectoryAsync(CancellationToken token = default) => throw new NotSupportedException();
        public IDisposable Every(TimeSpan interval, Func<CancellationToken, Task> callback) { Timer = callback; return new Release(() => Timer = null); }
        public Task Execute(string suffix) => Handlers[ExtensionId + "." + suffix](default);
        public Task Tick() => Timer!(default);
        public async Task Change(string key, object value) { Values[key] = value; if (Changed is not null) await Changed(default); }
        private sealed class Release(Action action) : IDisposable { public void Dispose() => action(); }
    }
}
