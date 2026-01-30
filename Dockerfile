# Base runtime image
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

# Build image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and external dependencies
COPY ["src/Applications/PowerPosition.Worker/PowerPosition.Worker.csproj", "src/Applications/PowerPosition.Worker/"]
COPY ["src/Libraries/External/PowerService.dll", "src/Libraries/External/"]

# Restore dependencies
RUN dotnet restore "src/Applications/PowerPosition.Worker/PowerPosition.Worker.csproj"

# Copy the rest of the source code
COPY . .
WORKDIR "/src/src/Applications/PowerPosition.Worker"
RUN dotnet build "PowerPosition.Worker.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "PowerPosition.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PowerPosition.Worker.dll"]
