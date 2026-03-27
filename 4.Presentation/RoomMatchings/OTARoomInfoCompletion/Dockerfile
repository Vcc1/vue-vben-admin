# 请参阅 https://aka.ms/customizecontainer 以了解如何自定义调试容器，以及 Visual Studio 如何使用此 Dockerfile 生成映像以更快地进行调试。

# 此阶段用于在快速模式(默认为调试配置)下从 VS 运行时
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app


# 此阶段用于生成服务项目
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["4.Presentation/RoomMatchings/OTARoomInfoCompletion/OTARoomInfoCompletion.csproj", "4.Presentation/RoomMatchings/OTARoomInfoCompletion/"]
COPY ["0.0VBaseFrameworks/VBaseFramework.Core/VBaseFramework.Core.csproj", "0.0VBaseFrameworks/VBaseFramework.Core/"]
COPY ["0.0VBaseFrameworks/VBaseFramework.ServiceDefaults/VBaseFramework.ServiceDefaults.csproj", "0.0VBaseFrameworks/VBaseFramework.ServiceDefaults/"]
COPY ["2.Infrastructure/Infrastructure.HTTP.SupStaticData/Infrastructure.HTTP.SupStaticData.csproj", "2.Infrastructure/Infrastructure.HTTP.SupStaticData/"]
COPY ["2.Infrastructure/Infrastructure.Sql.MengTuAPI/Infrastructure.Sql.MengTuAPI.csproj", "2.Infrastructure/Infrastructure.Sql.MengTuAPI/"]
COPY ["2.Infrastructure/Infrastructure.Sql.RoomMapping/Infrastructure.Sql.RoomMapping.csproj", "2.Infrastructure/Infrastructure.Sql.RoomMapping/"]
COPY ["1.Domain/Domain.RoomMatch/Domain.RoomMatch.csproj", "1.Domain/Domain.RoomMatch/"]
COPY ["1.Domain/OTADomain/OTADomain.csproj", "1.Domain/OTADomain/"]
COPY ["3.Application/RoomMatchings/RoomMatch.Core/RoomMatch.Core.csproj", "3.Application/RoomMatchings/RoomMatch.Core/"]
COPY ["2.Infrastructure/Infrastructure.Sql.MengTuSup/Infrastructure.Sql.MengTuSup.csproj", "2.Infrastructure/Infrastructure.Sql.MengTuSup/"]
COPY ["2.Infrastructure/Infrastructure.Sql.MengTuYDOTA/Infrastructure.Sql.MengTuYDOTA.csproj", "2.Infrastructure/Infrastructure.Sql.MengTuYDOTA/"]
COPY ["2.Infrastructure/Infrastructure.Sql.MixedData/Infrastructure.Sql.MixedData.csproj", "2.Infrastructure/Infrastructure.Sql.MixedData/"]
RUN dotnet restore "./4.Presentation/RoomMatchings/OTARoomInfoCompletion/OTARoomInfoCompletion.csproj"
COPY . .
WORKDIR "/src/4.Presentation/RoomMatchings/OTARoomInfoCompletion"
RUN dotnet build "./OTARoomInfoCompletion.csproj" -c $BUILD_CONFIGURATION -o /app/build

# 此阶段用于发布要复制到最终阶段的服务项目
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./OTARoomInfoCompletion.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# 此阶段在生产中使用，或在常规模式下从 VS 运行时使用(在不使用调试配置时为默认值)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OTARoomInfoCompletion.dll"]