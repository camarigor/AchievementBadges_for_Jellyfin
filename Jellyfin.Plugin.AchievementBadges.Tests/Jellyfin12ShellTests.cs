using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.AchievementBadges.Tests;

/// <summary>
/// Jellyfin 12 ships a new default layout ("modern") that keeps the old
/// .mainDrawer and .skinHeader in the DOM only so legacy scripts do not
/// throw; both are hidden. The plugin's sidebar entry and equipped-badge
/// strip were injected into exactly those two elements, so on 12 they
/// existed and nobody could see them, and the only way to the achievements
/// page was gone. These pin the two new hooks: the avatar menu (an MUI Menu
/// with a stable id that stays mounted while closed) and the MUI toolbar.
/// </summary>
public class Jellyfin12ShellTests
{
    private static string ReadEmbedded(string suffix)
    {
        var assembly = typeof(Plugin).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(suffix, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void TheAvatarMenuGetsAnAchievementsEntryBelowProfile()
    {
        var js = ReadEmbedded("sidebar.js");

        Assert.Contains("getElementById('app-user-menu')", js, StringComparison.Ordinal);
        Assert.Contains("a[href*=\"/userprofile\"]", js, StringComparison.Ordinal);
        Assert.Contains("item.setAttribute('href','#/achievements')", js, StringComparison.Ordinal);

        // Placement: right after the Profile item, never appended blindly
        // while Profile exists.
        var inject = js.IndexOf("function injectUserMenu()", StringComparison.Ordinal);
        var afterProfile = js.IndexOf("list.insertBefore(item, profile.nextSibling)", inject, StringComparison.Ordinal);
        Assert.True(inject >= 0);
        Assert.True(afterProfile > inject);

        // The clone has no React handler, so the script closes the menu itself.
        Assert.Contains("#app-user-menu .MuiBackdrop-root", js, StringComparison.Ordinal);
    }

    [Fact]
    public void TheEntryIsInjectedOnEveryPassAndTranslated()
    {
        var js = ReadEmbedded("sidebar.js");

        var tryInject = js.IndexOf("function tryInject()", StringComparison.Ordinal);
        var header = js.IndexOf("injectHeader();", tryInject, StringComparison.Ordinal);
        var userMenu = js.IndexOf("injectUserMenu();", header, StringComparison.Ordinal);
        Assert.True(tryInject >= 0);
        Assert.True(header > tryInject);
        Assert.True(userMenu > header);

        var translate = js.IndexOf("function applyTranslations()", StringComparison.Ordinal);
        var entryText = js.IndexOf("getElementById(USER_MENU_ID)", translate, StringComparison.Ordinal);
        Assert.True(entryText > translate);
    }

    [Fact]
    public void TheEquippedStripFallsBackToTheMuiToolbar()
    {
        var js = ReadEmbedded("sidebar.js");

        var injectHeader = js.IndexOf("function injectHeader()", StringComparison.Ordinal);
        var legacy = js.IndexOf("querySelector('.headerRight')", injectHeader, StringComparison.Ordinal);
        var modern = js.IndexOf("button[aria-controls=\"app-user-menu\"]", legacy, StringComparison.Ordinal);
        Assert.True(injectHeader >= 0);
        Assert.True(legacy > injectHeader);
        Assert.True(modern > legacy);
    }

    [Theory]
    [InlineData("sidebar.js")]
    [InlineData("standalone.js")]
    [InlineData("enhance.js")]
    [InlineData("Pages.index.html")]
    [InlineData("configPage.html")]
    public void EveryAuthenticatedCallSendsTheCurrentAuthorizationHeader(string asset)
    {
        // Jellyfin 12.0 runs a migration (DisableLegacyAuthorization) that
        // refuses X-Emby-Token, X-MediaBrowser-Token and api_key in the
        // query. Every plugin call that carried only the legacy header
        // answered 401 on 12: badges, preferences, friends, the admin
        // catalog. The current form is the only one 12 accepts, and 10.11
        // accepts both, so both are sent.
        var text = ReadEmbedded(asset);

        Assert.Contains("MediaBrowser Token=\"'", text, StringComparison.Ordinal);
        // The legacy header never travels alone any more: each place that
        // sets it also sets Authorization within the same block.
        var sites = 0;
        foreach (var needle in new[] { "['X-Emby-Token'] =", "['X-Emby-Token']=" })
        {
            var index = text.IndexOf(needle, StringComparison.Ordinal);
            while (index >= 0)
            {
                sites++;
                var windowStart = Math.Max(0, index - 400);
                Assert.Contains("Authorization", text.Substring(windowStart, index - windowStart), StringComparison.Ordinal);
                index = text.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
            }
        }
        Assert.True(sites > 0, "the asset sets no token header at all");
    }

    [Fact]
    public void TheLegacyDrawerInjectionStays()
    {
        // Jellyfin 12 still offers desktop-legacy, mobile-legacy and tv
        // layouts, all on the old drawer.
        var js = ReadEmbedded("sidebar.js");
        Assert.Contains("function injectSidebar()", js, StringComparison.Ordinal);
        Assert.Contains("querySelectorAll('.navMenuOption')", js, StringComparison.Ordinal);
    }
}
