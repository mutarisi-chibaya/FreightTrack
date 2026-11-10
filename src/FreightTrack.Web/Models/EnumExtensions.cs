using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace FreightTrack.Web.Models;

public static class EnumExtensions
{
    /// <summary>
    /// Reads the [Display(Name = "...")] attribute so we can show "In transit" instead of "InTransit".
    /// This is an *extension method* (the "this" keyword): C# lets us add methods to existing types,
    /// so we can write status.GetDisplayName(). Java has no equivalent; you'd use a static utility method.
    /// </summary>
    public static string GetDisplayName(this Enum value)
    {
        var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
        return member?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString();
    }
}
