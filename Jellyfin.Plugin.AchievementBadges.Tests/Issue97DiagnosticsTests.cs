using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.AchievementBadges.Api;
using Jellyfin.Plugin.AchievementBadges.Helpers;
using Jellyfin.Plugin.AchievementBadges.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.AchievementBadges.Tests;

/// <summary>
/// Issue #97 reported "Failed to reset badges. Request failed: 400" together
/// with a scan that found nothing for one account. Two things made that
/// report undiagnosable for a month: the reset route answered 200 for any
/// string at all, so probing it proved nothing, and the admin page reports
/// a bare status code for whichever of the chained requests failed. These
/// pin the diagnostics that replace the guesswork.
/// </summary>
public class Issue97DiagnosticsTests : IDisposable
{
    private static readonly Guid Live = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Unknown = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly string _dataDir;
    private readonly Mock<IApplicationPaths> _paths;
    private readonly Mock<IUserManager> _users;
    private readonly AchievementBadgeService _badges;

    public Issue97DiagnosticsTests()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "ab97_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dataDir);

        _paths = new Mock<IApplicationPaths>();
        _paths.SetupGet(p => p.PluginConfigurationsPath).Returns(_dataDir);

        _users = new Mock<IUserManager>();
        _users.Setup(m => m.GetUserById(Live)).Returns(new User("Alice", "prov", "reset"));
        _users.Setup(m => m.GetUserById(Unknown)).Returns((User?)null);

        _badges = new AchievementBadgeService(
            _paths.Object,
            _users.Object,
            new WebhookNotifier(NullLogger<WebhookNotifier>.Instance),
            new AuditLogService(_paths.Object, NullLogger<AuditLogService>.Instance),
            NullLogger<AchievementBadgeService>.Instance);
    }

    public void Dispose()
    {
        _badges.Dispose();
        try { Directory.Delete(_dataDir, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// The controller takes fifteen services. The reset action reads two of
    /// them, so the instance is created uninitialised and only those two are
    /// set; anything else it touched would fail loudly as a null reference.
    /// </summary>
    private AchievementBadgesController Controller()
    {
        var controller = (AchievementBadgesController)RuntimeHelpers.GetUninitializedObject(typeof(AchievementBadgesController));
        Set(controller, "_userManager", _users.Object);
        Set(controller, "_badgeService", _badges);
        return controller;
    }

    private static void Set(object target, string field, object value)
    {
        var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("field " + field + " not found");
        f.SetValue(target, value);
    }

    [Theory]
    [InlineData("undefined")]
    [InlineData("null")]
    [InlineData("not-a-guid")]
    public void ResetRejectsAnIdThatIsNotAGuid(string userId)
    {
        var result = Controller().ResetBadges(userId).Result;

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("GUID", bad.Value!.ToString(), StringComparison.Ordinal);
        Assert.Null(_badges.PeekProfile(userId));
    }

    [Fact]
    public void ResetRefusesAnAccountJellyfinDoesNotKnow()
    {
        var result = Controller().ResetBadges(Unknown.ToString("D")).Result;

        Assert.IsType<NotFoundObjectResult>(result);
        // Before the check, the reset created an empty profile under the
        // unknown id as a side effect of "resetting" it.
        Assert.Null(_badges.PeekProfile(Unknown.ToString("D")));
    }

    [Fact]
    public void ResetStillWorksForARealAccount()
    {
        _badges.RecordPlayback(Live.ToString("D"), isMovie: true, playedAt: DateTimeOffset.Now);

        var result = Controller().ResetBadges(Live.ToString("D")).Result;

        Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(_badges.PeekProfile(Live.ToString("D")));
    }

    [Fact]
    public void AScanThatSeesNothingSaysWhereToLook()
    {
        var library = new Mock<ILibraryManager>();
        library.Setup(l => l.GetItemsResult(It.IsAny<InternalItemsQuery>()))
            .Returns(new QueryResult<BaseItem>(Array.Empty<BaseItem>()));
        var userData = new Mock<IUserDataManager>();

        var completion = new LibraryCompletionService(library.Object, _users.Object, _badges, NullLogger<LibraryCompletionService>.Instance);
        var custom = new CustomBadgeService(_paths.Object, NullLogger<CustomBadgeService>.Instance);
        var targets = new TargetProgressService(library.Object, userData.Object, _users.Object, custom, _badges, NullLogger<TargetProgressService>.Instance);
        var scan = new WatchHistoryBackfillService(
            library.Object, _users.Object, userData.Object, _badges, completion, targets,
            new TracearrCreditLedger(), new WatchCarryStore(TimeSpan.Zero), NullLogger<WatchHistoryBackfillService>.Instance);

        var result = scan.BackfillUser(Live.ToString("D"));

        var type = result.GetType();
        Assert.True((bool)type.GetProperty("Success")!.GetValue(result)!);
        var hint = type.GetProperty("Hint")!.GetValue(result) as string;
        Assert.NotNull(hint);
        Assert.Contains("Access", hint, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAdminPageNamesTheRequestThatFailed()
    {
        var html = ReadEmbedded("Pages.index.html");

        // A failure carries the method and route, so a 400 from the reload
        // that follows a reset cannot be mistaken for the reset itself.
        Assert.Contains("var where = ' (' + httpMethod + ' ' + path.replace(", html, StringComparison.Ordinal);
        Assert.Contains("err.request = httpMethod + ' ' + path;", html, StringComparison.Ordinal);

        // Reset and scan each report their own failure separately from the
        // reload that follows them: two-argument then(), not a shared catch.
        var reset = html.IndexOf("function resetBadges()", StringComparison.Ordinal);
        var resetSplit = html.IndexOf("return reloadAll().then(function () {", reset, StringComparison.Ordinal);
        var resetOwn = html.IndexOf("}, function (error) {", resetSplit, StringComparison.Ordinal);
        Assert.True(reset >= 0 && resetSplit > reset && resetOwn > resetSplit);

        var scanHint = html.IndexOf("if (result.Hint) summary +=", StringComparison.Ordinal);
        Assert.True(scanHint > 0);
        Assert.Contains("admin.msg.scan_empty_hint", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("es")]
    [InlineData("fr")]
    [InlineData("it")]
    [InlineData("pt")]
    [InlineData("zh")]
    [InlineData("ja")]
    public void TheEmptyScanHintIsTranslated(string lang)
    {
        var json = ReadEmbedded("translations." + lang + ".json");
        Assert.Contains("\"admin.msg.scan_empty_hint\"", json, StringComparison.Ordinal);
    }

    private static string ReadEmbedded(string suffix)
    {
        var assembly = typeof(Plugin).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(suffix, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
