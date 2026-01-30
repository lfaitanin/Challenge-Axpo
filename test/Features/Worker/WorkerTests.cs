using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PowerPosition.Worker.Features.PowerPosition;
using Xunit;

namespace PowerPosition.Worker.Tests.Features.Worker
{
    public class WorkerTests
    {
        private readonly Mock<ILogger<PowerPosition.Worker.Worker>> _loggerMock;
        private readonly Mock<IMediator> _mediatorMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly PowerPosition.Worker.Worker _worker;

        public WorkerTests()
        {
            _loggerMock = new Mock<ILogger<PowerPosition.Worker.Worker>>();
            _mediatorMock = new Mock<IMediator>();
            _configurationMock = new Mock<IConfiguration>();

            _configurationMock.Setup(c => c.GetSection("PowerPosition:ScheduleIntervalMinutes").Value)
                .Returns("0.0001"); // Very small interval (approx 6ms) to test loop

            _worker = new PowerPosition.Worker.Worker(_loggerMock.Object, _mediatorMock.Object, _configurationMock.Object);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldTriggerExtraction_Repeatedly()
        {
            // Arrange
            var cts = new CancellationTokenSource();

            _mediatorMock.Setup(m => m.Send(It.IsAny<ExtractPowerPositionCommand>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _worker.StartAsync(cts.Token);
            await Task.Delay(100);

            cts.Cancel();

            // Assert
            _mediatorMock.Verify(m => m.Send(It.IsAny<ExtractPowerPositionCommand>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));
        }

        [Fact]
        public async Task ExecuteAsync_ShouldLog_WhenExceptionOccurs()
        {
            var cts = new CancellationTokenSource();
            _mediatorMock.Setup(m => m.Send(It.IsAny<ExtractPowerPositionCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Test Exception"));

            // Act
            await _worker.StartAsync(cts.Token);
            await Task.Delay(100);

            cts.Cancel();

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.AtLeastOnce);
        }
    }
}
