FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Directory.Build.props", "./"]
COPY ["src/OrderFlow.API/OrderFlow.API.csproj", "src/OrderFlow.API/"]
COPY ["src/OrderFlow.Application/OrderFlow.Application.csproj", "src/OrderFlow.Application/"]
COPY ["src/OrderFlow.Infrastructure/OrderFlow.Infrastructure.csproj", "src/OrderFlow.Infrastructure/"]
COPY ["src/OrderFlow.Domain/OrderFlow.Domain.csproj", "src/OrderFlow.Domain/"]
RUN dotnet restore "src/OrderFlow.API/OrderFlow.API.csproj"

COPY . .
WORKDIR "/src/src/OrderFlow.API"
RUN dotnet build "OrderFlow.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "OrderFlow.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "OrderFlow.API.dll"]
