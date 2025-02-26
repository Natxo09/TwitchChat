public class BadgeService
{
    private readonly List<Badge> _badges = new()
    {
        new Badge("moderator", "moderator", "🛡️ "),
        new Badge("sub", "sub", "⭐ "),
        new Badge("vip", "vip", "💎 "),
        new Badge("broadcaster", "broadcaster", "📺 "),
        new Badge("dj", "dj", "🎧 "),
        new Badge("partner", "partner", "✓ "),
        new Badge("turbo", "turbo", "🚀 "),
        new Badge("staff", "twitchstaff", "🔧 ")
    };

    public string GetBadges(string badgePart)
    {
        string badges = "";
        foreach (var badge in _badges)
        {
            if (badgePart.Contains(badge.Type))
            {
                badges += badge.GetIcon();
            }
        }
        return badges;
    }
}
