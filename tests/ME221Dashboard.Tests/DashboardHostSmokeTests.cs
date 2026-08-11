using FluentAssertions;
using ME221Dashboard.Services;
using Xunit;

namespace ME221Dashboard.Tests;

public class DashboardHostSmokeTests
{
    [Fact]
    public void MauiAppAssemblyLoads()
    {
        typeof(DashboardConfig).Assembly.GetName().Name.Should().Be("ME221Dashboard");
    }
}
