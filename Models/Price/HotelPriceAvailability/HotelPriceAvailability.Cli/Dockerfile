FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
ENV TZ=Asia/Shanghai
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Models/Price/HotelPriceAvailability/HotelPriceAvailability.Cli/HotelPriceAvailability.Cli.csproj", "Models/Price/HotelPriceAvailability/HotelPriceAvailability.Cli/"]
COPY ["Models/Price/HotelPriceAvailability/HotelPriceAvailability.App/HotelPriceAvailability.App.csproj", "Models/Price/HotelPriceAvailability/HotelPriceAvailability.App/"]
COPY ["Models/Price/HotelPriceAvailability/HotelPriceAvailability.Domain/HotelPriceAvailability.Domain.csproj", "Models/Price/HotelPriceAvailability/HotelPriceAvailability.Domain/"]
COPY ["Models/Price/HotelPriceAvailability/HotelPriceAvailability.Infra/HotelPriceAvailability.Infra.csproj", "Models/Price/HotelPriceAvailability/HotelPriceAvailability.Infra/"]
COPY ["BaseFrameworks/VBaseFramework.Core/VBaseFramework.Core.csproj", "BaseFrameworks/VBaseFramework.Core/"]
COPY ["BaseFrameworks/VBaseFramework.ServiceDefaults/VBaseFramework.ServiceDefaults.csproj", "BaseFrameworks/VBaseFramework.ServiceDefaults/"]
COPY ["Providers/Infrastructure.Sql.RoomMapping/Infrastructure.Sql.RoomMapping.csproj", "Providers/Infrastructure.Sql.RoomMapping/"]
COPY ["Models/Room/RoomMatchings/Core/RoomMatch.Domain/RoomMatch.Domain.csproj", "Models/Room/RoomMatchings/Core/RoomMatch.Domain/"]
COPY ["Models/Base/Shared/OTA.Domain/OTA.Domain.csproj", "Models/Base/Shared/OTA.Domain/"]
RUN dotnet restore "Models/Price/HotelPriceAvailability/HotelPriceAvailability.Cli/HotelPriceAvailability.Cli.csproj"
COPY . .
WORKDIR "/src/Models/Price/HotelPriceAvailability/HotelPriceAvailability.Cli"
RUN dotnet build "HotelPriceAvailability.Cli.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "HotelPriceAvailability.Cli.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HotelPriceAvailability.Cli.dll"]
