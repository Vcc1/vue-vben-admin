# 请参�?https://aka.ms/customizecontainer 以了解如何自定义调试容器，以�?Visual Studio 如何使用�?Dockerfile 生成映像以更快地进行调试�?

# 此阶段用于在快速模�?默认为调试配�?下从 VS 运行�?
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 5126


# 此阶段用于生成服务项�?
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Models/Base/Shared/Presentation/SupplierStaticDataAppTester/SupplierStaticDataAppTester.csproj", "Models/Base/Shared/Presentation/SupplierStaticDataAppTester/"]
COPY ["Models/Base/Shared/Infrastructure/VBaseFramework.Core/VBaseFramework.Core.csproj", "Models/Base/Shared/Infrastructure/VBaseFramework.Core/"]
COPY ["Models/Base/Shared/Infrastructure/VBaseFramework.ServiceDefaults/VBaseFramework.ServiceDefaults.csproj", "Models/Base/Shared/Infrastructure/VBaseFramework.ServiceDefaults/"]
COPY ["Models/Base/Shared/Application/SupplierStaticDataApp/SupplierStaticDataApp.csproj", "Models/Base/Shared/Application/SupplierStaticDataApp/"]
COPY ["Models/RoomMatchings/Core/Domain/Domain.RoomMatch/Domain.RoomMatch.csproj", "Models/RoomMatchings/Core/Domain/Domain.RoomMatch/"]
COPY ["Models/Base/Shared/Domain/OTADomain/OTADomain.csproj", "Models/Base/Shared/Domain/OTADomain/"]
COPY ["Providers/Infrastructure.SupplierStaticData/Infrastructure.SupplierStaticData.csproj", "Providers/Infrastructure.SupplierStaticData/"]
COPY ["Providers/Infrastructure.Sql.RoomMapping/Infrastructure.Sql.RoomMapping.csproj", "Providers/Infrastructure.Sql.RoomMapping/"]
COPY ["Providers/Infrastructure.HTTP.SupStaticData/Infrastructure.HTTP.SupStaticData.csproj", "Providers/Infrastructure.HTTP.SupStaticData/"]
COPY ["Providers/Infrastructure.Redis.RoomMatchingRedis/Infrastructure.Redis.RoomMatchingRedis.csproj", "Providers/Infrastructure.Redis.RoomMatchingRedis/"]
COPY ["Providers/Infrastructure.Redis.SupRoomInfo.DirectPriceDiscovery/Infrastructure.Redis.SupRoomInfo.DirectPriceDiscovery.csproj", "Providers/Infrastructure.Redis.SupRoomInfo.DirectPriceDiscovery/"]
COPY ["Providers/Infrastructure.Sql.MengTuSup/Infrastructure.Sql.MengTuSup.csproj", "Providers/Infrastructure.Sql.MengTuSup/"]
COPY ["Providers/Infrastructure.Sql.MixedData/Infrastructure.Sql.MixedData.csproj", "Providers/Infrastructure.Sql.MixedData/"]
RUN dotnet restore "./Models/Base/Shared/Presentation/SupplierStaticDataAppTester/SupplierStaticDataAppTester.csproj"
COPY . .
WORKDIR "/src/Models/Base/Shared/Presentation/SupplierStaticDataAppTester"
RUN dotnet build "./SupplierStaticDataAppTester.csproj" -c $BUILD_CONFIGURATION -o /app/build

# 此阶段用于发布要复制到最终阶段的服务项目
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SupplierStaticDataAppTester.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# 此阶段在生产中使用，或在常规模式下从 VS 运行时使�?在不使用调试配置时为默认�?
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SupplierStaticDataAppTester.dll"]
