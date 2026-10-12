# 请参阅 https://aka.ms/customizecontainer 以了解如何自定义调试容器，以及 Visual Studio 如何使用此 Dockerfile 生成映像以更快地进行调试。

# 此阶段用于在快速模式(默认为调试配置)下从 VS 运行时
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
ENV TZ=Asia/Shanghai
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone
USER $APP_UID
WORKDIR /app


# 此阶段用于生成服务项目
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Models/Room/RoomMatchings/OTARoomBuilder/OTARoomBuilder.Cli/OTARoomBuilder.Cli.csproj", "Models/Room/RoomMatchings/OTARoomBuilder/OTARoomBuilder.Cli/"]
COPY ["BaseFrameworks/VBaseFramework.Core/VBaseFramework.Core.csproj", "BaseFrameworks/VBaseFramework.Core/"]
COPY ["BaseFrameworks/VBaseFramework.ServiceDefaults/VBaseFramework.ServiceDefaults.csproj", "BaseFrameworks/VBaseFramework.ServiceDefaults/"]
COPY ["Models/Room/RoomMatchings/OTARoomBuilder/OTARoomBuilder.App/OTARoomBuilder.App.csproj", "Models/Room/RoomMatchings/OTARoomBuilder/OTARoomBuilder.App/"]
COPY ["Models/Room/RoomMatchings/Core/RoomMatch.Domain/RoomMatch.Domain.csproj", "Models/Room/RoomMatchings/Core/RoomMatch.Domain/"]
COPY ["Models/Base/Shared/OTA.Domain/OTA.Domain.csproj", "Models/Base/Shared/OTA.Domain/"]
COPY ["Models/Room/RoomMatchings/Core/RoomMatch.Core.App/RoomMatch.Core.App.csproj", "Models/Room/RoomMatchings/Core/RoomMatch.Core.App/"]
COPY ["Providers/Infrastructure.Sql.RoomMapping/Infrastructure.Sql.RoomMapping.csproj", "Providers/Infrastructure.Sql.RoomMapping/"]
COPY ["Models/Room/RoomMatchings/OTARoomBuilder/OTARoomBuilder.Infra/OTARoomBuilder.Infra.csproj", "Models/Room/RoomMatchings/OTARoomBuilder/OTARoomBuilder.Infra/"]
COPY ["Models/Hotel/SupplierStaticData/SupplierStaticData.App/SupplierStaticData.App.csproj", "Models/Hotel/SupplierStaticData/SupplierStaticData.App/"]
COPY ["Providers/Infrastructure.HTTP.OTARoomCreate/Infrastructure.HTTP.OTARoomCreate.csproj", "Providers/Infrastructure.HTTP.OTARoomCreate/"]
COPY ["Providers/Infrastructure.SupplierStaticData/Infrastructure.SupplierStaticData.csproj", "Providers/Infrastructure.SupplierStaticData/"]
COPY ["Models/Room/RoomMatchings/Core/RoomMatch.Infra/RoomMatch.Infra.csproj", "Models/Room/RoomMatchings/Core/RoomMatch.Infra/"]
COPY ["Providers/Infrastructure.HTTP.SupStaticData/Infrastructure.HTTP.SupStaticData.csproj", "Providers/Infrastructure.HTTP.SupStaticData/"]
COPY ["Providers/Infrastructure.Redis.RoomMatchingRedis/Infrastructure.Redis.RoomMatchingRedis.csproj", "Providers/Infrastructure.Redis.RoomMatchingRedis/"]
COPY ["Providers/Infrastructure.Redis.SupRoomInfo.DirectPriceDiscovery/Infrastructure.Redis.SupRoomInfo.DirectPriceDiscovery.csproj", "Providers/Infrastructure.Redis.SupRoomInfo.DirectPriceDiscovery/"]
COPY ["Providers/Infrastructure.Sql.MengTuAPI/Infrastructure.Sql.MengTuAPI.csproj", "Providers/Infrastructure.Sql.MengTuAPI/"]
COPY ["Providers/Infrastructure.Sql.MengTuSup/Infrastructure.Sql.MengTuSup.csproj", "Providers/Infrastructure.Sql.MengTuSup/"]
COPY ["Providers/Infrastructure.Sql.MixedData/Infrastructure.Sql.MixedData.csproj", "Providers/Infrastructure.Sql.MixedData/"]
RUN dotnet restore "./Models/Room/RoomMatchings/OTARoomBuilder/OTARoomBuilder.Cli/OTARoomBuilder.Cli.csproj"
COPY . .
WORKDIR "/src/Models/Room/RoomMatchings/OTARoomBuilder/OTARoomBuilder.Cli"
RUN dotnet build "./OTARoomBuilder.Cli.csproj" -c $BUILD_CONFIGURATION -o /app/build

# 此阶段用于发布要复制到最终阶段的服务项目
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./OTARoomBuilder.Cli.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# 此阶段在生产中使用，或在常规模式下从 VS 运行时使用(在不使用调试配置时为默认值)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OTARoomBuilder.Cli.dll"]
