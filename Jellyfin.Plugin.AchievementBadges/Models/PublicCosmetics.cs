namespace Jellyfin.Plugin.AchievementBadges.Models;

/// <summary>
/// [issue #42] The equipped shop cosmetics a user shows to other people:
/// the custom rank title and the badge frame. Resolved against the shop
/// catalog, so a stale or unknown id comes back as null instead of leaking
/// into markup, and gated by the same privacy toggles as the equipped badge
/// preview: a user who hid their showcase shows no bling either.
/// <para>
/// The profile theme is not here on purpose. The shareable card skins are
/// drawn in the rank tier's colour, and painting them in the user's theme
/// is a redesign of the three templates rather than a projection of data.
/// </para>
/// </summary>
public sealed class PublicCosmetics
{
    /// <summary>Display name of the equipped custom title (for example
    /// "Tastemaker"), or null when none is equipped or the id is not in the
    /// catalog.</summary>
    public string? CustomTitle { get; init; }

    /// <summary>Catalog id of the equipped badge frame (for example
    /// "frame-gilded"), or null for the default frame, an unknown id, or a
    /// hidden showcase. Safe to use as a CSS class: it can only be an id
    /// that exists in the catalog.</summary>
    public string? BadgeFrameId { get; init; }

    /// <summary>Catalog id of the equipped profile theme (for example
    /// "theme-pastel"), or null for the default theme, an unknown id, or a
    /// hidden showcase. Same guarantee as the frame: only a catalog id, so it
    /// is safe as a CSS class on the shareable card.</summary>
    public string? ProfileThemeId { get; init; }

    /// <summary>The equipped shop avatar's glyph (an emoji such as 🏆), or
    /// null when none is equipped. Taken from the catalog entry, never from
    /// the user, so it is safe to render.</summary>
    public string? AvatarGlyph { get; init; }

    /// <summary>Catalog id of the equipped animated background (for example
    /// "bg-nebula"), or null for none / unknown / hidden. The id names an
    /// embedded video asset or a CSS-only effect; the card decides which.</summary>
    public string? BackgroundId { get; init; }

    /// <summary>Catalog id of the equipped profile border (for example
    /// "border-gold-shimmer"), or null for none / unknown / hidden. Safe as
    /// a CSS class for the same reason as the frame.</summary>
    public string? ProfileBorderId { get; init; }

    public static PublicCosmetics None { get; } = new();
}
