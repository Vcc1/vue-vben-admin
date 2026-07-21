# 请参阅 https://aka.ms/customizecontainer 以了解如何自定义调试容器，以及 Visual Studio 如何使用此 Dockerfile 生成映像以更快地进行调试。

# 此阶段用于在快速模式(默认为调试配置)下从 VS 运行时
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
ENV TZ=Asia/Shanghai
USER $APP_UID
WORKDIR /app


# 此阶段用于生成服务项目
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Models/Hotel/OTAHotelInfoSync/OTAHotelInfoSync.Cli/OTAHotelInfoSync.Cli.csproj", "Models/Hotel/OTAHotelInfoSync/OTAHotelInfoSync.Cli/"]
COPY ["BaseFrameworks/VBaseFramework.Core/VBaseFramework.Core.csproj", "BaseFrameworks/VBaseFramework.Core/"]
COPY ["BaseFrameworks/VBaseFramework.ServiceDefaults/VBaseFramework.ServiceDefaults.csproj", "BaseFrameworks/VBaseFramework.ServiceDefaults/"]
COPY ["Models/Hotel/OTAHotelInfoSync/OTAHotelInfoSync.App/OTAHotelInfoSync.App.csproj", "Models/Hotel/OTAHotelInfoSync/OTAHotelInfoSync.App/"]
COPY ["Models/Hotel/OTAHotelInfoSync/OTAHotelInfoSync.Infra/OTAHotelInfoSync.Infra.csproj", "Models/Hotel/OTAHotelInfoSync/OTAHotelInfoSync.Infra/"]
COPY ["Providers/Infrastructure.HTTP.OTAHotelInfo/Infrastructure.HTTP.OTAHotelInfo.csproj", "Providers/Infrastructure.HTTP.OTAHotelInfo/"]
COPY ["Providers/Infrastructure.HTTP.WorkWechatAlert/Infrastructure.HTTP.WorkWechatAlert.csproj", "Providers/Infrastructure.HTTP.WorkWechatAlert/"]
COPY ["Providers/Infrastructure.Sql.RoomMapping/Infrastructure.Sql.RoomMapping.csproj", "Providers/Infrastructure.Sql.RoomMapping/"]
COPY ["Models/Room/RoomMatchings/Core/RoomMatch.Domain/RoomMatch.Domain.csproj", "Models/Room/RoomMatchings/Core/RoomMatch.Domain/"]
COPY ["Models/Base/Shared/OTA.Domain/OTA.Domain.csproj", "Models/Base/Shared/OTA.Domain/"]
RUN dotnet restore "./Models/Hotel/OTAHotelInfoSync/OTAHotelInfoSync.Cli/OTAHotelInfoSync.Cli.csproj"
COPY . .
WORKDIR "/src/Models/Hotel/OTAHotelInfoSync/OTAHotelInfoSync.Cli"
RUN dotnet build "./OTAHotelInfoSync.Cli.csproj" -c $BUILD_CONFIGURATION -o /app/build

# 此阶段用于发布要复制到最终阶段的服务项目
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./OTAHotelInfoSync.Cli.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# 此阶段在生产中使用，或在常规模式下从 VS 运行时使用(在不使用调试配置时为默认值)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OTAHotelInfoSync.Cli.dll"]
