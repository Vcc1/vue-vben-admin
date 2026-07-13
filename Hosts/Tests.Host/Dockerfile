FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Hosts/Tests.Host/Tests.Host.csproj", "Hosts/Tests.Host/"]
COPY ["BaseFrameworks/VBaseFramework.Core/VBaseFramework.Core.csproj", "BaseFrameworks/VBaseFramework.Core/"]
COPY ["BaseFrameworks/VBaseFramework.ServiceDefaults/VBaseFramework.ServiceDefaults.csproj", "BaseFrameworks/VBaseFramework.ServiceDefaults/"]
COPY ["Models/Base/Shared/OTA.Domain/OTA.Domain.csproj", "Models/Base/Shared/OTA.Domain/"]
COPY ["Models/Hotel/SupplierStaticData/SupplierStaticData.App/SupplierStaticData.App.csproj", "Models/Hotel/SupplierStaticData/SupplierStaticData.App/"]
COPY ["Models/Room/RoomMatchings/Core/RoomMatch.Core.App/RoomMatch.Core.App.csproj", "Models/Room/RoomMatchings/Core/RoomMatch.Core.App/"]
COPY ["Models/Room/RoomMatchings/Core/RoomMatch.Domain/RoomMatch.Domain.csproj", "Models/Room/RoomMatchings/Core/RoomMatch.Domain/"]
COPY ["Models/Room/RoomMatchings/Core/RoomMatch.Infra/RoomMatch.Infra.csproj", "Models/Room/RoomMatchings/Core/RoomMatch.Infra/"]
COPY ["Providers/Infrastructure.HTTP.SupStaticData/Infrastructure.HTTP.SupStaticData.csproj", "Providers/Infrastructure.HTTP.SupStaticData/"]
COPY ["Providers/Infrastructure.Redis.RoomMatchingRedis/Infrastructure.Redis.RoomMatchingRedis.csproj", "Providers/Infrastructure.Redis.RoomMatchingRedis/"]
COPY ["Providers/Infrastructure.Redis.SupRoomInfo.DirectPriceDiscovery/Infrastructure.Redis.SupRoomInfo.DirectPriceDiscovery.csproj", "Providers/Infrastructure.Redis.SupRoomInfo.DirectPriceDiscovery/"]
COPY ["Providers/Infrastructure.Sql.MengTuAPI/Infrastructure.Sql.MengTuAPI.csproj", "Providers/Infrastructure.Sql.MengTuAPI/"]
COPY ["Providers/Infrastructure.Sql.MengTuSup/Infrastructure.Sql.MengTuSup.csproj", "Providers/Infrastructure.Sql.MengTuSup/"]
COPY ["Providers/Infrastructure.Sql.MixedData/Infrastructure.Sql.MixedData.csproj", "Providers/Infrastructure.Sql.MixedData/"]
COPY ["Providers/Infrastructure.Sql.RoomMapping/Infrastructure.Sql.RoomMapping.csproj", "Providers/Infrastructure.Sql.RoomMapping/"]
COPY ["Providers/Infrastructure.SupplierStaticData/Infrastructure.SupplierStaticData.csproj", "Providers/Infrastructure.SupplierStaticData/"]
RUN dotnet restore "./Hosts/Tests.Host/Tests.Host.csproj"
COPY . .
WORKDIR "/src/Hosts/Tests.Host"
RUN dotnet build "./Tests.Host.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Tests.Host.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Tests.Host.dll"]
