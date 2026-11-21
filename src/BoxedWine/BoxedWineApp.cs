using BoxedWine.Lib;
using Kazo.Os.App.Abstractions;

namespace BoxedWine;

/// <summary>
/// The manifest of BoxedWine (<c>ca.kazo.boxedwine</c>, decision 222 A): an ordinary Kazo OS window
/// app, the interface of Os-x86 for Windows programs (decisions 40, 49 A). It is the app associated
/// with <c>.exe</c>, <c>.com</c>, <c>.bat</c>, <c>.cmd</c> and <c>.msi</c> (Os-x86 registers no
/// association, 64), requires the Feature <c>ca.kazo.x86</c> (ADR 0248 of Kazo) and launches the
/// programs through <c>Ix86</c>. The build generates <c>manifest.json</c> from this class; it is
/// never instantiated at run time.
/// </summary>
[AppManifest]
public sealed class BoxedWineApp : AppDefinition
{
    /// <summary>The app id, also the name of its folder in <c>~/.config/</c>.</summary>
    public const string AppId = "ca.kazo.boxedwine";

    /// <summary>The Feature BoxedWine requires.</summary>
    public const string X86FeatureId = "ca.kazo.x86";

    /// <summary>The lowest version of Os-x86 whose <c>Ix86</c> has <c>RunProgramAsync</c> (work item 10).</summary>
    public const string X86MinimumVersion = "0.1.0";

    /// <summary>The permission of Os-x86 that <c>Ix86.RunProgramAsync</c> requires (decision 176 A).</summary>
    public const string ProgramsLaunch = "x86.programs.launch";

    /// <summary>The setting that records, per user, that the tutorial was shown (decision 71: required at the first launch).</summary>
    public const string TutorialSeenKey = "tutorial-seen";

    /// <inheritdoc />
    public override string Id => AppId;

    /// <inheritdoc />
    public override string Name => "BoxedWine";

    /// <inheritdoc />
    public override AppKind Kind => AppKind.Window;

    /// <inheritdoc />
    public override Type EntryPoint => typeof(BoxedWineWindow);

    /// <inheritdoc />
    public override Type? MobileEntryPoint => typeof(BoxedWineWindow);

    /// <inheritdoc />
    public override string PublisherId => "pub.kazo";

    /// <inheritdoc />
    public override string Description => "Runs Windows programs with Os-x86: opens .exe, .com, .bat, .cmd and .msi files";

    /// <inheritdoc />
    public override string Category => "system";

    /// <inheritdoc />
    /// <remarks>The tag that publishes to the Kazo OS Store must be v + this version.</remarks>
    public override string Version => "0.1.0";

    /// <inheritdoc />
    /// <remarks>Not single instance (decision 184 A): every launch is a new window with its own program.</remarks>
    public override bool SingleInstancePerUser => false;

    /// <inheritdoc />
    public override IReadOnlyList<string> Capabilities =>
    [
        // The program the OS asks BoxedWine to open, usually in the user's home.
        AppCapabilities.FileSystemUserHomeRead,

        // "Open a program…": the file dialog of the OS.
        AppCapabilities.DialogFileOpen,

        // ~/.config/ca.kazo.boxedwine/settings.config: the tutorial seen (per user).
        AppCapabilities.FileSystemPrivateRead,
        AppCapabilities.FileSystemPrivateWrite,

        // Not here: ProgramsLaunch, the permission of Os-x86 that Ix86.RunProgramAsync requires
        // (ADR 0246 of Kazo, normal). The manifest generator of Kazo 3.5.2 drops the dependencies
        // before it checks the capabilities, so it refuses the permission of a feature in an app
        // (KOSAPP0009, question 321). build/FeaturePermissions.targets adds FeaturePermissions to the
        // generated manifest.json until Kazo is fixed; then it moves back here.
    ];

    /// <summary>
    /// The permissions of Os-x86 BoxedWine needs, added to <c>manifest.json</c> after the generator
    /// (<c>build/FeaturePermissions.targets</c>, question 321) and checked by the tests.
    /// </summary>
    public static IReadOnlyList<string> FeaturePermissions { get; } = [ProgramsLaunch];

    /// <inheritdoc />
    /// <remarks>
    /// ADR 0248 of Kazo: installing BoxedWine installs Os-x86 with it (one consent, an administrator
    /// for the Feature); removing Os-x86 disables BoxedWine.
    /// </remarks>
    public override IReadOnlyList<AppDependencyManifest> Dependencies =>
        [new(X86FeatureId, X86MinimumVersion, Optional: false)];

    /// <inheritdoc />
    /// <remarks>
    /// <c>open</c>: the double-click, « Ouvrir avec », <c>xdg-open</c> (ADR 0251 §4 of Kazo: unchanged).
    /// <c>execute</c>: a path typed in the shell (ADR 0251 §1, 253 A), for PE, NE, batch files and
    /// installers; a pure DOS program goes to the <c>x86</c> command, so <c>.com</c> is <c>open</c>
    /// only. Kazo 3.5.2 does not read <c>execute</c> yet: it takes effect when the route of the shell
    /// is published.
    /// </remarks>
    public override IReadOnlyList<FileHandler> FileHandlers =>
    [
        .. BwLib.HandledFileTypes
            .GroupBy(static type => (type.MediaType, type.Execute))
            .Select(static group => new FileHandler(
                group.Key.MediaType,
                [.. group.Select(static type => type.Extension)],
                group.Key.Execute ? ["open", "execute"] : ["open"])),
    ];

    /// <inheritdoc />
    public override AppSettingsSchemaManifest? Settings => new(
        1,
        [new AppSettingFieldManifest(TutorialSeenKey, AppSettingValueType.Boolean, "false", AppSettingScope.AppUser, AppSettingSensitivity.Public)]);

    /// <inheritdoc />
    public override IReadOnlyList<string> Icons => ["icons/app.svg"];

    /// <inheritdoc />
    public override IReadOnlyList<AssetManifest> Assets =>
        [new("icons/app.svg", AssetKind.Image, new string('0', 64), "app-icon")];

    /// <inheritdoc />
    public override WindowOptions? Window => new()
    {
        DefaultWidth = 760,
        DefaultHeight = 520,
        MinWidth = 420,
        MinHeight = 320,
    };
}
