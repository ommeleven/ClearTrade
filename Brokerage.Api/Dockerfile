# syntax=docker/dockerfile:1
# Build from the repository root:  docker build -f Brokerage.Api/Dockerfile -t cleartrade-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached between source-only changes.
COPY Brokerage.Api/Brokerage.Api.csproj Brokerage.Api/
COPY Brokerage.Core/Brokerage.Core.csproj Brokerage.Core/
COPY Brokerage.Data/Brokerage.Data.csproj Brokerage.Data/
COPY Brokerage.Services/Brokerage.Services.csproj Brokerage.Services/
RUN dotnet restore Brokerage.Api/Brokerage.Api.csproj

COPY Brokerage.Api/ Brokerage.Api/
COPY Brokerage.Core/ Brokerage.Core/
COPY Brokerage.Data/ Brokerage.Data/
COPY Brokerage.Services/ Brokerage.Services/
RUN dotnet publish Brokerage.Api/Brokerage.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ARG GIT_SHA=""
ENV GIT_SHA=$GIT_SHA \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=0
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Brokerage.Api.dll"]
