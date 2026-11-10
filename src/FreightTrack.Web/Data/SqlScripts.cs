using System.Reflection;

namespace FreightTrack.Web.Data;

/// <summary>Reads the .sql files from /database that are embedded in this assembly (see the .csproj).</summary>
public static class SqlScripts
{
    public static string Read(string fileName)
    {
        var resourceName = $"FreightTrack.Sql.{fileName}";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded SQL script '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
