# Build context: repository root
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY MikrotikScheduler.sln ./
COPY src/MikrotikScheduler.Domain/*.csproj src/MikrotikScheduler.Domain/
COPY src/MikrotikScheduler.Application/*.csproj src/MikrotikScheduler.Application/
COPY src/MikrotikScheduler.Infrastructure/*.csproj src/MikrotikScheduler.Infrastructure/
COPY src/MikrotikScheduler.Api/*.csproj src/MikrotikScheduler.Api/
COPY src/MikrotikScheduler.Web/*.csproj src/MikrotikScheduler.Web/
RUN dotnet restore src/MikrotikScheduler.Api/MikrotikScheduler.Api.csproj
COPY src/ src/
RUN dotnet publish src/MikrotikScheduler.Api/MikrotikScheduler.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# Data Protection keys (encrypt router/PPPoE passwords) must survive container restarts -> mount a volume here.
RUN mkdir -p /keys && chown app:app /keys
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    DataProtection__KeysPath=/keys
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "MikrotikScheduler.Api.dll"]
