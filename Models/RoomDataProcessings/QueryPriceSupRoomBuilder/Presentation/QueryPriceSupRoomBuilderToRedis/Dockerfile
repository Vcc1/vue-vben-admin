# 请参�?https://aka.ms/customizecontainer 以了解如何自定义调试容器，以�?Visual Studio 如何使用�?Dockerfile 生成映像以更快地进行调试�?

# 此阶段用于在快速模�?默认为调试配�?下从 VS 运行�?
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app


# 此阶段用于生成服务项�?
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Models/RoomDataProcessings/QueryPriceSupRoomBuilder/Presentation/QueryPriceSupRoomBuilderToRedis/QueryPriceSupRoomBuilderToRedis.csproj", "Models/RoomDataProcessings/QueryPriceSupRoomBuilder/Presentation/QueryPriceSupRoomBuilderToRedis/"]
COPY ["Models/Base/Shared/Infrastructure/VBaseFramework.Core/VBaseFramework.Core.csproj", "Models/Base/Shared/Infrastructure/VBaseFramework.Core/"]
COPY ["Models/Base/Shared/Infrastructure/VBaseFramework.ServiceDefaults/VBaseFramework.ServiceDefaults.csproj", "Models/Base/Shared/Infrastructure/VBaseFramework.ServiceDefaults/"]
COPY ["Models/RoomDataProcessings/QueryPriceSupRoomBuilder/Application/QueryPriceSupRoomBuilder/QueryPriceSupRoomBuilder.csproj", "Models/RoomDataProcessings/QueryPriceSupRoomBuilder/Application/QueryPriceSupRoomBuilder/"]
COPY ["Models/Base/Shared/Domain/OTADomain/OTADomain.csproj", "Models/Base/Shared/Domain/OTADomain/"]
COPY ["Models/RoomMatchings/Core/Application/RoomMatch.Core/RoomMatch.Core.csproj", "Models/RoomMatchings/Core/Application/RoomMatch.Core/"]
COPY ["Models/RoomMatchings/Core/Domain/Domain.RoomMatch/Domain.RoomMatch.csproj", "Models/RoomMatchings/Core/Domain/Domain.RoomMatch/"]
COPY ["Models/RoomDataProcessings/QueryPriceSupRoomBuilder/Infrastructure/Infrastructure.QueryPriceSupRoomBuilder/Infrastructure.QueryPriceSupRoomBuilder.csproj", "Models/RoomDataProcessings/QueryPriceSupRoomBuilder/Infrastructure/Infrastructure.QueryPriceSupRoomBuilder/"]
COPY ["Providers/Infrastructure.Redis.SupRoomInfo.DirectPriceDiscovery/Infrastructure.Redis.SupRoomInfo.DirectPriceDiscovery.csproj", "Providers/Infrastructure.Redis.SupRoomInfo.DirectPriceDiscovery/"]
COPY ["Providers/Infrastructure.Sql.RoomMapping/Infrastructure.Sql.RoomMapping.csproj", "Providers/Infrastructure.Sql.RoomMapping/"]
COPY ["Providers/Infrastructure.HTTP.QueryPrice/Infrastructure.HTTP.QueryPrice.csproj", "Providers/Infrastructure.HTTP.QueryPrice/"]
COPY ["Providers/Infrastructure.Redis.TriggerQueue/Infrastructure.Redis.TriggerQueue.csproj", "Providers/Infrastructure.Redis.TriggerQueue/"]
COPY ["Models/RoomMatchings/Core/Infrastructure/Infrastructure.RoomMatchApp/Infrastructure.RoomMatchApp.csproj", "Models/RoomMatchings/Core/Infrastructure/Infrastructure.RoomMatchApp/"]
COPY ["Providers/Infrastructure.HTTP.SupStaticData/Infrastructure.HTTP.SupStaticData.csproj", "Providers/Infrastructure.HTTP.SupStaticData/"]
COPY ["Providers/Infrastructure.Redis.RoomMatchingRedis/Infrastructure.Redis.RoomMatchingRedis.csproj", "Providers/Infrastructure.Redis.RoomMatchingRedis/"]
COPY ["Providers/Infrastructure.Sql.MengTuAPI/Infrastructure.Sql.MengTuAPI.csproj", "Providers/Infrastructure.Sql.MengTuAPI/"]
COPY ["Providers/Infrastructure.Sql.MengTuSup/Infrastructure.Sql.MengTuSup.csproj", "Providers/Infrastructure.Sql.MengTuSup/"]
COPY ["Providers/Infrastructure.Sql.MixedData/Infrastructure.Sql.MixedData.csproj", "Providers/Infrastructure.Sql.MixedData/"]
RUN dotnet restore "./Models/RoomDataProcessings/QueryPriceSupRoomBuilder/Presentation/QueryPriceSupRoomBuilderToRedis/QueryPriceSupRoomBuilderToRedis.csproj"
COPY . .
WORKDIR "/src/Models/RoomDataProcessings/QueryPriceSupRoomBuilder/Presentation/QueryPriceSupRoomBuilderToRedis"
RUN dotnet build "./QueryPriceSupRoomBuilderToRedis.csproj" -c $BUILD_CONFIGURATION -o /app/build

# 此阶段用于发布要复制到最终阶段的服务项目
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./QueryPriceSupRoomBuilderToRedis.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# 此阶段在生产中使用，或在常规模式下从 VS 运行时使�?在不使用调试配置时为默认�?
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "QueryPriceSupRoomBuilderToRedis.dll"]