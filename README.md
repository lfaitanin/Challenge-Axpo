# Coding Challenge - AXPO

This project is a background worker service designed to extract, aggregate, and report power trading positions. It interacts with an external Power Service to retrieve trading data, aggregates the volumes by time period, and generates CSV reports.

## Technologies

*   **Framework**: .NET 10
*   **Architecture**: Vertical Slice Architecture using **MediatR** for decoupled request handling.
*   **Resilience**: Implements **Polly** (v8) resilience pipelines for robust error handling and retries with exponential backoff.
*   **Testing**: Comprehensive Unit Tests using **xUnit**, **Moq**, and **FluentAssertions**.
*   **Containerization**: **Docker** support for easy deployment.

## Architecture

The application follows the **Vertical Slice Architecture**:
- **Worker**: The background service that triggers the extraction process periodically.
- **Features**: Logic is grouped by feature (e.g., `ExtractPowerPosition`).
- **Services**: `ResilientPowerService` acts as a decorator for the external `Axpo.PowerService`, adding logging and retry logic transparently.


## Resilience & Error Handling
The application uses **Polly** to handle transient failures from the external Power Service.
- **Retry Policy**: Retries up to 3 times with exponential backoff (2s initial delay).
- **Logging**: Detailed logs for retry attempts and final failures.

## Test Coverage
We strive for 100% test coverage.
- **Handler Logic**: Fully covered, including edge cases (missing periods, invalid IDs).
- **Worker Service**: Covered for execution flow and exception handling.

![Test Coverage](assets/Test_coverage.png)
