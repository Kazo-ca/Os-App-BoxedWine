using BoxedWine.Lib;
using Kazo.Os.App.Abstractions;
using Kazo.Os.Platform.Core;
using Kazo.Os.Platform.Core.Intents;
using Kazo.Os.Platform.Core.Lifecycle;
using Kazo.Os.Platform.Core.Settings;
using Kazo.Os.Simulation.Abstractions;
using Kazo.Os.Simulation.Abstractions.Gateways;
using Kazo.Os.Simulation.Abstractions.Settings;

namespace BoxedWine.Tests;

/// <summary>
/// The manifest BoxedWine ships (the <c>manifest.json</c> the build writes), read by the real
/// catalog and file associations of Kazo OS: the five extensions go to BoxedWine, the Feature
/// <c>ca.kazo.x86</c> is required (ADR 0248), and the permission of Os-x86 is there.
/// </summary>
[Trait("Category", "unit")]
public sealed class ManifestTests
{
    private static readonly AppManifest Shipped = AppManifestJsonSerializer.DeserializeStrict(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "shipped", "manifest.json")));

    [Fact]
    public void The_shipped_manifest_is_valid_for_Kazo()
    {
        Assert.Empty(AppManifestValidator.Validate(Shipped).Errors);
        Assert.Equal("ca.kazo.boxedwine", Shipped.Id);
        Assert.Equal(AppKind.Window, Shipped.Kind);
        Assert.False(Shipped.SingleInstancePerUser);
    }

    [Fact]
    public void It_requires_Os_x86_and_carries_its_permission()
    {
        AppDependencyManifest dependency = Assert.Single(Shipped.Dependencies);
        Assert.Equal(new AppDependencyManifest("ca.kazo.x86", "0.1.0", null, Optional: false), dependency);
        Assert.Contains("x86.programs.launch", Shipped.Capabilities);
    }

    [Fact]
    public void The_shipped_manifest_is_the_definition_plus_the_permissions_of_the_workaround()
    {
        AppManifest generated = AppDefinitionMapper.ToManifest(new BoxedWineApp());

        Assert.Equal(
            [.. generated.Capabilities.Concat(BoxedWineApp.FeaturePermissions).Order(StringComparer.Ordinal)],
            Shipped.Capabilities);
        Assert.DoesNotContain(BoxedWineApp.ProgramsLaunch, generated.Capabilities);
    }

    [Fact]
    public void It_opens_the_five_extensions_and_executes_all_but_dos()
    {
        Dictionary<string, FileHandlerManifest> byExtension = Shipped.FileHandlers
            .SelectMany(static handler => handler.Extensions.Select(extension => (extension, handler)))
            .ToDictionary(static pair => pair.extension, static pair => pair.handler);

        Assert.Equal([".bat", ".cmd", ".com", ".exe", ".msi"], byExtension.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("application/vnd.microsoft.portable-executable", byExtension[".exe"].MediaType);
        Assert.Equal("application/x-dosexec", byExtension[".com"].MediaType);
        Assert.Equal("application/x-bat", byExtension[".bat"].MediaType);
        Assert.Equal("application/x-msi", byExtension[".msi"].MediaType);
        Assert.Equal(["open"], byExtension[".com"].Actions);
        Assert.All([".exe", ".bat", ".cmd", ".msi"], extension => Assert.Equal(["open", "execute"], byExtension[extension].Actions));
    }

    [Fact]
    public void The_tutorial_seen_is_a_setting_of_each_user()
    {
        AppSettingFieldManifest field = Assert.Single(Shipped.Settings!.Fields);
        Assert.Equal(BoxedWineApp.TutorialSeenKey, field.Key);
        Assert.Equal(AppSettingValueType.Boolean, field.ValueType);
        Assert.Equal("false", field.DefaultValue);
        Assert.Equal(AppSettingScope.AppUser, field.Scope);
    }

    [Fact]
    public void Without_Os_x86_the_catalog_leaves_BoxedWine_out()
    {
        AppCatalogBuildResult built = AppCatalog.Build([Shipped]);

        Assert.Contains(built.Errors, static error => error.AppId == "ca.kazo.boxedwine" && error.Code == "catalog.dependency.missing");
    }

    [Theory]
    [InlineData("/home/user/Downloads/setup.exe")]
    [InlineData("/home/user/SETUP.EXE")]
    [InlineData("/home/user/install.msi")]
    [InlineData("/home/user/run.bat")]
    [InlineData("/home/user/run.cmd")]
    [InlineData("/home/user/game.com")]
    public async Task Opening_a_program_file_goes_to_BoxedWine(string path)
    {
        FileAssociationResolver resolver = Resolver(out _);

        FileHandlerResolution resolution = await resolver.ResolveAsync(new OpenFileIntent(VirtualPath.Parse(path), FileIntentAction.Open));

        Assert.Equal(FileHandlerResolutionStatus.SoleCandidate, resolution.Status);
        Assert.Equal("ca.kazo.boxedwine", resolution.AppId);
    }

    [Fact]
    public async Task Other_files_do_not_go_to_BoxedWine()
    {
        FileAssociationResolver resolver = Resolver(out _);

        FileHandlerResolution resolution = await resolver.ResolveAsync(new OpenFileIntent(VirtualPath.Parse("/home/user/notes.txt"), FileIntentAction.Open));

        Assert.NotEqual("ca.kazo.boxedwine", resolution.AppId);
    }

    [Fact]
    public async Task Os_x86_registers_no_association_so_BoxedWine_is_the_only_candidate()
    {
        FileAssociationResolver resolver = Resolver(out AppCatalog catalog);

        FileHandlerCandidates candidates = await resolver.GetHandlerAppIdsAsync(VirtualPath.Parse("/home/user/setup.exe"));

        Assert.Equal(["ca.kazo.boxedwine"], candidates.AppIds);
        Assert.True(catalog.Manifests.ContainsKey("ca.kazo.x86"));
    }

    private static FileAssociationResolver Resolver(out AppCatalog catalog)
    {
        AppCatalogBuildResult built = AppCatalog.Build([.. AppDefinitionMapper.ToFeatureManifests(new FakeX86Feature()).All, Shipped]);
        Assert.True(built.IsSuccess, string.Join(", ", built.Errors.Select(static error => error.Message)));
        catalog = built.Catalog!;
        return new FileAssociationResolver(catalog, new AppEnablementRegistry(catalog), new EmptySettings());
    }

    /// <summary>The identity, the version and the permission of Os-x86, as its Feature declares them.</summary>
    private sealed class FakeX86Feature : FeatureDefinition
    {
        public override string Id => "ca.kazo.x86";

        public override string Name => "Os-x86";

        public override string Version => "0.1.0";

        public override string? CapabilityPrefix => "x86";

        public override IReadOnlyList<FeatureEntryPoint> EntryPoints =>
        [
            new("cli", "x86", AppKind.Terminal, typeof(FakeX86Feature))
            {
                Terminal = new("x86", "Manages x86 virtual machines", []),
            },
        ];

        public override IReadOnlyList<ProvidedCapability> ProvidedCapabilities =>
            [new("x86.programs.launch", CapabilityRisk.Normal, "Run programs in x86 virtual machines")];
    }

    /// <summary>OS settings with no file: no association configured by an administrator.</summary>
    private sealed class EmptySettings : IOsSettingsService
    {
        public event EventHandler<OsSettingsChangedEventArgs>? DocumentChanged
        {
            add { }
            remove { }
        }

        public string? CurrentHomeDirectory => null;

        public ValueTask<LayeredConfigSnapshot> ReadAsync(OsSettingsDocument document, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new LayeredConfigSnapshot(
                new Dictionary<string, string>(),
                new Dictionary<string, SettingLayer>(),
                new HashSet<string>(),
                []));

        public ValueTask<string?> ReadFileAsync(OsSettingsFile file, CancellationToken cancellationToken = default) => ValueTask.FromResult<string?>(null);

        public ValueTask<string> ReadValidatedFileAsync(OsSettingsFile file, ISettingsDocumentValidator validator, string defaultContent, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(defaultContent);

        public ValueTask SetAsync(OsSettingsDocument document, string qualifiedKey, string value, SettingLayer target = SettingLayer.User, IAppFileSystemGateway? writer = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask SetManyAsync(OsSettingsDocument document, IReadOnlyList<KeyValuePair<string, string>> values, SettingLayer target = SettingLayer.User, IAppFileSystemGateway? writer = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask ResetAsync(OsSettingsDocument document, string qualifiedKey, SettingLayer target = SettingLayer.User, IAppFileSystemGateway? writer = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask WriteFileAsync(OsSettingsFile file, string content, IAppFileSystemGateway? writer = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask WatchAsync(OsSettingsDocument document, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
