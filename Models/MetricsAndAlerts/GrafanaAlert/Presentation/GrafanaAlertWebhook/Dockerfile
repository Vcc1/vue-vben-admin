# 请参�?https://aka.ms/customizecontainer 以了解如何自定义调试容器，以�?Visual Studio 如何使用�?Dockerfile 生成映像以更快地进行调试�?

# 此阶段用于在快速模�?默认为调试配�?下从 VS 运行�?
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 5000


# 此阶段用于生成服务项�?
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Models/MetricsAndAlerts/GrafanaAlert/Presentation/GrafanaAlertWebhook/GrafanaAlertWebhook.csproj", "Models/MetricsAndAlerts/GrafanaAlert/Presentation/GrafanaAlertWebhook/"]
COPY ["Models/Base/Shared/Infrastructure/VBaseFramework.ServiceDefaults/VBaseFramework.ServiceDefaults.csproj", "Models/Base/Shared/Infrastructure/VBaseFramework.ServiceDefaults/"]
COPY ["Models/Base/Shared/Infrastructure/VBaseFramework.Core/VBaseFramework.Core.csproj", "Models/Base/Shared/Infrastructure/VBaseFramework.Core/"]
COPY ["Models/MetricsAndAlerts/GrafanaAlert/Application/GrafanaAlertApp/GrafanaAlertApp.csproj", "Models/MetricsAndAlerts/GrafanaAlert/Application/GrafanaAlertApp/"]
COPY ["Models/MetricsAndAlerts/GrafanaAlert/Infrastructure/Infrastructure.GrafanaAlertApp/Infrastructure.GrafanaAlertApp.csproj", "Models/MetricsAndAlerts/GrafanaAlert/Infrastructure/Infrastructure.GrafanaAlertApp/"]
COPY ["Providers/Infrastructure.HTTP.WorkWechatAlert/Infrastructure.HTTP.WorkWechatAlert.csproj", "Providers/Infrastructure.HTTP.WorkWechatAlert/"]
RUN dotnet restore "./Models/MetricsAndAlerts/GrafanaAlert/Presentation/GrafanaAlertWebhook/GrafanaAlertWebhook.csproj"
COPY . .
WORKDIR "/src/Models/MetricsAndAlerts/GrafanaAlert/Presentation/GrafanaAlertWebhook"
RUN dotnet build "./GrafanaAlertWebhook.csproj" -c $BUILD_CONFIGURATION -o /app/build

# 此阶段用于发布要复制到最终阶段的服务项目
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./GrafanaAlertWebhook.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# 此阶段在生产中使用，或在常规模式下从 VS 运行时使�?在不使用调试配置时为默认�?
FROM base AS final
WORKDIR /app
USER root
# 设置时区为东八区（Asia/Shanghai），并清�?apt 缓存
ENV TZ=Asia/Shanghai DEBIAN_FRONTEND=noninteractive
ENV ASPNETCORE_URLS=http://0.0.0.0:5000
RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata \
    && ln -sf /usr/share/zoneinfo/Asia/Shanghai /etc/localtime \
    && echo "Asia/Shanghai" > /etc/timezone \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=publish /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "GrafanaAlertWebhook.dll"]
