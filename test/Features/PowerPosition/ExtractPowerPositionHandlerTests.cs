using Axpo;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PowerPosition.Worker.Features.PowerPosition;
using Xunit;
using System.Reflection;
using System.Runtime.Serialization;

namespace Tests.Features.PowerPosition
{
    public class ExtractPowerPositionHandlerTests
    {
        private readonly Mock<IPowerService> _powerServiceMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<ILogger<ExtractPowerPositionHandler>> _loggerMock;
        private readonly ExtractPowerPositionHandler _handler;
        private readonly string _testOutputDir;

        public ExtractPowerPositionHandlerTests()
        {
            _powerServiceMock = new Mock<IPowerService>();
            _configurationMock = new Mock<IConfiguration>();
            _loggerMock = new Mock<ILogger<ExtractPowerPositionHandler>>();

            _testOutputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _configurationMock.Setup(c => c.GetSection("PowerPosition:OutputDirectory").Value).Returns(_testOutputDir);

            var inMemorySettings = new Dictionary<string, string?> {
                {"PowerPosition:OutputDirectory", _testOutputDir}
            };
            IConfiguration infoConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            _handler = new ExtractPowerPositionHandler(_powerServiceMock.Object, infoConfiguration, _loggerMock.Object);
        }

        [Fact]
        public async Task Handle_ShouldGenerateCsvFile_WithAggregatedVolumes()
        {
            var date = new DateTime(2024, 1, 1);
            var command = new ExtractPowerPositionCommand(date);

            var powerPeriods1 = new[]
            {
                CreatePowerPeriod(1, 100),
                CreatePowerPeriod(2, 50)
            };
            var trade1 = CreatePowerTrade(date, powerPeriods1);

            var powerPeriods2 = new[]
            {
                CreatePowerPeriod(1, 50),
                CreatePowerPeriod(2, 20)
            };
            var trade2 = CreatePowerTrade(date, powerPeriods2);

            _powerServiceMock.Setup(x => x.GetTradesAsync(It.IsAny<DateTime>())).ReturnsAsync(new[] { trade1, trade2 });

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            Directory.Exists(_testOutputDir).Should().BeTrue();

            var files = Directory.GetFiles(_testOutputDir, "*.csv");
            files.Should().HaveCount(1);
            var filePath = files.First();

            var lines = await File.ReadAllLinesAsync(filePath);

            lines.Length.Should().Be(25);
            lines[0].Should().Be("Local Time,Volume");

            lines[1].Should().Contain(",150");

            lines[2].Should().Contain(",70");

            lines[3].Should().Contain(",0");

            if (Directory.Exists(_testOutputDir))
            {
                Directory.Delete(_testOutputDir, true);
            }
        }

        [Fact]
        public async Task Handle_ShouldHandleEmptyTrades()
        {
            // Arrange
            var date = new DateTime(2024, 1, 2);
            var command = new ExtractPowerPositionCommand(date);

            _powerServiceMock.Setup(x => x.GetTradesAsync(date)).ReturnsAsync(Array.Empty<PowerTrade>());

            var inMemorySettings = new Dictionary<string, string?> {
                {"PowerPosition:OutputDirectory", _testOutputDir}
            };
            IConfiguration infoConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            var handler = new ExtractPowerPositionHandler(_powerServiceMock.Object, infoConfiguration, _loggerMock.Object);

            // Act
            await handler.Handle(command, CancellationToken.None);

            // Assert
            var files = Directory.GetFiles(_testOutputDir, "*.csv");
            files.Should().HaveCount(1);
            var lines = await File.ReadAllLinesAsync(files.First());
            lines.Length.Should().Be(25);
            lines[1].Should().EndWith(",0");

            // Cleanup
            if (Directory.Exists(_testOutputDir))
            {
                Directory.Delete(_testOutputDir, true);
            }
        }

        [Fact]
        public async Task Handle_ShouldIgnoreTrade_WhenPeriodsAreMissing()
        {
            // Arrange
            var date = new DateTime(2024, 1, 3);
            var command = new ExtractPowerPositionCommand(date);

            var tradeWithNullPeriods = (PowerTrade)FormatterServices.GetUninitializedObject(typeof(PowerTrade));
            typeof(PowerTrade)
               .GetField("<Date>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
               ?.SetValue(tradeWithNullPeriods, date);

            var tradeWithEmptyPeriods = CreatePowerTrade(date, Array.Empty<PowerPeriod>());

            _powerServiceMock.Setup(x => x.GetTradesAsync(It.IsAny<DateTime>())).ReturnsAsync(new[] { tradeWithNullPeriods, tradeWithEmptyPeriods });

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            var lines = await File.ReadAllLinesAsync(Directory.GetFiles(_testOutputDir).First());
            lines[1].Should().EndWith(",0");
        }

        [Fact]
        public async Task Handle_ShouldSkipAggregation_WhenPeriodIsInvalid()
        {
            var date = new DateTime(2024, 1, 4);
            var command = new ExtractPowerPositionCommand(date);

            var p = CreatePowerPeriod(25, 100);
            var trade = CreatePowerTrade(date, new[] { p });

            _powerServiceMock.Setup(x => x.GetTradesAsync(It.IsAny<DateTime>())).ReturnsAsync(new[] { trade });

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            var lines = await File.ReadAllLinesAsync(Directory.GetFiles(_testOutputDir).First());
            lines[1].Should().EndWith(",0");
        }

        [Fact]
        public async Task Handle_ShouldUseDefaultOutputDirectory_WhenConfigIsMissing()
        {
            var date = new DateTime(2024, 1, 5);
            var command = new ExtractPowerPositionCommand(date);
            _powerServiceMock.Setup(x => x.GetTradesAsync(It.IsAny<DateTime>())).ReturnsAsync(Array.Empty<PowerTrade>());

            var emptyConfigMock = new Mock<IConfiguration>();

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

            var handler = new ExtractPowerPositionHandler(_powerServiceMock.Object, config, _loggerMock.Object);

            var defaultDir = Path.Combine(Directory.GetCurrentDirectory(), "output");
            if (Directory.Exists(defaultDir)) Directory.Delete(defaultDir, true);

            try
            {
                // Act
                await handler.Handle(command, CancellationToken.None);

                // Assert
                Directory.Exists(defaultDir).Should().BeTrue();
            }
            finally
            {
                if (Directory.Exists(defaultDir)) Directory.Delete(defaultDir, true);
            }
        }

        private PowerPeriod CreatePowerPeriod(int period, double volume)
        {
            var p = new PowerPeriod(period);
            p.SetVolume(volume);
            return p;
        }

        private PowerTrade CreatePowerTrade(DateTime date, PowerPeriod[] periods)
        {
            var trades2 = (PowerTrade)FormatterServices.GetUninitializedObject(typeof(PowerTrade));

            typeof(PowerTrade)
                .GetField("<Date>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(trades2, date);

            typeof(PowerTrade)
                .GetField("<Periods>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(trades2, periods);

            return trades2;
        }
    }
}
