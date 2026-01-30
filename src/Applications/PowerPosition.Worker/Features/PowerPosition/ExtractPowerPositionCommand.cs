using MediatR;

namespace PowerPosition.Worker.Features.PowerPosition;

public record ExtractPowerPositionCommand(DateTime Date) : IRequest;
