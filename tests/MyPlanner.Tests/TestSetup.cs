using System.Runtime.CompilerServices;

namespace MyPlanner.Tests;

internal static class TestSetup
{
    /// <summary>Тесты работают с отдельной временной папкой данных, а не с реальными данными пользователя.</summary>
    [ModuleInitializer]
    internal static void Init()
    {
        var dir = Path.Combine(Path.GetTempPath(), "MyPlannerTests-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("MYPLANNER_DATA", dir);
    }
}
