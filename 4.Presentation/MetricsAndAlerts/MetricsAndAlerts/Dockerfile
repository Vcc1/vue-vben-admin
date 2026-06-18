# See https://aka.ms/customizecontainer for Visual Studio container customization.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 5010

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["4.Presentation/MetricsAndAlerts/MetricsAndAlerts/MetricsAndAlerts.csproj", "4.Presentation/MetricsAndAlerts/MetricsAndAlerts/"]
COPY ["0.0VBaseFrameworks/VBaseFramework.ServiceDefaults/VBaseFramework.ServiceDefaults.csproj", "0.0VBaseFrameworks/VBaseFramework.ServiceDefaults/"]
COPY ["0.0VBaseFrameworks/VBaseFramework.Core/VBaseFramework.Core.csproj", "0.0VBaseFrameworks/VBaseFramework.Core/"]
COPY ["2.Application/MetricsAndAlerts/MetricsAndAlertsApp/MetricsAndAlertsApp.csproj", "2.Application/MetricsAndAlerts/MetricsAndAlertsApp/"]
COPY ["3.Infrastructure/Core/MetricsAndAlerts/Infrastructure.MetricsAndAlertsApp/Infrastructure.MetricsAndAlertsApp.csproj", "3.Infrastructure/Core/MetricsAndAlerts/Infrastructure.MetricsAndAlertsApp/"]
COPY ["3.Infrastructure/Providers/Infrastructure.HTTP.Prometheus/Infrastructure.HTTP.Prometheus.csproj", "3.Infrastructure/Providers/Infrastructure.HTTP.Prometheus/"]
COPY ["3.Infrastructure/Providers/Infrastructure.HTTP.WorkWechatAlert/Infrastructure.HTTP.WorkWechatAlert.csproj", "3.Infrastructure/Providers/Infrastructure.HTTP.WorkWechatAlert/"]
COPY ["3.Infrastructure/Providers/Infrastructure.Sql.MetricsAndAlerts/Infrastructure.Sql.MetricsAndAlerts.csproj", "3.Infrastructure/Providers/Infrastructure.Sql.MetricsAndAlerts/"]
RUN dotnet restore "./4.Presentation/MetricsAndAlerts/MetricsAndAlerts/MetricsAndAlerts.csproj"
COPY . .
WORKDIR "/src/4.Presentation/MetricsAndAlerts/MetricsAndAlerts"
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
