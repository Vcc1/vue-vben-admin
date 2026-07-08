# See https://aka.ms/customizecontainer for Visual Studio container customization.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 5010

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Models/MetricsAndAlerts/MetricsAndAlerts/Presentation/MetricsAndAlerts/MetricsAndAlerts.csproj", "Models/MetricsAndAlerts/MetricsAndAlerts/Presentation/MetricsAndAlerts/"]
COPY ["Models/Base/Shared/Infrastructure/VBaseFramework.ServiceDefaults/VBaseFramework.ServiceDefaults.csproj", "Models/Base/Shared/Infrastructure/VBaseFramework.ServiceDefaults/"]
COPY ["Models/Base/Shared/Infrastructure/VBaseFramework.Core/VBaseFramework.Core.csproj", "Models/Base/Shared/Infrastructure/VBaseFramework.Core/"]
COPY ["Models/MetricsAndAlerts/MetricsAndAlerts/Application/MetricsAndAlertsApp/MetricsAndAlertsApp.csproj", "Models/MetricsAndAlerts/MetricsAndAlerts/Application/MetricsAndAlertsApp/"]
COPY ["Models/MetricsAndAlerts/MetricsAndAlerts/Infrastructure/Infrastructure.MetricsAndAlertsApp/Infrastructure.MetricsAndAlertsApp.csproj", "Models/MetricsAndAlerts/MetricsAndAlerts/Infrastructure/Infrastructure.MetricsAndAlertsApp/"]
COPY ["Providers/Infrastructure.HTTP.Prometheus/Infrastructure.HTTP.Prometheus.csproj", "Providers/Infrastructure.HTTP.Prometheus/"]
COPY ["Providers/Infrastructure.HTTP.WorkWechatAlert/Infrastructure.HTTP.WorkWechatAlert.csproj", "Providers/Infrastructure.HTTP.WorkWechatAlert/"]
COPY ["Providers/Infrastructure.Sql.MetricsAndAlerts/Infrastructure.Sql.MetricsAndAlerts.csproj", "Providers/Infrastructure.Sql.MetricsAndAlerts/"]
RUN dotnet restore "./Models/MetricsAndAlerts/MetricsAndAlerts/Presentation/MetricsAndAlerts/MetricsAndAlerts.csproj"
COPY . .
WORKDIR "/src/Models/MetricsAndAlerts/MetricsAndAlerts/Presentation/MetricsAndAlerts"
RUN dotnet build "./MetricsAndAlerts.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./MetricsAndAlerts.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
USER root
ENV TZ=Asia/Shanghai DEBIAN_FRONTEND=noninteractive
ENV ASPNETCORE_URLS=http://0.0.0.0:5010
RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata \
    && ln -sf /usr/share/zoneinfo/Asia/Shanghai /etc/localtime \
    && echo "Asia/Shanghai" > /etc/timezone \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=publish /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "MetricsAndAlerts.dll"]
