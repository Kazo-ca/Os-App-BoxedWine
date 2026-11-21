using BoxedWine.Lib;
using Kazo.Os.AppSdk;
using Kazo.Os.AppSdk.Blazor;
using Kazo.Os.AppSdk.Globalization;
using Kazo.Os.AppSdk.Settings;
using Microsoft.Extensions.Localization;
using OsX86.Interface;

namespace BoxedWine;

/// <summary>
/// The window of BoxedWine (work item 13, milestone 0). Opened with a file (double-click, « Ouvrir
/// avec », <c>xdg-open</c>: the OS passes its path as the first argument), it reads the program,
/// asks Os-x86 what it is, and launches it through <c>Ix86</c>, with its console in the window.
/// Opened alone, it offers « Ouvrir un programme… ». What it does is in <see cref="WindowController"/>;
/// this class only binds it to Kazo OS (the feature service, the file system, the settings, the file
/// dialog) and to the markup.
/// </summary>
public partial class BoxedWineWindow : WindowAppBase, IAsyncDisposable
{
    private IStringLocalizer<Strings>? _localizer;
    private WindowController? _controller;
    private string _line = string.Empty;

    private IStringLocalizer<Strings> L => _localizer ??= AppContext.GetLocalizer<Strings>();

    private WindowController Controller => _controller ??= CreateController();

    private string StatusText => Controller.Status is { } status ? L[status.Key, status.Arguments] : string.Empty;

    /// <summary>Closes the console, which ends the program and releases its VM (decision 147).</summary>
    public async ValueTask DisposeAsync()
    {
        if (_controller is { } controller)
        {
            controller.Changed -= OnChanged;
            await controller.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected override async Task OnAppInitializedAsync()
    {
        await SetWindowTitleAsync(Manifest.Name);
        await Controller.InitializeAsync(LaunchFile, [.. LaunchArguments.Skip(1)]);
    }

    private WindowController CreateController()
    {
        IAppExecutionContext context = AppContext;
        var controller = new WindowController(
            () => new ProgramSession(
                new BwLib(async cancellationToken => await context.Features.GetServiceAsync(typeof(Ix86), cancellationToken) as Ix86),
                new ProgramFiles(context.FileSystem)),
            new SettingsTutorialStore(context));
        controller.Changed += OnChanged;
        return controller;
    }

    private void OnChanged() => _ = RenderAsync();

    /// <summary>Renders after a change raised on any thread; a change after the window closed is dropped.</summary>
    private async Task RenderAsync()
    {
        try
        {
            await InvokeAsync(StateHasChanged);
        }
        catch (ObjectDisposedException)
        {
            // The renderer is gone with the window: nothing left to show.
        }
    }

    private async Task OpenProgramAsync()
    {
        OpenFileDialogResult result = await OpenFileAsync(new OpenFileDialogRequest
        {
            Title = L["Dialog.Open.Title"],
            Filters =
            [
                new FileDialogFilter(
                    L["Filter.Programs"],
                    [.. BwLib.HandledFileTypes.Select(static type => type.Extension)],
                    [.. BwLib.HandledFileTypes.Select(static type => type.MediaType).Distinct()]),
            ],
        });
        if (result.Status == FileDialogStatus.Selected && result.Resources is [{ } selected, ..])
        {
            await Controller.OpenAsync(selected.Path);
        }
    }

    private async Task SendLineAsync()
    {
        if (Controller.Session is { } session)
        {
            string line = _line;
            _line = string.Empty;
            await session.SendLineAsync(line);
        }
    }

    private async Task InterruptAsync()
    {
        if (Controller.Session is { } session)
        {
            await session.InterruptAsync();
        }
    }

    /// <summary>The paragraphs of a text, separated by blank lines in the resources.</summary>
    private IEnumerable<string> Paragraphs(string key) =>
        L[key].Value.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>The tutorial seen, in the user layer of the app's settings (<c>~/.config/ca.kazo.boxedwine/</c>).</summary>
    private sealed class SettingsTutorialStore(IAppExecutionContext context) : ITutorialStore
    {
        public async ValueTask<bool> SeenAsync()
        {
            try
            {
                return await KazoSettings.GetAsync<bool>(context, BoxedWineApp.TutorialSeenKey);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or InvalidOperationException or FormatException)
            {
                // Without its settings BoxedWine still works; the tutorial shows, as for a first launch.
                return false;
            }
        }

        public async ValueTask MarkSeenAsync()
        {
            try
            {
                await KazoSettings.SetAsync(context, BoxedWineApp.TutorialSeenKey, true);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or InvalidOperationException)
            {
                // Not recorded: the tutorial shows again at the next launch, nothing else is lost.
            }
        }
    }
}
