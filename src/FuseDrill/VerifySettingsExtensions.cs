namespace FuseDrill;

public static class VerifySettingsExtensions
{
    public static void IncludePrimitiveMembers(this VerifySettings settings)
    {
        settings.AlwaysIncludeMembersWithType(typeof(int));
        settings.AlwaysIncludeMembersWithType(typeof(decimal));
        settings.AlwaysIncludeMembersWithType(typeof(double));
        settings.AlwaysIncludeMembersWithType(typeof(string));
        settings.AlwaysIncludeMembersWithType(typeof(Enum));
        settings.AlwaysIncludeMembersWithType(typeof(DateTime));
        settings.AlwaysIncludeMembersWithType(typeof(DateTimeOffset));
        settings.AlwaysIncludeMembersWithType(typeof(TimeSpan));
        settings.AlwaysIncludeMembersWithType(typeof(Guid));
    }
}